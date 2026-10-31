// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LaoQin.ProgressManager {
 public static partial class Dialogs {
  static string FileName(string text){return String.Join("_",text.Split(Path.GetInvalidFileNameChars()));}
  public static void Export(Window owner,Store store,List<TaskItem> tasks,DateTime viewStart,DateTime viewEnd){
   var w=Ui.Dialog(owner,"导出项目排期",475);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text("导出项目排期",21,null,true));var desc=Ui.Text("在本地生成文件，无需联网。",12,Ui.Muted);desc.Margin=new Thickness(0,7,0,0);p.Children.Add(desc);var selectedStatuses=Model.Statuses.Where(status=>tasks.Any(t=>t.Status==status)).ToList();var selectionHint=Ui.Text(tasks.Count==0?"当前甘特图状态筛选没有可导出的排期。":"PDF / PNG 使用当前甘特图筛选："+String.Join("、",selectedStatuses)+"（"+tasks.Count+" 项）。XML 仍包含当前项目完整数据。",11,Ui.Muted);selectionHint.Margin=new Thickness(0,11,0,0);p.Children.Add(selectionHint);
   var format=new ComboBox{ItemsSource=new[]{"PDF 文档","PNG 图片","XML 数据"},SelectedIndex=0};Ui.Label(p,"导出格式",format);var scope=new ComboBox{ItemsSource=new[]{"全部排期日期（沿用甘特图状态筛选）","当前视图日期范围"},SelectedIndex=0};Ui.Label(p,"日期范围",scope);
   var main=owner as MainWindow;var details=new CheckBox{Content="是否显示详情",IsChecked=main!=null&&main.ShowTaskDetails,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,18,0,0)};p.Children.Add(details);details.Checked+=(s,e)=>{if(main!=null)main.SetShowTaskDetails(true);};details.Unchecked+=(s,e)=>{if(main!=null)main.SetShowTaskDetails(false);};var summary=Ui.Text("勾选后，甘特预览和 PDF / 图片会在图表下方显示同一份编号任务名称说明。XML 始终包含完整任务数据。",12,Ui.Muted);var box=Ui.Card(summary,13);box.Background=Ui.Bg;box.BorderThickness=new Thickness(0);box.Margin=new Thickness(0,20,0,0);p.Children.Add(box);format.SelectionChanged+=(s,e)=>{details.IsEnabled=format.SelectedIndex!=2;};
   Footer(p,w,()=>{
    var project=store.Data.Projects.First(x=>x.Id==store.Data.CurrentProjectId);int selected=format.SelectedIndex;List<ChartPage> pages=null;
    bool includeDetails=selected!=2&&details.IsChecked==true;if(selected!=2){if(tasks.Count==0)throw new InvalidOperationException("没有可导出的排期，请先安排任务或调整甘特图的状态筛选。");DateTime a=scope.SelectedIndex==1?viewStart:tasks.Min(t=>Model.Parse(t.Start)),b=scope.SelectedIndex==1?viewEnd:tasks.Max(t=>Model.Parse(t.End));pages=Exporter.Pages(tasks,a,b,includeDetails);}
    bool zip=selected==1&&pages.Count>1;var save=new SaveFileDialog{FileName=FileName(project.Name)+"-排期"+(includeDetails?"-含详情":"")+"-"+DateTime.Now.ToString("yyyyMMdd"),Filter=selected==0?"PDF 文档 (*.pdf)|*.pdf":selected==2?"XML 数据 (*.xml)|*.xml":zip?"PNG 图片压缩包 (*.zip)|*.zip":"PNG 图片 (*.png)|*.png",AddExtension=true,OverwritePrompt=true};if(save.ShowDialog(w)!=true)return;
    Mouse.OverrideCursor=Cursors.Wait;try{if(selected==0)Exporter.Pdf(save.FileName,store.Data,project.Name,pages);else if(selected==1)Exporter.Png(save.FileName,store.Data,project.Name,pages);else Exporter.Xml(save.FileName,store.Data,project.Id);}finally{Mouse.OverrideCursor=null;}
    MessageBox.Show(w,"已导出到：\n"+save.FileName+(zip?"\n多页图片已打包为 ZIP，解压后可逐页查看。":""),"导出完成",MessageBoxButton.OK,MessageBoxImage.Information);w.DialogResult=true;
   },"导出文件");w.Content=p;w.ShowDialog();
  }
  public static void Backups(Window owner,Store store){
   var w=Ui.Dialog(owner,"全部数据导入与导出",520);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text("全部数据导入与导出",21,null,true));Ui.Label(p,"本地数据目录",Ui.Text(store.DirectoryPath,12,Ui.Muted));var hint=Ui.Text("完整数据包含全部项目、人员（含历史人员记录）、任务、当前排期及所有状态时间线。导出 JSON 后可在另一台电脑离线导入。",12,Ui.Muted);hint.Margin=new Thickness(0,17,0,15);p.Children.Add(hint);var counts=Ui.Card(Ui.Text(store.Data.Projects.Count+" 个项目  ·  "+store.Data.People.Count+" 位人员  ·  "+store.Data.Tasks.Count+" 项任务\n"+store.Data.Tasks.Sum(t=>t.History.Count)+" 条时间线记录",12,Ui.Muted),12);counts.Background=Ui.Bg;counts.BorderThickness=new Thickness(0);counts.Margin=new Thickness(0,0,0,18);p.Children.Add(counts);
   var backup=Ui.Button("导出全部数据",()=>{var save=new SaveFileDialog{Filter="老秦完整数据 (*.json)|*.json",FileName="老秦全部数据-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),AddExtension=true,OverwritePrompt=true};if(save.ShowDialog(w)==true){store.Backup(save.FileName);MessageBox.Show(w,"全部数据已导出，可在另一台电脑使用“导入全部数据”恢复。","老秦进度管理软件");}},true);p.Children.Add(backup);
   var restore=Ui.Button("导入全部数据",()=>{var open=new OpenFileDialog{Filter="老秦完整数据或备份 (*.json;*.bak)|*.json;*.bak|所有文件 (*.*)|*.*"};if(open.ShowDialog(w)!=true)return;var incoming=Store.Decode(File.ReadAllBytes(open.FileName));if(MessageBox.Show(w,"即将导入 "+incoming.Projects.Count+" 个项目、"+incoming.People.Count+" 位人员、"+incoming.Tasks.Count+" 项任务及完整时间线。\n这将覆盖当前全部数据，导入前会自动保存一份完整备份。是否继续？","全部数据导入确认",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;string safety=store.ImportAll(open.FileName);MessageBox.Show(w,"全部数据导入完成。\n原有数据已备份至：\n"+safety,"老秦进度管理软件");w.Close();var main=owner as MainWindow;if(main!=null)main.Refresh();});restore.Margin=new Thickness(0,11,0,11);p.Children.Add(restore);
   p.Children.Add(Ui.Button("打开数据文件夹",()=>Process.Start(new ProcessStartInfo("explorer.exe",store.DirectoryPath){UseShellExecute=true})));var close=Ui.Button("关闭",()=>w.Close());close.Margin=new Thickness(0,22,0,0);p.Children.Add(close);w.Content=p;w.ShowDialog();
  }
  public static void Help(Window owner,Store store){
   var w=Ui.Dialog(owner,"使用说明",520);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text("老秦 · 离线项目与甘特图",21,null,true));
   var excelHelp=Ui.Text("任务 Excel：在任务列表点击“导出 Excel”，首次默认勾选全部表头；拖动左侧 ≡ 调整列顺序。成功导出后记住顺序和勾选，重启后保留；“恢复默认”重置顺序并全选。导出当前项目、状态、优先级和关键词匹配的全部任务（包含所有分页）；无筛选时导出当前项目全部任务。追加需求按时间逐行展示，同一任务的基础信息合并。取消勾选的字段不导出，取消弹窗不生成文件，当前筛选条件保持不变。",12,Ui.Muted);excelHelp.Margin=new Thickness(0,17,0,0);p.Children.Add(excelHelp);
   foreach(var text in new[]{"1. 左侧项目下拉可切换、新增和删除项目，可删除最后一个项目。删除时清除该项目的任务、排期和时间线，共享人员及备份保留。","2. 新建任务后，点击任务名称查看时间线；点击状态或“编辑任务”修改信息。设为执行中时必须选择负责人和起止日期。","3. 甘特图默认显示执行中。顶部状态下拉可切换任务状态、全部状态，或使用自选多状态；有负责人和完整计划日期的任务会按所选状态显示，已明确退出排期的任务除外。已完成视图也包含有日期的已完成任务。PDF / PNG 导出和图表详情沿用当前状态筛选。","4. 点击甘特条，先按状态筛选，再按任务名称搜索并多选。搜索后点击“全选”只选择匹配结果；保存时选中的任务替换当前甘特条。原任务若未再次选中，则退出本次排期，任务、原日期和历史记录保留。","5. 同一任务在当前排期中只有一个甘特条。多选任务使用同一负责人和起止日期，任务状态保持不变。日期按自然日计算，包含结束当天；周六、周日单独标注。","6. PDF 使用 A4 横向分页；PNG 多页自动打包为 ZIP。XML 为通用数据格式，并非 Microsoft Project 的专用格式。","7. 左侧“数据导入导出”可导出全部项目、人员、任务、排期和时间线。导入全部数据会覆盖当前数据，并自动备份导入前的数据。首次启动的示例项目可自行删除。","快捷键：Ctrl+N 新建任务，Ctrl+E 导出甘特图。"}){var t=Ui.Text(text,12,Ui.Muted);t.Margin=new Thickness(0,17,0,0);p.Children.Add(t);}var close=Ui.Button("知道了",()=>w.Close(),true);close.Margin=new Thickness(0,24,0,0);p.Children.Add(close);w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
  }
 }
}
