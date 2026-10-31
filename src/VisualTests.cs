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

namespace LaoQin.ProgressManager {
 internal static class VisualTests {
  static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T)yield return (T)c;foreach(var d in Children<T>(c))yield return d;}}
  static void Save(FrameworkElement el,string path,bool arrange=false,double width=360){if(arrange){el.Measure(new Size(width,Double.PositiveInfinity));el.Arrange(new Rect(new Point(0,0),el.DesiredSize));}el.UpdateLayout();if(el.ActualWidth<1||el.ActualHeight<1)throw new Exception("Empty layout: "+path);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,el.ActualWidth,el.ActualHeight));dc.DrawRectangle(new VisualBrush(el){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,el.ActualWidth,el.ActualHeight));}var b=new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth),(int)Math.Ceiling(el.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(visual);Store.SafeOutput(path,Exporter.Image(b,false));}
  static List<string> Texts(DependencyObject root){return Children<TextBlock>(root).Select(t=>t.Text).ToList();}
  static Grid TaskRow(MainWindow main,string name){return Children<Grid>((DependencyObject)main.Content).First(g=>g.ColumnDefinitions.Count==5&&Texts(g).Contains(name));}
  static MultiPicker TaskFilter(MainWindow main,string name){return Children<MultiPicker>((DependencyObject)main.Content).Single(p=>p.Name==name);}
  static void SetTaskFilter(MainWindow main,string name,IEnumerable<string> selected){var picker=TaskFilter(main,name);picker.SetSelected(selected);picker.Changed();}
  static void VerifyTaskExcelExport(MainWindow main,Store store,string folder){
   main.TestView(false);SetTaskFilter(main,"TaskPriorityFilter",new[]{"P3"});
   SetTaskFilter(main,"TaskStatusFilter",new[]{"待讨论"});
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="无初始说明";main.UpdateLayout();
   var before=Store.Encode(store.Data);bool opened=false;
   Ui.TestDialogReady=w=>{
    opened=true;w.UpdateLayout();var checks=Children<CheckBox>((DependencyObject)w.Content).ToList();var labels=Texts((DependencyObject)w.Content);
    if(w.Title!="导出任务 Excel"||checks.Count!=13||checks.Any(c=>c.IsChecked!=true)||!labels.Any(t=>t.Contains("全部 1 项匹配任务"))||!labels.Any(t=>t.Contains("状态：待讨论")&&t.Contains("优先级：P3")&&t.Contains("关键词：无初始说明")))throw new Exception("Excel 导出没有沿用列表筛选或默认全选字段");
    Save((FrameworkElement)w.Content,Path.Combine(folder,"任务Excel-字段勾选.png"));
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="全不选").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(checks.Any(c=>c.IsChecked==true)||Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").IsEnabled)throw new Exception("没有字段时导出按钮仍然可用");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="全选").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(checks.Any(c=>c.IsChecked!=true))throw new Exception("全选按钮没有恢复所有字段");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出 Excel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(!opened||!before.SequenceEqual(Store.Encode(store.Data))||Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text!="无初始说明"||!TaskFilter(main,"TaskPriorityFilter").Selected.SetEquals(new[]{"P3"}))throw new Exception("取消导出后数据或筛选条件发生变化");
   Ui.TestDialogReady=w=>{var checks=Children<CheckBox>((DependencyObject)w.Content).ToList();foreach(var c in checks)c.IsChecked=(TaskExportField)c.Tag==TaskExportField.Name||(TaskExportField)c.Tag==TaskExportField.Requirements;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   var selected=Dialogs.ChooseTaskExportColumns(main,1,"字段选择结果验证");if(!selected.SequenceEqual(new[]{TaskExportField.Name,TaskExportField.Requirements}))throw new Exception("取消勾选的字段仍在导出结果中");
   TaskExportPreferences savedPreferences=null;
   Ui.TestDialogReady=w=>{
    var picker=Children<TaskExportColumnPicker>((DependencyObject)w.Content).Single();picker.MoveColumn(TaskExportField.Requirements,TaskExportField.Number,false);
    foreach(var c in Children<CheckBox>(picker))c.IsChecked=(TaskExportField)c.Tag==TaskExportField.Name||(TaskExportField)c.Tag==TaskExportField.Requirements;
    w.UpdateLayout();var order=Children<CheckBox>(picker).Select(c=>(TaskExportField)c.Tag).ToList();if(order[0]!=TaskExportField.Requirements)throw new Exception("移动表头后界面顺序未更新");
    Save((FrameworkElement)w.Content,Path.Combine(folder,"任务Excel-拖动排序.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   selected=Dialogs.ChooseTaskExportColumns(main,1,"拖动表头排序验证",null,p=>savedPreferences=p);if(!selected.SequenceEqual(new[]{TaskExportField.Requirements,TaskExportField.Name})||savedPreferences.Order.First()!="Requirements")throw new Exception("表头调整后的导出顺序不正确");
   Ui.TestDialogReady=w=>{
    var picker=Children<TaskExportColumnPicker>((DependencyObject)w.Content).Single();if(!picker.SelectedFields.SequenceEqual(selected)||picker.OrderedFields.First()!=TaskExportField.Requirements)throw new Exception("弹窗未恢复上次表头顺序与勾选");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="恢复默认").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(!picker.OrderedFields.SequenceEqual(TaskExcelExporter.Columns.Select(c=>c.Field))||picker.SelectedFields.Count!=13)throw new Exception("恢复默认没有重置顺序与勾选");
    w.Close();
   };
   if(Dialogs.ChooseTaskExportColumns(main,1,"恢复默认与取消验证",savedPreferences)!=null||savedPreferences.Order.First()!="Requirements"||savedPreferences.Selected.Count!=2)throw new Exception("取消弹窗修改了已保存的字段偏好");
   Ui.TestDialogReady=w=>w.Close();if(Dialogs.ChooseTaskExportColumns(main,1,"关闭弹窗验证")!=null)throw new Exception("关闭窗口没有终止导出");
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="";
   SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);SetTaskFilter(main,"TaskStatusFilter",Model.Statuses);
   double previousWidth=main.Width,previousHeight=main.Height;main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"任务Excel-最小窗口入口.png"));main.Width=previousWidth;main.Height=previousHeight;
   File.WriteAllText(Path.Combine(folder,"任务Excel-界面检查.txt"),"PASS: 导出入口沿用状态、优先级与关键词，首次默认全选 13 列。\nPASS: 全选、全不选、零字段禁用、取消勾选与窗口关闭均符合预期。\nPASS: 调整表头后界面与导出字段顺序一致，正确恢复偏好，恢复默认与取消互不影响。\nPASS: 取消导出保留当前筛选与项目数据；最小窗口入口可见。\n");
  }
  static void VerifyTaskIndicators(MainWindow main,Store store,string folder){
   var task=store.Data.Tasks[0];var before=Texts(TaskRow(main,task.Name));if(!before.Contains("无备注")||!before.Contains("4 节点"))throw new Exception("无备注任务的列表标签不正确");
   var withDetails=Texts(TaskRow(main,store.Data.Tasks[1].Name));if(!withDetails.Contains("有备注")||!withDetails.Contains("1 节点"))throw new Exception("有备注任务的列表标签不正确");
   int appended=0;Ui.TestDialogReady=w=>{
    if(w.Title=="追加需求"){Children<TextBox>((DependencyObject)w.Content).First().Text="列表节点更新验证 "+appended;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="追加到时间线").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    else if(appended<2){appended++;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="＋ 追加需求").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    else w.Close();
   };
   Children<Button>(TaskRow(main,task.Name)).First(b=>b.Content as string=="查看").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.UpdateLayout();
   var after=Texts(TaskRow(main,task.Name));if(appended!=2||!after.Contains("无备注")||!after.Contains("6 节点"))throw new Exception("连续追加后返回列表未更新节点标签");
   Save((FrameworkElement)main.Content,Path.Combine(folder,"任务列表-详情与节点标签.png"));double width=main.Width,height=main.Height;main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"任务列表-标签最小窗口.png"));main.Width=width;main.Height=height;main.UpdateLayout();
  }
  static void VerifyCreateAndAppend(MainWindow main,Store store,string folder){
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="无初始说明的新任务";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Priorities).SelectedItem="P3";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Statuses).SelectedItem="待讨论";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="创建任务").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};if(!Dialogs.TaskEditor(main,store,null))throw new Exception("New task form did not save");
   var task=store.Data.Tasks.Last();if(task.Priority!="P3"||!String.IsNullOrEmpty(task.Note)||task.History.Count!=1||task.History[0].Kind!="创建任务")throw new Exception("Creation without initial details was not recorded");
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="新增验收要求：确认备案通过后再上线。";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="追加到时间线").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};if(!Dialogs.AppendRequirement(main,store,task.Id))throw new Exception("Append requirement form did not save");
   task=store.Data.Tasks.First(t=>t.Id==task.Id);if(!String.IsNullOrEmpty(task.Note)||task.History.Count!=2||task.History[0].Detail!="新增验收要求：确认备案通过后再上线。")throw new Exception("Append requirement modified the original note or lost the new content");
   Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 2 条")||!texts.Where(t=>t=="创建任务"||t=="需求追加").SequenceEqual(new[]{"创建任务","需求追加"})||!texts.Contains(task.History[0].Detail))throw new Exception("Created task and appended requirement are not in one chronological timeline");Save((FrameworkElement)w.Content,Path.Combine(folder,"创建与追加时间线.png"));w.Close();};Dialogs.Detail(main,store,task.Id);
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="含初始说明的新任务";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Statuses).SelectedItem="待讨论";Children<CheckBox>((DependencyObject)w.Content).First().IsChecked=true;Children<TextBox>((DependencyObject)w.Content).Skip(1).First().Text="初始需求：完成备案资料核验。";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="创建任务").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,null);task=store.Data.Tasks.Last();
   Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 1 条")||texts.Count(t=>t=="创建任务")!=1||texts.Count(t=>t=="初始需求：完成备案资料核验。")!=1)throw new Exception("Initial requirement did not render once inside the creation node");Save((FrameworkElement)w.Content,Path.Combine(folder,"创建与备注时间线.png"));w.Close();};Dialogs.Detail(main,store,task.Id);
  }
  static void VerifyEditableNote(MainWindow main,Store store,string folder){
   string id=store.Data.Tasks.Last().Id;string originalNote=store.Data.Tasks.Last().Note;int originalHistory=store.Data.Tasks.Last().History.Count;string changed="修改后的备注：核对资料并更新验收要求。\n补充第二行说明。";
   Ui.TestDialogReady=w=>{
    var note=Children<TextBox>((DependencyObject)w.Content).First(t=>t.AcceptsReturn);var toggle=Children<CheckBox>((DependencyObject)w.Content).Single();
    if(note.IsReadOnly||note.Text!=originalNote||!note.IsVisible||toggle.IsChecked!=true)throw new Exception("已有备注未预填、展开或仍处于只读状态");
    note.Text=changed;w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"备注-直接编辑.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   if(!Dialogs.TaskEditor(main,store,id))throw new Exception("备注编辑没有保存");var task=store.Data.Tasks.First(t=>t.Id==id);
   if(task.Note!=changed||task.History.Count!=originalHistory+1||task.History[0].Kind!="任务修改"||Store.Decode(File.ReadAllBytes(store.FilePath)).Tasks.First(t=>t.Id==id).Note!=changed)throw new Exception("修改备注未保存到磁盘或未记录任务修改");
   Ui.TestDialogReady=w=>{var note=Children<TextBox>((DependencyObject)w.Content).First(t=>t.AcceptsReturn);if(note.Text!=changed)throw new Exception("重新打开任务没有恢复修改后的备注");note.Text="取消的修改";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   if(Dialogs.TaskEditor(main,store,id)||store.Data.Tasks.First(t=>t.Id==id).Note!=changed)throw new Exception("取消编辑覆盖了已保存的备注");
   Ui.TestDialogReady=w=>{Children<CheckBox>((DependencyObject)w.Content).Single().IsChecked=false;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);
   if(store.Data.Tasks.First(t=>t.Id==id).Note!=changed||store.Data.Tasks.First(t=>t.Id==id).History.Count!=originalHistory+1)throw new Exception("收起备注导致内容丢失或产生无效历史");
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First(t=>t.AcceptsReturn).Text="";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);
   if(!String.IsNullOrEmpty(store.Data.Tasks.First(t=>t.Id==id).Note))throw new Exception("清空备注后未正确保存");
   File.WriteAllText(Path.Combine(folder,"备注编辑检查.txt"),"PASS: 已有备注可直接编辑，默认预填并展开。\nPASS: 修改保存到磁盘并记录任务修改，重新打开恢复内容。\nPASS: 取消编辑保留已保存内容；仅收起备注不清空内容，清空输入后可正常保存。\n");
  }
  static void VerifyTestingStatus(MainWindow main,Store store,string folder){
   string id=store.Data.Tasks.Last().Id;
   Ui.TestDialogReady=w=>{var status=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.ItemsSource==Model.Statuses);if(status.Items.IndexOf("测试中")+1!=status.Items.IndexOf("已完成"))throw new Exception("任务编辑中测试中未显示在已完成前");status.SelectedItem="测试中";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   Dialogs.TaskEditor(main,store,id);var task=store.Data.Tasks.First(t=>t.Id==id);if(task.Status!="测试中"||task.History[0].After!="测试中")throw new Exception("测试中状态未保存或未记录时间线");
   main.TestView(false);SetTaskFilter(main,"TaskStatusFilter",new[]{"测试中"});main.UpdateLayout();if(!Texts((DependencyObject)main.Content).Contains(task.Name)||new TaskQuery(store.Data.CurrentProjectId,"测试中","全部","").Apply(store.Data).Count!=1)throw new Exception("任务列表不支持测试中筛选");Save((FrameworkElement)main.Content,Path.Combine(folder,"测试中-任务列表.png"));
   main.TestView(true);var chartStatus=Children<ComboBox>((DependencyObject)main.Content).Single(c=>c.Items.Contains("全部状态"));if(chartStatus.Items.IndexOf("测试中")+1!=chartStatus.Items.IndexOf("已完成"))throw new Exception("甘特图缺少测试中状态");chartStatus.SelectedItem="测试中";
   File.WriteAllText(Path.Combine(folder,"测试中-检查.txt"),"PASS: 任务编辑与甘特图中测试中排在已完成前。\nPASS: 编辑保存测试中状态和时间线，列表按测试中筛选正确。\n");
  }
  static void VerifyDateRange(MainWindow main,Store store,string folder){
   var picker=new DateRangePicker("2026-11-02","2026-11-04");var preview=picker.Preview();var inputs=Children<TextBox>(preview).ToList();inputs[0].Text="2026-11-10";
   if(!picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,10)||picker.End!=new DateTime(2026,11,12))throw new Exception("开始日期后移时结束日期没有按原天数联动");
   preview=picker.Preview();picker.SelectDate(new DateTime(2026,11,14));if(!picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,14)||picker.End!=new DateTime(2026,11,16))throw new Exception("日历修改开始日期没有联动保留计划天数");
   preview=picker.Preview();inputs=Children<TextBox>(preview).ToList();inputs[1].RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,0,null,inputs[1]){RoutedEvent=System.Windows.Input.Keyboard.GotKeyboardFocusEvent});inputs[1].Text="2026-11-01";
   if(!picker.ApplyDraft()||picker.Start!=new DateTime(2026,10,30)||picker.End!=new DateTime(2026,11,1))throw new Exception("结束日期提前时开始日期没有联动");
   picker.SetRange(new DateTime(2026,11,2),new DateTime(2026,11,4));preview=picker.Preview();picker.SelectDate(new DateTime(2026,11,28));picker.SelectDate(new DateTime(2026,12,3));Save(preview,Path.Combine(folder,"日期范围-双月选择.png"),true,562);
   if(!picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,28)||picker.End!=new DateTime(2026,12,3))throw new Exception("跨月选择未保存完整日期范围");
   preview=picker.Preview();picker.SelectDate(new DateTime(2026,11,20));picker.SelectDate(new DateTime(2026,11,15));if(!picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,15)||picker.End!=new DateTime(2026,11,20))throw new Exception("反向选择日期没有自动排序");
   preview=picker.Preview();inputs=Children<TextBox>(preview).ToList();inputs[0].Text="2026-02-30";if(picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,15))throw new Exception("无效日期覆盖了原范围");
   preview=picker.Preview();picker.SelectDate(new DateTime(2026,11,1));if(picker.Start!=new DateTime(2026,11,15))throw new Exception("日期草稿未经确认写回表单");
   preview=picker.Preview();Children<Button>(preview).First(b=>b.Content as string=="未来 7 天").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!picker.ApplyDraft()||picker.Start!=DateTime.Today||picker.End!=DateTime.Today.AddDays(6))throw new Exception("未来 7 天未包含首尾日期");
   preview=picker.Preview();Children<Button>(preview).First(b=>b.Content as string=="清除").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!picker.ApplyDraft()||picker.Start.HasValue||picker.End.HasValue)throw new Exception("清除日期范围失败");
   var required=new DateRangePicker("2026-11-02","2026-11-04",false);preview=required.Preview();inputs=Children<TextBox>(preview).ToList();inputs[0].Text="";inputs[1].Text="";if(required.ApplyDraft()||Children<Button>(preview).Any(b=>b.Content as string=="清除"))throw new Exception("必填排期允许清空日期范围");
   string id=store.Data.Tasks.Last().Id;Ui.TestDialogReady=w=>{Children<DateRangePicker>((DependencyObject)w.Content).Single().SetRange(new DateTime(2026,11,28),new DateTime(2026,12,3));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);if(store.Data.Tasks.Last().Start!="2026-11-28"||store.Data.Tasks.Last().End!="2026-12-03")throw new Exception("任务表单未保存日期范围");
   File.WriteAllText(Path.Combine(folder,"日期范围-检查.txt"),"PASS: 日期两端联动保持原天数，跨月与反向选择正确。\nPASS: 无效输入、未确认的草稿不覆盖原范围，快捷选择与清除正确。\nPASS: 必填排期不能清空，任务表单正确保存起止日期。\n");
  }
  static void VerifyMultiFilters(MainWindow main,Store store,string folder){
   main.TestView(false);SetTaskFilter(main,"TaskStatusFilter",Model.Statuses);SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);
   var statusPicker=TaskFilter(main,"TaskStatusFilter");var statusPanel=statusPicker.Preview();Save(statusPanel,Path.Combine(folder,"多选筛选-全部状态.png"),true,230);
   Children<Button>(statusPanel).First(b=>b.Content as string=="清空").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.UpdateLayout();if(!Texts((DependencyObject)main.Content).Contains("暂无符合筛选条件的任务。"))throw new Exception("清空状态没有显示空结果");
   foreach(string option in new[]{"执行中","待执行"}){var check=Children<CheckBox>(statusPanel).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)==option);check.IsChecked=true;check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
   var priorityPicker=TaskFilter(main,"TaskPriorityFilter");var priorityPanel=priorityPicker.Preview();Save(priorityPanel,Path.Combine(folder,"多选筛选-全部优先级.png"),true,230);Children<Button>(priorityPanel).First(b=>b.Content as string=="清空").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.UpdateLayout();
   foreach(string option in new[]{"P0","P1"}){var check=Children<CheckBox>(priorityPanel).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)==option);check.IsChecked=true;check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
   main.UpdateLayout();var expected=store.Data.Tasks.Where(t=>t.ProjectId==store.Data.CurrentProjectId&&(t.Status=="执行中"||t.Status=="待执行")&&(t.Priority=="P0"||t.Priority=="P1")).ToList();var labels=Texts((DependencyObject)main.Content);if(expected.Count==0||expected.Any(t=>!labels.Contains(t.Name))||!statusPicker.Selected.SetEquals(new[]{"执行中","待执行"})||!priorityPicker.Selected.SetEquals(new[]{"P0","P1"}))throw new Exception("状态与优先级多选组合结果不正确");Save((FrameworkElement)main.Content,Path.Combine(folder,"多选筛选-组合结果.png"));
   Save(statusPicker.Preview(),Path.Combine(folder,"多选筛选-状态勾选.png"),true,230);Save(priorityPicker.Preview(),Path.Combine(folder,"多选筛选-优先级勾选.png"),true,230);
   bool opened=false;Ui.TestDialogReady=w=>{opened=true;var text=Texts((DependencyObject)w.Content);if(!text.Any(t=>t.Contains("全部 "+expected.Count+" 项匹配任务")&&t.Contains("状态：待执行、执行中")&&t.Contains("优先级：P0、P1")))throw new Exception("导出弹窗未沿用多选条件");w.Close();};Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出 Excel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!opened||!statusPicker.Selected.SetEquals(new[]{"执行中","待执行"})||!priorityPicker.Selected.SetEquals(new[]{"P0","P1"}))throw new Exception("取消导出丢失多选筛选");
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="设备";main.UpdateLayout();var names=Texts((DependencyObject)main.Content);if(expected.Where(t=>t.Name.Contains("设备")).Any(t=>!names.Contains(t.Name))||expected.Where(t=>!t.Name.Contains("设备")&&!(t.Note??"").Contains("设备")).Any(t=>names.Contains(t.Name)))throw new Exception("多选筛选与关键词未组合生效");
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="";SetTaskFilter(main,"TaskStatusFilter",Model.Statuses);SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);
   File.WriteAllText(Path.Combine(folder,"多选筛选-检查.txt"),"PASS: 状态与优先级下拉均支持逐项多选、全选与清空，组合结果正确。\nPASS: 与关键词搜索共同生效，导出弹窗保留多选条件，取消导出后选择不丢失。\n");
  }
  public static int Run(Application app,string folder){
   Directory.CreateDirectory(folder);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;int result=0;
   using(var store=new Store(Path.Combine(folder,"ui-store"))){store.Commit(d=>{for(int i=0;i<4;i++)d.Tasks[i].Priority=Model.Priorities[i];var t=d.Tasks[0];t.Note="";t.History.Clear();t.Status="待讨论";Model.Record(d,t,"创建任务","","");t.History[0].At=DateTimeOffset.Now.AddDays(-3).ToString("o");t.Status="待执行";Model.Record(d,t,"状态变更","待讨论","");t.History[0].At=DateTimeOffset.Now.AddDays(-2).ToString("o");Model.Record(d,t,"需求追加",t.Status,"核实是否通过，进度如何");t.History[0].At=DateTimeOffset.Now.AddDays(-1).ToString("o");t.Status="执行中";Model.Record(d,t,"状态变更","待执行","");});var main=new MainWindow(store);main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=>{
    try{
     main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-甘特图.png"));main.TestView(false);main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-任务列表.png"));
     Save(Children<EntityPicker>((DependencyObject)main.Content).First().Preview(),Path.Combine(folder,"项目管理下拉.png"),true);
     Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 4 条")||!texts.Where(t=>t=="创建任务"||t=="状态变更"||t=="需求追加").SequenceEqual(new[]{"创建任务","状态变更","需求追加","状态变更"}))throw new Exception("Creation, requirements and operations are split or incorrectly ordered");Save((FrameworkElement)w.Content,Path.Combine(folder,"任务时间线.png"));Children<ScrollViewer>((DependencyObject)w.Content).First().ScrollToBottom();w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务时间线-后续记录.png"));w.Close();};Dialogs.Detail(main,store,store.Data.Tasks[0].Id);
     VerifyTaskIndicators(main,store,folder);
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"替换排期编辑.png"));var picks=Children<MultiPicker>((DependencyObject)w.Content).ToList();if(picks.Count!=2)throw new Exception("Expected status and task multiselect controls");Children<ComboBox>((DependencyObject)w.Content).First(x=>x.Items.Contains("保持原优先级")).SelectedItem="P1";picks[0].SelectAll();picks[1].SelectAll();if(picks[1].Selected.Count!=16)throw new Exception("Multi-state filter did not expose all 16 tasks");Save(picks[1].Preview(),Path.Combine(folder,"任务多选下拉.png"),true);picks[1].Selected.Clear();picks[1].Selected.Add(store.Data.Tasks[1].Id);picks[1].Selected.Add(store.Data.Tasks[5].Id);Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存并同步甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.Schedule(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[5].Priority!="P1"||store.Data.Tasks[0].InGantt||!store.Data.Tasks[5].InGantt)throw new Exception("Schedule form save failed to replace original bar");
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务编辑.png"));Children<TextBox>((DependencyObject)w.Content).First().Text="知识库对接（界面保存验证）";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Priorities).SelectedItem="P2";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[0].Priority!="P2"||store.Data.Tasks[0].Name!="知识库对接（界面保存验证）")throw new Exception("Task editor save did not persist the edited name");
     VerifyCreateAndAppend(main,store,folder);VerifyEditableNote(main,store,folder);main.Refresh();main.TestView(false);SetTaskFilter(main,"TaskPriorityFilter",new[]{"P3"});main.UpdateLayout();var filtered=Texts((DependencyObject)main.Content);if(!filtered.Contains("无初始说明的新任务")||filtered.Contains("知识库对接（界面保存验证）"))throw new Exception("Priority filter did not restrict tasks");Save((FrameworkElement)main.Content,Path.Combine(folder,"优先级筛选.png"));SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"导出设置.png"));w.Close();};Dialogs.Export(main,store,store.Data.Tasks.Where(t=>t.InGantt).ToList(),DateTime.Today,DateTime.Today.AddDays(13));
     Ui.TestDialogReady=w=>{w.UpdateLayout();var pathText=Children<System.Windows.Controls.TextBlock>((DependencyObject)w.Content).FirstOrDefault(t=>t.Text==store.DirectoryPath);if(pathText!=null)pathText.Text="%LOCALAPPDATA%\\LaoQinProgress";w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"全部数据导入导出.png"));var names=Children<Button>((DependencyObject)w.Content).Select(b=>b.Content as string).ToList();if(!names.Contains("导出全部数据")||!names.Contains("导入全部数据"))throw new Exception("Missing full-data import/export buttons");w.Close();};Dialogs.Backups(main,store);
     VerifyTaskExcelExport(main,store,folder);VerifyMultiFilters(main,store,folder);VerifyTestingStatus(main,store,folder);VerifyDateRange(main,store,folder);
     main.TestView(true);main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"最小窗口.png"));store.Commit(d=>{foreach(var id in d.Projects.Select(p=>p.Id).ToArray())Model.DeleteProject(d,id);});main.Width=1380;main.Height=920;main.Refresh();main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"无项目状态.png"));main.TestView(false);main.UpdateLayout();if(store.Data.Projects.Count!=0||store.Data.Tasks.Count!=0)throw new Exception("Zero-project workspace not empty");
     Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="重新创建的项目";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="确定").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="＋ 新建第一个项目").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(store.Data.Projects.Count!=1||store.Data.Projects[0].Name!="重新创建的项目")throw new Exception("Creating project from empty workspace failed");
     File.WriteAllText(Path.Combine(folder,"界面检查.txt"),"PASS: Native WPF layout rendered at normal and minimum sizes.\nPASS: 详情与节点标签正常显示，连续追加后返回列表自动更新，并已渲染最小窗口。\nPASS: Creation, requirements and operations share one chronological timeline.\nPASS: Task creation and requirement append forms save records, including tasks without initial details.\nPASS: Initial details appear once inside the creation node.\nPASS: Task and status multiselect connected; all 16 tasks selected across multiple statuses.\nPASS: Schedule form save button replaced original bar and scheduled selected tasks.\nPASS: Task editor save button persisted edited task name.\nPASS: Timeline, task editor, schedule editor and full-data dialogs rendered.\nPASS: Zero-project workspace renders both task and Gantt views.\nPASS: Empty-workspace new-project button saves a usable project.\n");
    }catch(Exception ex){result=1;File.WriteAllText(Path.Combine(folder,"FAILED.txt"),ex.ToString());}
    finally{Ui.TestDialogReady=null;main.Close();app.Shutdown();}
   }),DispatcherPriority.ApplicationIdle);app.Run(main);}
   return result;
  }
 }
}
