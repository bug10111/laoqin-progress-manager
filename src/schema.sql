-- SPDX-License-Identifier: Apache-2.0
DO $laoqin_init$
DECLARE
    table_name text;
    relation oid;
    legacy oid;
    found_count integer := 0;
    table_names text[] := ARRAY['laoqin_admin','laoqin_projects','laoqin_people','laoqin_tasks',
        'laoqin_timeline','laoqin_settings','laoqin_export_preferences'];
BEGIN
    IF pg_catalog.current_database() <> 'laoqin' THEN
        RAISE EXCEPTION USING ERRCODE = 'LQ002', MESSAGE = '请在 laoqin 数据库执行此脚本。';
    END IF;
    FOREACH table_name IN ARRAY table_names LOOP
        relation := pg_catalog.to_regclass('public.' || table_name);
        IF relation IS NULL THEN CONTINUE; END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_class WHERE oid = relation AND relkind = 'r'
            AND NOT relispartition AND pg_catalog.obj_description(oid, 'pg_class') = 'LaoQin.ProgressManager.Relational/v2') THEN
            RAISE EXCEPTION USING ERRCODE = 'LQ001', MESSAGE = '无法确认同名表归属：public.' || table_name;
        END IF;
        found_count := found_count + 1;
    END LOOP;
    IF found_count = pg_catalog.array_length(table_names, 1) THEN RETURN; END IF;
    IF found_count <> 0 THEN
        RAISE EXCEPTION USING ERRCODE = 'LQ001', MESSAGE = '应用表不完整，已停止初始化。';
    END IF;

    legacy := pg_catalog.to_regclass('public.laoqin_workspace');
    IF legacy IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_class WHERE oid = legacy AND relkind = 'r'
          AND pg_catalog.obj_description(oid, 'pg_class') = 'LaoQin.ProgressManager.Workspace/v1'
    ) THEN
        RAISE EXCEPTION USING ERRCODE = 'LQ001', MESSAGE = '无法确认旧工作区表的归属。';
    END IF;

    -- SQLite 使用这段相同的 DDL，将 public 限定名去掉。
    /* COMMON_DDL_START */
    CREATE TABLE public.laoqin_projects (
        id text PRIMARY KEY NOT NULL, sort_order bigint NOT NULL, name text NOT NULL,
        example integer NOT NULL CHECK (example IN (0, 1))
    );
    CREATE TABLE public.laoqin_people (
        id text PRIMARY KEY NOT NULL, sort_order bigint NOT NULL, name text NOT NULL,
        deleted integer NOT NULL CHECK (deleted IN (0, 1))
    );
    CREATE TABLE public.laoqin_tasks (
        id text PRIMARY KEY NOT NULL, sort_order bigint NOT NULL,
        project_id text NOT NULL REFERENCES public.laoqin_projects(id) ON DELETE RESTRICT,
        name text NOT NULL, status text NOT NULL,
        priority text NOT NULL CHECK (priority IN ('P0','P1','P2','P3')),
        person_id text REFERENCES public.laoqin_people(id) ON DELETE RESTRICT,
        plan_start text, plan_end text, legacy_note text,
        in_gantt integer NOT NULL CHECK (in_gantt IN (0, 1))
    );
    CREATE TABLE public.laoqin_timeline (
        id text PRIMARY KEY NOT NULL,
        task_id text NOT NULL REFERENCES public.laoqin_tasks(id) ON DELETE RESTRICT,
        sort_order bigint NOT NULL, event_at text NOT NULL, event_kind text,
        before_status text, after_status text NOT NULL, priority text, task_name text,
        person_name text, plan_start text, plan_end text, detail text, note text
    );
    CREATE TABLE public.laoqin_settings (
        id text PRIMARY KEY NOT NULL CHECK (id = 'workspace'),
        current_project_id text REFERENCES public.laoqin_projects(id) ON DELETE SET NULL,
        has_preferences integer NOT NULL CHECK (has_preferences IN (0, 1)),
        has_column_order integer NOT NULL CHECK (has_column_order IN (0, 1)),
        has_selected_columns integer NOT NULL CHECK (has_selected_columns IN (0, 1)),
        has_timeline_types integer NOT NULL CHECK (has_timeline_types IN (0, 1))
    );
    CREATE TABLE public.laoqin_export_preferences (
        id text PRIMARY KEY NOT NULL, kind text NOT NULL CHECK (kind IN ('order','selected','timeline')),
        sort_order bigint NOT NULL, field_value text, UNIQUE (kind, sort_order)
    );
    CREATE INDEX laoqin_tasks_project_idx ON public.laoqin_tasks(project_id, status, sort_order);
    CREATE INDEX laoqin_tasks_person_idx ON public.laoqin_tasks(person_id);
    CREATE INDEX laoqin_timeline_task_idx ON public.laoqin_timeline(task_id, sort_order);
    /* COMMON_DDL_END */

    CREATE TABLE public.laoqin_admin (
        id integer PRIMARY KEY CHECK (id = 1), password_hash text,
        data_generation bigint NOT NULL DEFAULT 1 CHECK (data_generation > 0),
        migration_pending integer NOT NULL CHECK (migration_pending IN (0, 1))
    );
    FOREACH table_name IN ARRAY table_names LOOP
        EXECUTE pg_catalog.format('COMMENT ON TABLE public.%I IS %L', table_name, 'LaoQin.ProgressManager.Relational/v2');
    END LOOP;
    INSERT INTO public.laoqin_admin (id, migration_pending) VALUES (1, CASE WHEN legacy IS NULL THEN 0 ELSE 1 END);
    INSERT INTO public.laoqin_settings VALUES ('workspace', NULL, 0, 0, 0, 0);
END;
$laoqin_init$;
