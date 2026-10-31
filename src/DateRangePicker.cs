// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 // 日期修改先保存在草稿中，确认后才写回表单；取消和点击外部不会改变原计划。
 public sealed class DateRangePicker:StackPanel {
  sealed class DayCell {public DateTime Date;public Button Button;}
  readonly Button startButton,endButton;readonly TextBlock startValue,endValue,summary;
  readonly Popup popup;readonly Button clearButton;
  FrameworkElement popupView;TextBox startInput,endInput;TextBlock instruction,error,draftSummary;Button applyButton,previous,next;Grid months;
  readonly List<DayCell> days=new List<DayCell>();
  DateTime? draftStart,draftEnd,hoverDate;DateTime month;bool selectingStart=true,syncing;
  public DateTime? Start {get;private set;}
  public DateTime? End {get;private set;}
  public event Action Changed;

  public DateRangePicker(string start,string end){
   Start=Model.IsDate(start)?(DateTime?)Model.Parse(start):null;End=Model.IsDate(end)?(DateTime?)Model.Parse(end):null;
   var fields=new Grid();fields.ColumnDefinitions.Add(new ColumnDefinition());fields.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});fields.ColumnDefinitions.Add(new ColumnDefinition());
   startValue=Ui.Text("",14,null,true);endValue=Ui.Text("",14,null,true);
   startButton=DateButton("开始日期",startValue,()=>Open(true));endButton=DateButton("结束日期",endValue,()=>Open(false));
   fields.Children.Add(startButton);var arrow=Ui.Text("→",15,Ui.Muted);arrow.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(arrow,1);fields.Children.Add(arrow);Grid.SetColumn(endButton,2);fields.Children.Add(endButton);
   Children.Add(new Border{Child=fields,Background=Brushes.White,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});
   summary=Ui.Text("",11,Ui.Muted);summary.Margin=new Thickness(1,7,0,0);var summaryRow=new DockPanel();clearButton=Ui.Button("清空时间",()=>{popup.IsOpen=false;SetRange(null,null);});clearButton.FontSize=11;clearButton.Foreground=Ui.Brand;clearButton.Padding=new Thickness(2,0,2,0);clearButton.Margin=new Thickness(8,7,0,0);clearButton.BorderThickness=new Thickness(0);clearButton.ToolTip="清空起止日期，保存后取消当前排期";DockPanel.SetDock(clearButton,Dock.Right);summaryRow.Children.Add(clearButton);summaryRow.Children.Add(summary);Children.Add(summaryRow);
   popup=new Popup{PlacementTarget=this,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,VerticalOffset=6,PopupAnimation=PopupAnimation.Fade};
   popup.Closed+=(s,e)=>{startButton.Background=Brushes.White;endButton.Background=Brushes.White;};
   Unloaded+=(s,e)=>popup.IsOpen=false;UpdateField();
  }
  static Button DateButton(string caption,TextBlock value,Action click){
   var content=Ui.Stack();content.Children.Add(Ui.Text(caption,10,Ui.Muted));value.Margin=new Thickness(0,4,0,0);content.Children.Add(value);
   var button=Ui.Button(caption,click);button.Content=content;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(12,9,10,9);button.BorderThickness=new Thickness(0);return button;
  }
  public void SetRange(DateTime? start,DateTime? end){
   if(start.HasValue!=end.HasValue||start.HasValue&&start.Value.Date>end.Value.Date)throw new InvalidOperationException("请填写正确、完整的起止日期。");
   Start=start.HasValue?(DateTime?)start.Value.Date:null;End=end.HasValue?(DateTime?)end.Value.Date:null;UpdateField();if(Changed!=null)Changed();
  }
  static string DateText(DateTime? date){return date.HasValue?date.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"";}
  static string RangeSummary(DateTime? start,DateTime? end){return start.HasValue&&end.HasValue?"共 "+((end.Value-start.Value).Days+1)+" 天 · 包含结束当天":"尚未安排 · 可重新选择计划时间";}
  void UpdateField(){
   startValue.Text=Start.HasValue?DateText(Start):"选择开始日期";endValue.Text=End.HasValue?DateText(End):"选择结束日期";
   startValue.Foreground=Start.HasValue?Ui.Ink:Ui.Muted;endValue.Foreground=End.HasValue?Ui.Ink:Ui.Muted;summary.Text=RangeSummary(Start,End);
   clearButton.Visibility=Start.HasValue||End.HasValue?Visibility.Visible:Visibility.Collapsed;
   AutomationProperties.SetName(startButton,"开始日期："+(Start.HasValue?DateText(Start):"未选择"));AutomationProperties.SetName(endButton,"结束日期："+(End.HasValue?DateText(End):"未选择"));
  }
  internal void Open(bool editStart){
   BeginEdit(editStart);popup.IsOpen=true;startButton.Background=editStart?Ui.BrandSoft:Brushes.White;endButton.Background=editStart?Brushes.White:Ui.BrandSoft;
   Dispatcher.BeginInvoke(new Action(()=>{if(popup.IsOpen){var input=selectingStart?startInput:endInput;input.Focus();input.SelectAll();}}),System.Windows.Threading.DispatcherPriority.Input);
  }
  internal FrameworkElement Preview(){BeginEdit(true);popupView.Measure(new Size(562,Double.PositiveInfinity));popupView.Arrange(new Rect(new Point(),popupView.DesiredSize));return popupView;}
  internal FrameworkElement PopupContent {get{return popupView;}}
  void BeginEdit(bool editStart){
   draftStart=Start;draftEnd=End;hoverDate=null;selectingStart=editStart||!draftStart.HasValue;
   var anchor=editStart?(Start??DateTime.Today):(End??Start??DateTime.Today);month=ClampMonth(anchor);BuildPopup();SyncInputs();RenderMonths();
  }
  static DateTime ClampMonth(DateTime date){return new DateTime(date.Year==9999&&date.Month==12?9999:date.Year,date.Year==9999&&date.Month==12?11:date.Month,1);}
  void BuildPopup(){
   var content=Ui.Stack();content.Width=528;
   var heading=new DockPanel();previous=NavButton("‹",()=>ChangeMonth(-1));next=NavButton("›",()=>ChangeMonth(1));var navigation=Ui.Stack(true);navigation.Children.Add(previous);navigation.Children.Add(next);DockPanel.SetDock(navigation,Dock.Right);heading.Children.Add(navigation);
   instruction=Ui.Text("",16,null,true);heading.Children.Add(instruction);content.Children.Add(heading);
   var hint=Ui.Text("连续选择起止日期，浅色区域为选中的时间范围",11,Ui.Muted);hint.Margin=new Thickness(0,6,0,12);content.Children.Add(hint);
   var inputs=new Grid();inputs.ColumnDefinitions.Add(new ColumnDefinition());inputs.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});inputs.ColumnDefinitions.Add(new ColumnDefinition());
   startInput=Input("开始日期",true);endInput=Input("结束日期",false);var a=Ui.Stack();a.Children.Add(Ui.Text("开始日期",11,Ui.Muted));a.Children.Add(startInput);inputs.Children.Add(a);var b=Ui.Stack();b.Children.Add(Ui.Text("结束日期",11,Ui.Muted));b.Children.Add(endInput);Grid.SetColumn(b,2);inputs.Children.Add(b);content.Children.Add(inputs);
   months=new Grid{Margin=new Thickness(0,14,0,12)};months.ColumnDefinitions.Add(new ColumnDefinition());months.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});months.ColumnDefinitions.Add(new ColumnDefinition());content.Children.Add(months);
   months.MouseLeave+=(s,e)=>{hoverDate=null;PaintDays();};
   var shortcuts=Ui.Stack(true);foreach(string caption in new[]{"今天","本周","下周","未来 7 天"}){string choice=caption;var button=Ui.Button(caption,()=>Shortcut(choice));button.Padding=new Thickness(11,7,11,7);button.Margin=new Thickness(0,0,7,0);shortcuts.Children.Add(button);}content.Children.Add(shortcuts);
   error=Ui.Text("",11,Ui.B("#BE3445"));error.Margin=new Thickness(0,8,0,0);error.Visibility=Visibility.Collapsed;content.Children.Add(error);
   var footer=new DockPanel{Margin=new Thickness(0,14,0,0)};var actions=Ui.Stack(true);DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
   var clear=Ui.Button("清空时间",()=>{draftStart=null;draftEnd=null;selectingStart=true;hoverDate=null;SyncInputs();PaintDays();});clear.Padding=new Thickness(10,8,10,8);clear.Margin=new Thickness(0,0,7,0);actions.Children.Add(clear);
   var cancel=Ui.Button("取消",()=>popup.IsOpen=false);cancel.Padding=new Thickness(10,8,10,8);cancel.Margin=new Thickness(0,0,7,0);actions.Children.Add(cancel);
   applyButton=Ui.Button("应用日期",()=>ApplyDraft(),true);applyButton.Padding=new Thickness(12,8,12,8);actions.Children.Add(applyButton);draftSummary=Ui.Text("",11,Ui.Muted);draftSummary.MaxWidth=180;footer.Children.Add(draftSummary);content.Children.Add(footer);
   var frame=new Border{Child=new ScrollViewer{Content=content,MaxHeight=Math.Max(300,Math.Min(610,SystemParameters.WorkArea.Height-70)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},Background=Brushes.White,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16)};
   frame.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){popup.IsOpen=false;startButton.Focus();e.Handled=true;}else if(e.Key==Key.Enter&&applyButton.IsEnabled){ApplyDraft();e.Handled=true;}};
   popupView=frame;popup.Child=frame;
  }
  static Button NavButton(string text,Action action){var button=Ui.Button(text,action);button.Width=32;button.Height=30;button.Padding=new Thickness(0);button.FontSize=21;button.Margin=new Thickness(5,0,0,0);AutomationProperties.SetName(button,text=="‹"?"上一个月":"下一个月");return button;}
  TextBox Input(string caption,bool isStart){
   var input=new TextBox{MinHeight=36,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,5,0,0),MaxLength=10,ToolTip="输入日期，例如 2026-11-02"};AutomationProperties.SetName(input,caption);
   input.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border></ControlTemplate>");
   input.GotKeyboardFocus+=(s,e)=>{selectingStart=isStart;hoverDate=null;UpdateDraftSummary();PaintDays();};
   input.LostKeyboardFocus+=(s,e)=>{if(!syncing)CommitInput(isStart);};
   input.TextChanged+=(s,e)=>{if(!syncing){DateTime? value;bool valid=TryDate(input.Text,out value);input.BorderBrush=valid?Ui.Brand:Ui.B("#BE3445");applyButton.IsEnabled=InputsValid();}};
   return input;
  }
  static bool TryDate(string text,out DateTime? date){
   date=null;if(String.IsNullOrWhiteSpace(text))return true;DateTime parsed;
   if(!DateTime.TryParseExact(text.Trim(),new[]{"yyyy-MM-dd","yyyy-M-d","yyyy/MM/dd","yyyy/M/d","yyyyMMdd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))return false;date=parsed.Date;return true;
  }
  bool InputsValid(){DateTime? a,b;if(!TryDate(startInput.Text,out a)||!TryDate(endInput.Text,out b))return false;return a.HasValue==b.HasValue;}
  bool CommitInput(bool isStart){
   DateTime? date;var input=isStart?startInput:endInput;if(!TryDate(input.Text,out date)){error.Text="日期格式不正确，请输入 yyyy-MM-dd，或直接点击日历。";error.Visibility=Visibility.Visible;applyButton.IsEnabled=false;return false;}
   int duration=draftStart.HasValue&&draftEnd.HasValue?Math.Max(0,(draftEnd.Value-draftStart.Value).Days):0;
   if(isStart){draftStart=date;if(date.HasValue&&draftEnd.HasValue&&date.Value>draftEnd.Value)draftEnd=date.Value.AddDays(Math.Min(duration,(DateTime.MaxValue.Date-date.Value).Days));}
   else{draftEnd=date;if(date.HasValue&&draftStart.HasValue&&date.Value<draftStart.Value)draftStart=date.Value.AddDays(-Math.Min(duration,(date.Value-DateTime.MinValue.Date).Days));}
   syncing=true;try{input.Text=DateText(date);if(isStart)endInput.Text=DateText(draftEnd);else startInput.Text=DateText(draftStart);}finally{syncing=false;}
   error.Visibility=Visibility.Collapsed;UpdateDraftSummary();PaintDays();return true;
  }
  bool CommitInputs(){
   // 清空必须同时作用于两端，避免逐端提交时旧草稿覆盖空输入。
   if(String.IsNullOrWhiteSpace(startInput.Text)||String.IsNullOrWhiteSpace(endInput.Text)){
    if(String.IsNullOrWhiteSpace(startInput.Text)&&String.IsNullOrWhiteSpace(endInput.Text)){draftStart=null;draftEnd=null;SyncInputs();return true;}
    error.Text="请填写完整的起止日期，或点击“清空时间”。";error.Visibility=Visibility.Visible;applyButton.IsEnabled=false;return false;
   }
   // 先提交当前正在编辑的一端，使自动联动不会覆盖尚未提交的输入。
   bool first=startInput.IsKeyboardFocusWithin||!endInput.IsKeyboardFocusWithin&&selectingStart;
   return CommitInput(first)&&CommitInput(!first);
  }
  void SyncInputs(){syncing=true;try{startInput.Text=DateText(draftStart);endInput.Text=DateText(draftEnd);}finally{syncing=false;}error.Visibility=Visibility.Collapsed;UpdateDraftSummary();}
  void UpdateDraftSummary(){
   instruction.Text=selectingStart?"选择开始日期":"选择结束日期";draftSummary.Text=RangeSummary(draftStart,draftEnd);applyButton.IsEnabled=InputsValid();startInput.BorderBrush=selectingStart?Ui.Brand:Ui.Line;endInput.BorderBrush=selectingStart?Ui.Line:Ui.Brand;
  }
  internal void SelectDate(DateTime date){
   if(!CommitInputs())SyncInputs();date=date.Date;
   if(selectingStart||!draftStart.HasValue){int span=draftStart.HasValue&&draftEnd.HasValue?Math.Max(0,(draftEnd.Value-draftStart.Value).Days):0;draftStart=date;if(!draftEnd.HasValue||draftEnd.Value<date)draftEnd=date.AddDays(Math.Min(span,(DateTime.MaxValue.Date-date).Days));selectingStart=false;}
   else{var start=draftStart.Value;draftStart=date<start?date:start;draftEnd=date<start?start:date;selectingStart=true;}
   hoverDate=null;SyncInputs();PaintDays();
  }
  void Shortcut(string choice){
   var today=DateTime.Today;var monday=Model.Monday(today);draftStart=choice=="下周"?monday.AddDays(7):choice=="本周"?monday:today;
   draftEnd=choice=="今天"?today:choice=="本周"?monday.AddDays(6):choice=="下周"?monday.AddDays(13):today.AddDays(6);selectingStart=true;hoverDate=null;month=ClampMonth(draftStart.Value);SyncInputs();RenderMonths();
  }
  void ChangeMonth(int step){if(!CommitInputs())return;month=month.AddMonths(step);RenderMonths();}
  void RenderMonths(){
   months.Children.Clear();days.Clear();var left=MonthView(month);months.Children.Add(left);var right=MonthView(month.AddMonths(1));Grid.SetColumn(right,2);months.Children.Add(right);previous.IsEnabled=month>new DateTime(1,1,1);next.IsEnabled=month<new DateTime(9999,11,1);PaintDays();
  }
  Grid MonthView(DateTime first){
   var grid=new Grid();for(int c=0;c<7;c++)grid.ColumnDefinitions.Add(new ColumnDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(30)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(26)});for(int row=0;row<6;row++)grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});
   var title=Ui.Text(first.ToString("yyyy 年 M 月"),13,null,true);title.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumnSpan(title,7);grid.Children.Add(title);
   for(int c=0;c<7;c++){var week=Ui.Text(new[]{"一","二","三","四","五","六","日"}[c],11,Ui.Muted);week.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(week,1);Grid.SetColumn(week,c);grid.Children.Add(week);}
   int offset=((int)first.DayOfWeek+6)%7;
   for(int day=1;day<=DateTime.DaysInMonth(first.Year,first.Month);day++){
    var date=first.AddDays(day-1);var button=Ui.Button(day.ToString(),()=>SelectDate(date));button.Height=30;button.Padding=new Thickness(0);button.Margin=new Thickness(1);button.FontSize=12;button.Tag=date;button.ToolTip=date.ToString("yyyy-MM-dd dddd",CultureInfo.GetCultureInfo("zh-CN"));AutomationProperties.SetName(button,"选择日期 "+DateText(date));
    button.MouseEnter+=(s,e)=>{if(!selectingStart&&draftStart.HasValue){hoverDate=date;PaintDays();}};
    int position=offset+day-1;Grid.SetColumn(button,position%7);Grid.SetRow(button,2+position/7);grid.Children.Add(button);days.Add(new DayCell{Date=date,Button=button});
   }
   return grid;
  }
  void PaintDays(){
   DateTime? a=draftStart,b=draftEnd;if(!selectingStart&&a.HasValue&&hoverDate.HasValue){b=hoverDate;if(b.Value<a.Value){var swap=a;a=b;b=swap;}}
   foreach(var item in days){bool edge=a.HasValue&&item.Date==a.Value||b.HasValue&&item.Date==b.Value;bool inside=a.HasValue&&b.HasValue&&item.Date>a.Value&&item.Date<b.Value;item.Button.Background=edge?Ui.Brand:inside?Ui.BrandSoft:Brushes.White;item.Button.Foreground=edge?Brushes.White:Ui.Ink;item.Button.BorderBrush=Ui.Brand;item.Button.BorderThickness=item.Date==DateTime.Today&&!edge?new Thickness(1):new Thickness(0);}
  }
  internal bool ApplyDraft(){
   if(!CommitInputs()||!InputsValid())return false;
   SetRange(draftStart,draftEnd);popup.IsOpen=false;return true;
  }
 }
}
