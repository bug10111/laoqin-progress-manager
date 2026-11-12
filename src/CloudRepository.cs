// SPDX-License-Identifier: Apache-2.0
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace LaoQin.ProgressManager;

internal enum WorkspaceTable { Missing, Legacy, Owned, Unrecognized }
internal sealed record CloudCredential(string? Hash, bool MigrationPending, long DataGeneration);
internal sealed record LegacyWorkspace(string Json, string? Hash);

internal static class CloudRepository
{
    internal const string Prefix = "public.";
    internal const string UnrecognizedTableSqlState = "LQ001";
    private const string AdminTable = "public.laoqin_admin";
    private const string LegacyTable = "public.laoqin_workspace";
    private const string LegacyName = "laoqin_workspace";
    private const string OwnershipMarker = "LaoQin.ProgressManager.Relational/v2";
    private const string LegacyMarker = "LaoQin.ProgressManager.Workspace/v1";
    private static readonly string[] Tables = ["laoqin_admin", .. RelationalWorkspace.TableNames];
    private static readonly string TableList = string.Join(", ", Tables.Select(name => "ONLY " + Prefix + name));
    internal static string TableNames => string.Join(", ", Tables.Select(name => Prefix + name));

    internal static async Task<WorkspaceTable> InspectTableAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        const string sql = """
            SELECT relname::text, relkind = 'r' AND NOT relispartition, pg_catalog.obj_description(c.oid, 'pg_class')
            FROM pg_catalog.pg_class AS c JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname::text = ANY($1)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(Tables.Append(LegacyName).ToArray());
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var found = new Dictionary<string,string?>(StringComparer.Ordinal);
        while (await reader.ReadAsync().ConfigureAwait(false))
            found.Add(reader.GetString(0), reader.GetBoolean(1) && !reader.IsDBNull(2) ? reader.GetString(2) : null);
        var hasLegacy = found.Remove(LegacyName, out var legacy);
        if (found.Count > 0)
            return found.Count == Tables.Length && found.Values.All(marker => marker == OwnershipMarker)
                ? WorkspaceTable.Owned : WorkspaceTable.Unrecognized;
        return !hasLegacy ? WorkspaceTable.Missing : legacy == LegacyMarker ? WorkspaceTable.Legacy : WorkspaceTable.Unrecognized;
    }

    internal static async Task<WorkspaceTable> LockTableAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var state = await InspectTableAsync(connection, transaction).ConfigureAwait(false);
        if (state != WorkspaceTable.Owned) return state;
        await using var command = new NpgsqlCommand("LOCK TABLE " + TableList + " IN ACCESS SHARE MODE", connection, transaction);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return await InspectTableAsync(connection, transaction).ConfigureAwait(false);
    }

    internal static async Task InitializeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schema)
    {
        await using var command = new NpgsqlCommand(schema, connection, transaction);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    internal static async Task<CloudCredential> ReadCredentialAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string rowLock = "")
    {
        await using var command = new NpgsqlCommand(
            "SELECT password_hash, migration_pending, data_generation FROM ONLY " + AdminTable + " WHERE id = 1" + rowLock, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false)) throw new InvalidOperationException("工作区数据库缺少管理员记录。");
        return new CloudCredential(reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetInt32(1) != 0, reader.GetInt64(2));
    }

    internal static async Task<LegacyWorkspace> ReadLegacyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using (var hold = new NpgsqlCommand("LOCK TABLE ONLY " + LegacyTable + " IN SHARE MODE", connection, transaction))
            await hold.ExecuteNonQueryAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT snapshot::text, pg_catalog.to_jsonb(w) ->> 'admin_password_hash'
            FROM ONLY {LegacyTable} AS w
            WHERE id = 1 AND EXISTS (
                SELECT 1 FROM pg_catalog.pg_class
                WHERE oid = pg_catalog.to_regclass('{LegacyTable}') AND relkind = 'r'
                AND pg_catalog.obj_description(oid, 'pg_class') = $1
            )
            """, connection, transaction);
        command.Parameters.AddWithValue(LegacyMarker);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            throw new InvalidOperationException("无法确认旧工作区数据，已停止迁移。");
        return new LegacyWorkspace(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    internal static async Task SetPasswordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? hash)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE ONLY " + AdminTable + " SET password_hash = $1, migration_pending = 0 WHERE id = 1", connection, transaction);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)hash ?? DBNull.Value);
        if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) != 1)
            throw new InvalidOperationException("工作区数据库缺少管理员记录。");
    }

    internal static async Task<long> AdvanceDataGenerationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand("UPDATE ONLY " + AdminTable
            + " SET data_generation = data_generation + 1 WHERE id = 1 RETURNING data_generation", connection, transaction);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is long generation
            ? generation : throw new InvalidOperationException("工作区数据库缺少管理员记录。");
    }
}
