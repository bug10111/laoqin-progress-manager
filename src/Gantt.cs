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
 public class BarHit {public Rect Rect;public TaskItem Task;}
 public class TaskLegendItem {public int Number;public TaskItem Task;}
 public class GanttView:FrameworkElement {
  public AppData Data;public List<TaskItem> Tasks;public DateTime Start;public int Days;public bool GroupPeople=true;public bool ShowDetails;public Action<string> TaskClicked;
  List<BarHit> hits=new List<BarHit>();
  public GanttView(AppData data,List<TaskItem> tasks,DateTime start,int days,bool group,bool showDetails=false){Data=data;Tasks=tasks;Start=start.Date;Days=days;GroupPeople=group;ShowDetails=showDetails;MinWidth=166+days*33;Height=MeasureHeight(data,tasks,start,days,MinWidth,false,showDetails);VerticalAlignment=VerticalAlignment.Top;SnapsToDevicePixels=true;MouseLeftButtonUp+=Clicked;MouseMove+=Moved;MouseLeave+=(s,e)=>Cursor=Cursors.Arrow;SizeChanged+=(s,e)=>{if(e.WidthChanged)ResizeToContent();};}
  public List<TaskItem> Ordered(){return Order(Data,Tasks,GroupPeople);}
  public void ResizeToContent(){Height=MeasureHeight(Data,Ordered(),Start,Days,Math.Max(MinWidth,ActualWidth),false,ShowDetails);InvalidateVisual();}
  public static List<TaskItem> Order(AppData d,IEnumerable<TaskItem> tasks,bool people){return people?tasks.OrderBy(t=>Model.PersonName(d,t.PersonId)).ThenBy(t=>Array.IndexOf(Model.Statuses,t.Status)).ThenBy(t=>Array.IndexOf(Model.Priorities,t.Priority)).ThenBy(t=>Model.TaskPrefix(t.Name)).ThenBy(t=>t.Start).ThenBy(t=>t.Name).ToList():Model.OrderTasks(tasks).ToList();}
  protected override void OnRender(DrawingContext c){base.OnRender(c);hits=Draw(c,Data,Ordered(),Start,Days,ActualWidth,false,"",1,1,GroupPeople,ShowDetails);}
  void Clicked(object s,MouseButtonEventArgs e){var hit=hits.FirstOrDefault(x=>x.Rect.Contains(e.GetPosition(this)));if(hit!=null&&TaskClicked!=null)TaskClicked(hit.Task.Id);}
  void Moved(object s,MouseEventArgs e){Cursor=hits.Any(x=>x.Rect.Contains(e.GetPosition(this)))?Cursors.Hand:Cursors.Arrow;}
  static Brush TaskMarker{get{return Ui.B("#E4512B");}}
  static void PriorityTag(DrawingContext c,string priority,double x,double y){c.DrawRoundedRectangle(Ui.PriorityFill(priority),null,new Rect(x,y,29,20),4,4);Text(c,priority,x+5,y+2,10,Ui.PriorityInk(priority),23,true);}
  static FormattedText Paragraph(string text,double size,Brush brush,double maxWidth,bool bold=false){var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);return f;}
  static double MeasureLegend(List<TaskLegendItem> items,double textWidth){double height=40;foreach(var item in items)height+=Paragraph(item.Task.Name,12,Ui.Ink,Math.Max(1,textWidth-38),true).Height+10;return height+12;}
  static void LegendDot(DrawingContext c,int number,double x,double y,double radius){c.DrawEllipse(TaskMarker,null,new Point(x,y),radius,radius);Text(c,number.ToString(),x-(number>9?6:3),y-6,9,Brushes.White,radius*2,true);}
  static List<TaskLegendItem> VisibleTasks(List<TaskItem> tasks,DateTime start,int days){var end=start.Date.AddDays(days-1);var visible=tasks.Where(t=>Model.Parse(t.End)>=start.Date&&Model.Parse(t.Start)<=end).ToList();return visible.Select((t,i)=>new TaskLegendItem{Number=i+1,Task=t}).ToList();}
  static double LegendHeight(List<TaskLegendItem> items,double width){return items.Count==0?0:MeasureLegend(items,width);}
  static void Text(DrawingContext c,string text,double x,double y,double size,Brush brush,double maxWidth,bool bold=false){
   if(maxWidth<=0)return;var f=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush);f.MaxTextWidth=Math.Max(1,maxWidth);f.MaxLineCount=1;f.Trimming=TextTrimming.CharacterEllipsis;c.DrawText(f,new Point(x,y));
  }
  public static double MeasureHeight(AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,bool includeDetails){const double label=166,row=62,head=64;double top=document?76:0;double height=top+head+Math.Max(tasks.Count,document?1:4)*row+38;var items=VisibleTasks(tasks,start,days);if(includeDetails&&items.Count>0)height+=LegendHeight(items,Math.Max(1,width-label-70));return height;}
  public static List<BarHit> Draw(DrawingContext c,AppData data,List<TaskItem> tasks,DateTime start,int days,double width,bool document,string project,int page,int total,bool group=true,bool includeDetails=false){
   const double label=166,row=62,head=64;double top=document?76:0;double baseHeight=top+head+Math.Max(tasks.Count,document?1:4)*row+38;var legend=VisibleTasks(tasks,start,days);double legendHeight=includeDetails?LegendHeight(legend,Math.Max(1,width-label-70)):0;double height=baseHeight+legendHeight;double cw=(width-label)/days;var hits=new List<BarHit>();var pen=new Pen(Ui.Line,1);
   c.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));
   if(document){Text(c,"老秦进度管理软件  /  "+project,22,14,20,Ui.Ink,width-44,true);Text(c,start.ToString("yyyy.MM.dd")+" — "+start.AddDays(days-1).ToString("yyyy.MM.dd")+"    ·    人员与任务排期",22,46,12,Ui.Muted,width-200);Text(c,page+" / "+total,width-90,23,12,Ui.Muted,70);}
   for(int i=0;i<days;i++){
    DateTime d=start.AddDays(i);double x=label+i*cw;if(d.DayOfWeek==DayOfWeek.Saturday||d.DayOfWeek==DayOfWeek.Sunday)c.DrawRectangle(Ui.B("#F4F5FA"),null,new Rect(x,top,cw,baseHeight-top-38));
    c.DrawLine(pen,new Point(x,top),new Point(x,baseHeight-38));
    string week=new[]{"周日","周一","周二","周三","周四","周五","周六"}[(int)d.DayOfWeek];
    if(d.Day==1)Text(c,d.Month+"月",x+5,top+1,10,Ui.Muted,cw-5);
    Text(c,week,x+(cw-25)/2,top+14,11,d.Date==DateTime.Today?Ui.Brand:Ui.Muted,cw-3);
    if(d.Date==DateTime.Today)c.DrawRoundedRectangle(Ui.Brand,null,new Rect(x+(cw-26)/2,top+34,26,24),5,5);
    Text(c,d.ToString("dd"),x+(cw-17)/2,top+37,13,d.Date==DateTime.Today?Brushes.White:Ui.Ink,cw-4);
   }
   Text(c,"人员 / 任务",16,top+34,12,Ui.Muted,label-25);c.DrawLine(pen,new Point(0,top+head),new Point(width,top+head));
   int currentDay=(DateTime.Today-start).Days;if(currentDay>=0&&currentDay<days){var np=new Pen(Ui.Brand,.8);np.DashStyle=DashStyles.Dash;c.DrawLine(np,new Point(label+(currentDay+.5)*cw,top+head),new Point(label+(currentDay+.5)*cw,baseHeight-38));}
   string previous=null;var legendIndex=0;
   for(int r=0;r<tasks.Count;r++){
    var t=tasks[r];double y=top+head+r*row;bool first=!group||t.PersonId!=previous;
    if(first){if(r>0)c.DrawLine(pen,new Point(0,y),new Point(width,y));string person=Model.PersonName(data,t.PersonId);c.DrawEllipse(Ui.BrandSoft,null,new Point(29,y+28),14,14);Text(c,person.Length>0?person.Substring(0,1):"人",23,y+19,12,Ui.Brand,19);Text(c,group?person:t.Name,52,y+12,13,Ui.Ink,label-59,true);int count=tasks.Count(x=>x.PersonId==t.PersonId);Text(c,group?count+" 项任务":person,52,y+34,11,Ui.Muted,label-59);}
    previous=t.PersonId;int a=(Model.Parse(t.Start)-start).Days,b=(Model.Parse(t.End)-start).Days+1;int l=Math.Max(0,a),rr=Math.Min(days,b);if(l>=rr)continue;
    var rect=new Rect(label+l*cw+4,y+15,Math.Max(2,(rr-l)*cw-8),32);Brush fill=Ui.StatusFill(t.Status);Brush color=Ui.StatusInk(t.Status);
    c.DrawRoundedRectangle(fill,null,rect,5,5);string name=(a<0?"‹ ":"")+t.Name+(b>days?" ›":"");string duration=((Model.Parse(t.End)-Model.Parse(t.Start)).Days+1)+"天";const double durationWidth=40;double durationX=rect.Right+6;bool durationInside=durationX+durationWidth>width-4;double nameX=rect.X+(includeDetails?29:10);double nameWidth=Math.Max(0,rect.Right-nameX-6-(durationInside?durationWidth+4:0));if(includeDetails){legendIndex++;LegendDot(c,legendIndex,rect.X+15,rect.Y+16,9);}bool showPriority=nameWidth>=29;double titleWidth=Math.Max(0,nameWidth-(showPriority?36:0));Text(c,name,nameX,rect.Y+7,12,color,titleWidth,true);if(showPriority){double used=titleWidth>0?Math.Min(titleWidth,Paragraph(name,12,color,Math.Max(1,titleWidth),true).Width):0;PriorityTag(c,t.Priority,nameX+used+(used>0?7:0),rect.Y+6);}if(durationInside)Text(c,duration,Math.Max(rect.X+rect.Width-durationWidth-4,rect.X+2),rect.Y+8,11,color,durationWidth);else Text(c,duration,durationX,rect.Y+8,11,color,durationWidth);
    hits.Add(new BarHit{Rect=rect,Task=t});
   }
   if(tasks.Count==0){Text(c,"还没有符合筛选条件的排期",label+35,top+110,16,Ui.Muted,width-label-70);Text(c,"请检查任务状态和计划日期，或点击“安排现有任务”。",label+35,top+145,12,Ui.Muted,width-label-70);}
   double fy=baseHeight-25,lx=16;foreach(var status in Model.Statuses.Where(s=>tasks.Any(t=>t.Status==s))){c.DrawRoundedRectangle(Ui.StatusInk(status),null,new Rect(lx,fy+5,7,7),2,2);Text(c,status,lx+13,fy,11,Ui.Muted,57);lx+=74;}c.DrawRectangle(Ui.B("#F0F1F6"),pen,new Rect(lx,fy+5,7,7));Text(c,"周六 / 周日",lx+13,fy,11,Ui.Muted,100);if(document)Text(c,"生成于 "+DateTime.Now.ToString("yyyy.MM.dd HH:mm")+"  ·  所有日期均为自然日",width-360,fy,10,Ui.Muted,344);else if(width-220>lx+125)Text(c,"点击甘特条编辑任务与时间",width-220,fy,11,Ui.Muted,200);
   if(includeDetails&&legend.Count>0){double y=baseHeight+10;Text(c,"任务名称说明",label,y,14,Ui.Ink,width-label-30,true);y+=28;foreach(var item in legend){LegendDot(c,item.Number,label+11,y+10,9);var name=Paragraph(item.Task.Name,12,Ui.Ink,Math.Max(1,width-label-108),true);c.DrawText(name,new Point(label+31,y));PriorityTag(c,item.Task.Priority,label+31+name.Width+7,y);y+=name.Height+10;}}
   return hits;
  }
 }
}
