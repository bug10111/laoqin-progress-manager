// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LaoQin.ProgressManager {
 public static partial class Dialogs {
  static string lastTaskStatus="待讨论",lastTaskPersonId="";
  public static DateTime? DateValue(DatePicker p){if(String.IsNullOrWhiteSpace(p.Text))return null;DateTime d;if(!DateTime.TryParse(p.Text,CultureInfo.CurrentCulture,DateTimeStyles.None,out d))throw new InvalidOperationException("日期格式不正确，请从日历选择日期。");return d.Date;}
  public static DatePicker DateBox(string date){var p=new DatePicker{SelectedDate=Model.IsDate(date)?(DateTime?)Model.Parse(date):null,MinHeight=36};p.DateValidationError+=(s,e)=>{e.ThrowException=false;};return p;}
  static void Footer(Panel p,Window w,Action save,string label="保存变更") {var row=Ui.Stack(true);row.Margin=new Thickness(0,22,0,0);row.HorizontalAlignment=HorizontalAlignment.Right;var cancel=Ui.Button("取消",()=>w.Close());cancel.Margin=new Thickness(0,0,8,0);row.Children.Add(cancel);row.Children.Add(Ui.Button(label,save,true));p.Children.Add(row);}
  static Grid DatePair(DatePicker a,DatePicker b){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(a);var to=Ui.Text("至",12,Ui.Muted);to.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(to,1);row.Children.Add(to);Grid.SetColumn(b,2);row.Children.Add(b);return row;}
  public static bool TaskEditor(Window owner,Store store,string id){
   if(!store.Data.Projects.Any(x=>x.Id==store.Data.CurrentProjectId)){MessageBox.Show(owner,"请先创建项目，再新建任务。","老秦进度管理软件");return false;}
   var original=store.Data.Tasks.FirstOrDefault(t=>t.Id==id);var w=Ui.Dialog(owner,original==null?"新建任务":"编辑任务",500);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text(original==null?"新建任务":"编辑任务",21,null,true));p.Children.Add(Ui.Text("执行中的任务须填写负责人和完整时间范围。",12,Ui.Muted));
   var name=new TextBox{Text=original==null?"":original.Name,MaxLength=500};Ui.Label(p,"任务名称",name);var titleHint=Ui.Text("用短标题概括工作；同一模块可用相同前缀（如【充值】）；完整原文和验收要求放在任务说明。",11,Ui.Muted);titleHint.Margin=new Thickness(0,5,0,0);p.Children.Add(titleHint);
   bool canEditInitialNote=original==null||String.IsNullOrWhiteSpace(original.Note);var note=new TextBox{Text="",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=78,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=50000};bool noteWasOpened=false;
   if(canEditInitialNote){var detailToggle=new CheckBox{Content="有详情（勾选后展开填写）",IsChecked=false,Foreground=Ui.Muted,Margin=new Thickness(0,10,0,0)};var notePanel=Ui.Stack();notePanel.Visibility=Visibility.Collapsed;notePanel.Margin=new Thickness(0,0,0,2);Ui.Label(notePanel,"初始详情 · 完整内容与验收说明",note);detailToggle.Checked+=(s,e)=>{noteWasOpened=true;notePanel.Visibility=Visibility.Visible;};detailToggle.Unchecked+=(s,e)=>{notePanel.Visibility=Visibility.Collapsed;};p.Children.Add(detailToggle);p.Children.Add(notePanel);}
   else{var noteTitle=Ui.Text("原始详情（只读保留）",12,Ui.Muted);noteTitle.Margin=new Thickness(0,14,0,7);p.Children.Add(noteTitle);var saved=new TextBox{Text=original.Note,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=78,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Ui.Bg,BorderThickness=new Thickness(0),Padding=new Thickness(11)};p.Children.Add(Ui.Card(saved,4));var noteHint=Ui.Text("后续补充请在任务详情中使用“追加需求”，每次追加都会带上时间并进入时间线。",11,Ui.Muted);noteHint.Margin=new Thickness(0,6,0,0);p.Children.Add(noteHint);}
   string defaultStatus=original==null?(Model.Statuses.Contains(lastTaskStatus)?lastTaskStatus:"待讨论"):original.Status;var status=new ComboBox{ItemsSource=Model.Statuses,SelectedItem=defaultStatus};Ui.Label(p,"任务状态",status);
   string defaultPerson=original==null?(store.Data.People.Any(x=>x.Id==lastTaskPersonId&&!x.Deleted)?lastTaskPersonId:""):original.PersonId;var person=new EntityPicker(store,w,false,defaultPerson,null);Ui.Label(p,"负责人",person);
   var start=DateBox(original==null?"":original.Start);var end=DateBox(original==null?"":original.End);Ui.Label(p,"计划时间 · 起止日期（包含结束当天）",DatePair(start,end));
   var hint=Ui.Text("状态变化、任务修改和排期变化都会记录操作时间。",11,Ui.Muted);hint.Margin=new Thickness(0,13,0,0);p.Children.Add(hint);
   Footer(p,w,()=>{
    string n=name.Text.Trim(),s=(string)status.SelectedItem,personId=person.SelectedId,savedNote=canEditInitialNote&&noteWasOpened?note.Text.Trim():(original==null?"":original.Note);var a=DateValue(start);var b=DateValue(end);
    if(n.Length==0)throw new InvalidOperationException("请输入任务名称。");if(a.HasValue!=b.HasValue||(a.HasValue&&a>b))throw new InvalidOperationException("请填写正确、完整的起止日期。");
    if(s=="执行中"&&(String.IsNullOrEmpty(personId)||!a.HasValue))throw new InvalidOperationException("任务设为执行中时，必须选择负责人和起止日期。");
    store.Commit(d=>{var t=d.Tasks.FirstOrDefault(x=>x.Id==id);bool created=t==null;if(created){t=new TaskItem{Id=Model.Id(),ProjectId=d.CurrentProjectId,History=new List<Change>()};d.Tasks.Add(t);}string before=t.Status,oldName=t.Name,oldRange=created?"":Model.Range(t),oldPerson=t.PersonId;bool wasIn=t.InGantt;t.Name=n;t.Status=s;t.PersonId=personId??"";t.Start=a.HasValue?Model.Date(a.Value):"";t.End=b.HasValue?Model.Date(b.Value):"";t.Note=savedNote;if(s=="执行中"&&(created||before!="执行中"))t.InGantt=true;if(!a.HasValue||String.IsNullOrEmpty(t.PersonId))t.InGantt=false;
     if(created||before!=s)Model.Record(d,t,created?"创建任务":"状态变更",before,created?"":"每次状态变更自动记录。");
     else if(oldName!=t.Name||oldRange!=Model.Range(t)||oldPerson!=t.PersonId||(original!=null&&original.Note!=t.Note)||wasIn!=t.InGantt)Model.Record(d,t,"任务修改",before,"原任务名称："+oldName+"；原计划："+oldRange+"。");
    });lastTaskStatus=s;lastTaskPersonId=personId??"";w.DialogResult=true;
   },original==null?"创建任务":"保存变更");
   w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};return w.ShowDialog()==true;
  }
  static Border InfoTile(string caption,string value){var panel=Ui.Stack();panel.Children.Add(Ui.Text(caption,11,Ui.Muted));var text=Ui.Text(value,13,null,true);text.Margin=new Thickness(0,5,0,0);panel.Children.Add(text);var tile=Ui.Card(panel,13);tile.Background=Ui.Bg;tile.BorderThickness=new Thickness(0);tile.Margin=new Thickness(0,0,8,8);return tile;}
  static UIElement TimelineEntry(string label,string timestamp,UIElement content,bool original,bool last){
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});row.ColumnDefinitions.Add(new ColumnDefinition());
   row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var dot=new Border{Width=10,Height=10,CornerRadius=new CornerRadius(5),Background=original?Ui.Muted:Ui.Brand,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,5,0,0)};Grid.SetColumn(dot,0);Grid.SetRow(dot,0);row.Children.Add(dot);
   var time=Ui.Text(timestamp,11,Ui.Muted);Grid.SetColumn(time,1);Grid.SetRow(time,0);row.Children.Add(time);
   if(!last){var line=new Border{Width=2,Background=Ui.Line,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Stretch,Margin=new Thickness(0,0,0,-19)};Grid.SetColumn(line,0);Grid.SetRow(line,1);row.Children.Add(line);}
   var cardContent=Ui.Stack();var tag=new Border{Child=Ui.Text(label,11,original?Ui.Muted:Ui.Brand,true),Background=original?Ui.Side:Ui.BrandSoft,CornerRadius=new CornerRadius(4),Padding=new Thickness(7,3,7,3),HorizontalAlignment=HorizontalAlignment.Left};cardContent.Children.Add(tag);var body=new Border{Child=content,Margin=new Thickness(0,10,0,0)};cardContent.Children.Add(body);
   var card=Ui.Card(cardContent,14);card.Margin=new Thickness(0,8,0,14);Grid.SetColumn(card,1);Grid.SetRow(card,1);row.Children.Add(card);return row;
  }
  static UIElement ActivityContent(Change h){
   if(h.Kind=="需求追加")return Ui.Text(h.Detail,12);
   var panel=Ui.Stack();
   string state=String.IsNullOrEmpty(h.Before)?h.After:(h.Before==h.After?"状态："+h.After:h.Before+"  →  "+h.After);var status=Ui.Text(state,12,h.After=="执行中"?Ui.Brand:Ui.Muted);status.Margin=new Thickness(0,5,0,0);panel.Children.Add(status);
   var details=Ui.Stack();if(!String.IsNullOrEmpty(h.PersonName))details.Children.Add(Ui.Text("负责人："+h.PersonName,11,Ui.Muted));if(Model.IsDate(h.Start))details.Children.Add(Ui.Text("计划："+h.Start+" — "+h.End,11,Ui.Muted));if(!String.IsNullOrEmpty(h.Detail))details.Children.Add(Ui.Text(h.Detail,11,Ui.Muted));if(details.Children.Count>0){details.Margin=new Thickness(0,5,0,0);panel.Children.Add(details);}
   return panel;
  }
  internal static bool AppendRequirement(Window owner,Store store,string id){
   var task=store.Data.Tasks.FirstOrDefault(x=>x.Id==id);if(task==null)return false;
   var w=Ui.Dialog(owner,"追加需求",520);var p=Ui.Stack();p.Margin=new Thickness(24);
   p.Children.Add(Ui.Text("追加需求",21,null,true));var intro=Ui.Text("原始详情会保留不变。新内容将以当前时间追加到任务时间线。",12,Ui.Muted);intro.Margin=new Thickness(0,6,0,4);p.Children.Add(intro);
   var field=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=190,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=50000};Ui.Label(p,"本次补充内容",field);
   var hint=Ui.Text("保存后按时间顺序显示在任务详情中；记录时间由软件自动生成。",11,Ui.Muted);hint.Margin=new Thickness(0,8,0,0);p.Children.Add(hint);
   Footer(p,w,()=>{string content=field.Text.Trim();if(content.Length==0)throw new InvalidOperationException("请输入本次补充内容。");store.Commit(d=>{var current=d.Tasks.FirstOrDefault(x=>x.Id==id);if(current==null)throw new InvalidOperationException("任务不存在或已被删除。");Model.Record(d,current,"需求追加",current.Status,content);});w.DialogResult=true;},"追加到时间线");
   w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.Loaded+=(s,e)=>field.Focus();return w.ShowDialog()==true;
  }
  public static void Detail(Window owner,Store store,string id){
   var t=store.Data.Tasks.FirstOrDefault(x=>x.Id==id);if(t==null)return;
   var w=Ui.Dialog(owner,"任务详情 · "+t.Name,720);w.SizeToContent=SizeToContent.Manual;w.Height=790;w.MaxHeight=Math.Min(850,SystemParameters.WorkArea.Height-50);w.ResizeMode=ResizeMode.CanResize;w.MinWidth=620;w.MinHeight=520;
   var main=new DockPanel{Margin=new Thickness(24)};
   var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});DockPanel.SetDock(footer,Dock.Bottom);footer.Margin=new Thickness(0,16,0,0);
   var add=Ui.Button("＋ 追加需求",()=>{if(AppendRequirement(w,store,id)){w.Close();owner.Dispatcher.BeginInvoke(new Action(()=>Detail(owner,store,id)));}},true);add.HorizontalAlignment=HorizontalAlignment.Left;Grid.SetColumn(add,0);footer.Children.Add(add);
   var actions=Ui.Stack(true);actions.HorizontalAlignment=HorizontalAlignment.Right;
   var del=Ui.Button("删除任务",()=>{if(MessageBox.Show(w,"删除任务“"+t.Name+"”及其时间线？","删除确认",MessageBoxButton.OKCancel,MessageBoxImage.Warning)==MessageBoxResult.OK){store.Commit(d=>d.Tasks.RemoveAll(x=>x.Id==id));w.Close();}});del.Foreground=Ui.Muted;del.Margin=new Thickness(0,0,8,0);actions.Children.Add(del);
   var edit=Ui.Button("编辑任务",()=>{if(TaskEditor(w,store,id))w.Close();});edit.Margin=new Thickness(0,0,8,0);actions.Children.Add(edit);
   actions.Children.Add(Ui.Button("关闭",()=>w.Close()));Grid.SetColumn(actions,1);footer.Children.Add(actions);main.Children.Add(footer);
   var content=Ui.Stack();var heading=Ui.Stack();heading.Children.Add(Ui.Text("任务详情",12,Ui.Muted));var title=Ui.Text(t.Name,24,null,true);title.Margin=new Thickness(0,10,0,12);heading.Children.Add(title);var statusRow=Ui.Stack(true);statusRow.Children.Add(Ui.Badge(t.Status));var project=store.Data.Projects.FirstOrDefault(x=>x.Id==t.ProjectId);var projectLabel=Ui.Text(project==null?"项目已删除":project.Name,12,Ui.Muted);projectLabel.Margin=new Thickness(10,0,0,0);statusRow.Children.Add(projectLabel);heading.Children.Add(statusRow);var hero=Ui.Card(heading,20);hero.Margin=new Thickness(0,0,0,16);content.Children.Add(hero);
   var tiles=new Grid();tiles.ColumnDefinitions.Add(new ColumnDefinition());tiles.ColumnDefinitions.Add(new ColumnDefinition());tiles.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});tiles.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var personTile=InfoTile("负责人",Model.PersonName(store.Data,t.PersonId));Grid.SetColumn(personTile,0);Grid.SetRow(personTile,0);tiles.Children.Add(personTile);var dateTile=InfoTile("计划时间",Model.Range(t));Grid.SetColumn(dateTile,1);Grid.SetRow(dateTile,0);tiles.Children.Add(dateTile);var ganttTile=InfoTile("甘特图",t.InGantt?"已纳入当前排期":"未纳入当前排期");Grid.SetColumn(ganttTile,0);Grid.SetRow(ganttTile,1);tiles.Children.Add(ganttTile);var projectTile=InfoTile("所属项目",project==null?"项目已删除":project.Name);Grid.SetColumn(projectTile,1);Grid.SetRow(projectTile,1);tiles.Children.Add(projectTile);content.Children.Add(tiles);
   var history=Model.Timeline(t);var created=history.FirstOrDefault(h=>h.Kind=="创建任务");bool legacyNote=created==null&&!String.IsNullOrWhiteSpace(t.Note);var timelineHeading=Ui.Text("任务时间线 · "+(history.Count+(legacyNote?1:0))+" 条",16,null,true);timelineHeading.Margin=new Thickness(0,14,0,10);content.Children.Add(timelineHeading);
   if(legacyNote)content.Children.Add(TimelineEntry("初始详情","创建时间未记录",Ui.Text(t.Note,12),true,history.Count==0));
   if(history.Count==0&&!legacyNote)content.Children.Add(Ui.Card(Ui.Text("暂无时间线记录，可通过“追加需求”记录新的补充内容。",12,Ui.Muted),14));
   for(int i=0;i<history.Count;i++){var h=history[i];var entry=Ui.Stack();if(h==created&&!String.IsNullOrWhiteSpace(t.Note)){entry.Children.Add(Ui.Text(t.Note,12));var details=new Border{Child=ActivityContent(h),Margin=new Thickness(0,10,0,0)};entry.Children.Add(details);}else entry.Children.Add(ActivityContent(h));content.Children.Add(TimelineEntry(h.Kind,Model.HistoryTime(h),entry,h.Kind=="创建任务",i==history.Count-1));}
   main.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});w.Content=main;w.ShowDialog();
  }  public class ScheduleResult {public DateTime Start;public HashSet<string> Statuses;}
  public static ScheduleResult Schedule(Window owner,Store store,string originalId){
   var original=store.Data.Tasks.FirstOrDefault(t=>t.Id==originalId);var w=Ui.Dialog(owner,original==null?"安排现有任务":"替换甘特条与调整排期",525);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text(original==null?"安排现有任务":"编辑甘特图排期",21,null,true));var desc=Ui.Text(original==null?"从当前项目选择现有任务，支持多选。":"用选中的任务替换当前甘特条，原任务与历史记录保留。",12,Ui.Muted);desc.Margin=new Thickness(0,7,0,0);p.Children.Add(desc);
   var statuses=new MultiPicker("状态",Model.Statuses.Select(s=>new Choice{Id=s,Label=s}),original==null?Model.Statuses:new[]{original.Status});Ui.Label(p,"按状态筛选 · 可多选",statuses);
   Func<List<Choice>> options=()=>store.Data.Tasks.Where(t=>t.ProjectId==store.Data.CurrentProjectId&&statuses.Selected.Contains(t.Status)).Select(t=>new Choice{Id=t.Id,Label=t.Name+"  ·  "+t.Status}).ToList();
   var taskPicker=new MultiPicker("任务",options(),original==null?new string[0]:new[]{original.Id});Ui.Label(p,"任务名称 · 现有任务下拉多选",taskPicker);statuses.Changed=()=>taskPicker.SetOptions(options(),true);
   var person=new EntityPicker(store,w,false,original==null?"":original.PersonId,null);Ui.Label(p,"负责人 · 对全部选中任务生效",person);
   var start=DateBox(original==null?Model.Date(DateTime.Today):original.Start);var end=DateBox(original==null?Model.Date(DateTime.Today.AddDays(4)):original.End);Ui.Label(p,"计划时间 · 对全部选中任务生效",DatePair(start,end));
   var hint=Ui.Card(Ui.Text("任务状态保持不变。未再次选中的原任务退出本次排期；其原有日期和时间线保留。",12,Ui.Muted),12);hint.Background=Ui.Bg;hint.BorderThickness=new Thickness(0);hint.Margin=new Thickness(0,18,0,0);p.Children.Add(hint);ScheduleResult result=null;
   Footer(p,w,()=>{var a=DateValue(start);var b=DateValue(end);if(!a.HasValue||!b.HasValue)throw new InvalidOperationException("请选择完整的起止日期。");if(String.IsNullOrEmpty(person.SelectedId))throw new InvalidOperationException("请选择负责人。");var ids=new HashSet<string>(taskPicker.Selected);store.Commit(d=>Model.ReplaceSchedule(d,originalId,ids,a.Value,b.Value,person.SelectedId));result=new ScheduleResult{Start=a.Value,Statuses=new HashSet<string>(store.Data.Tasks.Where(t=>ids.Contains(t.Id)).Select(t=>t.Status))};w.DialogResult=true;},original==null?"加入甘特图":"保存并同步甘特图");w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();return result;
  }
 }
}
