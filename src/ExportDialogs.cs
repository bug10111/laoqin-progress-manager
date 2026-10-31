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
  public static void Export(Window owner,Store store,ChartPage chart){
   var w=Ui.Dialog(owner,"导出项目排期",475);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text("导出项目排期",21,null,true));var desc=Ui.Text("按当前甘特图的样式导出为一个文件。",12,Ui.Muted);desc.Margin=new Thickness(0,7,0,0);p.Children.Add(desc);
   var format=new ComboBox{ItemsSource=new[]{"PDF 文档","PNG 图片","XML 数据"},SelectedIndex=0};Ui.Label(p,"导出格式",format);
   var viewInfo=Ui.Text(chart.Start.ToString("yyyy.MM.dd")+" — "+chart.Start.AddDays(chart.Days-1).ToString("yyyy.MM.dd")+"\n"+(chart.GroupPeople?"按人员分组":"按任务展示")+" · "+chart.Tasks.Count+" 项任务 · "+(chart.IncludeDetails?"显示详情":"隐藏详情"),12,Ui.Muted);Ui.Label(p,"当前甘特图",viewInfo);
   var summary=Ui.Text("保留当前日期范围、状态筛选、分组、任务顺序、缩放列宽和详情设置，包含滚动区域内的完整甘特图。PDF 导出为一张连续页面，PNG 导出为一张完整图片。",12,Ui.Muted);var box=Ui.Card(summary,13);box.Background=Ui.Bg;box.BorderThickness=new Thickness(0);box.Margin=new Thickness(0,20,0,0);p.Children.Add(box);string chartSummary=summary.Text;format.SelectionChanged+=(s,e)=>{viewInfo.IsEnabled=format.SelectedIndex!=2;summary.Text=format.SelectedIndex==2?"XML 包含当前项目的全部任务、人员、排期和时间线，不受甘特图视图筛选影响。":chartSummary;};
   Footer(p,w,()=>{
    var project=store.Data.Projects.First(x=>x.Id==store.Data.CurrentProjectId);int selected=format.SelectedIndex;if(selected!=2)Exporter.RasterScale(chart);
    var save=new SaveFileDialog{FileName=FileName(project.Name)+"-排期"+(selected!=2&&chart.IncludeDetails?"-含详情":"")+"-"+DateTime.Now.ToString("yyyyMMdd"),Filter=selected==0?"PDF 文档 (*.pdf)|*.pdf":selected==2?"XML 数据 (*.xml)|*.xml":"PNG 图片 (*.png)|*.png",AddExtension=true,OverwritePrompt=true};if(save.ShowDialog(w)!=true)return;
    Mouse.OverrideCursor=Cursors.Wait;try{if(selected==0)Exporter.Pdf(save.FileName,store.Data,chart);else if(selected==1)Exporter.Png(save.FileName,store.Data,chart);else Exporter.Xml(save.FileName,store.Data,project.Id);}finally{Mouse.OverrideCursor=null;}
    MessageBox.Show(w,"已导出到：\n"+save.FileName,"导出完成",MessageBoxButton.OK,MessageBoxImage.Information);w.DialogResult=true;
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
   foreach(var text in new[]{"1. 左侧项目下拉可切换、新增和删除项目，可删除最后一个项目。删除时清除该项目的任务、排期和时间线，共享人员及备份保留。","2. 新建任务后，通过列表“查看”打开时间线，通过“编辑”修改信息。设为执行中时必须选择负责人，计划日期可留空。","3. 甘特图默认显示执行中。顶部使用一个状态多选下拉，单选、多选和全选均在其中完成；有负责人和完整计划日期的任务会按所选状态显示，已明确退出排期的任务除外。已完成视图也包含有日期的已完成任务。PDF / PNG 导出和图表详情沿用当前状态筛选。","4. 同一人的甘特条按计划天数从短到长排列。Shift + 单击任务条或名称可增减选择，拖选中条中间可整组移动；拖左、右边缘仅修改当前任务的起止日期。松开统一保存，Esc 取消。单击仍打开排期编辑窗口，先按状态筛选，再按任务名称搜索并多选。搜索后点击“全选”只选择匹配结果；保存时选中的任务替换当前甘特条。原任务若未再次选中，则退出本次排期，任务、原日期和历史记录保留。","5. 同一任务在当前排期中只有一个甘特条。多选任务使用同一负责人和起止日期；默认保持原状态，也可在“修改任务状态”中统一修改并记录变更。所有日期范围均可清空；保存后退出当前排期，保留任务状态及原计划历史。日期按自然日计算，包含结束当天；周六、周日单独标注。","6. PDF / PNG 沿用当前甘特图的日期、筛选、分组、任务顺序、缩放列宽和详情，完整保留滚动区域内容。PDF 为单张连续页面，PNG 为单张完整图片，只生成一个文件。XML 为通用数据格式，并非 Microsoft Project 的专用格式。","7. 左侧“数据导入导出”可导出全部项目、人员、任务、排期和时间线。导入全部数据会覆盖当前数据，并自动备份导入前的数据。首次启动的示例项目可自行删除。","快捷键：Ctrl+N 新建任务，Ctrl+E 导出甘特图。甘特图预览：日期栏固定顶部、人员列固定左侧，同组人员姓名吸顶；Shift + 滚轮缩放时间轴（25%～400%），底部滚动条可左右查看，重置按钮恢复 100%。"}){var t=Ui.Text(text,12,Ui.Muted);t.Margin=new Thickness(0,17,0,0);p.Children.Add(t);}var close=Ui.Button("知道了",()=>w.Close(),true);close.Margin=new Thickness(0,24,0,0);p.Children.Add(close);w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
  }
 }
}
