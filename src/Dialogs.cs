// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LaoQin.ProgressManager {
 public static partial class Dialogs {
  static string lastTaskStatus="待讨论",lastTaskPersonId="";
  static void Footer(Panel p,Window w,Action save,string label="保存变更") {var row=Ui.Stack(true);row.Margin=new Thickness(0,22,0,0);row.HorizontalAlignment=HorizontalAlignment.Right;var cancel=Ui.Button("取消",()=>w.Close());cancel.Margin=new Thickness(0,0,8,0);row.Children.Add(cancel);row.Children.Add(Ui.Button(label,save,true));p.Children.Add(row);}
  internal static TextBox ChangeNoteBox(string label,double minHeight=72){
   var field=new TextBox{
    Name="ChangeNoteBox",
    AcceptsReturn=true,
    TextWrapping=TextWrapping.Wrap,
    MinHeight=minHeight,
    HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
    VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,
    MaxLength=Model.ChangeNoteMaxLength
   };
   System.Windows.Automation.AutomationProperties.SetName(field,label);
   return field;
  }
  static TextBox ChangeNoteField(Panel panel,string label="备注（选填）"){
   var field=ChangeNoteBox(label);
   field.Height=field.MinHeight;
   field.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
   Ui.Label(panel,label,field);return field;
  }
  public static bool TaskEditor(Window owner,Store store,string id){
   if(!store.Data.Projects.Any(x=>x.Id==store.Data.CurrentProjectId)){MessageBox.Show(owner,"请先创建项目，再新建任务。","老秦进度管理软件");return false;}
   var original=store.Data.Tasks.FirstOrDefault(t=>t.Id==id);var w=Ui.Dialog(owner,original==null?"新建任务":"编辑任务",500);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text(original==null?"新建任务":"编辑任务",21,null,true));p.Children.Add(Ui.Text("执行中的任务须填写负责人；计划时间可清空，暂不排期。",12,Ui.Muted));
   var name=new TextBox{Text=original==null?"":original.Name,MaxLength=500};var priority=Ui.PriorityPicker(new ComboBox{ItemsSource=Model.Priorities,SelectedItem=original==null?"P0":original.Priority,Width=88,Margin=new Thickness(8,0,0,0),ToolTip="优先级 · P0 最高，P3 最低"});var nameRow=new Grid();nameRow.ColumnDefinitions.Add(new ColumnDefinition());nameRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});nameRow.Children.Add(name);Grid.SetColumn(priority,1);nameRow.Children.Add(priority);Ui.Label(p,"任务名称 / 优先级",nameRow);var titleHint=Ui.Text("用短标题概括工作；同一模块可用相同前缀（如【充值】）。",11,Ui.Muted);titleHint.Margin=new Thickness(0,5,0,0);p.Children.Add(titleHint);
   string defaultStatus=original==null?(Model.Statuses.Contains(lastTaskStatus)?lastTaskStatus:"待讨论"):original.Status;var status=new ComboBox{ItemsSource=Model.Statuses,SelectedItem=defaultStatus};Ui.Label(p,"任务状态",status);
   string defaultPerson=original==null?(store.Data.People.Any(x=>x.Id==lastTaskPersonId&&!x.Deleted)?lastTaskPersonId:""):original.PersonId;var person=new EntityPicker(store,w,false,defaultPerson,null,allowClear:true);Ui.Label(p,"负责人",person);
   var dates=new DateRangePicker(original==null?"":original.Start,original==null?"":original.End);Ui.Label(p,"计划时间",dates);
   CheckBox noteToggle=null;TextBox changeNote;
   if(original==null){
    noteToggle=new CheckBox{Content="填写备注（选填）",Foreground=Ui.Muted,Margin=new Thickness(0,14,0,0)};
    var notePanel=Ui.Stack();notePanel.Visibility=Visibility.Collapsed;
    changeNote=ChangeNoteField(notePanel,"备注");
    noteToggle.Checked+=(s,e)=>notePanel.Visibility=Visibility.Visible;
    noteToggle.Unchecked+=(s,e)=>notePanel.Visibility=Visibility.Collapsed;
    p.Children.Add(noteToggle);p.Children.Add(notePanel);
   }else changeNote=ChangeNoteField(p);
   var hint=Ui.Text(original==null?"备注随创建任务写入时间线。":"备注随本次变更写入时间线。",11,Ui.Muted);hint.Margin=new Thickness(0,8,0,0);p.Children.Add(hint);
   Footer(p,w,()=>{
    string n=name.Text.Trim(),s=(string)status.SelectedItem,personId=person.SelectedId;
    string operationNote=noteToggle==null||noteToggle.IsChecked==true?changeNote.Text:null;var a=dates.Start;var b=dates.End;
    if(n.Length==0)throw new InvalidOperationException("请输入任务名称。");if(a.HasValue!=b.HasValue||(a.HasValue&&a>b))throw new InvalidOperationException("请填写正确、完整的起止日期。");
    if(s=="执行中"&&String.IsNullOrEmpty(personId))throw new InvalidOperationException("任务设为执行中时，必须选择负责人。");
    store.Commit(d=>{
     var t=d.Tasks.FirstOrDefault(x=>x.Id==id);bool created=t==null;
     if(created){t=new TaskItem{Id=Model.Id(),ProjectId=d.CurrentProjectId};d.Tasks.Add(t);}
     string before=t.Status,oldName=t.Name,oldRange=Model.Range(t),oldPerson=t.PersonId,oldPriority=t.Priority;
     bool wasIn=Model.IsCurrentGanttTask(t);
     t.Name=n;t.Status=s;t.Priority=(string)priority.SelectedItem;t.PersonId=personId??"";t.Start=a.HasValue?Model.Date(a.Value):"";t.End=b.HasValue?Model.Date(b.Value):"";
     if(s=="执行中"&&(created||before!="执行中"))t.InGantt=true;
     if(!a.HasValue||String.IsNullOrEmpty(t.PersonId))t.InGantt=false;
     if(created){Model.Record(d,t,"创建任务","","",operationNote);return;}
     string range=Model.Range(t);bool isIn=Model.IsCurrentGanttTask(t);
     var changes=new[]{
      Model.ChangeLine("任务名称",oldName,t.Name),
      Model.ChangeLine("优先级",oldPriority,t.Priority),
      oldPerson==t.PersonId?"":Model.ChangeLine("负责人",Model.PersonName(d,oldPerson),Model.PersonName(d,t.PersonId)),
      Model.ChangeLine("计划时间",oldRange,range),
      Model.ChangeLine("当前排期",wasIn?"已纳入":"未纳入",isIn?"已纳入":"未纳入")
     };
     string detail=String.Join("\n",changes.Where(line=>line.Length>0));
     if(before!=s)Model.Record(d,t,"状态变更",before,detail,operationNote);
     else if(detail.Length>0)Model.Record(d,t,!a.HasValue&&oldRange!=range?"清空计划时间":"任务修改",before,detail,operationNote);
     else if(!String.IsNullOrWhiteSpace(operationNote))Model.Record(d,t,"追加备注",s,"",operationNote);
    });lastTaskStatus=s;lastTaskPersonId=personId??"";w.DialogResult=true;
   },original==null?"创建任务":"保存变更");
   w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};return w.ShowDialog()==true;
  }
  static Border InfoTile(string caption,string value){var panel=Ui.Stack();panel.Children.Add(Ui.Text(caption,11,Ui.Muted));var text=Ui.Text(value,13,null,true);text.Margin=new Thickness(0,5,0,0);panel.Children.Add(text);var tile=Ui.Card(panel,13);tile.Background=Ui.Bg;tile.BorderThickness=new Thickness(0);tile.Margin=new Thickness(0,0,8,8);return tile;}
  static UIElement TimelineEntry(Change entry,bool last,List<Button> noteButtons,Action<string> saveNote){
   bool original=entry.Kind=="创建任务"||entry.Kind=="旧版备注";
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});row.ColumnDefinitions.Add(new ColumnDefinition());
   row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var dot=new Border{Width=10,Height=10,CornerRadius=new CornerRadius(5),Background=original?Ui.Muted:Ui.Brand,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,5,0,0)};Grid.SetColumn(dot,0);Grid.SetRow(dot,0);row.Children.Add(dot);
   var time=Ui.Text(Model.HistoryTime(entry),11,Ui.Muted);Grid.SetColumn(time,1);Grid.SetRow(time,0);row.Children.Add(time);
   if(!last){var line=new Border{Width=2,Background=Ui.Line,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Stretch,Margin=new Thickness(0,0,0,-19)};Grid.SetColumn(line,0);Grid.SetRow(line,1);row.Children.Add(line);}
   var cardContent=Ui.Stack();
   var header=new DockPanel();
   Button editNote;
   var body=ActivityContent(entry,noteButtons,saveNote,out editNote);
   DockPanel.SetDock(editNote,Dock.Right);
   header.Children.Add(editNote);
   var tag=new Border{Child=Ui.Text(entry.Kind,11,original?Ui.Muted:Ui.Brand,true),Background=original?Ui.Side:Ui.BrandSoft,CornerRadius=new CornerRadius(4),Padding=new Thickness(7,3,7,3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
   header.Children.Add(tag);
   cardContent.Children.Add(header);
   body.Margin=new Thickness(0,8,0,0);
   cardContent.Children.Add(body);
   var card=Ui.Card(cardContent,14);card.Margin=new Thickness(0,8,0,14);Grid.SetColumn(card,1);Grid.SetRow(card,1);row.Children.Add(card);return row;
  }
  static StackPanel ActivityContent(Change h,List<Button> noteButtons,Action<string> saveNote,out Button editNote){
   var panel=Ui.Stack();string details=Model.TimelineDetails(h);
   if(details.Length>0)panel.Children.Add(Ui.Text(details,12,Ui.Muted));
   var notePanel=Ui.Stack();
   notePanel.Margin=new Thickness(0,details.Length>0?8:0,0,0);
   notePanel.Children.Add(Ui.Text("备注：",12));
   var note=Ui.Text(Model.DisplayNote(h),12);
   notePanel.Children.Add(note);
   panel.Children.Add(notePanel);

   // 在原卡片内编辑，只在保存时写入，取消时恢复当前备注。
   Button edit=null;
   edit=Ui.Button("修改备注",()=>{
    foreach(var button in noteButtons)button.IsEnabled=false;
    note.Visibility=Visibility.Collapsed;
    var editor=Ui.Stack();
    var field=ChangeNoteBox("时间线备注");
    field.Text=h.Note??"";
    field.CaretIndex=0;
    field.Margin=new Thickness(0,4,0,0);
    editor.Children.Add(field);
    var actions=Ui.Stack(true);
    actions.HorizontalAlignment=HorizontalAlignment.Right;
    actions.Margin=new Thickness(0,8,0,0);
    Action closeEditor=()=>{
     notePanel.Children.Remove(editor);
     note.Visibility=Visibility.Visible;
     foreach(var button in noteButtons)button.IsEnabled=true;
     edit.Focus();
    };
    var cancel=Ui.Button("取消",closeEditor);
    cancel.Margin=new Thickness(0,0,8,0);
    actions.Children.Add(cancel);
    actions.Children.Add(Ui.Button("保存备注",()=>{
     if(field.Text==(h.Note??"")){closeEditor();return;}
     saveNote(field.Text);
    }));
    editor.Children.Add(actions);
    notePanel.Children.Add(editor);
    field.Focus();
   });
   System.Windows.Automation.AutomationProperties.SetName(edit,"修改备注 · "+Model.TimelineHeading(h));
   noteButtons.Add(edit);
   editNote=edit;
   return panel;
  }
  internal static bool AppendRequirement(Window owner,Store store,string id){
   return AppendTimelineText(owner,store,id,true);
  }
  internal static bool AppendTimelineText(Window owner,Store store,string id,bool isRequirement){
   if(!store.Data.Tasks.Any(t=>t.Id==id))return false;
   string title=isRequirement?"追加需求":"追加备注";
   string label=isRequirement?"本次补充内容":"备注内容";
   string kind=isRequirement?"需求追加":"追加备注";
   var w=Ui.Dialog(owner,title,520);
   w.MaxHeight=Math.Min(w.MaxHeight,SystemParameters.WorkArea.Height-48);
   var layout=new DockPanel{Margin=new Thickness(24)};
   var p=Ui.Stack();
   p.Children.Add(Ui.Text(title,21,null,true));
   var field=ChangeNoteBox(label,190);
   Ui.Label(p,label,field);
   var hint=Ui.Text("保存后按时间顺序显示在任务时间线中。",11,Ui.Muted);
   hint.Margin=new Thickness(0,8,0,0);
   p.Children.Add(hint);
   var footer=Ui.Stack();
   Footer(footer,w,()=>{
    string content=field.Text.Trim();
    if(content.Length==0)throw new InvalidOperationException("请输入"+label+"。");
    store.Commit(d=>{
     var current=d.Tasks.FirstOrDefault(t=>t.Id==id);
     if(current==null)throw new InvalidOperationException("任务不存在或已被删除。");
     Model.Record(d,current,kind,current.Status,"",content);
    });
    w.DialogResult=true;
   },"追加到时间线");
   // 长内容由弹窗统一滚动，提交按钮留在底部。
   DockPanel.SetDock(footer,Dock.Bottom);
   layout.Children.Add(footer);
   layout.Children.Add(new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   w.Content=layout;
   w.Loaded+=(s,e)=>field.Focus();
   return w.ShowDialog()==true;
  }
  /// <summary>打开任务详情及可编辑的时间线。</summary>
  /// <param name="owner">任务详情的父窗口。</param>
  /// <param name="store">当前工作区。</param>
  /// <param name="id">任务标识。</param>
  /// <returns>是否保存了任务或时间线的改动。</returns>
  public static bool Detail(Window owner,Store store,string id){
   return Detail(owner,store,id,false);
  }
  internal static bool Detail(Window owner,Store store,string id,bool timelineOnly){
   bool changed=false;bool reopen;
   do{
    reopen=false;var t=store.Data.Tasks.FirstOrDefault(x=>x.Id==id);if(t==null)return changed;
    var w=Ui.Dialog(owner,(timelineOnly?"任务时间线 · ":"任务详情 · ")+t.Name,720);w.SizeToContent=SizeToContent.Manual;w.Height=790;w.MaxHeight=Math.Min(850,SystemParameters.WorkArea.Height-50);w.ResizeMode=ResizeMode.CanResize;w.MinWidth=620;w.MinHeight=520;
    var main=new DockPanel{Margin=new Thickness(24)};
    var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});DockPanel.SetDock(footer,Dock.Bottom);footer.Margin=new Thickness(0,16,0,0);
    var actions=Ui.Stack(true);actions.HorizontalAlignment=HorizontalAlignment.Right;
    if(!timelineOnly){
     var add=Ui.Button("＋ 追加需求",()=>{if(AppendRequirement(w,store,id)){changed=true;reopen=true;w.Close();}},true);add.HorizontalAlignment=HorizontalAlignment.Left;Grid.SetColumn(add,0);footer.Children.Add(add);
     var del=Ui.Button("删除任务",()=>{if(MessageBox.Show(w,"删除任务“"+t.Name+"”及其时间线？","删除确认",MessageBoxButton.OKCancel,MessageBoxImage.Warning)==MessageBoxResult.OK){store.Commit(d=>d.Tasks.RemoveAll(x=>x.Id==id));changed=true;w.Close();}});del.Foreground=Ui.Muted;del.Margin=new Thickness(0,0,8,0);actions.Children.Add(del);
     var edit=Ui.Button("编辑任务",()=>{if(TaskEditor(w,store,id)){changed=true;w.Close();}});edit.Margin=new Thickness(0,0,8,0);actions.Children.Add(edit);
    }
    var content=Ui.Stack();
    if(!timelineOnly){
     var heading=Ui.Stack();heading.Children.Add(Ui.Text("任务详情",12,Ui.Muted));var title=Ui.TaskTitle(t.Name,t.Priority,24);title.Margin=new Thickness(0,10,0,12);heading.Children.Add(title);var statusRow=Ui.Stack(true);statusRow.Children.Add(Ui.Badge(t.Status));var project=store.Data.Projects.FirstOrDefault(x=>x.Id==t.ProjectId);var projectLabel=Ui.Text(project==null?"项目已删除":project.Name,12,Ui.Muted);projectLabel.Margin=new Thickness(10,0,0,0);statusRow.Children.Add(projectLabel);heading.Children.Add(statusRow);heading.Children.Add(new TaskProgressView(t){Margin=new Thickness(0,8,0,0)});var hero=Ui.Card(heading,20);hero.Margin=new Thickness(0,0,0,16);content.Children.Add(hero);
     var tiles=new Grid();tiles.ColumnDefinitions.Add(new ColumnDefinition());tiles.ColumnDefinitions.Add(new ColumnDefinition());tiles.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});tiles.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var personTile=InfoTile("负责人",Model.PersonName(store.Data,t.PersonId));Grid.SetColumn(personTile,0);Grid.SetRow(personTile,0);tiles.Children.Add(personTile);var dateTile=InfoTile("计划时间",Model.Range(t));Grid.SetColumn(dateTile,1);Grid.SetRow(dateTile,0);tiles.Children.Add(dateTile);var ganttTile=InfoTile("甘特图",Model.IsCurrentGanttTask(t)?"已纳入当前排期":"未纳入当前排期");Grid.SetColumn(ganttTile,0);Grid.SetRow(ganttTile,1);tiles.Children.Add(ganttTile);var projectTile=InfoTile("所属项目",project==null?"项目已删除":project.Name);Grid.SetColumn(projectTile,1);Grid.SetRow(projectTile,1);tiles.Children.Add(projectTile);content.Children.Add(tiles);
    }
    var timeline=Ui.Stack();
    content.Children.Add(timeline);
    var scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    Action markChanged=()=>changed=true;
    Button appendNote=null;
    if(timelineOnly){
     appendNote=Ui.Button("追加备注",()=>{
      if(!AppendTimelineText(w,store,id,false))return;

      // 保持当前窗口打开，追加成功后定位到新记录。
      markChanged();
      RenderTimeline(timeline,store,id,markChanged,appendNote);
      scroll.ScrollToBottom();
     },true);
     appendNote.Margin=new Thickness(0,0,8,0);
     actions.Children.Add(appendNote);
    }
    actions.Children.Add(Ui.Button("关闭",()=>w.Close()));Grid.SetColumn(actions,1);footer.Children.Add(actions);main.Children.Add(footer);
    RenderTimeline(timeline,store,id,markChanged,appendNote);
    main.Children.Add(scroll);w.Content=main;w.ShowDialog();
   }while(reopen);
   return changed;
  }

   static List<Button> RenderTimeline(Panel panel,Store store,string taskId,Action onSaved,Button appendNote){
    var task=store.Data.Tasks.First(t=>t.Id==taskId);
    var history=Model.TimelineEntries(task);
    panel.Children.Clear();
    var heading=Ui.Text("任务时间线 · "+history.Count+" 条",16,null,true);
    heading.Margin=new Thickness(0,14,0,10);
    panel.Children.Add(heading);
    if(history.Count==0)panel.Children.Add(Ui.Card(Ui.Text("暂无时间线记录",12,Ui.Muted),14));
    var noteButtons=new List<Button>();
    for(int i=0;i<history.Count;i++){
     int position=i;
     panel.Children.Add(TimelineEntry(history[i],i==history.Count-1,noteButtons,note=>{
      store.Commit(d=>{
       var current=d.Tasks.FirstOrDefault(t=>t.Id==taskId);
       if(current==null)throw new InvalidOperationException("任务不存在或已被删除。");
       Model.EditTimelineNote(current,position,note);
      });
      onSaved();
      var buttons=RenderTimeline(panel,store,taskId,onSaved,appendNote);
      // 保存后把键盘焦点放回原记录的操作，旧版备注清空时移到相邻记录。
      if(buttons.Count>0)buttons[Math.Min(position,buttons.Count-1)].Focus();
     }));
    }
    // 编辑某条备注时同时暂停追加，保留尚未保存的编辑内容。
    if(appendNote!=null){
     appendNote.IsEnabled=true;
     noteButtons.Add(appendNote);
    }
    return noteButtons;
   }

   public class ScheduleResult {public DateTime? Start,End;public HashSet<string> Statuses;}

  sealed class ScheduleField {
   readonly string[] choices;
   readonly string mixedLabel;
   string current;
   bool refreshing;
   public ComboBox Picker {get;private set;}
   public string Override {get;private set;}

   public ScheduleField(string name,string[] choices,string mixedLabel){
    this.choices=choices;
    this.mixedLabel=mixedLabel;
    Picker=new ComboBox{Name=name};
    Picker.SelectionChanged+=(s,e)=>{
     if(refreshing)return;
     string selected=Picker.SelectedItem as string;
     Override=selected!=current&&choices.Contains(selected)?selected:null;
    };
   }

   public void Refresh(IEnumerable<string> selectedValues){
    var values=selectedValues.Distinct().Take(2).ToArray();
    refreshing=true;
    try{
     if(values.Length==0){
      Override=null;
      current="未选择任务";
      Picker.ItemsSource=new[]{current};
     }else if(values.Length==1){
      current=values[0];
      Picker.ItemsSource=choices;
     }else{
      current=mixedLabel;
      Picker.ItemsSource=new[]{mixedLabel}.Concat(choices);
     }
     Picker.IsEnabled=values.Length>0;
     Picker.SelectedItem=Override??current;
    }finally{refreshing=false;}
   }
  }

  public static ScheduleResult Schedule(Window owner,Store store,string originalId){
   var original=store.Data.Tasks.FirstOrDefault(t=>t.Id==originalId);var w=Ui.Dialog(owner,original==null?"安排现有任务":"替换甘特条与调整排期",525);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text(original==null?"安排现有任务":"编辑甘特图排期",21,null,true));var desc=Ui.Text(original==null?"按状态筛选，再输入关键词筛选当前项目的任务。":"输入关键词筛选要放入当前甘特条的任务；原任务与历史记录保留。",12,Ui.Muted);desc.Margin=new Thickness(0,7,0,0);p.Children.Add(desc);
   var statuses=new MultiPicker("状态",Model.Statuses.Select(s=>new Choice{Id=s,Label=s}),original==null?Model.Statuses:new[]{original.Status});Ui.Label(p,"按状态筛选",statuses);
   Func<List<Choice>> options=()=>store.Data.Tasks.Where(t=>t.ProjectId==store.Data.CurrentProjectId&&statuses.Selected.Contains(t.Status)).Select(t=>new Choice{Id=t.Id,Label=t.Name,Priority=t.Priority,Detail=t.Status}).ToList();
   var taskPicker=new MultiPicker("任务",options(),original==null?new string[0]:new[]{original.Id},true);
   var taskRow=new DockPanel();
   var timeline=Ui.Button("查看时间线",()=>Detail(w,store,taskPicker.Selected.Single(),true));
   timeline.Margin=new Thickness(8,0,0,0);
   ToolTipService.SetShowOnDisabled(timeline,true);
   System.Windows.Automation.AutomationProperties.SetHelpText(timeline,"选择一项任务后可查看时间线并追加或修改备注。");
   DockPanel.SetDock(timeline,Dock.Right);
   taskRow.Children.Add(timeline);
   taskRow.Children.Add(taskPicker);
   Ui.Label(p,"任务名称",taskRow);
   var targetStatus=new ScheduleField("ScheduleStatus",Model.Statuses,"多种状态");Ui.Label(p,"修改任务状态",targetStatus.Picker);
   var priority=new ScheduleField("SchedulePriority",Model.Priorities,"多种优先级");Ui.Label(p,"优先级",Ui.PriorityPicker(priority.Picker));
   // 状态筛选也可能移除已选任务，两个字段都按剩余任务重新显示。
   Action refreshFields=()=>{
    var selected=store.Data.Tasks.Where(t=>taskPicker.Selected.Contains(t.Id)).ToList();
    targetStatus.Refresh(selected.Select(t=>t.Status));
    priority.Refresh(selected.Select(t=>t.Priority));
     timeline.IsEnabled=selected.Count==1;
     timeline.ToolTip=selected.Count==1?null:"请选择一项任务以查看时间线。";
   };
   taskPicker.Changed=refreshFields;
   statuses.Changed=()=>{taskPicker.SetOptions(options(),true);refreshFields();};
   refreshFields();
   var person=new EntityPicker(store,w,false,original==null?"":original.PersonId,null);Ui.Label(p,"负责人",person);
   var dates=new DateRangePicker(original==null?Model.Date(DateTime.Today):original.Start,original==null?Model.Date(DateTime.Today.AddDays(4)):original.End);Ui.Label(p,"计划时间",dates);
   var hint=Ui.Card(Ui.Text("时间可清空，保存后选中任务退出当前排期；状态按上方选择保存，原计划会记录到时间线。未再次选中的原任务及其历史保留。",12,Ui.Muted),12);hint.Background=Ui.Bg;hint.BorderThickness=new Thickness(0);hint.Margin=new Thickness(0,18,0,0);p.Children.Add(hint);ScheduleResult result=null;
   Footer(p,w,()=>{var a=dates.Start;var b=dates.End;if(a.HasValue!=b.HasValue)throw new InvalidOperationException("请填写完整的起止日期，或清空时间。");if(a.HasValue&&String.IsNullOrEmpty(person.SelectedId))throw new InvalidOperationException("请选择负责人。");var ids=new HashSet<string>(taskPicker.Selected);store.Commit(d=>Model.ReplaceSchedule(d,originalId,ids,a,b,person.SelectedId,priority.Override,targetStatus.Override));result=new ScheduleResult{Start=a,End=b,Statuses=new HashSet<string>(store.Data.Tasks.Where(t=>ids.Contains(t.Id)).Select(t=>t.Status))};w.DialogResult=true;},original==null?"加入甘特图":"保存并同步甘特图");w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();return result;
  }
 }
}
