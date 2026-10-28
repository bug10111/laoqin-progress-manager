// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Xucheng {
 internal static class VisualTests {
  static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T)yield return (T)c;foreach(var d in Children<T>(c))yield return d;}}
  static void Save(FrameworkElement el,string path,bool arrange=false){if(arrange){el.Measure(new Size(360,Double.PositiveInfinity));el.Arrange(new Rect(new Point(0,0),el.DesiredSize));}el.UpdateLayout();if(el.ActualWidth<1||el.ActualHeight<1)throw new Exception("Empty layout: "+path);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,el.ActualWidth,el.ActualHeight));dc.DrawRectangle(new VisualBrush(el){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,el.ActualWidth,el.ActualHeight));}var b=new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth),(int)Math.Ceiling(el.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(visual);Store.SafeOutput(path,Exporter.Image(b,false));}
  public static int Run(Application app,string folder){
   Directory.CreateDirectory(folder);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;int result=0;
   using(var store=new Store(Path.Combine(folder,"ui-store"))){store.Commit(d=>{var t=d.Tasks[0];t.History.Clear();t.Status="待讨论";Model.Record(d,t,"创建任务","","");t.History[0].At=DateTimeOffset.Now.AddDays(-3).ToString("o");t.Status="待执行";Model.Record(d,t,"状态变更","待讨论","");t.History[0].At=DateTimeOffset.Now.AddDays(-2).ToString("o");t.Status="执行中";Model.Record(d,t,"状态变更","待执行","");});var main=new MainWindow(store);main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=>{
    try{
     main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-甘特图.png"));main.TestView(false);main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-任务列表.png"));
     Save(Children<EntityPicker>((DependencyObject)main.Content).First().Preview(),Path.Combine(folder,"项目管理下拉.png"),true);
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务时间线.png"));w.Close();};Dialogs.Detail(main,store,store.Data.Tasks[0].Id);
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"替换排期编辑.png"));var picks=Children<MultiPicker>((DependencyObject)w.Content).ToList();if(picks.Count!=2)throw new Exception("Expected status and task multiselect controls");picks[0].SelectAll();picks[1].SelectAll();if(picks[1].Selected.Count!=16)throw new Exception("Multi-state filter did not expose all 16 tasks");Save(picks[1].Preview(),Path.Combine(folder,"任务多选下拉.png"),true);picks[1].Selected.Clear();picks[1].Selected.Add(store.Data.Tasks[1].Id);picks[1].Selected.Add(store.Data.Tasks[5].Id);Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存并同步甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.Schedule(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[0].InGantt||!store.Data.Tasks[5].InGantt)throw new Exception("Schedule form save failed to replace original bar");
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务编辑.png"));Children<TextBox>((DependencyObject)w.Content).First().Text="知识库对接（界面保存验证）";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[0].Name!="知识库对接（界面保存验证）")throw new Exception("Task editor save did not persist the edited name");
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"导出设置.png"));w.Close();};Dialogs.Export(main,store,store.Data.Tasks.Where(t=>t.InGantt).ToList(),DateTime.Today,DateTime.Today.AddDays(13));
     Ui.TestDialogReady=w=>{w.UpdateLayout();var pathText=Children<System.Windows.Controls.TextBlock>((DependencyObject)w.Content).FirstOrDefault(t=>t.Text==store.DirectoryPath);if(pathText!=null)pathText.Text="%LOCALAPPDATA%\\LaoQinProgress";w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"全部数据导入导出.png"));var names=Children<Button>((DependencyObject)w.Content).Select(b=>b.Content as string).ToList();if(!names.Contains("导出全部数据")||!names.Contains("导入全部数据"))throw new Exception("Missing full-data import/export buttons");w.Close();};Dialogs.Backups(main,store);
     main.TestView(true);main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"最小窗口.png"));store.Commit(d=>{foreach(var id in d.Projects.Select(p=>p.Id).ToArray())Model.DeleteProject(d,id);});main.Width=1380;main.Height=920;main.Refresh();main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"无项目状态.png"));main.TestView(false);main.UpdateLayout();if(store.Data.Projects.Count!=0||store.Data.Tasks.Count!=0)throw new Exception("Zero-project workspace not empty");
     Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="重新创建的项目";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="确定").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="＋ 新建第一个项目").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(store.Data.Projects.Count!=1||store.Data.Projects[0].Name!="重新创建的项目")throw new Exception("Creating project from empty workspace failed");
     File.WriteAllText(Path.Combine(folder,"界面检查.txt"),"PASS: Native WPF layout rendered at normal and minimum sizes.\nPASS: Task and status multiselect connected; all 16 tasks selected across multiple statuses.\nPASS: Schedule form save button replaced original bar and scheduled selected tasks.\nPASS: Task editor save button persisted edited task name.\nPASS: Timeline, task editor, schedule editor and full-data dialogs rendered.\nPASS: Zero-project workspace renders both task and Gantt views.\nPASS: Empty-workspace new-project button saves a usable project.\n");
    }catch(Exception ex){result=1;File.WriteAllText(Path.Combine(folder,"FAILED.txt"),ex.ToString());}
    finally{Ui.TestDialogReady=null;main.Close();app.Shutdown();}
   }),DispatcherPriority.ApplicationIdle);app.Run(main);}
   return result;
  }
 }
}
