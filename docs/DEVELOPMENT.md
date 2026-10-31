# 构建与验证

[返回首页](../README.md) · [使用手册](USER_GUIDE.md)

## 环境与构建

项目使用 C#、WPF 与 .NET Framework，目标框架为 4.6，AnyCPU 构建。运行和普通构建无需 Node.js、Python、浏览器、.NET SDK 或第三方 NuGet 包。

`build.ps1` 使用 Windows Framework 目录中的 `csc.exe`，优先 64 位，不存在时回退到 32 位。传统格式的 `LaoQin.ProgressManager.csproj` 使用 `src/*.cs` 纳入所有源码。

在允许本地脚本运行的 PowerShell 中进入仓库：

```powershell
.\build.ps1
.\dist\LaoQinProgressManager.exe
```

指定目录：

```powershell
.\build.ps1 -OutputPath .\release\local
.\release\local\LaoQinProgressManager.exe
```

输出文件名为 `LaoQinProgressManager.exe`。脚本保留 UTF-8 BOM，以兼容 Windows PowerShell 5.1。部分系统会显示旧版 `FormattedText` 构造函数的弃用警告，当前保留该调用以兼容目标框架。

## 独立数据目录

默认目录为 `%LOCALAPPDATA%\LaoQinProgress`。开发预览可使用独立数据：

```powershell
.\dist\LaoQinProgressManager.exe --data-dir .\qa\manual-demo
```

同一目录只允许一个进程写入。测试、截图使用独立示例数据，不要复制真实项目数据进仓库。

## 自检与界面检查

使用全新目录，避免锁冲突和旧结果干扰：

```powershell
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$exePath = Join-Path $PWD 'dist\LaoQinProgressManager.exe'
$coreDir = Join-Path $PWD "qa\core-$runId"
$uiDir = Join-Path $PWD "qa\ui-$runId"

$coreRun = Start-Process -FilePath $exePath -ArgumentList ('--self-test "{0}"' -f $coreDir) -WorkingDirectory $PWD.Path -WindowStyle Hidden -Wait -PassThru
$uiRun = Start-Process -FilePath $exePath -ArgumentList ('--ui-check "{0}"' -f $uiDir) -WorkingDirectory $PWD.Path -WindowStyle Hidden -Wait -PassThru

if ($coreRun.ExitCode -ne 0 -or $uiRun.ExitCode -ne 0) {
    throw '检查未通过，请查看对应目录中的 FAILED.txt'
}
Get-Content (Join-Path $coreDir '测试结果.txt')
Get-Content (Join-Path $uiDir '界面检查.txt')
```

退出码 `0` 表示成功，失败时生成 `FAILED.txt`。检查不修改正常使用的数据。界面检查会打开原生 WPF 测试窗口，触发表单保存、筛选与拖动，并输出截图和分项记录。

| 检查 | 覆盖 |
| --- | --- |
| 数据 | JSON 往返、写入锁、备份恢复、导入校验、项目删除隔离、零项目工作区 |
| 任务 | 状态、优先级、排序、备注、需求追加及时间线 |
| 排期 | 多任务替换、修改状态、日期清空和日期联动 |
| 拖动 | 单端、整体、批量原子保存、日期边界、取消及历史记录 |
| 视图 | 缩放锚点、滚动、多选、日期与人员冻结、分组吸顶、统一状态筛选 |
| 导出 | Excel 字段、合并、长文本；甘特图与导出像素对齐、当前视图状态、单页 PDF、单张 PNG 及 XML |

更新文档截图时，选择检查输出的对应 PNG 复制到 `docs/images/`，检查不含真实数据目录、个人数据或桌面内容。

## 源码结构

| 文件 | 职责 |
| --- | --- |
| `src/Program.cs` | 启动、程序集版本、命令行及旧目录迁移 |
| `src/Models.cs` / `src/Store.cs` | 模型、校验、事务保存、备份和导入 |
| `src/MainWindow.cs` | 主界面、列表、筛选和视图状态 |
| `src/Dialogs.cs` / `src/DateRangePicker.cs` | 表单与日期范围 |
| `src/Gantt.cs` / `src/GanttDrag.cs` | 绘制、排序、多选及拖动 |
| `src/GanttViewport.cs` / `src/GanttFrozenHeader.cs` | 缩放、滚动、冻结日期及人员 |
| `src/Export.cs` / `src/ExportDialogs.cs` | PDF、PNG、XML 和导出窗口 |
| `src/TaskExcelExport.cs` / `src/TaskExport*.cs` | Excel 查询、字段、排序及导出 |
| `src/*Tests.cs` | 数据及原生界面检查 |

`dist/`、`release/`、`qa/`、本地数据、备份和临时文件由 `.gitignore` 排除。提交前运行 `git diff --check`，只提交源码、文档及已检查的示例截图。
