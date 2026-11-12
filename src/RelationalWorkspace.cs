// SPDX-License-Identifier: Apache-2.0
#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace LaoQin.ProgressManager;

internal static class RelationalWorkspace
{
    internal static readonly string[] TableNames =
        ["laoqin_projects", "laoqin_people", "laoqin_tasks", "laoqin_timeline", "laoqin_settings", "laoqin_export_preferences"];

    private sealed record Table(string Name, string Columns, string Sort = "sort_order, id")
    {
        internal string[] Fields { get; } = Columns.Split(',');
    }
    private static readonly Table Projects = new(TableNames[0], "id,sort_order,name,example");
    private static readonly Table People = new(TableNames[1], "id,sort_order,name,deleted");
    private static readonly Table Tasks = new(TableNames[2], "id,sort_order,project_id,name,status,priority,person_id,plan_start,plan_end,legacy_note,in_gantt");
    private static readonly Table Timeline = new(TableNames[3], "id,task_id,sort_order,event_at,event_kind,before_status,after_status,priority,task_name,person_name,plan_start,plan_end,detail,note", "task_id, sort_order, id");
    private static readonly Table Settings = new(TableNames[4], "id,current_project_id,has_preferences,has_column_order,has_selected_columns,has_timeline_types", "id");
    private static readonly Table Preferences = new(TableNames[5], "id,kind,sort_order,field_value", "kind, sort_order");
    private static readonly Table[] Tables = [Projects, People, Tasks, Timeline, Settings, Preferences];
    private sealed record Mutation(string Sql, object?[] Values);

    internal static async Task<AppData> ReadAsync(DbConnection db, DbTransaction transaction, string prefix)
    {
        var data = new AppData { CurrentProjectId = "" };
        using var command = Command(db, transaction, string.Join(";\n",
            Tables.Select(table => $"SELECT {table.Columns} FROM {prefix}{table.Name} ORDER BY {table.Sort}")));
        using var rows = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await ReadRows(rows, r => data.Projects.Add(new Project {
            Id = r.GetString(0), StorageOrder = r.GetInt64(1), Name = r.GetString(2), Example = r.GetInt32(3) != 0
        })).ConfigureAwait(false);
        await ReadRows(rows, r => data.People.Add(new Person {
            Id = r.GetString(0), StorageOrder = r.GetInt64(1), Name = r.GetString(2), Deleted = r.GetInt32(3) != 0
        })).ConfigureAwait(false);
        await ReadRows(rows, r => data.Tasks.Add(new TaskItem {
            Id = r.GetString(0), StorageOrder = r.GetInt64(1), ProjectId = r.GetString(2), Name = r.GetString(3),
            Status = r.GetString(4), Priority = r.GetString(5), PersonId = Text(r, 6) ?? "", Start = Text(r, 7),
            End = Text(r, 8), Note = Text(r, 9), InGantt = r.GetInt32(10) != 0
        })).ConfigureAwait(false);
        var tasks = data.Tasks.ToDictionary(t => t.Id);
        await ReadRows(rows, r => tasks[r.GetString(1)].History.Add(new Change {
            StorageId = r.GetString(0), StorageOrder = r.GetInt64(2), At = r.GetString(3), Kind = Text(r, 4),
            Before = Text(r, 5), After = r.GetString(6), Priority = Text(r, 7), Name = Text(r, 8), PersonName = Text(r, 9),
            Start = Text(r, 10), End = Text(r, 11), Detail = Text(r, 12), Note = Text(r, 13)
        })).ConfigureAwait(false);

        var settingsCount = await ReadRows(rows, r => {
            data.StoredCurrentProjectId = Text(r, 1);
            data.HasStoredCurrentProjectId = true;
            data.CurrentProjectId = data.StoredCurrentProjectId ?? "";
            if (r.GetInt32(2) != 0) data.TaskExcelPreferences = new TaskExportPreferences {
                Order = r.GetInt32(3) == 0 ? null : [], Selected = r.GetInt32(4) == 0 ? null : [],
                TimelineTypes = r.GetInt32(5) == 0 ? null : []
            };
        }).ConfigureAwait(false);
        if (settingsCount != 1) throw new InvalidOperationException("工作区数据库缺少设置记录。");
        await ReadRows(rows, r => {
            var preferences = data.TaskExcelPreferences;
            var list = r.GetString(1) switch {
                "order" => preferences?.Order, "selected" => preferences?.Selected, "timeline" => preferences?.TimelineTypes,
                _ => null
            };
            if (list is null) throw new InvalidOperationException("工作区导出设置不完整。");
            list.Add(Text(r, 3)!);
        }).ConfigureAwait(false);
        Model.Check(data);
        return data;
    }

