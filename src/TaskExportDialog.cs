// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace LaoQin.ProgressManager {
 public static partial class Dialogs {
  // 先选字段，再选择保存位置；关闭任一弹窗都不会生成文件。
  internal static List<TaskExportField> ChooseTaskExportColumns(Window owner,int taskCount,string summary,TaskExportPreferences preferences=null,Action<TaskExportPreferences> onAccepted=null){
   List<TaskExportField> result=null;var w=Ui.Dialog(owner,"导出任务 Excel",560);w.MaxHeight=Math.Min(820,SystemParameters.WorkArea.Height-60);w.SizeToContent=SizeToContent.Manual;w.Height=w.MaxHeight;
   var root=new DockPanel{Margin=new Thickness(25)};var heading=Ui.Stack();DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
   heading.Children.Add(Ui.Text("导出任务 Excel",21,null,true));var scope=Ui.Text("将导出全部 "+taskCount+" 项匹配任务（包含所有分页）。\n"+summary,12,Ui.Muted);scope.Margin=new Thickness(0,10,0,16);heading.Children.Add(scope);
   var picker=new TaskExportColumnPicker(preferences);var footer=Ui.Stack();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var selectionCount=Ui.Text("",12,Ui.Muted);selectionCount.Margin=new Thickness(0,14,0,0);footer.Children.Add(selectionCount);
   var note=Ui.Text("每条追加需求单独一行，带时间与序号；同一任务的基础信息纵向合并。长文本自动换行，超长内容续行保留。",11,Ui.Muted);note.Margin=new Thickness(0,9,0,0);footer.Children.Add(note);
   var actions=Ui.Stack(true);actions.HorizontalAlignment=HorizontalAlignment.Right;actions.Margin=new Thickness(0,20,0,0);footer.Children.Add(actions);
   var cancel=Ui.Button("取消",()=>w.Close());cancel.IsCancel=true;cancel.Margin=new Thickness(0,0,9,0);actions.Children.Add(cancel);
   var export=Ui.Button("选择保存位置",()=>{result=picker.SelectedFields;if(result.Count==0)throw new InvalidOperationException("请至少勾选一个导出字段。");if(onAccepted!=null)onAccepted(picker.Preferences());w.DialogResult=true;},true);actions.Children.Add(export);
   var content=Ui.Stack();var allRow=Ui.Stack(true);allRow.Margin=new Thickness(0,0,0,8);content.Children.Add(allRow);
   var selectAll=Ui.Button("全选",()=>picker.SelectAll(true));selectAll.Margin=new Thickness(0,0,8,0);allRow.Children.Add(selectAll);var selectNone=Ui.Button("全不选",()=>picker.SelectAll(false));selectNone.Margin=new Thickness(0,0,8,0);allRow.Children.Add(selectNone);allRow.Children.Add(Ui.Button("恢复默认",()=>picker.ResetDefaults()));
   content.Children.Add(Ui.Text("按住左侧 ≡ 上下拖动，调整表头顺序",12,null,true));var remembered=Ui.Text("从上到下对应 Excel 从左到右；成功导出后记住顺序与勾选。",11,Ui.Muted);remembered.Margin=new Thickness(0,6,0,10);content.Children.Add(remembered);DockPanel.SetDock(content,Dock.Top);root.Children.Add(content);
   Action update=()=>{int count=picker.SelectedFields.Count;selectionCount.Text="已选择 "+count+" / "+TaskExcelExporter.Columns.Length+" 列";export.IsEnabled=count>0;};picker.Changed+=update;
   update();root.Children.Add(new ScrollViewer{Content=picker,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});w.Content=root;
   return w.ShowDialog()==true?result:null;
  }
  public static void ExportTasks(Window owner,Store store,TaskQuery query){
   int count=query.Apply(store.Data).Count;if(count==0){MessageBox.Show(owner,"当前筛选条件下没有可导出的任务，请调整筛选条件。","导出任务 Excel",MessageBoxButton.OK,MessageBoxImage.Information);return;}
   TaskExportPreferences preferences=null;var columns=ChooseTaskExportColumns(owner,count,query.Summary(store.Data),store.Data.TaskExcelPreferences,p=>preferences=p);if(columns==null)return;
   var project=store.Data.Projects.First(p=>p.Id==query.ProjectId);
   var save=new SaveFileDialog{Title="保存任务 Excel",FileName=FileName(project.Name)+"-任务清单-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),Filter="Excel 工作簿 (*.xlsx)|*.xlsx",DefaultExt=".xlsx",AddExtension=true,OverwritePrompt=true};
   if(save.ShowDialog(owner)!=true)return;
   Mouse.OverrideCursor=Cursors.Wait;try{TaskExcelExporter.Export(save.FileName,store.Data,query,columns);store.Commit(d=>d.TaskExcelPreferences=preferences);}finally{Mouse.OverrideCursor=null;}
   MessageBox.Show(owner,"已导出 "+count+" 项任务、"+columns.Count+" 列。\n"+save.FileName,"导出完成",MessageBoxButton.OK,MessageBoxImage.Information);
  }
 }
}
