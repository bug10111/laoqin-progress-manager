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
using System.Windows.Input;
using System.Windows.Threading;

namespace LaoQin.ProgressManager {
 internal static class VisualTests {
  static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T)yield return (T)c;foreach(var d in Children<T>(c))yield return d;}}
  static void Save(FrameworkElement el,string path,bool arrange=false,double width=360){if(arrange){el.Measure(new Size(width,Double.PositiveInfinity));el.Arrange(new Rect(new Point(0,0),el.DesiredSize));}el.UpdateLayout();if(el.ActualWidth<1||el.ActualHeight<1)throw new Exception("Empty layout: "+path);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,el.ActualWidth,el.ActualHeight));dc.DrawRectangle(new VisualBrush(el){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,el.ActualWidth,el.ActualHeight));}var b=new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth),(int)Math.Ceiling(el.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(visual);Store.SafeOutput(path,Exporter.Image(b,false));}
  static void VerifyExpandedNoteBox(TextBox field){
   var lastLine=field.GetRectFromCharacterIndex(field.Text.Length-1);
   if(!field.AcceptsReturn||field.VerticalScrollBarVisibility!=ScrollBarVisibility.Disabled||
      field.ActualHeight<=field.MinHeight||lastLine.IsEmpty||lastLine.Bottom>field.ActualHeight)
    throw new Exception("多行备注文本域未展开到最后一行");
  }
  static List<string> Texts(DependencyObject root){return Children<TextBlock>(root).Select(t=>t.Text).ToList();}
  static Grid TaskRow(MainWindow main,string name){return Children<Grid>((DependencyObject)main.Content).First(g=>g.ColumnDefinitions.Count==5&&Texts(g).Contains(name));}
  static MultiPicker TaskFilter(MainWindow main,string name){return Children<MultiPicker>((DependencyObject)main.Content).Single(p=>p.Name==name);}
  static MultiPicker ChartFilter(MainWindow main){main.UpdateLayout();return Children<MultiPicker>((DependencyObject)main.Content).Single(p=>p.Name=="GanttStatusFilter");}
  static FrameworkElement ChartFilterPreview(MainWindow main){var popup=ChartFilter(main).Preview();popup.Measure(new Size(230,Double.PositiveInfinity));popup.Arrange(new Rect(new Point(),popup.DesiredSize));popup.UpdateLayout();return popup;}
  static void SetChartFilter(MainWindow main,IEnumerable<string> statuses){var picker=ChartFilter(main);picker.SetSelected(statuses);picker.Changed();main.UpdateLayout();}
  static void SetTaskFilter(MainWindow main,string name,IEnumerable<string> selected){var picker=TaskFilter(main,name);picker.SetSelected(selected);picker.Changed();}
  static void VerifyTaskExcelExport(MainWindow main,Store store,string folder){
   main.TestView(false);SetTaskFilter(main,"TaskPriorityFilter",new[]{"P3"});
   SetTaskFilter(main,"TaskStatusFilter",new[]{"待讨论"});
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="无初始说明";main.UpdateLayout();
   var before=Store.Encode(store.Data);bool opened=false;
   Ui.TestDialogReady=w=>{
    opened=true;w.UpdateLayout();var checks=Children<CheckBox>((DependencyObject)w.Content).Where(c=>c.Tag is TaskExportField).ToList();var labels=Texts((DependencyObject)w.Content);
    var types=Children<CheckBox>((DependencyObject)w.Content).Where(c=>c.Tag is string).ToList();
    if(w.Title!="导出任务 Excel"||checks.Count!=12||checks.Any(c=>c.IsChecked!=true)||!checks.Any(c=>(TaskExportField)c.Tag==TaskExportField.Timeline&&c.Content as string=="任务时间线")||!labels.Any(t=>t.Contains("全部 1 项匹配任务"))||!labels.Any(t=>t.Contains("状态：待讨论")&&t.Contains("优先级：P3")&&t.Contains("关键词：无初始说明")))throw new Exception("Excel 导出没有沿用列表筛选或默认全选字段");
    Save((FrameworkElement)w.Content,Path.Combine(folder,"任务Excel-字段勾选.png"));
    if(types.Count!=TaskTimelineTypes.All.Length||types.Any(c=>c.IsChecked!=true))throw new Exception("首次导出没有选中全部时间线类型");
    var save=Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置");
    Children<Button>((DependencyObject)w.Content).Single(b=>b.Content as string=="清空类型").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(save.IsEnabled||types.Any(c=>c.IsChecked==true))throw new Exception("清空类型后仍可导出空时间线");
    var status=types.Single(c=>c.Tag as string=="状态变更");status.IsChecked=true;
    if(!save.IsEnabled)throw new Exception("选中一个时间线类型后无法导出");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="全不选").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(checks.Any(c=>c.IsChecked==true)||Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").IsEnabled)throw new Exception("没有字段时导出按钮仍然可用");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="全选").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(checks.Any(c=>c.IsChecked!=true)||types.Any(c=>c.IsChecked!=true))throw new Exception("全选按钮没有恢复所有字段和时间线类型");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出 Excel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(!opened||!before.SequenceEqual(Store.Encode(store.Data))||Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text!="无初始说明"||!TaskFilter(main,"TaskPriorityFilter").Selected.SetEquals(new[]{"P3"}))throw new Exception("取消导出后数据或筛选条件发生变化");
   Ui.TestDialogReady=w=>{var checks=Children<CheckBox>((DependencyObject)w.Content).Where(c=>c.Tag is TaskExportField).ToList();foreach(var c in checks)c.IsChecked=(TaskExportField)c.Tag==TaskExportField.Name||(TaskExportField)c.Tag==TaskExportField.Timeline;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   var selected=Dialogs.ChooseTaskExportColumns(main,1,"字段选择结果验证");if(!selected.SequenceEqual(new[]{TaskExportField.Name,TaskExportField.Timeline}))throw new Exception("取消勾选的字段仍在导出结果中");
   TaskExportPreferences savedPreferences=null;
   Ui.TestDialogReady=w=>{
    var picker=Children<TaskExportColumnPicker>((DependencyObject)w.Content).Single();picker.MoveColumn(TaskExportField.Timeline,TaskExportField.Number,false);
    foreach(var c in Children<CheckBox>(picker).Where(c=>c.Tag is TaskExportField))c.IsChecked=(TaskExportField)c.Tag==TaskExportField.Name||(TaskExportField)c.Tag==TaskExportField.Timeline;
    foreach(var c in Children<CheckBox>(picker).Where(c=>c.Tag is string))c.IsChecked=c.Tag as string==TaskTimelineTypes.Notes||c.Tag as string=="状态变更";
    w.UpdateLayout();var order=Children<CheckBox>(picker).Where(c=>c.Tag is TaskExportField).Select(c=>(TaskExportField)c.Tag).ToList();if(order[0]!=TaskExportField.Timeline)throw new Exception("移动表头后界面顺序未更新");
    Save((FrameworkElement)w.Content,Path.Combine(folder,"任务Excel-拖动排序.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="选择保存位置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   selected=Dialogs.ChooseTaskExportColumns(main,1,"拖动表头排序验证",null,p=>savedPreferences=p);if(!selected.SequenceEqual(new[]{TaskExportField.Timeline,TaskExportField.Name})||savedPreferences.Order.First()!="Timeline"||!savedPreferences.TimelineTypes.SequenceEqual(new[]{"状态变更",TaskTimelineTypes.Notes}))throw new Exception("导出顺序或所选时间线类型未保存");
   Ui.TestDialogReady=w=>{
    var picker=Children<TaskExportColumnPicker>((DependencyObject)w.Content).Single();if(!picker.SelectedFields.SequenceEqual(selected)||picker.OrderedFields.First()!=TaskExportField.Timeline||!picker.Preferences().TimelineTypes.SequenceEqual(savedPreferences.TimelineTypes))throw new Exception("弹窗未恢复上次表头顺序、勾选或时间线类型");
    Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="恢复默认").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(!picker.OrderedFields.SequenceEqual(TaskExcelExporter.Columns.Select(c=>c.Field))||picker.SelectedFields.Count!=12||!picker.Preferences().TimelineTypes.SequenceEqual(TaskTimelineTypes.All))throw new Exception("恢复默认没有重置顺序与勾选");
    w.Close();
   };
   if(Dialogs.ChooseTaskExportColumns(main,1,"恢复默认与取消验证",savedPreferences)!=null||savedPreferences.Order.First()!="Timeline"||savedPreferences.Selected.Count!=2)throw new Exception("取消弹窗修改了已保存的字段偏好");
   Ui.TestDialogReady=w=>w.Close();if(Dialogs.ChooseTaskExportColumns(main,1,"关闭弹窗验证")!=null)throw new Exception("关闭窗口没有终止导出");
   Children<TextBox>((DependencyObject)main.Content).First(t=>t.Name=="TaskSearchBox").Text="";
   SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);SetTaskFilter(main,"TaskStatusFilter",Model.Statuses);
   double previousWidth=main.Width,previousHeight=main.Height;main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"任务Excel-最小窗口入口.png"));main.Width=previousWidth;main.Height=previousHeight;
   File.WriteAllText(Path.Combine(folder,"任务Excel-界面检查.txt"),"PASS: 导出沿用状态、优先级与关键词；默认全选 12 列及所有时间线类型。\nPASS: 清空类型时阻止导出，选择任意类型后恢复。\nPASS: 表头顺序、字段和类型勾选可恢复，恢复默认与取消互不影响。\nPASS: 取消导出保留当前筛选与项目数据；最小窗口入口可见。\n");
  }
  static void VerifyTaskIndicators(MainWindow main,Store store,string folder){
   var task=store.Data.Tasks[0];var before=Texts(TaskRow(main,task.Name));if(!before.Contains("有备注")||!before.Contains("4 节点"))throw new Exception("旧版需求追加未计入时间线备注");
   var withDetails=Texts(TaskRow(main,store.Data.Tasks[1].Name));if(!withDetails.Contains("有备注")||!withDetails.Contains("1 节点"))throw new Exception("有备注任务的列表标签不正确");
   int appended=0;Ui.TestDialogReady=w=>{
    if(w.Title=="追加需求"){Children<TextBox>((DependencyObject)w.Content).First().Text="列表节点更新验证 "+appended;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="追加到时间线").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    else if(appended<2){appended++;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="＋ 追加需求").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    else w.Close();
   };
   Children<Button>(TaskRow(main,task.Name)).First(b=>b.Content as string=="查看").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.UpdateLayout();
   var after=Texts(TaskRow(main,task.Name));if(appended!=2||!after.Contains("有备注")||!after.Contains("6 节点"))throw new Exception("连续追加后返回列表未更新节点标签");
   Save((FrameworkElement)main.Content,Path.Combine(folder,"任务列表-详情与节点标签.png"));double width=main.Width,height=main.Height;main.Width=1040;main.Height=720;main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"任务列表-标签最小窗口.png"));main.Width=width;main.Height=height;main.UpdateLayout();
  }
  static void VerifyCreateAndAppend(MainWindow main,Store store,string folder){
   Ui.TestDialogReady=w=>{
    var content=(FrameworkElement)w.Content;
    Children<TextBox>(content).First().Text="无初始说明的新任务";
    Children<ComboBox>(content).First(x=>x.ItemsSource==Model.Priorities).SelectedItem="P3";
    Children<ComboBox>(content).First(x=>x.ItemsSource==Model.Statuses).SelectedItem="待讨论";
    var toggle=Children<CheckBox>(content).Single();toggle.IsChecked=true;
    Children<TextBox>(content).Single(t=>t.Name=="ChangeNoteBox").Text="取消勾选的备注";
    toggle.IsChecked=false;
    Children<Button>(content).First(b=>b.Content as string=="创建任务").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };if(!Dialogs.TaskEditor(main,store,null))throw new Exception("New task form did not save");
   var task=store.Data.Tasks.Last();if(task.Priority!="P3"||Model.HasTimelineNotes(task)||task.History.Count!=1||task.History[0].Kind!="创建任务")throw new Exception("Creation without initial details was not recorded");
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="新增验收要求：确认备案通过后再上线。";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="追加到时间线").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};if(!Dialogs.AppendRequirement(main,store,task.Id))throw new Exception("Append requirement form did not save");
   task=store.Data.Tasks.First(t=>t.Id==task.Id);if(task.History.Count!=2||task.History[0].Note!="新增验收要求：确认备案通过后再上线。"||task.History[1].Note!=null)throw new Exception("追加内容应写入自己的备注，创建备注仍为空");
   Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 2 条")||!texts.Where(t=>t=="创建任务"||t=="需求追加").SequenceEqual(new[]{"创建任务","需求追加"})||texts.Count(t=>t=="备注：")!=2||!texts.Contains(task.History[0].Note)||!texts.Contains("无"))throw new Exception("创建和追加应按顺序显示，备注正文另起一行，未填写备注显示无");Save((FrameworkElement)w.Content,Path.Combine(folder,"创建与追加时间线.png"));w.Close();};Dialogs.Detail(main,store,task.Id);
   Ui.TestDialogReady=w=>{Children<TextBox>((DependencyObject)w.Content).First().Text="含初始说明的新任务";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Statuses).SelectedItem="待讨论";Children<CheckBox>((DependencyObject)w.Content).First().IsChecked=true;Children<TextBox>((DependencyObject)w.Content).Skip(1).First().Text="初始需求：完成备案资料核验。";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="创建任务").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,null);task=store.Data.Tasks.Last();
   if(task.Note!=null||task.History.Single().Note!="初始需求：完成备案资料核验。")throw new Exception("勾选填写的备注未随创建任务节点保存");
   Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 1 条")||texts.Count(t=>t=="创建任务")!=1||texts.Count(t=>t=="初始需求：完成备案资料核验。")!=1)throw new Exception("创建备注应在创建节点内展示一次");Save((FrameworkElement)w.Content,Path.Combine(folder,"创建与备注时间线.png"));w.Close();};Dialogs.Detail(main,store,task.Id);
  }
  static void VerifyEditableNote(MainWindow main,Store store,string folder){
   string id=store.Data.Tasks.Last().Id;string originalNote=store.Data.Tasks.Last().History.Single().Note;int originalHistory=store.Data.Tasks.Last().History.Count;string changed="核对资料并更新验收要求。\n补充第二行说明。";
   Ui.TestDialogReady=w=>{
    var note=Children<TextBox>((DependencyObject)w.Content).Single(t=>t.AcceptsReturn);
    if(note.IsReadOnly||note.Text!=""||!note.IsVisible||Children<CheckBox>((DependencyObject)w.Content).Any())throw new Exception("编辑应仅显示一个空白的可选备注输入框");
    note.Text=changed;Children<TextBox>((DependencyObject)w.Content).First().Text="含初始说明的任务（已更新）";
    Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.ItemsSource==Model.Statuses).SelectedItem="待执行";
    w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"备注-编辑任务.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   if(!Dialogs.TaskEditor(main,store,id))throw new Exception("备注编辑没有保存");var task=store.Data.Tasks.First(t=>t.Id==id);
   if(task.History.Count!=originalHistory+1||task.History[0].Kind!="状态变更"||!task.History[0].Detail.Contains("含初始说明的新任务 → 含初始说明的任务（已更新）")||task.History.Last().Note!=originalNote||LocalWorkspace.Read(store.FilePath).Tasks.First(t=>t.Id==id).History[0].Note!=changed)throw new Exception("多字段变更、创建备注或本次备注未完整保存");
   byte[] saved=Store.Encode(store.Data);
   Ui.TestDialogReady=w=>{var note=Children<TextBox>((DependencyObject)w.Content).Single(t=>t.AcceptsReturn);if(note.Text!="")throw new Exception("重新编辑应填写新的备注");note.Text="取消的修改";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   if(Dialogs.TaskEditor(main,store,id)||!saved.SequenceEqual(Store.Encode(store.Data)))throw new Exception("取消编辑写入了数据");
   Ui.TestDialogReady=w=>{Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.ItemsSource==Model.Priorities).SelectedItem="P1";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);
   task=store.Data.Tasks.First(t=>t.Id==id);if(task.History.Count!=originalHistory+2||task.History[0].Note!=null||task.History[1].Note!=changed)throw new Exception("空备注应随真实变更留下节点，之前的备注应保留");
   File.WriteAllText(Path.Combine(folder,"备注编辑检查.txt"),"PASS: 编辑只显示一个空白备注输入框。\nPASS: 同时修改多个字段只写入一次，保留创建备注及本次多行备注。\nPASS: 取消不保存，留空也可以记录变更。\n");
  }
  static void VerifyTestingStatus(MainWindow main,Store store,string folder){
   string id=store.Data.Tasks.Last().Id,originalNote=store.Data.Tasks.Last().Note;
   const string note="测试环境恢复，继续验收。";
   Ui.TestDialogReady=w=>{var status=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.ItemsSource==Model.Statuses);if(status.Items.IndexOf("测试中")+1!=status.Items.IndexOf("已完成"))throw new Exception("任务编辑中测试中未显示在已完成前");status.SelectedItem="测试中";Children<TextBox>((DependencyObject)w.Content).Single(t=>t.Name=="ChangeNoteBox").Text=note;Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   Dialogs.TaskEditor(main,store,id);var task=store.Data.Tasks.First(t=>t.Id==id);if(task.Status!="测试中"||task.History[0].After!="测试中"||task.History[0].Note!=note||task.Note!=originalNote)throw new Exception("状态与备注未一同保存，或原任务备注发生变化");
   main.TestView(false);SetTaskFilter(main,"TaskStatusFilter",new[]{"测试中"});main.UpdateLayout();if(!Texts((DependencyObject)main.Content).Contains(task.Name)||new TaskQuery(store.Data.CurrentProjectId,"测试中","全部","").Apply(store.Data).Count!=1)throw new Exception("任务列表不支持测试中筛选");Save((FrameworkElement)main.Content,Path.Combine(folder,"测试中-任务列表.png"));
   main.TestView(true);var options=Children<CheckBox>(ChartFilterPreview(main)).Select(c=>System.Windows.Automation.AutomationProperties.GetName(c)).ToList();if(options.IndexOf("测试中")+1!=options.IndexOf("已完成"))throw new Exception("甘特图缺少测试中状态");SetChartFilter(main,new[]{"测试中"});
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
   preview=picker.Preview();Children<Button>(preview).First(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(picker.Start!=DateTime.Today)throw new Exception("清空草稿未经确认写回日期");Children<Button>(preview).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));preview=picker.Preview();if(picker.Start!=DateTime.Today)throw new Exception("取消清空改变原日期");
   Children<Button>(preview).First(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!picker.ApplyDraft()||picker.Start.HasValue||picker.End.HasValue)throw new Exception("清空日期范围失败");
   picker.SetRange(new DateTime(2026,11,2),new DateTime(2026,11,4));preview=picker.Preview();inputs=Children<TextBox>(preview).ToList();inputs[0].Text="";if(picker.ApplyDraft()||picker.Start!=new DateTime(2026,11,2))throw new Exception("只清空一端覆盖原日期");inputs[1].Text="";if(!picker.ApplyDraft()||picker.Start.HasValue||picker.End.HasValue)throw new Exception("手动同时清空日期失败");
   string id=store.Data.Tasks.Last().Id;Ui.TestDialogReady=w=>{Children<DateRangePicker>((DependencyObject)w.Content).Single().SetRange(new DateTime(2026,11,28),new DateTime(2026,12,3));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);if(store.Data.Tasks.Last().Start!="2026-11-28"||store.Data.Tasks.Last().End!="2026-12-03")throw new Exception("任务表单未保存日期范围");
   File.WriteAllText(Path.Combine(folder,"日期范围-检查.txt"),"PASS: 日期两端联动保持原天数，跨月与反向选择正确。\nPASS: 无效输入、单端清空及未确认的草稿不覆盖原范围。\nPASS: 清空草稿取消后保留原日期；同时清空两端可应用。\nPASS: 任务表单正确保存起止日期。\n");
   VerifyGanttDateRange(main,store,folder);
  }
  static void VerifyGanttDateRange(MainWindow main,Store store,string folder){
   main.TestView(true);main.UpdateLayout();
   var original=main.CaptureGanttExport();
   var dataBefore=Store.Encode(store.Data);
   Func<DateRangePicker> getPicker=()=>Children<DateRangePicker>((DependencyObject)main.Content).Single(p=>p.Name=="GanttDateRange");
   var picker=getPicker();
   var month=new DateTime(original.Start.Year,original.Start.Month,1);
   DateTime start=month.AddDays(27),end=month.AddMonths(1).AddDays(2);
   var preview=picker.Preview();
   Children<Button>(preview).Single(b=>b.Tag is DateTime&&(DateTime)b.Tag==start).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Children<Button>(preview).Single(b=>b.Tag is DateTime&&(DateTime)b.Tag==end).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(main.CaptureGanttExport().Start!=original.Start)throw new Exception("日期草稿提前改变了甘特图");
   Save(preview,Path.Combine(folder,"甘特图-跨月日期选择.png"),true,562);
   Children<Button>(preview).Single(b=>b.Content as string=="应用日期").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   main.UpdateLayout();
   var selected=main.CaptureGanttExport();
   int days=(end-start).Days+1;
   var dayInput=Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays");
   if(selected.Start!=start||selected.Days!=days||dayInput.Text!=days.ToString()||getPicker().End!=end)
    throw new Exception("甘特图、导出或天数未采用选中的完整日期范围");
   Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-时间范围.png"));

   picker=getPicker();preview=picker.Preview();picker.SelectDate(start);picker.SelectDate(start.AddDays(3660));
   if(picker.ApplyDraft()||main.CaptureGanttExport().Days!=days)throw new Exception("超长日期范围被应用到甘特图");
   Children<Button>(preview).Single(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   dayInput.Text="15";
   Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   main.UpdateLayout();
   if(getPicker().Start!=start||getPicker().End!=start.AddDays(14)||!dataBefore.SequenceEqual(Store.Encode(store.Data)))
    throw new Exception("调整天数后日期选择未同步，或视图操作修改了任务数据");
   getPicker().SetRange(original.Start,original.Start.AddDays(original.Days-1));
   File.WriteAllText(Path.Combine(folder,"甘特图-时间范围检查.txt"),"PASS: 跨月选择应用到甘特图与导出，起止日均计入天数。\nPASS: 超长草稿不能应用，天数调整同步起止日期，任务数据不变。\n");
   VerifyGanttRangeRows(main,store,folder);
  }
  static void VerifyGanttRangeRows(MainWindow main,Store store,string folder){
   var original=main.CaptureGanttExport();
   var statuses=ChartFilter(main).Selected.ToArray();
   DateTime start=original.Start,end=start.AddDays(5);
   var ids=Enumerable.Range(0,12).Select(i=>Model.Id()).ToArray();
   store.Commit(d=>{
    for(int i=0;i<ids.Length;i++){
     DateTime from=i==0?start.AddDays(-2):i==1?end:i<7?start.AddDays(-8):end.AddDays(8);
     DateTime to=i==0?start:i==1?end.AddDays(2):from;
     d.Tasks.Add(new TaskItem{Id=ids[i],ProjectId=d.CurrentProjectId,Name=i==0?"跨入范围首日":i==1?"跨出范围末日":"范围外任务 "+i,Status="已暂停",PersonId=d.People[0].Id,Start=Model.Date(from),End=Model.Date(to),InGantt=true});
    }
   });
   main.Refresh();SetChartFilter(main,new[]{"已暂停"});
   Func<DateRangePicker> picker=()=>Children<DateRangePicker>((DependencyObject)main.Content).Single(p=>p.Name=="GanttDateRange");
   picker().SetRange(start,end);main.UpdateLayout();
   Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-日期范围任务行.png"));
   var chart=Children<GanttView>((DependencyObject)main.Content).Single();
   var export=main.CaptureGanttExport();
   var expected=ids.Take(2).ToArray();
   if(chart.Tasks.Count!=2||!new HashSet<string>(chart.Tasks.Select(t=>t.Id)).SetEquals(expected)||!export.Tasks.SequenceEqual(chart.Ordered())||chart.HitRegions.Count(h=>!h.IsLabel)!=2)
    throw new Exception("日期范围内只有 2 项任务，实际占用 "+chart.Tasks.Count+" 行");
   if(!Children<Button>((DependencyObject)main.Content).Any(b=>b.Content as string=="≡  甘特图  2")||chart.Height>300)
    throw new Exception("甘特图任务数或画布高度未按可见行更新");
   picker().SetRange(end.AddDays(20),end.AddDays(22));main.UpdateLayout();
   if(main.CaptureGanttExport().Tasks.Count!=0||!Children<Button>((DependencyObject)main.Content).Any(b=>b.Content as string=="≡  甘特图  0"))
    throw new Exception("空日期范围未同步视图和任务数");
   store.Commit(d=>d.Tasks.RemoveAll(t=>ids.Contains(t.Id)));
   main.Refresh();picker().SetRange(original.Start,original.Start.AddDays(original.Days-1));SetChartFilter(main,statuses);
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
  static void VerifyTaskPlanFilter(MainWindow main,Store store,string folder){
   main.TestView(false);
   var task=store.Data.Tasks.First(t=>Model.IsDate(t.Start)&&Model.IsDate(t.End));
   var date=Model.Parse(task.End);
   var search=Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="TaskSearchBox");
   search.Text=task.Name;SetTaskFilter(main,"TaskPriorityFilter",new[]{task.Priority});
   var picker=Children<DateRangePicker>((DependencyObject)main.Content).Single(p=>p.Name=="TaskPlanFilter");
   if(!Texts((DependencyObject)main.Content).Contains("计划时间段")||!Children<Button>(picker).Any(b=>(b.Content as string??"").StartsWith("选择时间段")))
    throw new Exception("任务列表没有显示计划时间段筛选入口");
   picker.Preview();picker.SelectDate(date.AddDays(1));
   if(!picker.ApplyDraft())throw new Exception("无法应用计划时间筛选");
   main.UpdateLayout();if(Texts((DependencyObject)main.Content).Contains(task.Name))throw new Exception("日期不重叠的任务仍在列表中");
   picker.Preview();picker.SelectDate(date);picker.SelectDate(date);
   if(!picker.ApplyDraft())throw new Exception("无法应用单日计划时间筛选");
   main.UpdateLayout();if(!Texts((DependencyObject)main.Content).Contains(task.Name))throw new Exception("计划结束当天的任务未显示");
   double originalWidth=main.Width,originalHeight=main.Height;main.Width=main.MinWidth;main.Height=main.MinHeight;main.UpdateLayout();
   Save((FrameworkElement)main.Content,Path.Combine(folder,"计划时间筛选-最小窗口.png"));main.Width=originalWidth;main.Height=originalHeight;
   bool excelOpened=false,ganttOpened=false;
   Ui.TestDialogReady=w=>{excelOpened=true;if(!Texts((DependencyObject)w.Content).Any(t=>t.Contains("全部 1 项匹配任务")&&t.Contains("计划时间："+task.End+" — "+task.End)))throw new Exception("Excel 导出未沿用计划时间筛选");w.Close();};
   Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出 Excel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Ui.TestDialogReady=w=>{
    ganttOpened=true;
    var labels=Texts((DependencyObject)w.Content);if(!labels.Contains("任务列表筛选结果")||!labels.Any(t=>t.StartsWith(date.ToString("yyyy.MM.dd")+" — "+date.ToString("yyyy.MM.dd"))&&t.Contains("1 项任务")))throw new Exception("甘特图导出未使用列表任务与日期范围");
    Children<ComboBox>((DependencyObject)w.Content).Single().SelectedIndex=2;
    if(!Texts((DependencyObject)w.Content).Any(t=>t.Contains("匹配任务的原始计划日期")))throw new Exception("XML 导出未显示筛选范围");
    w.Close();
   };
   Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(!excelOpened||!ganttOpened||picker.Start!=date)throw new Exception("导出入口未打开或筛选日期丢失");
   Children<Button>(picker).Single(b=>b.Content as string=="清除").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   search.Text="无初始说明的新任务";SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);main.UpdateLayout();
   if(picker.Start.HasValue||!Texts((DependencyObject)main.Content).Contains("无初始说明的新任务"))throw new Exception("清除筛选后未恢复无计划时间的任务");
   search.Text="";
  }
  static void VerifyGanttStatusChange(MainWindow main,Store store,string folder){
   Action layout=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};
   double windowHeight=main.Height;
   main.TestView(true);main.Height=720;layout();
   DateTime rangeStart=Children<GanttView>((DependencyObject)main.Content).Single().Start;
   var fixture=Enumerable.Range(0,24).Select(i=>Model.Id()).ToArray();
   store.Commit(d=>{
    for(int i=0;i<fixture.Length;i++)d.Tasks.Add(new TaskItem{
     Id=fixture[i],ProjectId=d.CurrentProjectId,Name="滚动位置验证 "+i.ToString("D2"),PersonId=d.People[0].Id,
     Start=Model.Date(rangeStart.AddDays(-2)),End=Model.Date(rangeStart.AddDays(i<14?1:5)),Status="执行中",InGantt=true
    });
   });
   main.TestView(true);SetChartFilter(main,new[]{"执行中"});layout();
   var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();
   string id=fixture[7];var original=store.Data.Tasks.Single(t=>t.Id==id);
   int oldIndex=viewport.Chart.Ordered().FindIndex(t=>t.Id==id);
   viewport.SetZoom(1.6,700);layout();
   viewport.Scroll.ScrollToHorizontalOffset(180);
   viewport.Scroll.ScrollToVerticalOffset(oldIndex*62-85);layout();
   double horizontal=viewport.Scroll.HorizontalOffset,vertical=viewport.Scroll.VerticalOffset,zoom=viewport.Zoom;
   if(horizontal<=0||vertical<=0)throw new Exception("滚动位置验证需要非零的横纵偏移");
   Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-改状态前位置.png"));
   Ui.TestDialogReady=w=>{
    var target=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.Name=="ScheduleStatus");
    var priority=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.Name=="SchedulePriority");
    if((string)target.SelectedItem!=original.Status||(string)priority.SelectedItem!=original.Priority)throw new Exception("排期编辑未显示当前状态和优先级");
    target.SelectedItem="测试中";w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"甘特图-修改任务状态.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存并同步甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   viewport.Chart.TaskClicked(id);layout();
   var updatedViewport=Children<GanttViewport>((DependencyObject)main.Content).Single();
   int newIndex=updatedViewport.Chart.Ordered().FindIndex(t=>t.Id==id);
   if(updatedViewport.Chart.Start!=rangeStart||Math.Abs(updatedViewport.Scroll.HorizontalOffset-horizontal)>.1||Math.Abs(updatedViewport.Zoom-zoom)>.001||Math.Abs(newIndex*62-updatedViewport.Scroll.VerticalOffset-(oldIndex*62-vertical))>.1||!Object.ReferenceEquals(viewport,updatedViewport))
    throw new Exception("保存状态后甘特图跳动，日期 "+rangeStart.ToShortDateString()+" → "+updatedViewport.Chart.Start.ToShortDateString()+"，纵向偏移 "+vertical+" → "+updatedViewport.Scroll.VerticalOffset);
   Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-改状态后位置.png"));
   var task=store.Data.Tasks.Single(t=>t.Id==id);if(task.Status!="测试中"||!task.History.Any(h=>h.Kind=="状态变更"&&h.Before=="执行中"&&h.After=="测试中"&&h.Note==null)||!Children<GanttView>((DependencyObject)main.Content).Single().Tasks.Any(t=>t.Id==id&&t.Status=="测试中"))throw new Exception("甘特条点击后的状态修改、时间线记录或筛选同步失败");
   Ui.TestDialogReady=w=>{var content=(FrameworkElement)w.Content;Children<ScrollViewer>(content).First().ScrollToBottom();w.UpdateLayout();if(Texts(content).Count(t=>t=="状态：执行中 → 测试中")!=1)throw new Exception("任务详情未展示独立的状态变更记录");Save(content,Path.Combine(folder,"任务详情-状态变更.png"));w.Close();};
   Dialogs.Detail(main,store,id);
   byte[] saved=Store.Encode(store.Data);Ui.TestDialogReady=w=>{Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.Name=="ScheduleStatus").SelectedItem="已完成";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   double afterVertical=updatedViewport.Scroll.VerticalOffset;
   updatedViewport.Chart.TaskClicked(id);layout();
   if(!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath)))||!Object.ReferenceEquals(updatedViewport,Children<GanttViewport>((DependencyObject)main.Content).Single())||Math.Abs(updatedViewport.Scroll.VerticalOffset-afterVertical)>.1)throw new Exception("取消排期状态修改后数据或视图发生变化");
   store.Commit(d=>d.Tasks.RemoveAll(t=>fixture.Contains(t.Id)));main.Height=windowHeight;main.Refresh();
   File.WriteAllText(Path.Combine(folder,"甘特图-修改状态检查.txt"),"PASS: 编辑排期默认显示任务当前状态和优先级。\nPASS: 状态变更写入时间线，甘特图保留同一个视图、日期范围、缩放、横向位置和编辑行的屏幕位置。\nPASS: 取消编辑不写入状态，视图也不跳动。\n");
  }
  static void VerifyClearDates(MainWindow main,Store store,string folder){
   main.TestView(true);SetChartFilter(main,new[]{"执行中"});main.UpdateLayout();var original=store.Data.Tasks.First(t=>t.InGantt&&t.Status=="执行中");string id=original.Id,range=Model.Range(original);byte[] saved=Store.Encode(store.Data);
   Ui.TestDialogReady=w=>{var dates=Children<DateRangePicker>((DependencyObject)w.Content).Single();Children<Button>(dates).Single(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(dates.Start.HasValue||dates.End.HasValue)throw new Exception("直接清空按钮未移除起止日期");w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"甘特图-清空时间.png"));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="取消").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   Children<GanttView>((DependencyObject)main.Content).Single().TaskClicked(id);main.UpdateLayout();if(!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("取消日期清空修改了任务");
   Ui.TestDialogReady=w=>{var dates=Children<DateRangePicker>((DependencyObject)w.Content).Single();Children<Button>(dates).Single(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存并同步甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   Children<GanttView>((DependencyObject)main.Content).Single().TaskClicked(id);main.UpdateLayout();var task=store.Data.Tasks.Single(t=>t.Id==id);if(task.Status!="执行中"||task.Start!=""||task.End!=""||task.InGantt||Children<GanttView>((DependencyObject)main.Content).Single().Tasks.Any(t=>t.Id==id)||!task.History.Any(h=>h.Kind=="清空计划时间"&&h.Detail.Contains(range)))throw new Exception("甘特图清空日期未保留状态、退出排期或记录原计划");
   var edit=store.Data.Tasks.First(t=>t.InGantt&&t.Status=="执行中");id=edit.Id;Ui.TestDialogReady=w=>{var dates=Children<DateRangePicker>((DependencyObject)w.Content).Single();Children<Button>(dates).Single(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,id);if(store.Data.Tasks.Single(t=>t.Id==id).Start!=""||store.Data.Tasks.Single(t=>t.Id==id).Status!="执行中")throw new Exception("任务编辑清空日期未保存或改变状态");
   Ui.TestDialogReady=w=>{var dates=Children<DateRangePicker>((DependencyObject)w.Content).Single();dates.SetRange(DateTime.Today,DateTime.Today);Children<Button>(dates).Single(b=>b.Content as string=="清空时间").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Children<TextBox>((DependencyObject)w.Content).First(t=>!t.AcceptsReturn).Text="无日期新任务";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="创建任务").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,null);if(!store.Data.Tasks.Any(t=>t.Name=="无日期新任务"&&t.Start==""&&t.End==""))throw new Exception("新建任务不能清空日期");
   File.WriteAllText(Path.Combine(folder,"清空时间-检查.txt"),"PASS: 新建任务、编辑任务与甘特图排期均可直接清空时间。\nPASS: 保存后保留状态，退出甘特图并记录原计划。\nPASS: 取消清空不改变内存或磁盘中的任务。\n");
  }
  static void VerifyGanttZoom(MainWindow main,Store store,string folder){
   byte[] saved=Store.Encode(store.Data);double originalWidth=main.Width,originalHeight=main.Height;Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};
   main.TestView(true);main.Height=720;flush();Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="45";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));flush();
   var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();if(viewport.Scroll.ScrollableWidth<=0||viewport.Chart.ActualWidth<=viewport.Scroll.ViewportWidth)throw new Exception("长日期范围没有横向滚动区域");
   viewport.Scroll.ScrollToHorizontalOffset(300);viewport.Scroll.ScrollToVerticalOffset(60);flush();double mouseX=viewport.Scroll.ViewportWidth/2,oldZoom=viewport.Zoom,oldVertical=viewport.Scroll.VerticalOffset;
   Func<double> dateAtMouse=()=>((viewport.Scroll.HorizontalOffset+mouseX-166)/(viewport.Chart.TimelineWidth-166))*viewport.Chart.Days;double before=dateAtMouse();
   if(viewport.HandleWheel(120,ModifierKeys.None,mouseX)||viewport.Zoom!=oldZoom)throw new Exception("普通滚轮错误触发甘特图缩放");
   if(!viewport.HandleWheel(240,ModifierKeys.Shift,mouseX)||viewport.Zoom<=oldZoom||Math.Abs(dateAtMouse()-before)>.001||Math.Abs(viewport.Scroll.VerticalOffset-oldVertical)>.1)throw new Exception("Shift 滚轮放大未保持鼠标日期和纵向位置");
   if(!viewport.HandleWheel(-240,ModifierKeys.Shift,mouseX)||Math.Abs(viewport.Zoom-oldZoom)>.0001||Math.Abs(dateAtMouse()-before)>.001)throw new Exception("Shift 滚轮缩小未保持日期锚点");
   viewport.HandleWheel(Int32.MaxValue,ModifierKeys.Shift,mouseX);if(viewport.Zoom!=4||Children<Button>(viewport).Single(b=>b.Name=="GanttZoomIn").IsEnabled)throw new Exception("放大超过 400% 或按钮没有禁用");
   viewport.Scroll.ScrollToHorizontalOffset(0);viewport.Scroll.ScrollToVerticalOffset(0);flush();Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-放大与横向滚动.png"));viewport.HandleWheel(Int32.MinValue,ModifierKeys.Shift,mouseX);if(viewport.Zoom!=.25||Children<Button>(viewport).Single(b=>b.Name=="GanttZoomOut").IsEnabled)throw new Exception("缩小超过 25% 或按钮没有禁用");viewport.Scroll.ScrollToVerticalOffset(0);flush();Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-缩小预览.png"));
   Children<Button>(viewport).Single(b=>b.Name=="GanttZoomReset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(viewport.Zoom!=1||Children<TextBlock>(viewport).Single(t=>t.Name=="GanttZoomValue").Text!="100%")throw new Exception("重置缩放失败");
   Children<Button>(viewport).Single(b=>b.Name=="GanttZoomIn").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(viewport.Zoom<=1)throw new Exception("放大按钮未生效");double retained=viewport.Zoom;viewport.Scroll.ScrollToHorizontalOffset(180);flush();
   main.TestView(false);main.TestView(true);flush();viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();if(Math.Abs(viewport.Zoom-retained)>.001||Math.Abs(viewport.Scroll.HorizontalOffset-180)>.1)throw new Exception("切换列表后丢失甘特图缩放和横向位置");
   main.Width=1040;flush();if(viewport.Chart.ActualWidth<=viewport.Scroll.ViewportWidth||viewport.Scroll.ScrollableWidth<=0||Children<Button>(viewport).Any(b=>b.ActualWidth<1))throw new Exception("最小窗口下横向滚动或缩放控件不可用");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-缩放最小窗口.png"));
   if(!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("预览缩放改变了已保存的任务数据");
   Children<Button>(viewport).Single(b=>b.Name=="GanttZoomReset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="14";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.Width=originalWidth;main.Height=originalHeight;flush();
   File.WriteAllText(Path.Combine(folder,"甘特图-缩放检查.txt"),"PASS: Shift + 滚轮以鼠标日期为锚点缩放，普通滚轮不触发缩放。\nPASS: 缩放范围 25% 至 400%，按钮和重置正确，保留纵向位置。\nPASS: 长时间轴可横向滚动，切换列表后保留缩放和横向位置，最小窗口可用。\nPASS: 仅改变预览，任务日期及已保存数据保持不变。\n");
  }
  static void VerifyGanttDragging(MainWindow main,Store store,string folder){
   Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};main.TestView(true);flush();string id=store.Data.Tasks[4].Id;int clicks=0;
   Func<GanttView> chart=()=>Children<GanttView>((DependencyObject)main.Content).Single();Func<TaskItem> task=()=>store.Data.Tasks.Single(t=>t.Id==id);
   Action<GanttDragMode,int> drag=(mode,days)=>{var view=chart();view.TaskClicked=_=>clicks++;var hit=view.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==id);var point=new Point(mode==GanttDragMode.Start?hit.Rect.Left+1:mode==GanttDragMode.End?hit.Rect.Right-1:hit.Rect.Left+hit.Rect.Width/2,hit.Rect.Top+hit.Rect.Height/2);if(GanttView.PickDragMode(hit,point.X)!=mode)throw new Exception("鼠标位置没有识别为预期的拖动方式");
    byte[] saved=Store.Encode(store.Data);var expected=GanttDateChange.Create(task(),mode,days);int history=task().History.Count;double dayWidth=(view.TimelineWidth-166)/view.Days;var end=new Point(point.X+days*dayWidth,point.Y);if(!view.BeginPointer(point))throw new Exception("鼠标按下未命中任务条");view.MovePointer(end);flush();if(!view.IsDragging||!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("拖动草稿在松开前保存了数据");
    var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();double zoom=viewport.Zoom;if(!viewport.HandleWheel(120,ModifierKeys.Shift,point.X)||viewport.Zoom!=zoom)throw new Exception("拖动期间滚轮改变了时间轴缩放");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-拖动"+mode+"预览.png"));view.EndPointer(end);flush();if(task().Start!=Model.Date(expected.Start)||task().End!=Model.Date(expected.End)||task().History.Count!=history+1||clicks!=0)throw new Exception("拖动松开后的日期、时间线或点击抑制不正确");};
   drag(GanttDragMode.Move,-2);drag(GanttDragMode.Start,-1);drag(GanttDragMode.End,1);
   var viewportZoom=Children<GanttViewport>((DependencyObject)main.Content).Single();viewportZoom.SetZoom(1.8,viewportZoom.Scroll.ViewportWidth/2);viewportZoom.Scroll.ScrollToHorizontalOffset(220);flush();drag(GanttDragMode.Move,1);
   var current=chart();current.TaskClicked=_=>clicks++;var bar=current.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==id);var center=new Point(bar.Rect.Left+bar.Rect.Width/2,bar.Rect.Top+16);byte[] beforeCancel=Store.Encode(store.Data);current.BeginPointer(center);current.MovePointer(new Point(center.X+2*(current.TimelineWidth-166)/current.Days,center.Y));flush();
   current.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(current),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});current.EndPointer(center);flush();if(current.IsPointerActive||!beforeCancel.SequenceEqual(Store.Encode(store.Data))||!beforeCancel.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath)))||clicks!=0)throw new Exception("Esc 取消拖动没有保留原排期");
   current=chart();bar=current.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==id);center=new Point(bar.Rect.Left+bar.Rect.Width/2,bar.Rect.Top+16);current.BeginPointer(center);current.MovePointer(new Point(center.X+70,center.Y));current.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.LostMouseCaptureEvent});flush();if(current.IsPointerActive||!beforeCancel.SequenceEqual(Store.Encode(store.Data)))throw new Exception("失去鼠标捕获后未取消拖动");
   current=chart();current.TaskClicked=_=>clicks++;bar=current.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==id);center=new Point(bar.Rect.Left+bar.Rect.Width/2,bar.Rect.Top+16);current.BeginPointer(center);current.MovePointer(new Point(center.X+5,center.Y));current.EndPointer(new Point(center.X+5,center.Y));flush();if(clicks!=0||!beforeCancel.SequenceEqual(Store.Encode(store.Data)))throw new Exception("不足一天的小幅拖动产生了保存或编辑弹窗");
   current.BeginPointer(center);current.EndPointer(center);flush();var label=current.HitRegions.Single(h=>h.IsLabel&&h.Task.Id==id);var labelPoint=new Point(label.Rect.Left+label.Rect.Width/2,label.Rect.Top+10);current.BeginPointer(labelPoint);current.EndPointer(labelPoint);if(clicks!=2||!beforeCancel.SequenceEqual(Store.Encode(store.Data)))throw new Exception("单击任务条或名称不再进入编辑，或错误保存日期");
   var clipped=new BarHit{Rect=new Rect(170,0,80,32),StartVisible=false,EndVisible=false};if(GanttView.PickDragMode(clipped,171)!=GanttDragMode.Move||GanttView.PickDragMode(clipped,249)!=GanttDragMode.Move)throw new Exception("被视图裁切的边缘被误当成真实起止日期");
   var resetViewport=Children<GanttViewport>((DependencyObject)main.Content).Single();resetViewport.SetZoom(1,resetViewport.Scroll.ViewportWidth/2);main.Refresh();flush();File.WriteAllText(Path.Combine(folder,"甘特图-拖动检查.txt"),"PASS: 中间整体移动、左端改开始、右端改结束，松开后各保存一次。\nPASS: 拖动草稿不写入内存或磁盘，缩放及横向滚动后仍按自然日准确落位。\nPASS: Esc、失去鼠标捕获和小幅拖动不保存；真正拖动不打开编辑弹窗。\nPASS: 单击任务条和名称仍可编辑，视图裁切的边缘不能误改日期。\n");
  }
  static void VerifyGanttMultiDrag(MainWindow main,Store store,string folder){
   Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};main.TestView(true);flush();Func<GanttView> chart=()=>Children<GanttView>((DependencyObject)main.Content).Single();var ids=new[]{store.Data.Tasks[2].Id,store.Data.Tasks[3].Id};int clicks=0;chart().ClearSelection();
   Action<string> shiftClick=id=>{var view=chart();view.TaskClicked=_=>clicks++;var hit=view.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==id);var point=new Point(hit.Rect.Left+hit.Rect.Width/2,hit.Rect.Top+16);view.BeginPointer(point,ModifierKeys.Shift);view.EndPointer(point);flush();};
   byte[] saved=Store.Encode(store.Data);shiftClick(ids[0]);shiftClick(ids[1]);if(!new System.Collections.Generic.HashSet<string>(chart().SelectedTaskIds).SetEquals(ids)||clicks!=0||!saved.SequenceEqual(Store.Encode(store.Data)))throw new Exception("Shift 多选打开了编辑窗口或修改了任务日期");
   shiftClick(ids[0]);if(chart().SelectedTaskIds.Contains(ids[0]))throw new Exception("Shift 再次点击没有取消选择");shiftClick(ids[0]);main.SetShowTaskDetails(true);flush();Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-Shift多选.png"));
   var before=store.Data.Tasks.Where(t=>ids.Contains(t.Id)).ToDictionary(t=>t.Id,t=>new[]{t.Start,t.End,t.History.Count.ToString()});var viewDrag=chart();viewDrag.TaskClicked=_=>clicks++;var bar=viewDrag.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==ids[0]);var center=new Point(bar.Rect.Left+bar.Rect.Width/2,bar.Rect.Top+16);double dayWidth=(viewDrag.TimelineWidth-166)/viewDrag.Days;var end=new Point(center.X+dayWidth,center.Y);
   viewDrag.BeginPointer(center);viewDrag.MovePointer(end);flush();if(!saved.SequenceEqual(Store.Encode(store.Data)))throw new Exception("批量拖动草稿修改了已保存数据");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-多任务一起移动.png"));viewDrag.EndPointer(end);flush();
   if(ids.Any(id=>{var t=store.Data.Tasks.Single(x=>x.Id==id);return t.Start!=Model.Date(Model.Parse(before[id][0]).AddDays(1))||t.End!=Model.Date(Model.Parse(before[id][1]).AddDays(1))||t.History.Count!=Int32.Parse(before[id][2])+1;})||clicks!=0||chart().SelectedTaskIds.Count()!=2)throw new Exception("批量移动没有同时更新全部选中任务或丢失选择");
   saved=Store.Encode(store.Data);viewDrag=chart();bar=viewDrag.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==ids[0]);center=new Point(bar.Rect.Left+bar.Rect.Width/2,bar.Rect.Top+16);viewDrag.BeginPointer(center,ModifierKeys.Shift);viewDrag.MovePointer(new Point(center.X-dayWidth,center.Y));viewDrag.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(viewDrag),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});flush();if(!saved.SequenceEqual(Store.Encode(store.Data))||chart().SelectedTaskIds.Count()!=2)throw new Exception("取消批量拖动后改变了日期或选择");
   string otherStart=store.Data.Tasks.Single(t=>t.Id==ids[1]).Start,otherEnd=store.Data.Tasks.Single(t=>t.Id==ids[1]).End;viewDrag=chart();bar=viewDrag.HitRegions.Single(h=>!h.IsLabel&&h.Task.Id==ids[0]);var edge=new Point(bar.Rect.Right-1,bar.Rect.Top+16);dayWidth=(viewDrag.TimelineWidth-166)/viewDrag.Days;viewDrag.BeginPointer(edge);viewDrag.MovePointer(new Point(edge.X+dayWidth,edge.Y));viewDrag.EndPointer(new Point(edge.X+dayWidth,edge.Y));flush();if(store.Data.Tasks.Single(t=>t.Id==ids[1]).Start!=otherStart||store.Data.Tasks.Single(t=>t.Id==ids[1]).End!=otherEnd)throw new Exception("多选后拖动边缘误改其他任务");
   foreach(var group in chart().Ordered().GroupBy(t=>t.PersonId)){var lengths=group.Select(t=>(Model.Parse(t.End)-Model.Parse(t.Start)).Days).ToList();if(!lengths.SequenceEqual(lengths.OrderBy(n=>n)))throw new Exception("同人任务没有按短到长排列");}
   main.TestView(false);main.TestView(true);flush();if(chart().SelectedTaskIds.Count()!=2)throw new Exception("切换任务列表后多选丢失");SetChartFilter(main,new[]{"测试中"});flush();if(chart().SelectedTaskIds.Any())throw new Exception("筛选隐藏任务后仍保留批量操作选择");SetChartFilter(main,new[]{"执行中"});flush();shiftClick(ids[0]);chart().RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(chart()),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});flush();if(chart().SelectedTaskIds.Any())throw new Exception("Esc 没有清空多选");main.SetShowTaskDetails(false);main.Refresh();flush();
   File.WriteAllText(Path.Combine(folder,"甘特图-多选拖动检查.txt"),"PASS: Shift 单击选中、再次单击取消，选择不修改日期或打开编辑。\nPASS: 拖动选中条中间统一移动所有选中任务，松开各记录一次，取消不保存。\nPASS: 拖动边缘仅修改当前任务，同人任务保存后仍按天数从短到长排序。\nPASS: 切换列表保留选择，筛选隐藏后剔除选择，Esc 清空选择。\n");
  }
  static void VerifyGanttFrozenHeader(MainWindow main,Store store,string folder){
   double originalHeight=main.Height;byte[] saved=Store.Encode(store.Data);Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};main.TestView(true);main.Height=720;flush();Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="31";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));flush();
   var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();var header=viewport.Header;if(header.ActualHeight!=64||viewport.Scroll.ScrollableHeight<=0)throw new Exception("冻结栏高度或纵向滚动范围不正确");
   Func<byte[]> headerPixels=()=>{int width=(int)header.ActualWidth;var bitmap=new RenderTargetBitmap(width,64,96,96,PixelFormats.Pbgra32);bitmap.Render(header);var pixels=new byte[width*64*4];bitmap.CopyPixels(pixels,width*4,0);return pixels;};
   Action verifyColumns=()=>{int width=(int)header.ActualWidth;var bitmap=new RenderTargetBitmap((int)Math.Ceiling(viewport.Chart.ActualWidth),64,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var context=visual.RenderOpen()){GanttView.Draw(context,store.Data,viewport.Chart.Ordered(),viewport.Chart.Start,viewport.Chart.Days,viewport.Chart.ActualWidth,false,"",1,1,viewport.Chart.GroupPeople,viewport.Chart.ShowDetails,viewport.Chart.TimelineWidth);}bitmap.Render(visual);var pixels=new byte[width*64*4];bitmap.CopyPixels(new Int32Rect((int)viewport.Scroll.HorizontalOffset,0,width,64),pixels,width*4,0);var actual=headerPixels();var checkRows=new[]{0,31,35,62};bool aligned=checkRows.All(y=>Enumerable.Range(y*width*4+168*4,(width-168)*4).All(i=>pixels[i]==actual[i]));if(!aligned){Store.SafeOutput(Path.Combine(folder,"冻结栏-实际.png"),Exporter.Image(BitmapSource.Create(width,64,96,96,PixelFormats.Pbgra32,null,actual,width*4),false));Store.SafeOutput(Path.Combine(folder,"冻结栏-参考.png"),Exporter.Image(BitmapSource.Create(width,64,96,96,PixelFormats.Pbgra32,null,pixels,width*4),false));Save((FrameworkElement)main.Content,Path.Combine(folder,"冻结栏-诊断.png"));int first=Enumerable.Range(0,pixels.Length).First(i=>pixels[i]!=actual[i]),count=Enumerable.Range(0,pixels.Length).Count(i=>pixels[i]!=actual[i]);throw new Exception("冻结日期栏与原时间轴的列坐标或绘制不一致；差异字节 "+count+"；首个差异 "+((first/4)%width)+","+((first/4)/width)+"；水平位置 "+viewport.Scroll.HorizontalOffset+"；栏边距 "+header.Margin);}};
   viewport.Scroll.ScrollToHorizontalOffset(180);viewport.Scroll.ScrollToVerticalOffset(0);flush();double top=header.TranslatePoint(new Point(),main).Y;var before=headerPixels();viewport.Scroll.ScrollToVerticalOffset(85);flush();if(Math.Abs(header.TranslatePoint(new Point(),main).Y-top)>.01||!before.SequenceEqual(headerPixels()))throw new Exception("向下滚动时日期栏移动或日期内容改变");verifyColumns();Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-向下滚动日期冻结.png"));
   var input=viewport.InputHitTest(header.TranslatePoint(new Point(190,35),viewport));if(!Object.ReferenceEquals(input,header))throw new Exception("冻结日期栏的鼠标事件穿透到了下方任务");var click=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent};header.RaiseEvent(click);if(!click.Handled)throw new Exception("冻结栏没有拦截任务点击");
   var horizontal=headerPixels();viewport.Scroll.ScrollToHorizontalOffset(260);flush();if(horizontal.SequenceEqual(headerPixels()))throw new Exception("水平滚动后冻结日期栏没有同步变化");verifyColumns();viewport.SetZoom(1.5,viewport.Scroll.ViewportWidth/2);flush();viewport.Scroll.ScrollToHorizontalOffset(220);flush();verifyColumns();if(Math.Abs(header.TranslatePoint(new Point(),main).Y-top)>.01)throw new Exception("缩放改变了冻结栏位置");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-冻结栏与缩放对齐.png"));
   viewport.Scroll.ScrollToVerticalOffset(0);flush();viewport.WheelOverHeader(-120,ModifierKeys.None,200);flush();if(SystemParameters.WheelScrollLines!=0&&viewport.Scroll.VerticalOffset<=0)throw new Exception("鼠标停在冻结栏上时无法滚动任务");double zoom=viewport.Zoom;viewport.WheelOverHeader(120,ModifierKeys.Shift,200);flush();if(viewport.Zoom<=zoom)throw new Exception("冻结栏上 Shift 滚轮无法缩放");viewport.Scroll.ScrollToHorizontalOffset(240);flush();verifyColumns();
   var view=viewport.Chart;var hit=view.HitRegions.Last(h=>!h.IsLabel);var point=new Point(hit.Rect.Left+hit.Rect.Width/2,hit.Rect.Top+16);before=headerPixels();view.BeginPointer(point);view.MovePointer(new Point(point.X+(view.TimelineWidth-166)/view.Days,point.Y));flush();if(!before.SequenceEqual(headerPixels()))throw new Exception("拖动任务时冻结日期栏与时间轴错位");view.CancelDrag();flush();if(!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("日期冻结改变了已保存任务数据");
   viewport.SetZoom(1,viewport.Scroll.ViewportWidth/2);Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="14";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));main.Height=originalHeight;flush();
   File.WriteAllText(Path.Combine(folder,"甘特图-冻结日期检查.txt"),"PASS: 日期栏固定在视口顶部，向下滚动时像素和位置保持不变。\nPASS: 横向滚动及缩放后，日期分隔线、周末底色和今日标记与原时间轴对齐。\nPASS: 固定栏拦截任务点击，栏上普通滚轮可滚动、Shift 滚轮可缩放。\nPASS: 任务拖动时日期栏保持对齐，取消拖动不修改数据。\n");
  }
  static void VerifySingleChartStatusFilter(MainWindow main,Store store,string folder){
   main.TestView(true);main.UpdateLayout();var picker=ChartFilter(main);var before=Store.Encode(store.Data);if(Children<MultiPicker>((DependencyObject)main.Content).Count(p=>p.Name=="GanttStatusFilter")!=1||Children<ComboBox>((DependencyObject)main.Content).Any(c=>c.Items.Contains("自选多状态")))throw new Exception("甘特图仍有两个状态筛选入口");
   var popup=ChartFilterPreview(main);if(Children<CheckBox>(popup).Count()!=Model.Statuses.Length)throw new Exception("统一状态下拉缺少选项");Children<Button>(popup).Single(b=>b.Content as string=="清空").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));popup=ChartFilterPreview(main);foreach(string name in new[]{"执行中","测试中"}){var checkbox=Children<CheckBox>(popup).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)==name);checkbox.IsChecked=true;checkbox.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}main.UpdateLayout();
   if(!picker.Selected.SetEquals(new[]{"执行中","测试中"})||Children<MultiPicker>((DependencyObject)main.Content).Count(p=>p.Name=="GanttStatusFilter")!=1||Children<GanttView>((DependencyObject)main.Content).Single().Tasks.Any(t=>!picker.Selected.Contains(t.Status)))throw new Exception("状态多选产生重复控件或筛选不正确");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-统一状态筛选.png"));Save(picker.Preview(),Path.Combine(folder,"甘特图-状态多选下拉.png"),true,230);
   main.TestView(false);main.TestView(true);main.UpdateLayout();if(!ChartFilter(main).Selected.SetEquals(new[]{"执行中","测试中"}))throw new Exception("切换页面后丢失状态多选");ChartFilter(main).SelectAll();main.UpdateLayout();if(!ChartFilter(main).Selected.SetEquals(Model.Statuses))throw new Exception("状态全选失败");SetChartFilter(main,new[]{"执行中"});if(!before.SequenceEqual(Store.Encode(store.Data)))throw new Exception("筛选改变了任务数据");File.WriteAllText(Path.Combine(folder,"甘特图-单一状态筛选检查.txt"),"PASS: 单选、多选和全选共用一个状态下拉，不再额外出现自选控件。\nPASS: 组合状态、切换页面保留选择及全选生效，数据不变。\n");
  }
  static void VerifyFrozenPeople(MainWindow main,Store store,string folder){
   double height=main.Height;byte[] saved=Store.Encode(store.Data);Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};main.TestView(true);main.Height=720;main.SetShowTaskDetails(true);flush();var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();viewport.SetZoom(1.6,700);flush();
   Func<byte[]> pixels=()=>{int width=(int)viewport.People.ActualWidth,h=(int)viewport.People.ActualHeight;var bitmap=new RenderTargetBitmap(width,h,96,96,PixelFormats.Pbgra32);bitmap.Render(viewport.People);var data=new byte[width*h*4];bitmap.CopyPixels(data,width*4,0);return data;};viewport.Scroll.ScrollToVerticalOffset(85);viewport.Scroll.ScrollToHorizontalOffset(0);flush();double x=viewport.People.TranslatePoint(new Point(),main).X;var before=pixels();viewport.Scroll.ScrollToHorizontalOffset(220);flush();if(Math.Abs(viewport.People.TranslatePoint(new Point(),main).X-x)>.01||!before.SequenceEqual(pixels()))throw new Exception("横向滚动带走了人员列");
   var ordered=viewport.Chart.Ordered();string expected=ordered[(int)(viewport.Scroll.VerticalOffset/62)].PersonId;if(viewport.People.PinnedPersonId!=expected)throw new Exception("向下查看同组任务时人员名称没有吸顶");Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-人员列与日期双冻结.png"));var input=viewport.InputHitTest(viewport.People.TranslatePoint(new Point(80,30),viewport));if(!Object.ReferenceEquals(input,viewport.People))throw new Exception("冻结人员列点击穿透到任务条");
   viewport.Scroll.ScrollToVerticalOffset(195);flush();expected=ordered[Math.Min(ordered.Count-1,(int)(viewport.Scroll.VerticalOffset/62))].PersonId;if(viewport.People.PinnedPersonId!=expected)throw new Exception("滚到下一组时人员名称没有切换");viewport.SetZoom(1.2,750);flush();if(Math.Abs(viewport.People.TranslatePoint(new Point(),main).X-x)>.01)throw new Exception("缩放移动了冻结人员列");
   if(!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("冻结人员列修改了数据");viewport.SetZoom(1,700);main.SetShowTaskDetails(false);main.Height=height;main.Refresh();flush();File.WriteAllText(Path.Combine(folder,"甘特图-冻结人员检查.txt"),"PASS: 横向滚动及缩放时人员列固定，日期左上角保持显示。\nPASS: 纵向查看同人任务时姓名吸顶，进入下一组后切换。\nPASS: 人员列不误触任务，所有操作不改变数据。\n");
  }
  static void VerifyGanttExport(MainWindow main,Store store,string folder){
   byte[] saved=Store.Encode(store.Data);Action flush=()=>{main.UpdateLayout();main.Dispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));main.UpdateLayout();};main.TestView(true);SetChartFilter(main,new[]{"执行中","测试中"});main.SetShowTaskDetails(true);
   Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="31";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Children<ComboBox>((DependencyObject)main.Content).Single(c=>c.Items.Contains("按任务展示")).SelectedIndex=1;flush();
   var viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();viewport.Chart.ClearSelection();viewport.SetZoom(.87,600);viewport.Scroll.ScrollToHorizontalOffset(180);viewport.Scroll.ScrollToVerticalOffset(85);flush();double horizontal=viewport.Scroll.HorizontalOffset,vertical=viewport.Scroll.VerticalOffset;var chart=main.CaptureGanttExport();
   if(chart.Start!=viewport.Chart.Start||chart.Days!=31||chart.GroupPeople||!chart.IncludeDetails||chart.TimelineWidth!=viewport.Chart.TimelineWidth||chart.Width!=viewport.Chart.Width||!chart.Tasks.SequenceEqual(viewport.Chart.Ordered())||chart.Tasks.Any(t=>t.Status!="执行中"&&t.Status!="测试中"))throw new Exception("导出没有完整沿用实际界面的日期、筛选、按任务展示、缩放和详情");
   Exporter.Png(Path.Combine(folder,"当前视图完整导出.png"),store.Data,chart);Exporter.Pdf(Path.Combine(folder,"当前视图完整导出.pdf"),store.Data,chart);Save((FrameworkElement)main.Content,Path.Combine(folder,"导出前的当前甘特图.png"));bool opened=false;
   Ui.TestDialogReady=w=>{opened=true;w.UpdateLayout();var formats=Children<ComboBox>((DependencyObject)w.Content).ToList();if(formats.Count!=1||Children<CheckBox>((DependencyObject)w.Content).Any()||!Texts((DependencyObject)w.Content).Any(t=>t.Contains("按任务展示")&&t.Contains("显示详情")))throw new Exception("导出弹窗仍有重新分段或改变当前布局的选项");formats[0].SelectedIndex=1;w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"图片导出-当前完整视图.png"));formats[0].SelectedIndex=2;w.UpdateLayout();if(!Texts((DependencyObject)w.Content).Any(t=>t.Contains("全部任务、人员、排期和时间线")))throw new Exception("XML 没有说明完整数据范围");formats[0].SelectedIndex=0;w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"导出设置.png"));w.Close();};Children<Button>((DependencyObject)main.Content).First(b=>b.Content as string=="↓ 导出甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(!opened||viewport.Scroll.HorizontalOffset!=horizontal||viewport.Scroll.VerticalOffset!=vertical||!saved.SequenceEqual(Store.Encode(store.Data))||!saved.SequenceEqual(Store.Encode(LocalWorkspace.Read(store.FilePath))))throw new Exception("取消导出改变了视图位置或项目数据");main.TestView(false);var fromList=main.CaptureGanttExport();if(fromList.TimelineWidth!=chart.TimelineWidth||fromList.Days!=chart.Days||fromList.GroupPeople!=chart.GroupPeople||fromList.IncludeDetails!=chart.IncludeDetails||!fromList.Tasks.SequenceEqual(chart.Tasks))throw new Exception("任务列表中的导出入口丢失最近甘特图的布局");
   main.TestView(true);main.SetShowTaskDetails(false);SetChartFilter(main,new[]{"执行中"});Children<ComboBox>((DependencyObject)main.Content).Single(c=>c.Items.Contains("按任务展示")).SelectedIndex=0;Children<TextBox>((DependencyObject)main.Content).Single(t=>t.Name=="GanttRangeDays").Text="14";Children<Button>((DependencyObject)main.Content).Single(b=>b.Content as string=="应用").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));flush();viewport=Children<GanttViewport>((DependencyObject)main.Content).Single();viewport.SetZoom(1,600);viewport.Scroll.ScrollToHorizontalOffset(0);viewport.Scroll.ScrollToVerticalOffset(0);flush();
   File.WriteAllText(Path.Combine(folder,"甘特图-当前视图导出检查.txt"),"PASS: 当前界面 31 天、执行中及测试中、按任务展示、87% 缩放和详情直接传递到导出。\nPASS: PDF 与 PNG 各生成一个完整文件，滚动区域内容完整保留。\nPASS: 弹窗只选择文件格式，XML 保持完整数据范围；取消导出保留数据和滚动位置。\nPASS: 任务列表入口沿用最近甘特图布局。\n");
  }
  public static int Run(Application app,string folder){
   Directory.CreateDirectory(folder);app.ShutdownMode=ShutdownMode.OnExplicitShutdown;int result=0;
   using(var store=new Store(Path.Combine(folder,"ui-store"))){store.Commit(d=>{for(int i=0;i<4;i++)d.Tasks[i].Priority=Model.Priorities[i];var t=d.Tasks[0];t.Note="";t.History.Clear();t.Status="待讨论";Model.Record(d,t,"创建任务","","");t.History[0].At=DateTimeOffset.Now.AddDays(-3).ToString("o");t.Status="待执行";Model.Record(d,t,"状态变更","待讨论","");t.History[0].At=DateTimeOffset.Now.AddDays(-2).ToString("o");Model.Record(d,t,"需求追加",t.Status,"核实是否通过，进度如何");t.History[0].At=DateTimeOffset.Now.AddDays(-1).ToString("o");t.Status="执行中";Model.Record(d,t,"状态变更","待执行","");});var main=new MainWindow(store);main.Loaded+=(s,e)=>main.Dispatcher.BeginInvoke(new Action(()=>{
    try{
     main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-甘特图.png"));
     var detailsView=Children<GanttViewport>((DependencyObject)main.Content).Single();
     double compactHeight=detailsView.Chart.Height;
     main.SetShowTaskDetails(true);main.UpdateLayout();
     if(detailsView.Chart.Height<=compactHeight)throw new Exception("显示详情未在甘特图下方展开变更记录");
     detailsView.Scroll.ScrollToVerticalOffset(compactHeight-72);main.UpdateLayout();
     Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-纵向变更记录.png"));
     main.SetChartTimelineTypes(new[]{TaskTimelineTypes.Notes});main.UpdateLayout();
     var timelinePicker=Children<MultiPicker>((DependencyObject)main.Content).Single(p=>p.Name=="GanttTimelineTypes");
     Save(timelinePicker.Preview(),Path.Combine(folder,"甘特图-时间线类型.png"),true,230);
     Save((FrameworkElement)main.Content,Path.Combine(folder,"甘特图-仅备注.png"));
     detailsView.Scroll.ScrollToVerticalOffset(0);main.SetShowTaskDetails(false);main.UpdateLayout();
     VerifySingleChartStatusFilter(main,store,folder);VerifyGanttDragging(main,store,folder);VerifyGanttMultiDrag(main,store,folder);VerifyGanttZoom(main,store,folder);VerifyGanttFrozenHeader(main,store,folder);VerifyFrozenPeople(main,store,folder);main.TestView(false);main.UpdateLayout();Save((FrameworkElement)main.Content,Path.Combine(folder,"主界面-任务列表.png"));
     Save(Children<EntityPicker>((DependencyObject)main.Content).First().Preview(),Path.Combine(folder,"项目管理下拉.png"),true);
     Ui.TestDialogReady=w=>{w.UpdateLayout();var texts=Texts((DependencyObject)w.Content);if(!texts.Contains("任务时间线 · 4 条")||!texts.Where(t=>t=="创建任务"||t=="状态变更"||t=="需求追加").SequenceEqual(new[]{"创建任务","状态变更","需求追加","状态变更"}))throw new Exception("Creation, requirements and operations are split or incorrectly ordered");Save((FrameworkElement)w.Content,Path.Combine(folder,"任务时间线.png"));Children<ScrollViewer>((DependencyObject)w.Content).First().ScrollToBottom();w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务时间线-后续记录.png"));w.Close();};Dialogs.Detail(main,store,store.Data.Tasks[0].Id);
     VerifyTaskIndicators(main,store,folder);
     string firstStatus=store.Data.Tasks[1].Status,secondStatus=store.Data.Tasks[5].Status;
     Ui.TestDialogReady=w=>{
      w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"替换排期编辑.png"));
      if(!Children<Button>((DependencyObject)w.Content).Single(b=>b.Content as string=="查看时间线").IsEnabled)throw new Exception("单选甘特任务应显示可用的查看时间线按钮");
      if(Children<TextBox>((DependencyObject)w.Content).Any(t=>t.Name=="ChangeNoteBox"))throw new Exception("甘特图排期表单仍显示备注输入框");
      Ui.TestDialogReady=timeline=>{
       try{
        var content=(FrameworkElement)timeline.Content;
        timeline.UpdateLayout();
        var buttons=Children<Button>(content).ToList();
        var append=buttons.Single(b=>b.Content as string=="追加备注");
        var close=buttons.Single(b=>b.Content as string=="关闭");
        if(append.Parent!=close.Parent||append.TranslatePoint(new Point(),content).X>=close.TranslatePoint(new Point(),content).X)throw new Exception("追加备注应位于关闭按钮左侧");
        var timelineScroll=Children<ScrollViewer>(content).First();
        timelineScroll.ScrollToBottom();
        Save(content,Path.Combine(folder,"甘特图-任务时间线.png"));
        string draft=String.Join("\n",Enumerable.Range(1,16).Select(i=>i+". 核对任务进度、验收资料和待补充事项。"));
        var editor=Dialogs.ChangeNoteBox("时间线备注");
        editor.Text=draft;
        var records=timelineScroll.Content;
        var preview=Ui.Stack();
        Ui.Label(preview,"备注：",editor);
        try{
         timelineScroll.Content=preview;
         timeline.UpdateLayout();
         Save(preview,Path.Combine(folder,"时间线-长备注文本域.png"));
         VerifyExpandedNoteBox(editor);
        }finally{timelineScroll.Content=records;}
        Ui.TestDialogReady=note=>{
         try{
          var dialogContent=(FrameworkElement)note.Content;
          var field=Children<TextBox>(dialogContent).Single();
          field.Text=draft;
          note.UpdateLayout();
          Save(dialogContent,Path.Combine(folder,"甘特图-追加备注.png"));
          VerifyExpandedNoteBox(field);

          // 更长的内容只滚动弹窗内容区，底部操作始终可见。
          field.Text=String.Join("\n",Enumerable.Repeat(draft,8));
          note.UpdateLayout();
          var scroll=Children<ScrollViewer>(dialogContent).First();
          var save=Children<Button>(dialogContent).Single(b=>b.Content as string=="追加到时间线");
          if(scroll.ScrollableHeight<=0||save.TranslatePoint(new Point(),dialogContent).Y+save.ActualHeight>dialogContent.ActualHeight)
           throw new Exception("超长追加内容应由弹窗滚动且保留底部操作");
         }
         finally{note.Close();}
        };
        Dialogs.AppendTimelineText(timeline,store,store.Data.Tasks[0].Id,false);
       }
       finally{timeline.Close();}
      };
      Dialogs.Detail(w,store,store.Data.Tasks[0].Id,true);
      var picks=Children<MultiPicker>((DependencyObject)w.Content).ToList();if(picks.Count!=2)throw new Exception("Expected status and task multiselect controls");
      var status=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.Name=="ScheduleStatus");
      var priority=Children<ComboBox>((DependencyObject)w.Content).Single(c=>c.Name=="SchedulePriority");
      picks[0].SelectAll();picks[1].SelectAll();
      if(picks[1].Selected.Count!=16)throw new Exception("Multi-state filter did not expose all 16 tasks");
      if((string)status.SelectedItem!="多种状态"||(string)priority.SelectedItem!="多种优先级")throw new Exception("多选任务未显示不同的状态和优先级");
      Save(picks[1].Preview(),Path.Combine(folder,"任务多选下拉.png"),true);
      priority.SelectedItem="P1";
      picks[1].SetSelected(new[]{store.Data.Tasks[1].Id,store.Data.Tasks[5].Id});picks[1].Changed();
      if((string)status.SelectedItem!="多种状态"||(string)priority.SelectedItem!="P1")throw new Exception("更新任务选择后，批量优先级或状态显示不正确");
      Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存并同步甘特图").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     };
     Dialogs.Schedule(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[5].Priority!="P1"||store.Data.Tasks[0].InGantt||!store.Data.Tasks[5].InGantt||store.Data.Tasks[1].Status!=firstStatus||store.Data.Tasks[5].Status!=secondStatus)throw new Exception("批量排期未正确更新优先级或意外统一了状态");
     Ui.TestDialogReady=w=>{w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"任务编辑.png"));Children<TextBox>((DependencyObject)w.Content).First().Text="知识库对接（界面保存验证）";Children<ComboBox>((DependencyObject)w.Content).First(x=>x.ItemsSource==Model.Priorities).SelectedItem="P2";Children<Button>((DependencyObject)w.Content).First(b=>b.Content as string=="保存变更").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};Dialogs.TaskEditor(main,store,store.Data.Tasks[0].Id);
     if(store.Data.Tasks[0].Priority!="P2"||store.Data.Tasks[0].Name!="知识库对接（界面保存验证）")throw new Exception("Task editor save did not persist the edited name");
     VerifyCreateAndAppend(main,store,folder);VerifyEditableNote(main,store,folder);main.Refresh();main.TestView(false);SetTaskFilter(main,"TaskPriorityFilter",new[]{"P3"});main.UpdateLayout();var filtered=Texts((DependencyObject)main.Content);if(!filtered.Contains("无初始说明的新任务")||filtered.Contains("知识库对接（界面保存验证）"))throw new Exception("Priority filter did not restrict tasks");Save((FrameworkElement)main.Content,Path.Combine(folder,"优先级筛选.png"));SetTaskFilter(main,"TaskPriorityFilter",Model.Priorities);
     VerifyGanttExport(main,store,folder);
     Ui.TestDialogReady=w=>{w.UpdateLayout();var pathText=Children<System.Windows.Controls.TextBlock>((DependencyObject)w.Content).FirstOrDefault(t=>t.Text==store.DirectoryPath);if(pathText!=null)pathText.Text="%LOCALAPPDATA%\\LaoQinProgress";w.UpdateLayout();Save((FrameworkElement)w.Content,Path.Combine(folder,"全部数据导入导出.png"));var names=Children<Button>((DependencyObject)w.Content).Select(b=>b.Content as string).ToList();if(!names.Contains("导出全部数据")||!names.Contains("导入全部数据"))throw new Exception("Missing full-data import/export buttons");w.Close();};Dialogs.Backups(main,store);
     VerifyTaskExcelExport(main,store,folder);VerifyMultiFilters(main,store,folder);VerifyTaskPlanFilter(main,store,folder);VerifyTestingStatus(main,store,folder);VerifyDateRange(main,store,folder);VerifyGanttStatusChange(main,store,folder);VerifyClearDates(main,store,folder);
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