    internal static async Task ApplyAsync(DbConnection db, DbTransaction transaction, string prefix, AppData before, AppData after)
    {
        Model.Check(after);
        var oldRows = Rows(before, true);
        var newRows = Rows(after, false);
        var writes = new List<Mutation>();
        // 先写父记录，再写子记录。删除反序且不级联，避免删掉其他电脑刚追加的记录。
        for (var i = 0; i < Tables.Length; i++)
        {
            foreach (var (key, row) in newRows[i])
            {
                if (!oldRows[i].TryGetValue(key, out var old))
                    writes.Add(Insert(prefix, Tables[i], row));
                else if (!old.SequenceEqual(row))
                    writes.Add(Update(prefix, Tables[i], old, row));
            }
        }
        for (var i = Tables.Length - 1; i >= 0; i--)
            foreach (var (key, row) in oldRows[i])
                if (!newRows[i].ContainsKey(key))
                    writes.Add(Delete(prefix, Tables[i], row));
        await ExecuteAsync(db, transaction, writes).ConfigureAwait(false);
        after.StoredCurrentProjectId = NullIfEmpty(after.CurrentProjectId);
        after.HasStoredCurrentProjectId = true;
    }

    private static Dictionary<string, object?[]>[] Rows(AppData data, bool original)
    {
        AssignOrder(data.Projects, p => p.StorageOrder, (p, order) => p.StorageOrder = order);
        AssignOrder(data.People, p => p.StorageOrder, (p, order) => p.StorageOrder = order);
        AssignOrder(data.Tasks, t => t.StorageOrder, (t, order) => t.StorageOrder = order);

        var timeline = new Dictionary<string, object?[]>();
        foreach (var task in data.Tasks)
        {
            // 新时间线节点插到前面，原有节点不改序号，也不用重写整条时间线。
            long next = task.History.Where(h => h.StorageOrder.HasValue).Select(h => h.StorageOrder!.Value)
                .DefaultIfEmpty(task.History.Count).Min();
            for (var index = task.History.Count - 1; index >= 0; index--)
            {
                var h = task.History[index];
                h.StorageId ??= Model.Id();
                h.StorageOrder ??= --next;
                timeline.Add(h.StorageId, [h.StorageId, task.Id, h.StorageOrder.Value, h.At, h.Kind, h.Before, h.After,
                    h.Priority, h.Name, h.PersonName, h.Start, h.End, h.Detail, h.Note]);
            }
        }
        var prefs = data.TaskExcelPreferences;
        var preferenceRows = new Dictionary<string, object?[]>();
        void AddPreferences(string kind, List<string>? values)
        {
            if (values is null) return;
            for (var i = 0; i < values.Count; i++)
            {
                var id = kind + ":" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                preferenceRows.Add(id, [id, kind, (long)i, values[i]]);
            }
        }
        AddPreferences("order", prefs?.Order);
        AddPreferences("selected", prefs?.Selected);
        AddPreferences("timeline", prefs?.TimelineTypes);
        return [
            data.Projects.ToDictionary(p => p.Id, p => new object?[]{p.Id, p.StorageOrder!.Value, p.Name, Flag(p.Example)}),
            data.People.ToDictionary(p => p.Id, p => new object?[]{p.Id, p.StorageOrder!.Value, p.Name, Flag(p.Deleted)}),
            data.Tasks.ToDictionary(t => t.Id, t => new object?[]{t.Id, t.StorageOrder!.Value, t.ProjectId, t.Name, t.Status,
                t.Priority, NullIfEmpty(t.PersonId), t.Start, t.End, t.Note, Flag(t.InGantt)}),
            timeline,
            new() { ["workspace"] = ["workspace", original && data.HasStoredCurrentProjectId ? data.StoredCurrentProjectId : NullIfEmpty(data.CurrentProjectId), Flag(prefs != null),
                Flag(prefs?.Order != null), Flag(prefs?.Selected != null), Flag(prefs?.TimelineTypes != null)] },
            preferenceRows
        ];
    }

