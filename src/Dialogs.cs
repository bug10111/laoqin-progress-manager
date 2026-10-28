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

namespace Xucheng {
 public static partial class Dialogs {
  public static DateTime? DateValue(DatePicker p){if(String.IsNullOrWhiteSpace(p.Text))return null;DateTime d;if(!DateTime.TryParse(p.Text,CultureInfo.CurrentCulture,DateTimeStyles.None,out d))throw new InvalidOperationException("日期格式不正确，请从日历选择日期。");return d.Date;}
  public static DatePicker DateBox(string date){var p=new DatePicker{SelectedDate=Model.IsDate(date)?(DateTime?)Model.Parse(date):null,MinHeight=36};p.DateValidationError+=(s,e)=>{e.ThrowException=false;};return p;}
  static void Footer(Panel p,Window w,Action save,string label="保存变更") {var row=Ui.Stack(true);row.Margin=new Thickness(0,22,0,0);row.HorizontalAlignment=HorizontalAlignment.Right;var cancel=Ui.Button("取消",()=>w.Close());cancel.Margin=new Thickness(0,0,8,0);row.Children.Add(cancel);row.Children.Add(Ui.Button(label,save,true));p.Children.Add(row);}
  static Grid DatePair(DatePicker a,DatePicker b){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(a);var to=Ui.Text("至",12,Ui.Muted);to.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(to,1);row.Children.Add(to);Grid.SetColumn(b,2);row.Children.Add(b);return row;}
  public static bool TaskEditor(Window owner,Store store,string id){
   if(!store.Data.Projects.Any(x=>x.Id==store.Data.CurrentProjectId)){MessageBox.Show(owner,"请先创建项目，再新建任务。","老秦进度管理软件");return false;}
   var original=store.Data.Tasks.FirstOrDefault(t=>t.Id==id);var w=Ui.Dialog(owner,original==null?"新建任务":"编辑任务",500);var p=Ui.Stack();p.Margin=new Thickness(25);p.Children.Add(Ui.Text(original==null?"新建任务":"编辑任务",21,null,true));p.Children.Add(Ui.Text("执行中的任务须填写负责人和完整时间范围。",12,Ui.Muted));
   var name=new TextBox{Text=original==null?"":original.Name,MaxLength=500};Ui.Label(p,"任务名称",name);
   var status=new ComboBox{ItemsSource=Model.Statuses,SelectedItem=original==null?"待讨论":original.Status};Ui.Label(p,"任务状态",status);
   var person=new EntityPicker(store,w,false,original==null?"":original.PersonId,null);Ui.Label(p,"负责人",person);
   var start=DateBox(original==null?"":original.Start);var end=DateBox(original==null?"":original.End);Ui.Label(p,"计划时间 · 起止日期（包含结束当天）",DatePair(start,end));
   var note=new TextBox{Text=original==null?"":original.Note,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=78,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=50000};Ui.Label(p,"任务说明",note);
   var hint=Ui.Text("状态变化、任务修改和排期变化都会记录操作时间。",11,Ui.Muted);hint.Margin=new Thickness(0,13,0,0);p.Children.Add(hint);
   Footer(p,w,()=>{
    string n=name.Text.Trim(),s=(string)status.SelectedItem,personId=person.SelectedId;var a=DateValue(start);var b=DateValue(end);
    if(n.Length==0)throw new InvalidOperationException("请输入任务名称。");if(a.HasValue!=b.HasValue||(a.HasValue&&a>b))throw new InvalidOperationException("请填写正确、完整的起止日期。");
    if(s=="执行中"&&(String.IsNullOrEmpty(personId)||!a.HasValue))throw new InvalidOperationException("任务设为执行中时，必须选择负责人和起止日期。");
    store.Commit(d=>{var t=d.Tasks.FirstOrDefault(x=>x.Id==id);bool created=t==null;if(created){t=new TaskItem{Id=Model.Id(),ProjectId=d.CurrentProjectId,History=new List<Change>()};d.Tasks.Add(t);}string before=t.Status,oldName=t.Name,oldRange=created?"":Model.Range(t),oldPerson=t.PersonId;bool wasIn=t.InGantt;t.Name=n;t.Status=s;t.PersonId=personId??"";t.Start=a.HasValue?Model.Date(a.Value):"";t.End=b.HasValue?Model.Date(b.Value):"";t.Note=note.Text.Trim();if(s=="执行中"&&(created||before!="执行中"))t.InGantt=true;if(!a.HasValue||String.IsNullOrEmpty(t.PersonId))t.InGantt=false;
     if(created||before!=s)Model.Record(d,t,created?"创建任务":"状态变更",before,created?"":"每次状态变更自动记录。");
     else if(oldName!=t.Name||oldRange!=Model.Range(t)||oldPerson!=t.PersonId||original.Note!=t.Note||wasIn!=t.InGantt)Model.Record(d,t,"任务修改",before,"原任务名称："+oldName+"；原计划："+oldRange+"。");
    });w.DialogResult=true;
   },original==null?"创建任务":"保存变更");
   w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};return w.ShowDialog()==true;
  }
  public static void Detail(Window owner,Store store,string id){
   var t=store.Data.Tasks.FirstOrDefault(x=>x.Id==id);if(t==null)return;var w=Ui.Dialog(owner,"任务详情 · "+t.Name,455);w.SizeToContent=SizeToContent.Manual;w.Height=780;w.ResizeMode=ResizeMode.CanResize;w.MinWidth=400;w.MinHeight=500;
   var main=new DockPanel{Margin=new Thickness(25)};var foot=Ui.Stack(true);foot.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(foot,Dock.Bottom);foot.Margin=new Thickness(0,18,0,0);
   var del=Ui.Button("删除任务",()=>{if(MessageBox.Show(w,"删除任务“"+t.Name+"”及其时间线？","删除确认",MessageBoxButton.OKCancel,MessageBoxImage.Warning)==MessageBoxResult.OK){store.Commit(d=>d.Tasks.RemoveAll(x=>x.Id==id));w.Close();}});del.Foreground=Ui.B("#A8742B");del.Margin=new Thickness(0,0,8,0);foot.Children.Add(del);foot.Children.Add(Ui.Button("编辑任务",()=>{if(TaskEditor(w,store,id))w.Close();},true));main.Children.Add(foot);
   var p=Ui.Stack();p.Children.Add(Ui.Text("任务详情",12,Ui.Muted));var title=Ui.Text(t.Name,23,null,true);title.Margin=new Thickness(0,18,0,12);p.Children.Add(title);p.Children.Add(Ui.Badge(t.Status));var project=store.Data.Projects.First(x=>x.Id==t.ProjectId);Ui.Label(p,"所属项目",Ui.Text(project.Name));Ui.Label(p,"负责人",Ui.Text(Model.PersonName(store.Data,t.PersonId)));Ui.Label(p,"计划时间",Ui.Text(Model.Range(t),12));Ui.Label(p,"甘特图",Ui.Text(t.InGantt?"已纳入当前排期":"未纳入当前排期",12,Ui.Muted));
   if(!String.IsNullOrWhiteSpace(t.Note)){var note=Ui.Card(Ui.Text(t.Note,12,Ui.Muted),12);note.Background=Ui.Bg;note.BorderThickness=new Thickness(0);note.Margin=new Thickness(0,17,0,0);p.Children.Add(note);}p.Children.Add(Ui.Rule());p.Children.Add(Ui.Text("状态与操作时间线 · "+t.History.Count+" 条记录",14,null,true));
   foreach(var h in t.History){var panel=Ui.Stack();panel.Margin=new Thickness(8,16,0,0);DateTimeOffset at;string timestamp=DateTimeOffset.TryParse(h.At,out at)?at.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"):h.At;panel.Children.Add(Ui.Text("●  "+timestamp,11,Ui.Muted));var kind=Ui.Text(h.Kind,13,null,true);kind.Margin=new Thickness(17,8,0,4);panel.Children.Add(kind);string change=String.IsNullOrEmpty(h.Before)?h.After:h.Before==h.After?"状态："+h.After:h.Before+"  →  "+h.After;var status=Ui.Text(change,12,h.After=="执行中"?Ui.Brand:Ui.Muted);status.Margin=new Thickness(17,0,0,3);panel.Children.Add(status);string details="";if(!String.IsNullOrEmpty(h.PersonName))details="负责人："+h.PersonName;if(Model.IsDate(h.Start))details+="\n计划："+h.Start+" — "+h.End;if(!String.IsNullOrEmpty(h.Detail))details+="\n"+h.Detail;var detail=Ui.Text(details.Trim(),11,Ui.Muted);detail.Margin=new Thickness(17,0,0,0);panel.Children.Add(detail);p.Children.Add(new Border{Child=panel,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,0,0,0)});}
   main.Children.Add(new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});w.Content=main;w.ShowDialog();
  }
  public class ScheduleResult {public DateTime Start;public HashSet<string> Statuses;}
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
