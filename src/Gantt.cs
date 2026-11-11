// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 public class BarHit {public Rect Rect;public TaskItem Task;public bool IsLabel,StartVisible,EndVisible;}
 public class TaskLegendItem {public int Number;public TaskItem Task;}
 internal sealed class LegendPlacement {
  public TaskLegendItem Item;public Rect Bounds;public FormattedText Name;
  public readonly List<FormattedText> Lines=new List<FormattedText>();
 }
 internal sealed class BarLabelPlacement {public Rect Bar,Label,Duration;public double TitleWidth;}
 public partial class GanttView:FrameworkElement {
  internal const double TaskRowHeight=62;
  const double StatusLegendInset=16,StatusLegendItemWidth=88,StatusLegendRowHeight=24;
  public AppData Data;public List<TaskItem> Tasks;public DateTime Start;public int Days;public bool GroupPeople=true;public bool ShowDetails {get{return TimelineTypes.Count>0;}}
  internal ISet<string> TimelineTypes {get;private set;}public Action<string> TaskClicked;
  public double TimelineWidth {get;private set;}
  List<BarHit> hits=new List<BarHit>();
  public GanttView(AppData data,List<TaskItem> tasks,DateTime start,int days,bool group,bool showDetails=false,IEnumerable<string> timelineTypes=null){Data=data;Tasks=tasks;Start=start.Date;Days=days;GroupPeople=group;TimelineTypes=showDetails?TaskTimelineTypes.Select(timelineTypes):new HashSet<string>();MinWidth=166+days*33;TimelineWidth=MinWidth;ResizeToContent();VerticalAlignment=VerticalAlignment.Top;SnapsToDevicePixels=true;InitializeDragging();}
  internal void SetTimelineTypes(IEnumerable<string> types){
   TimelineTypes=TaskTimelineTypes.Select(types);
   ResizeToContent();
  }
  public List<TaskItem> Ordered(){return Order(Data,Tasks,GroupPeople);}
  public void SetTimelineWidth(double width){TimelineWidth=Math.Max(167,width);ResizeToContent();}
  public void ResizeToContent(){var ordered=Ordered();Width=RequiredWidth(ordered,Start,Days,TimelineWidth,ShowDetails);Height=MeasureHeight(Data,ordered,Start,Days,Width,false,ShowDetails,TimelineTypes);InvalidateVisual();}
  public static List<TaskItem> Order(AppData d,IEnumerable<TaskItem> tasks,bool people){return people?tasks.OrderBy(t=>Model.PersonName(d,t.PersonId)).ThenBy(t=>Model.IsDate(t.Start)&&Model.IsDate(t.End)?(Model.Parse(t.End)-Model.Parse(t.Start)).Days:Int32.MaxValue).ThenBy(t=>Array.IndexOf(Model.Statuses,t.Status)).ThenBy(t=>Array.IndexOf(Model.Priorities,t.Priority)).ThenBy(t=>Model.TaskPrefix(t.Name)).ThenBy(t=>t.Start).ThenBy(t=>t.Name).ToList():Model.OrderTasks(tasks).ToList();}
  protected override void OnRender(DrawingContext c){base.OnRender(c);hits=Draw(c,Data,RenderTasks(),Start,Days,ActualWidth,false,"",1,1,GroupPeople,ShowDetails,TimelineWidth,TimelineTypes);DrawDragFeedback(c);}
  static Brush TaskMarker{get{return Ui.B("#E4512B");}}
  internal static Brush WeekendFill{get{return Ui.B("#F5F6F8");}}
  // 甘特条使用独立配色：比周末底色更饱满，轮廓和条内时长保持清晰。
  internal static Brush BarFill(string status){return Ui.B(status=="执行中"?"#C7D2FE":status=="测试中"?"#BAE6EE":status=="已完成"?"#C1E4D5":status=="待执行"?"#F5DEB2":status=="已暂停"?"#E0D4F0":status=="已关闭"?"#DEE1E6":"#DFE5ED");}
  internal static Brush BarEdge(string status){return Ui.B(status=="执行中"?"#7384D9":status=="测试中"?"#4B96A8":status=="已完成"?"#559A7C":status=="待执行"?"#B78A47":status=="已暂停"?"#9E7DB7":status=="已关闭"?"#939CA8":"#8C9AAD");}
  internal static Brush BarInk(string status){return Ui.B(status=="执行中"?"#3343A5":status=="测试中"?"#145C70":status=="已完成"?"#22634E":status=="待执行"?"#815918":status=="已暂停"?"#61457F":"#46536A");}
  static void PriorityTag(DrawingContext c,string priority,double x,double y){c.DrawRoundedRectangle(Ui.PriorityFill(priority),null,new Rect(x,y,29,20),4,4);Text(c,priority,x+5,y+2,10,Ui.PriorityInk(priority),23,true);}
  static FormattedText Paragraph(string text,double size,Brush brush,double maxWidth,bool bold=false){var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);f.Trimming=TextTrimming.None;return f;}
  const double DetailsInset=16,DetailsTop=40,DetailsCardGap=12,DetailsPadding=14,DetailsLineGap=5;
  static double DetailsWidth(double width){return Math.Max(1,Math.Min(880,width-2*DetailsInset));}
  // 预览与导出共用同一份纵向排版和换行测量。
  internal static List<LegendPlacement> LayoutLegend(List<TaskLegendItem> items,double width,ISet<string> timelineTypes=null){
   var result=new List<LegendPlacement>();double y=DetailsTop,bodyWidth=width-2*DetailsPadding-18;
   foreach(var item in items){
    var slot=new LegendPlacement{Item=item,Name=Paragraph(item.Task.Name,14,Ui.Ink,width-2*DetailsPadding-63,true)};
    var history=Model.TimelineEntries(item.Task);
    foreach(var entry in history){
     if(timelineTypes!=null&&!TaskTimelineTypes.Matches(entry,timelineTypes))continue;
     if(slot.Lines.Count>0)slot.Lines.Add(null);
     slot.Lines.Add(Paragraph(Model.TimelineHeading(entry),12,Ui.Brand,bodyWidth,true));
     // 仅因含备注而命中的记录保留操作来源，省略未勾选类型的变更明细。
     if(timelineTypes==null||timelineTypes.Contains(TaskTimelineTypes.Of(entry))){
      string details=Model.TimelineDetails(entry);
      if(details.Length>0)slot.Lines.Add(Paragraph(details,12,Ui.Muted,bodyWidth));
     }
     slot.Lines.Add(Paragraph("备注：\n"+Model.DisplayNote(entry),12,Ui.Ink,bodyWidth));
    }
    if(slot.Lines.Count==0){
     string empty=history.Count==0?"暂无时间线记录":"没有符合所选类型的记录";
     slot.Lines.Add(Paragraph(empty,12,Ui.Muted,bodyWidth));
    }
    double height=2*DetailsPadding+Math.Max(20,slot.Name.Height)+10+slot.Lines.Sum(line=>DetailLineHeight(line));
    slot.Bounds=new Rect(0,y,width,height);
    result.Add(slot);y+=height+DetailsCardGap;
   }
   return result;
  }
  static double DetailLineHeight(FormattedText line){return line==null?8:line.Height+DetailsLineGap;}
  // 标识始终跟在时间条后；预览通过扩展画布显示完整名称，文档在预留区域内显示。
  internal static BarLabelPlacement LayoutBarLabel(Rect bar,double chartLeft,double chartRight,string name,string duration,bool numbered,bool constrain=false){
   // 留出字宽取整余量，避免名称末尾的数字或英文被误判为溢出。
   double prefix=numbered?29:0,natural=Paragraph(name,12,Ui.Ink,100000,true).Width+prefix+40,durationWidth=Paragraph(duration,11,Ui.Ink,100000).Width+2;
   bool durationInside=bar.Width>=durationWidth+6;double durationX=durationInside?bar.Right-durationWidth-3:bar.Right+6,x=bar.Right+8+(durationInside?0:durationWidth+8),width=constrain?Math.Min(natural,Math.Max(prefix+40,chartRight-x)):natural;
   return new BarLabelPlacement{Bar=bar,Label=new Rect(x,bar.Y+6,width,20),TitleWidth=Math.Max(1,width-prefix-36),Duration=new Rect(durationX,bar.Y+(bar.Height-16)/2,durationWidth,16)};
  }
  public static double RequiredWidth(List<TaskItem> tasks,DateTime start,int days,double timelineWidth,bool numbered){
   double width=timelineWidth,cw=(timelineWidth-166)/days;foreach(var t in tasks){int a=(Model.Parse(t.Start)-start).Days,b=(Model.Parse(t.End)-start).Days+1,l=Math.Max(0,a),r=Math.Min(days,b);if(l>=r)continue;string name=(a<0?"‹ ":"")+t.Name+(b>days?" ›":""),duration=((Model.Parse(t.End)-Model.Parse(t.Start)).Days+1)+"天";var bar=new Rect(166+l*cw+4,0,Math.Max(2,(r-l)*cw-8),32);var placement=LayoutBarLabel(bar,170,timelineWidth,name,duration,numbered);width=Math.Max(width,placement.Label.Right+12);}return width;
  }
  static void LegendDot(DrawingContext c,int number,double x,double y,double radius){c.DrawEllipse(TaskMarker,null,new Point(x,y),radius,radius);Text(c,number.ToString(),x-(number>9?6:3),y-6,9,Brushes.White,radius*2,true);}
  static List<TaskItem> TasksWithBars(List<TaskItem> tasks,DateTime start,int days){
   var end=start.Date.AddDays(days-1);
   return tasks.Where(t=>{
    var first=Model.Parse(t.Start);
    var last=Model.Parse(t.End);
    return first<=last&&first<=end&&last>=start.Date;
   }).ToList();
  }
  static List<string> StatusLegendLabels(List<TaskItem> visibleTasks){
   // 只说明当前日期范围内实际绘制的任务条，重复状态只列一次。
   var statuses=visibleTasks.Select(task=>task.Status);
   var ordered=statuses.Distinct().OrderBy(status=>{
    int index=Array.IndexOf(Model.Statuses,status);
    return index<0?Model.Statuses.Length:index;
   });
   return ordered.ToList();
  }
  static int StatusLegendColumns(double width){
   return Math.Max(1,(int)((width-2*StatusLegendInset)/StatusLegendItemWidth));
  }
  static double StatusLegendHeight(int statusCount,double width){
   int columns=StatusLegendColumns(width);
   // 末项为周末底色，窄画布按整项换行。
   return 16+(statusCount/columns+1)*StatusLegendRowHeight;
  }
  static void DrawStatusLegend(DrawingContext context,List<string> statuses,double width,double top,bool document){
   int columns=StatusLegendColumns(width);
   for(int i=0;i<=statuses.Count;i++){
    double x=StatusLegendInset+i%columns*StatusLegendItemWidth;
    double y=top+8+i/columns*StatusLegendRowHeight;
    bool weekend=i==statuses.Count;
    string label=weekend?"周六 / 周日":statuses[i];
    Brush fill=weekend?WeekendFill:BarFill(label);
    Brush edge=weekend?Ui.Line:BarEdge(label);
    context.DrawRoundedRectangle(fill,new Pen(edge,1),new Rect(x,y+3,12,12),2,2);
    Text(context,label,x+16,y,12,Ui.Muted,StatusLegendItemWidth-20);
   }
   double right=StatusLegendInset+(statuses.Count+1)*StatusLegendItemWidth;
   if(document&&width-right>=360)
    Text(context,"生成于 "+DateTime.Now.ToString("yyyy.MM.dd HH:mm")+"  ·  所有日期均为自然日",width-360,top+8,10,Ui.Muted,344);
   else if(!document&&width-right>=220)
    Text(context,"点击甘特条编辑任务与时间",width-220,top+8,11,Ui.Muted,200);
  }
  static List<TaskLegendItem> NumberedTasks(List<TaskItem> visibleTasks){
   return visibleTasks.Select((task,i)=>new TaskLegendItem{Number=i+1,Task=task}).ToList();
  }
  static List<TaskLegendItem> VisibleTasks(List<TaskItem> tasks,DateTime start,int days){
   return NumberedTasks(TasksWithBars(tasks,start,days));
  }
  static double LegendHeight(List<LegendPlacement> layout){return layout.Count==0?0:layout[layout.Count-1].Bounds.Bottom+DetailsCardGap;}
  static void DrawTaskDetails(DrawingContext c,List<LegendPlacement> layout,double top,double width,Pen pen){
   if(layout.Count==0)return;
   Text(c,"任务变更记录",DetailsInset,top+10,15,Ui.Ink,width,true);
   foreach(var slot in layout){
    double x=DetailsInset+DetailsPadding,y=top+slot.Bounds.Top+DetailsPadding;
    c.DrawRoundedRectangle(Ui.Bg,pen,new Rect(DetailsInset,top+slot.Bounds.Top,slot.Bounds.Width,slot.Bounds.Height),6,6);
    LegendDot(c,slot.Item.Number,x+9,y+10,9);PriorityTag(c,slot.Item.Task.Priority,x+25,y);
    c.DrawText(slot.Name,new Point(x+63,y));y+=Math.Max(20,slot.Name.Height)+10;
    foreach(var line in slot.Lines){
     if(line!=null)c.DrawText(line,new Point(x+18,y));
     y+=DetailLineHeight(line);
    }
   }
  }
  static void Text(DrawingContext c,string text,double x,double y,double size,Brush brush,double maxWidth,bool bold=false){
   if(maxWidth<=0)return;var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);f.MaxLineCount=1;f.Trimming=TextTrimming.CharacterEllipsis;c.DrawText(f,new Point(x,y));
  }
  static int LayoutRowCount(int tasks,bool document){return tasks==0?(document?1:4):tasks;}
  /// <summary>计算含状态图例和可选任务说明的甘特图总高度。</summary>
  /// <param name="data">图表所属的项目数据。</param>
  /// <param name="tasks">图表的任务行。</param>
  /// <param name="start">时间轴首日。</param>
  /// <param name="days">时间轴天数。</param>
  /// <param name="width">图表画布宽度。</param>
  /// <param name="document">是否为带文档标题的布局。</param>
  /// <param name="includeDetails">是否显示任务变更记录。</param>
  /// <param name="timelineTypes">勾选的时间线类型，未指定时显示全部。</param>
  /// <returns>图表总高度。</returns>
  public static double MeasureHeight(AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,bool includeDetails,ISet<string> timelineTypes=null){
   const double head=64;
   double top=document?76:0;
   var visibleTasks=TasksWithBars(tasks,start,days);
   var statuses=StatusLegendLabels(visibleTasks);
   double height=top+head+LayoutRowCount(tasks.Count,document)*TaskRowHeight+StatusLegendHeight(statuses.Count,width);
   if(includeDetails)height+=LegendHeight(LayoutLegend(NumberedTasks(visibleTasks),DetailsWidth(width),timelineTypes));
   return height;
  }
  // 预览冻结栏和完整图表共用同一份日期绘制，横向滚动与缩放后仍逐列对齐。
  internal static void DrawCalendarHeader(DrawingContext c,DateTime start,int days,double width,double timelineWidth,double top){
   const double label=166,head=64;double cw=(timelineWidth-label)/days;var pen=new Pen(Ui.Line,1);c.DrawRectangle(Brushes.White,null,new Rect(0,top,width,head));
   int todayIndex=(DateTime.Today-start).Days,dateStep=Math.Max(1,(int)Math.Ceiling(28/cw));
   for(int i=0;i<days;i++){DateTime d=start.AddDays(i);double x=label+i*cw;if(d.DayOfWeek==DayOfWeek.Saturday||d.DayOfWeek==DayOfWeek.Sunday)c.DrawRectangle(Ui.B("#EFF1F4"),null,new Rect(x,top,cw,head));c.DrawLine(pen,new Point(x,top),new Point(x,top+head));
    string week=new[]{"周日","周一","周二","周三","周四","周五","周六"}[(int)d.DayOfWeek];if(d.Day==1)Text(c,d.Month+"月",x+3,top+1,10,Ui.Muted,Math.Max(24,cw-5));if(cw>=28)Text(c,week,x+(cw-25)/2,top+14,11,d.Date==DateTime.Today?Ui.Brand:Ui.Muted,cw-3);
    if(d.Date==DateTime.Today)c.DrawRoundedRectangle(Ui.Brand,null,new Rect(x+(cw-26)/2,top+34,26,24),5,5);
    if(i==todayIndex||i%dateStep==0&&(todayIndex<0||todayIndex>=days||Math.Abs(i-todayIndex)>=dateStep))Text(c,d.ToString("dd"),x+(cw-17)/2,top+37,13,d.Date==DateTime.Today?Brushes.White:Ui.Ink,Math.Max(24,cw-4));
   }c.DrawLine(pen,new Point(timelineWidth,top),new Point(timelineWidth,top+head));Text(c,"人员 / 任务",16,top+34,12,Ui.Muted,label-25);c.DrawLine(pen,new Point(0,top+head),new Point(width,top+head));
  }
  internal static void DrawPeopleHeading(DrawingContext c){c.DrawRectangle(Brushes.White,null,new Rect(0,0,166,64));Text(c,"人员 / 任务",16,34,12,Ui.Muted,141);var pen=new Pen(Ui.Line,1);c.DrawLine(pen,new Point(0,64),new Point(166,64));c.DrawLine(pen,new Point(166,0),new Point(166,64));}
  internal static void DrawPerson(DrawingContext c,AppData data,List<TaskItem> tasks,TaskItem task,double y,bool group){string person=Model.PersonName(data,task.PersonId);c.DrawEllipse(Ui.BrandSoft,null,new Point(29,y+28),14,14);Text(c,person.Length>0?person.Substring(0,1):"人",23,y+19,12,Ui.Brand,19);Text(c,group?person:task.Name,52,y+12,13,Ui.Ink,107,true);Text(c,group?tasks.Count(t=>t.PersonId==task.PersonId)+" 项任务":person,52,y+34,11,Ui.Muted,107);}
  public static List<BarHit> Draw(DrawingContext c,AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,string project,int page,int total,bool group=true,bool includeDetails=false,double timelineWidth=0,ISet<string> timelineTypes=null){
   const double label=166,row=TaskRowHeight,head=64;
   double top=document?76:0,rowsBottom=top+head+LayoutRowCount(tasks.Count,document)*row;
   var visibleTasks=TasksWithBars(tasks,start,days);
   var statuses=StatusLegendLabels(visibleTasks);
   double baseHeight=rowsBottom+StatusLegendHeight(statuses.Count,width);
   double detailsWidth=DetailsWidth(width);
   var legend=includeDetails?LayoutLegend(NumberedTasks(visibleTasks),detailsWidth,timelineTypes):null;
   double height=baseHeight+(includeDetails?LegendHeight(legend):0);
   if(timelineWidth<=label)timelineWidth=document?width-240:width;
   double cw=(timelineWidth-label)/days;var hits=new List<BarHit>();var pen=new Pen(Ui.Line,1);
   c.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));
   if(document){Text(c,"老秦进度管理软件  /  "+project,22,14,20,Ui.Ink,width-44,true);Text(c,start.ToString("yyyy.MM.dd")+" — "+start.AddDays(days-1).ToString("yyyy.MM.dd")+"    ·    人员与任务排期",22,46,12,Ui.Muted,width-200);Text(c,page+" / "+total,width-90,23,12,Ui.Muted,70);}
   for(int i=0;i<days;i++){
    DateTime d=start.AddDays(i);double x=label+i*cw;if(d.DayOfWeek==DayOfWeek.Saturday||d.DayOfWeek==DayOfWeek.Sunday)c.DrawRectangle(WeekendFill,null,new Rect(x,top+head,cw,rowsBottom-top-head));c.DrawLine(pen,new Point(x,top+head),new Point(x,rowsBottom));
   }
   c.DrawLine(pen,new Point(timelineWidth,top+head),new Point(timelineWidth,rowsBottom));DrawCalendarHeader(c,start,days,width,timelineWidth,top);
   int currentDay=(DateTime.Today-start).Days;if(currentDay>=0&&currentDay<days){var np=new Pen(Ui.Brand,.8);np.DashStyle=DashStyles.Dash;c.DrawLine(np,new Point(label+(currentDay+.5)*cw,top+head),new Point(label+(currentDay+.5)*cw,rowsBottom));}
   string previous=null;var legendIndex=0;
   for(int r=0;r<tasks.Count;r++){
    var t=tasks[r];double y=top+head+r*row;bool first=!group||t.PersonId!=previous;
    if(first){if(r>0)c.DrawLine(pen,new Point(0,y),new Point(width,y));DrawPerson(c,data,tasks,t,y,group);}
    previous=t.PersonId;int a=(Model.Parse(t.Start)-start).Days,b=(Model.Parse(t.End)-start).Days+1;int l=Math.Max(0,a),rr=Math.Min(days,b);if(l>=rr)continue;
    var rect=new Rect(label+l*cw+4,y+15,Math.Max(2,(rr-l)*cw-8),32);Brush fill=BarFill(t.Status),edge=BarEdge(t.Status),color=BarInk(t.Status);
    string name=(a<0?"‹ ":"")+t.Name+(b>days?" ›":""),duration=((Model.Parse(t.End)-Model.Parse(t.Start)).Days+1)+"天";var placement=LayoutBarLabel(rect,label+4,width-4,name,duration,includeDetails,document);
    c.DrawRoundedRectangle(fill,new Pen(edge,1),placement.Bar,5,5);if(placement.Bar.Width>=12)c.DrawRoundedRectangle(edge,null,new Rect(placement.Bar.X+3,placement.Bar.Y+6,3,placement.Bar.Height-12),1.5,1.5);double priorityX=placement.Label.X+(includeDetails?29:0);if(includeDetails){legendIndex++;LegendDot(c,legendIndex,placement.Label.X+11,placement.Label.Y+10,9);}PriorityTag(c,t.Priority,priorityX,placement.Label.Y);Text(c,name,priorityX+36,placement.Label.Y+1,12,Ui.Ink,placement.TitleWidth,true);Text(c,duration,placement.Duration.X,placement.Duration.Y,11,color,Math.Max(0,Math.Min(placement.Duration.Width,width-4-placement.Duration.X)));
    hits.Add(new BarHit{Rect=placement.Bar,Task=t,StartVisible=a>=0,EndVisible=b<=days});hits.Add(new BarHit{Rect=placement.Label,Task=t,IsLabel=true});
   }
   if(tasks.Count==0){Text(c,"还没有符合筛选条件的排期",label+35,top+110,16,Ui.Muted,width-label-70);Text(c,"请检查任务状态和计划日期，或点击“安排现有任务”。",label+35,top+145,12,Ui.Muted,width-label-70);}
   DrawStatusLegend(c,statuses,width,rowsBottom,document);
   if(includeDetails)DrawTaskDetails(c,legend,baseHeight,detailsWidth,pen);
   return hits;
  }
 }
}
