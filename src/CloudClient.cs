// SPDX-License-Identifier: Apache-2.0
#nullable enable

using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace LaoQin.ProgressManager;

internal sealed record CloudDatabaseTarget(string Database, string Table, string Server, bool TableExists, bool NeedsMigration);

internal sealed class CloudClient
{
    internal const string Username = "admin";
    internal const string DatabaseName = "laoqin";
    internal const int DefaultPort = 5432;
    private const string DefaultPassword = "123456";
    private const int PasswordIterations = 210_000;
    private readonly string connectionString;
    private string? passwordHash;

    private CloudClient(string connectionString, string? passwordHash)
    {
        this.connectionString = connectionString;
        this.passwordHash = passwordHash;
    }

    internal static async Task<CloudClient> LoginAsync(string connection, string password)
    {
        var candidate = NormalizeConnection(connection);
        var credential = await WithWorkspaceAsync(candidate, (db, transaction) =>
            CloudRepository.ReadCredentialAsync(db, transaction)).ConfigureAwait(false);
        if (credential.MigrationPending) throw MigrationRequired();
        var hash = credential.Hash;
        if (!VerifyPassword(password, hash)) throw new InvalidOperationException("管理员密码不正确。");
        return new CloudClient(candidate, hash);
    }

    internal async Task ChangePasswordAsync(string current, string next)
    {
        if (!VerifyPassword(current, passwordHash)) throw new InvalidOperationException("当前密码不正确。");
        if (string.IsNullOrEmpty(next)) throw new InvalidOperationException("请输入新密码。");

        var salt = RandomNumberGenerator.GetBytes(16);
        var newHash = Convert.ToBase64String(salt) + "." + Convert.ToBase64String(DerivePassword(next, salt));
        await WithWorkspaceAsync(connectionString, async (db, transaction) =>
        {
            await RequireCurrentPasswordAsync(db, transaction, " FOR UPDATE").ConfigureAwait(false);
            await CloudRepository.SetPasswordAsync(db, transaction, newHash).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
        passwordHash = newHash;
    }

    internal Task<AppData> ReadWorkspaceAsync() => WithWorkspaceAsync(connectionString, async (db, transaction) =>
    {
        var credential = await RequireCurrentPasswordAsync(db, transaction, "").ConfigureAwait(false);
        var data = await RelationalWorkspace.ReadAsync(db, transaction, CloudRepository.Prefix).ConfigureAwait(false);
        data.CloudGeneration = credential.DataGeneration;
        return data;
    }, IsolationLevel.RepeatableRead);

    internal Task SaveChangesAsync(AppData before, AppData after) => WithWorkspaceAsync(connectionString, async (db, transaction) =>
    {
        var credential = await RequireCurrentPasswordAsync(db, transaction, " FOR SHARE").ConfigureAwait(false);
        if (before.CloudGeneration != credential.DataGeneration)
            throw new InvalidOperationException("云端工作区已被整库覆盖，请刷新后再操作。");
        await RelationalWorkspace.ApplyAsync(db, transaction, CloudRepository.Prefix, before, after).ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        after.CloudGeneration = credential.DataGeneration;
        return true;
    });

    internal Task ReplaceAsync(AppData after, Action<AppData> backup) => WithWorkspaceAsync(connectionString, async (db, transaction) =>
    {
        // 排斥其他客户端的保存，备份与覆盖针对同一份已提交数据。
        await RequireCurrentPasswordAsync(db, transaction, " FOR UPDATE").ConfigureAwait(false);
        var before = await RelationalWorkspace.ReadAsync(db, transaction, CloudRepository.Prefix).ConfigureAwait(false);
        backup(before);
        await RelationalWorkspace.ApplyAsync(db, transaction, CloudRepository.Prefix, before, after).ConfigureAwait(false);
        var generation = await CloudRepository.AdvanceDataGenerationAsync(db, transaction).ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        after.CloudGeneration = generation;
        return true;
    });

    private async Task<CloudCredential> RequireCurrentPasswordAsync(NpgsqlConnection db, NpgsqlTransaction transaction, string rowLock)
    {
        var credential = await CloudRepository.ReadCredentialAsync(db, transaction, rowLock).ConfigureAwait(false);
        if (credential.MigrationPending) throw MigrationRequired();
        if (credential.Hash != passwordHash) throw PasswordChanged();
        return credential;
    }

    internal string BackupDirectory(string localDirectory)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(settings.Host + "\n" + settings.Port + "\n" + settings.Database));
        return Path.Combine(localDirectory, "cloud-backups", Convert.ToHexString(key.AsSpan(0, 12)));
    }

    internal static Task<CloudDatabaseTarget> PreviewDatabaseAsync(string connection) =>
        WithConnectionAsync(NormalizeConnection(connection), async db =>
        {
            var state = await CloudRepository.InspectTableAsync(db, null).ConfigureAwait(false);
            if (state == WorkspaceTable.Unrecognized) throw ForeignTable();
            return await DatabaseTargetAsync(db, null, state).ConfigureAwait(false);
        });

