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
 internal sealed class LegendPlacement {public TaskLegendItem Item;public Rect Bounds;public double TextWidth;}
 internal sealed class BarLabelPlacement {public Rect Bar,Label,Duration;public double TitleWidth;}
 public partial class GanttView:FrameworkElement {
  public AppData Data;public List<TaskItem> Tasks;public DateTime Start;public int Days;public bool GroupPeople=true;public bool ShowDetails;public Action<string> TaskClicked;
  public double TimelineWidth {get;private set;}
  List<BarHit> hits=new List<BarHit>();
  public GanttView(AppData data,List<TaskItem> tasks,DateTime start,int days,bool group,bool showDetails=false){Data=data;Tasks=tasks;Start=start.Date;Days=days;GroupPeople=group;ShowDetails=showDetails;MinWidth=166+days*33;TimelineWidth=MinWidth;ResizeToContent();VerticalAlignment=VerticalAlignment.Top;SnapsToDevicePixels=true;InitializeDragging();}
  public List<TaskItem> Ordered(){return Order(Data,Tasks,GroupPeople);}
  public void SetTimelineWidth(double width){TimelineWidth=Math.Max(167,width);ResizeToContent();}
  public void ResizeToContent(){var ordered=Ordered();Width=RequiredWidth(ordered,Start,Days,TimelineWidth,ShowDetails);Height=MeasureHeight(Data,ordered,Start,Days,Width,false,ShowDetails);InvalidateVisual();}
  public static List<TaskItem> Order(AppData d,IEnumerable<TaskItem> tasks,bool people){return people?tasks.OrderBy(t=>Model.PersonName(d,t.PersonId)).ThenBy(t=>Model.IsDate(t.Start)&&Model.IsDate(t.End)?(Model.Parse(t.End)-Model.Parse(t.Start)).Days:Int32.MaxValue).ThenBy(t=>Array.IndexOf(Model.Statuses,t.Status)).ThenBy(t=>Array.IndexOf(Model.Priorities,t.Priority)).ThenBy(t=>Model.TaskPrefix(t.Name)).ThenBy(t=>t.Start).ThenBy(t=>t.Name).ToList():Model.OrderTasks(tasks).ToList();}
  protected override void OnRender(DrawingContext c){base.OnRender(c);hits=Draw(c,Data,RenderTasks(),Start,Days,ActualWidth,false,"",1,1,GroupPeople,ShowDetails,TimelineWidth);DrawDragFeedback(c);}
  static Brush TaskMarker{get{return Ui.B("#E4512B");}}
  internal static Brush WeekendFill{get{return Ui.B("#F5F6F8");}}
  // 甘特条使用独立配色：比周末底色更饱满，轮廓和条内时长保持清晰。
  internal static Brush BarFill(string status){return Ui.B(status=="执行中"?"#C7D2FE":status=="测试中"?"#BAE6EE":status=="已完成"?"#C1E4D5":status=="待执行"?"#F5DEB2":status=="已暂停"?"#E0D4F0":status=="已关闭"?"#DEE1E6":"#DFE5ED");}
  internal static Brush BarEdge(string status){return Ui.B(status=="执行中"?"#7384D9":status=="测试中"?"#4B96A8":status=="已完成"?"#559A7C":status=="待执行"?"#B78A47":status=="已暂停"?"#9E7DB7":status=="已关闭"?"#939CA8":"#8C9AAD");}
  internal static Brush BarInk(string status){return Ui.B(status=="执行中"?"#3343A5":status=="测试中"?"#145C70":status=="已完成"?"#22634E":status=="待执行"?"#815918":status=="已暂停"?"#61457F":"#46536A");}
  static void PriorityTag(DrawingContext c,string priority,double x,double y){c.DrawRoundedRectangle(Ui.PriorityFill(priority),null,new Rect(x,y,29,20),4,4);Text(c,priority,x+5,y+2,10,Ui.PriorityInk(priority),23,true);}
  static FormattedText Paragraph(string text,double size,Brush brush,double maxWidth,bool bold=false){var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);return f;}
  // 编号按从左到右的顺序填入等宽列，跨行的编号和名称保持纵向对齐。
  internal static List<LegendPlacement> LayoutLegend(List<TaskLegendItem> items,double width){
   width=Math.Max(1,width);var result=new List<LegendPlacement>();int columns=Math.Max(1,Math.Min(4,(int)((width+24)/234)));double itemWidth=(width-(columns-1)*24)/columns,y=38;
   for(int row=0;row<items.Count;row+=columns){double rowHeight=20;for(int col=0;col<columns&&row+col<items.Count;col++){var item=items[row+col];double textWidth=Math.Max(1,itemWidth-72),height=Math.Max(20,Paragraph(item.Task.Name,12,Ui.Ink,textWidth,true).Height);result.Add(new LegendPlacement{Item=item,Bounds=new Rect(col*(itemWidth+24),y,itemWidth,height),TextWidth=textWidth});rowHeight=Math.Max(rowHeight,height);}y+=rowHeight+12;}return result;
  }
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
  static List<TaskLegendItem> VisibleTasks(List<TaskItem> tasks,DateTime start,int days){var end=start.Date.AddDays(days-1);var visible=tasks.Where(t=>Model.Parse(t.End)>=start.Date&&Model.Parse(t.Start)<=end).ToList();return visible.Select((t,i)=>new TaskLegendItem{Number=i+1,Task=t}).ToList();}
  static double LegendHeight(List<TaskLegendItem> items,double width){var layout=LayoutLegend(items,width);return layout.Count==0?0:layout.Max(item=>item.Bounds.Bottom)+12;}
  static void Text(DrawingContext c,string text,double x,double y,double size,Brush brush,double maxWidth,bool bold=false){
   if(maxWidth<=0)return;var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);f.MaxLineCount=1;f.Trimming=TextTrimming.CharacterEllipsis;c.DrawText(f,new Point(x,y));
  }
  public static double MeasureHeight(AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,bool includeDetails){const double label=166,row=62,head=64;double top=document?76:0;double height=top+head+Math.Max(tasks.Count,document?1:4)*row+38;var items=VisibleTasks(tasks,start,days);if(includeDetails&&items.Count>0)height+=LegendHeight(items,Math.Max(1,width-label-22));return height;}
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
  public static List<BarHit> Draw(DrawingContext c,AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,string project,int page,int total,bool group=true,bool includeDetails=false,double timelineWidth=0){
   const double label=166,row=62,head=64;double top=document?76:0;double baseHeight=top+head+Math.Max(tasks.Count,document?1:4)*row+38;var legend=VisibleTasks(tasks,start,days);double legendHeight=includeDetails?LegendHeight(legend,Math.Max(1,width-label-22)):0;double height=baseHeight+legendHeight;if(timelineWidth<=label)timelineWidth=document?width-240:width;double cw=(timelineWidth-label)/days;var hits=new List<BarHit>();var pen=new Pen(Ui.Line,1);
   c.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));
   if(document){Text(c,"老秦进度管理软件  /  "+project,22,14,20,Ui.Ink,width-44,true);Text(c,start.ToString("yyyy.MM.dd")+" — "+start.AddDays(days-1).ToString("yyyy.MM.dd")+"    ·    人员与任务排期",22,46,12,Ui.Muted,width-200);Text(c,page+" / "+total,width-90,23,12,Ui.Muted,70);}
   for(int i=0;i<days;i++){
    DateTime d=start.AddDays(i);double x=label+i*cw;if(d.DayOfWeek==DayOfWeek.Saturday||d.DayOfWeek==DayOfWeek.Sunday)c.DrawRectangle(WeekendFill,null,new Rect(x,top+head,cw,baseHeight-top-head-38));c.DrawLine(pen,new Point(x,top+head),new Point(x,baseHeight-38));
   }
   c.DrawLine(pen,new Point(timelineWidth,top+head),new Point(timelineWidth,baseHeight-38));DrawCalendarHeader(c,start,days,width,timelineWidth,top);
   int currentDay=(DateTime.Today-start).Days;if(currentDay>=0&&currentDay<days){var np=new Pen(Ui.Brand,.8);np.DashStyle=DashStyles.Dash;c.DrawLine(np,new Point(label+(currentDay+.5)*cw,top+head),new Point(label+(currentDay+.5)*cw,baseHeight-38));}
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
   double fy=baseHeight-25,lx=16;foreach(var status in Model.Statuses.Where(s=>tasks.Any(t=>t.Status==s))){c.DrawRoundedRectangle(BarFill(status),new Pen(BarEdge(status),1),new Rect(lx,fy+5,7,7),2,2);Text(c,status,lx+13,fy,11,Ui.Muted,57);lx+=74;}c.DrawRectangle(WeekendFill,pen,new Rect(lx,fy+5,7,7));Text(c,"周六 / 周日",lx+13,fy,11,Ui.Muted,100);if(document)Text(c,"生成于 "+DateTime.Now.ToString("yyyy.MM.dd HH:mm")+"  ·  所有日期均为自然日",width-360,fy,10,Ui.Muted,344);else if(width-220>lx+125)Text(c,"点击甘特条编辑任务与时间",width-220,fy,11,Ui.Muted,200);
   if(includeDetails&&legend.Count>0){Text(c,"任务名称说明",label,baseHeight+10,14,Ui.Ink,width-label-30,true);foreach(var slot in LayoutLegend(legend,Math.Max(1,width-label-22))){double x=label+slot.Bounds.X,y=baseHeight+slot.Bounds.Y;LegendDot(c,slot.Item.Number,x+11,y+10,9);PriorityTag(c,slot.Item.Task.Priority,x+31,y);var name=Paragraph(slot.Item.Task.Name,12,Ui.Ink,slot.TextWidth,true);c.DrawText(name,new Point(x+67,y));}}
   return hits;
  }
 }
}