    private static void AssignOrder<T>(IEnumerable<T> list, Func<T,long?> get, Action<T,long> set)
    {
        long next = list.Select(get).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty(-1).Max();
        foreach (var item in list) if (get(item) is null) set(item, ++next);
    }
    private static int Flag(bool value) => value ? 1 : 0;
    private static string? Text(DbDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static async Task<int> ReadRows(DbDataReader rows, Action<DbDataReader> read)
    {
        int count = 0;
        while (await rows.ReadAsync().ConfigureAwait(false)) { read(rows); count++; }
        await rows.NextResultAsync().ConfigureAwait(false);
        return count;
    }
    private static Mutation Insert(string prefix, Table table, object?[] values)
    {
        var args = string.Join(",", Enumerable.Range(0, values.Length).Select(Parameter));
        return new Mutation($"INSERT INTO {prefix}{table.Name} ({table.Columns}) VALUES ({args}) ON CONFLICT (id) DO NOTHING", values);
    }
    private static Mutation Update(string prefix, Table table, object?[] before, object?[] after)
    {
        var changed = Enumerable.Range(1, after.Length - 1).Where(i => !Equals(before[i], after[i])).ToArray();
        var sets = string.Join(",", changed.Select((index, i) => table.Fields[index] + "=" + Parameter(before.Length + i)));
        return new Mutation($"UPDATE {prefix}{table.Name} SET {sets} WHERE {Match(table)}", [..before, ..changed.Select(i => after[i])]);
    }
    private static Mutation Delete(string prefix, Table table, object?[] before) =>
        new($"DELETE FROM {prefix}{table.Name} WHERE {Match(table)}", before);

    private static string Match(Table table) => "id=@p0 AND " + string.Join(" AND ",
        table.Fields.Skip(1).Select((field, i) => field + " IS NOT DISTINCT FROM " + Parameter(i + 1)));
    private static string Parameter(int number) => "@p" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static async Task ExecuteAsync(DbConnection db, DbTransaction tx, List<Mutation> writes)
    {
        if (db is NpgsqlConnection pg && tx is NpgsqlTransaction pgTx)
        {
            // 按批发送参数化语句，跨网迁移时避免每行一次网络往返。
            foreach (var group in writes.Chunk(128))
            {
                await using var batch = new NpgsqlBatch(pg, pgTx);
                foreach (var write in group)
                {
                    var command = new NpgsqlBatchCommand(write.Sql);
                    AddParameters(command.Parameters, () => new NpgsqlParameter(), write.Values);
                    batch.BatchCommands.Add(command);
                }
                await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
                foreach (NpgsqlBatchCommand command in batch.BatchCommands)
                    if (command.Rows != 1) throw SaveConflict();
            }
            return;
        }
        foreach (var write in writes)
        {
            using var command = Command(db, tx, write.Sql);
            AddParameters(command.Parameters, command.CreateParameter, write.Values);
            if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) != 1) throw SaveConflict();
        }
    }
    private static InvalidOperationException SaveConflict() => new("这条数据已被其他设备修改或删除。请刷新云端数据后重试。");
    private static void AddParameters(DbParameterCollection parameters, Func<DbParameter> create, object?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var arg = create();
            arg.ParameterName = Parameter(i);
            arg.DbType = value switch { int => DbType.Int32, long => DbType.Int64, _ => DbType.String };
            arg.Value = value ?? DBNull.Value;
            parameters.Add(arg);
        }
    }
    internal static DbCommand Command(DbConnection db, DbTransaction tx, string sql)
    {
        var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        return command;
    }
}