    internal static Task InitializeDatabaseAsync(string connection, CloudDatabaseTarget confirmed) =>
        WithConnectionAsync(NormalizeConnection(connection), async db =>
        {
            await using var transaction = await db.BeginTransactionAsync().ConfigureAwait(false);
            var state = await CloudRepository.InspectTableAsync(db, transaction).ConfigureAwait(false);
            if (state == WorkspaceTable.Unrecognized) throw ForeignTable();
            var target = await DatabaseTargetAsync(db, transaction, state).ConfigureAwait(false);
            if (target != confirmed) throw new InvalidOperationException("初始化目标已变化，请重新核对数据库和表名。");

            var script = Encoding.UTF8.GetString(SchemaBytes());
            await CloudRepository.InitializeAsync(db, transaction, script).ConfigureAwait(false);
            await RequireOwnedTableAsync(db, transaction).ConfigureAwait(false);
            var credential = await CloudRepository.ReadCredentialAsync(db, transaction, " FOR UPDATE").ConfigureAwait(false);
            if (credential.MigrationPending)
            {
                var legacy = await CloudRepository.ReadLegacyAsync(db, transaction).ConfigureAwait(false);
                var existing = await RelationalWorkspace.ReadAsync(db, transaction, CloudRepository.Prefix).ConfigureAwait(false);
                if (existing.Projects.Count != 0 || existing.People.Count != 0 || existing.Tasks.Count != 0 || existing.TaskExcelPreferences != null)
                    throw new InvalidOperationException("新工作区已经有数据，已停止自动迁移。");
                var imported = WorkspaceJson.Decode(Encoding.UTF8.GetBytes(legacy.Json));
                await RelationalWorkspace.ApplyAsync(db, transaction, CloudRepository.Prefix, existing, imported).ConfigureAwait(false);
                await CloudRepository.SetPasswordAsync(db, transaction, legacy.Hash).ConfigureAwait(false);
            }
            await transaction.CommitAsync().ConfigureAwait(false);
            return true;
        });

