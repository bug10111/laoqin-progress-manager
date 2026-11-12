// SPDX-License-Identifier: Apache-2.0
#nullable enable

using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace LaoQin.ProgressManager;

internal static class LocalWorkspace
{
    internal static AppData Read(string path)
    {
        using var db = Open(path, SqliteOpenMode.ReadOnly);
        using var transaction = db.BeginTransaction(deferred: true);
        return RelationalWorkspace.ReadAsync(db, transaction, "").GetAwaiter().GetResult();
    }

    internal static void Save(string path, AppData before, AppData after)
    {
        using var db = Open(path, SqliteOpenMode.ReadWrite);
        using var transaction = db.BeginTransaction();
        RelationalWorkspace.ApplyAsync(db, transaction, "", before, after).GetAwaiter().GetResult();
        transaction.Commit();
    }

    internal static void Create(string path, AppData data)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var db = Open(temporary, SqliteOpenMode.ReadWriteCreate))
            using (var transaction = db.BeginTransaction())
            {
                const string start = "/* COMMON_DDL_START */";
                const string end = "/* COMMON_DDL_END */";
                var schema = System.Text.Encoding.UTF8.GetString(CloudClient.SchemaBytes());
                var ddlStart = schema.IndexOf(start, StringComparison.Ordinal) + start.Length;
                var ddlEnd = schema.IndexOf(end, StringComparison.Ordinal);
                using var command = RelationalWorkspace.Command(db, transaction,
                    schema[ddlStart..ddlEnd].Replace("public.", "", StringComparison.Ordinal)
                    + "INSERT INTO laoqin_settings VALUES ('workspace', NULL, 0, 0, 0, 0);");
                command.ExecuteNonQuery();
                RelationalWorkspace.ApplyAsync(db, transaction, "", new AppData(), data).GetAwaiter().GetResult();
                transaction.Commit();
            }
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path, Mode = mode, ForeignKeys = true, Pooling = false
        }.ConnectionString);
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }
}