    internal static byte[] SchemaBytes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LaoQin.ProgressManager.schema.sql")
            ?? throw new InvalidOperationException("找不到 PostgreSQL 初始化脚本。");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    internal static string BuildConnection(string address, string port, string username, string password, bool allowUnencrypted = false)
    {
        var host = address.Trim();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            throw new InvalidOperationException("请填写数据库域名或 IP 地址。");
        host = host.Trim('[', ']');
        if (!int.TryParse(port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
            throw new InvalidOperationException("请输入 1 到 65535 之间的数据库端口。");
        if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("请填写数据库账号。");

        var loopback = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = number,
            Database = DatabaseName,
            Username = username.Trim(),
            Password = password,
            SslMode = loopback ? SslMode.Prefer : SslMode.VerifyFull
        };
        if (allowUnencrypted) settings.SslMode = SslMode.Disable;
        return settings.ConnectionString;
    }

    internal static (string Host, int Port, string Username, string Password, bool AllowUnencrypted) RestoreConnection(string connection)
    {
        var settings = ParseConnection(connection);
        return (settings.Host ?? "", settings.Port, settings.Username ?? "", settings.Password ?? "", settings.SslMode == SslMode.Disable);
    }

    private static string NormalizeConnection(string connection) => ParseConnection(connection).ConnectionString;

    private static NpgsqlConnectionStringBuilder ParseConnection(string connection)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connection) || connection.Length > 4096) throw new ArgumentException();
            var settings = new NpgsqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username)) throw new ArgumentException();
            if (settings.Database != DatabaseName)
                throw new InvalidOperationException($"工作区只连接数据库 {DatabaseName}，请重新填写连接信息。");
            if (!settings.ShouldSerialize("SSL Mode")) settings.SslMode = SslMode.VerifyFull;
            return settings;
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("数据库连接信息无效，请检查地址、端口和账号。");
        }
    }

    private static async Task<T> WithConnectionAsync<T>(string connection, Func<NpgsqlConnection, Task<T>> action)
    {
        try
        {
            await using var db = new NpgsqlConnection(connection);
            await db.OpenAsync().ConfigureAwait(false);
            return await action(db).ConfigureAwait(false);
        }
        catch (PostgresException error) when (error.SqlState == CloudRepository.UnrecognizedTableSqlState)
        {
            throw ForeignTable();
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            throw new InvalidOperationException($"找不到数据库 {DatabaseName}，请先在 PostgreSQL 中创建该库。");
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            throw new InvalidOperationException("这是旧版工作区表。请点击“初始化并保存”补齐字段，已有工作区数据会保留。");
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.DuplicateTable or PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("工作区表在操作中发生变化，请重新检查目标数据库。");
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
        {
            throw new InvalidOperationException("相关数据已被其他设备修改，保存未生效。请刷新后重试。", error);
        }
        catch (PostgresException error)
        {
            var message = error.SqlState switch
            {
                PostgresErrorCodes.InvalidPassword => "数据库账号或密码不正确。请检查上面的数据库账号和密码。",
                PostgresErrorCodes.InvalidAuthorizationSpecification => "PostgreSQL 拒绝了此账号或来源地址。请检查数据库账号及服务器的 pg_hba.conf 设置。",
                PostgresErrorCodes.InsufficientPrivilege => "数据库账号权限不足。请检查连接库、访问工作区表或初始化所需的权限。",
                _ => $"PostgreSQL 返回错误（SQLSTATE {error.SqlState}）。"
            };
            throw new InvalidOperationException(message, error);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or AuthenticationException or SocketException)
        {
            throw new InvalidOperationException(DescribeConnectionFailure(error), error);
        }
    }

    private static string DescribeConnectionFailure(Exception error)
    {
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
        {
            if (cause is NpgsqlException && cause.Message.StartsWith("SSL connection requested. No SSL enabled connection", StringComparison.Ordinal))
                return "数据库端口已连通，但 PostgreSQL 未启用 TLS。若确认是可信内网，可勾选“允许非加密连接”后重试。";
            if (cause is AuthenticationException)
                return "PostgreSQL 的 TLS 握手或证书验证失败。请核对域名和服务器证书。";
            if (cause is TimeoutException)
                return "PostgreSQL 连接或操作超时。请检查网络和数据库服务状态。";
            if (cause is SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    SocketError.HostNotFound or SocketError.NoData => "找不到数据库地址。请核对域名或 DNS 设置。",
                    SocketError.ConnectionRefused => "数据库端口拒绝连接。请检查端口和 PostgreSQL 监听设置。",
                    SocketError.TimedOut => "连接数据库端口超时。请检查网络、防火墙和端口。",
                    _ => "无法连接数据库端口。请检查网络、地址和端口。"
                };
            }
        }
        return "PostgreSQL 连接中断或未返回有效响应。请检查此端口是否提供 PostgreSQL 服务。";
    }

    private static Task<T> WithWorkspaceAsync<T>(string connection, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action,
        IsolationLevel isolation = IsolationLevel.ReadCommitted) =>
        WithConnectionAsync(connection, async db =>
        {
            await using var transaction = await db.BeginTransactionAsync(isolation).ConfigureAwait(false);
            await RequireOwnedTableAsync(db, transaction).ConfigureAwait(false);
            return await action(db, transaction).ConfigureAwait(false);
        });

    private static async Task RequireOwnedTableAsync(NpgsqlConnection db, NpgsqlTransaction transaction)
    {
        var state = await CloudRepository.LockTableAsync(db, transaction).ConfigureAwait(false);
        if (state == WorkspaceTable.Unrecognized) throw ForeignTable();
        if (state == WorkspaceTable.Missing) throw MissingWorkspace();
        if (state == WorkspaceTable.Legacy) throw MigrationRequired();
    }

    private static async Task<CloudDatabaseTarget> DatabaseTargetAsync(NpgsqlConnection db, NpgsqlTransaction? transaction, WorkspaceTable state)
    {
        const string sql = "SELECT pg_catalog.current_database(), pg_catalog.host(pg_catalog.inet_server_addr()), pg_catalog.inet_server_port()";
        await using var command = new NpgsqlCommand(sql, db, transaction);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await reader.ReadAsync().ConfigureAwait(false);
        var host = reader.IsDBNull(1) ? db.Host : reader.GetString(1);
        var port = reader.IsDBNull(2) ? db.Port : reader.GetInt32(2);
        var database = reader.GetString(0);
        await reader.CloseAsync().ConfigureAwait(false);
        var pending = state == WorkspaceTable.Legacy || (state == WorkspaceTable.Owned
            && (await CloudRepository.ReadCredentialAsync(db, transaction).ConfigureAwait(false)).MigrationPending);
        return new CloudDatabaseTarget(database, CloudRepository.TableNames, $"{host}（端口 {port}）", state == WorkspaceTable.Owned, pending);
    }

    private static InvalidOperationException PasswordChanged() => new("管理员密码已在其他设备修改，请退出云端并重新登录。");
    private static InvalidOperationException MissingWorkspace() => new("该数据库尚未初始化。请点击“初始化并保存”或执行初始化 SQL。");
    private static InvalidOperationException MigrationRequired() => new("发现旧版云端数据。请点击“初始化并保存”迁移到拆分后的表。");
    private static InvalidOperationException ForeignTable() => new($"数据库 {DatabaseName} 已有同名表且无法确认归属。本次未写入，请改用另一个 PostgreSQL 实例。");

    private static byte[] DerivePassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);

    private static bool VerifyPassword(string password, string? stored)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (stored is null) return password == DefaultPassword;

        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            return salt.Length == 16 && expected.Length == 32 && CryptographicOperations.FixedTimeEquals(DerivePassword(password, salt), expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
