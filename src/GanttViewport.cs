// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LaoQin.ProgressManager {
 // 缩放调整日期列宽，导出沿用当前列宽；任务的计划日期不变。
 public sealed class GanttViewport:DockPanel {
  const double LabelWidth=166,DefaultDayWidth=66,MinZoom=.25,MaxZoom=4;
  readonly TextBlock zoomText;readonly Button zoomOut,zoomIn;double initialHorizontal,initialVertical;bool changing,interacted;
  public readonly GanttView Chart;public readonly ScrollViewer Scroll;
  internal readonly GanttFrozenHeader Header;
  internal readonly GanttFrozenPeople People;
  public double Zoom {get;private set;}
  public event Action Changed;
  public GanttViewport(GanttView chart,double zoom=1,double horizontal=0,double vertical=0){
   Chart=chart;Chart.MinWidth=0;Chart.HorizontalAlignment=HorizontalAlignment.Left;Zoom=Clamp(zoom,MinZoom,MaxZoom);initialHorizontal=horizontal;initialVertical=vertical;
   Scroll=new ScrollViewer{Name="GanttScrollViewer",Content=chart,CanContentScroll=false,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
   var toolbar=new DockPanel{Margin=new Thickness(10,4,10,4)};var actions=Ui.Stack(true);SetDock(actions,Dock.Right);toolbar.Children.Add(actions);
   zoomOut=Ui.Button("−",()=>SetZoom(Zoom/1.15,Scroll.ViewportWidth/2));zoomOut.Name="GanttZoomOut";zoomOut.ToolTip="缩小时间轴 · Shift + 向下滚轮";zoomOut.Width=29;zoomOut.Padding=new Thickness(0,3,0,3);actions.Children.Add(zoomOut);
   zoomText=Ui.Text("",11,Ui.Muted);zoomText.Name="GanttZoomValue";zoomText.Width=48;zoomText.TextAlignment=TextAlignment.Center;actions.Children.Add(zoomText);
   zoomIn=Ui.Button("＋",()=>SetZoom(Zoom*1.15,Scroll.ViewportWidth/2));zoomIn.Name="GanttZoomIn";zoomIn.ToolTip="放大时间轴 · Shift + 向上滚轮";zoomIn.Width=29;zoomIn.Padding=new Thickness(0,3,0,3);actions.Children.Add(zoomIn);
   var reset=Ui.Button("重置",()=>SetZoom(1,Scroll.ViewportWidth/2));reset.Name="GanttZoomReset";reset.ToolTip="恢复 100% 时间轴列宽";reset.Margin=new Thickness(8,0,0,0);reset.Padding=new Thickness(8,3,8,3);actions.Children.Add(reset);
   var tip=Ui.Text("Shift + 单击多选 · Shift + 滚轮缩放",11,Ui.Muted);tip.Name="GanttSelectionHint";tip.Margin=new Thickness(0,0,12,0);toolbar.Children.Add(tip);Chart.SelectionChanged+=()=>{int count=0;foreach(var id in Chart.SelectedTaskIds)count++;tip.Text=count>0?"已选 "+count+" 项 · 拖中间一起移动 · Shift + 单击增减 · Esc 清空":"Shift + 单击多选 · Shift + 滚轮缩放";};var frame=new Border{Child=toolbar,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,1,0,0)};SetDock(frame,Dock.Bottom);Children.Add(frame);
   var layers=new Grid{ClipToBounds=true};layers.Children.Add(Scroll);People=new GanttFrozenPeople(Chart,Scroll);Panel.SetZIndex(People,1);layers.Children.Add(People);Header=new GanttFrozenHeader(Chart,Scroll);Panel.SetZIndex(Header,2);layers.Children.Add(Header);Children.Add(layers);
   Header.PreviewMouseWheel+=(s,e)=>{WheelOverHeader(e.Delta,Keyboard.Modifiers,e.GetPosition(Scroll).X);e.Handled=true;};
   People.PreviewMouseWheel+=(s,e)=>{WheelOverHeader(e.Delta,Keyboard.Modifiers,e.GetPosition(Scroll).X);e.Handled=true;};
   Scroll.PreviewMouseWheel+=(s,e)=>{if(HandleWheel(e.Delta,Keyboard.Modifiers,e.GetPosition(Scroll).X))e.Handled=true;};
   var autoPan=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};autoPan.Tick+=(s,e)=>{if(!Chart.IsDragging||Mouse.LeftButton!=MouseButtonState.Pressed)return;var point=Mouse.GetPosition(Scroll);double step=point.X<LabelWidth+28?-18:point.X>Scroll.ViewportWidth-28?18:0;if(step==0)return;Scroll.ScrollToHorizontalOffset(Scroll.HorizontalOffset+step);Scroll.UpdateLayout();Chart.MovePointer(Mouse.GetPosition(Chart));};Chart.DragStateChanged+=active=>{if(active)autoPan.Start();else{autoPan.Stop();UpdateWidth();}};Unloaded+=(s,e)=>{autoPan.Stop();Chart.CancelDrag();};
   Scroll.ScrollChanged+=(s,e)=>{if(e.ViewportWidthChange!=0&&!changing)UpdateWidth();UpdateHeader();Notify();};Scroll.SizeChanged+=(s,e)=>UpdateHeader();Chart.SizeChanged+=(s,e)=>UpdateHeader();
   Loaded+=(s,e)=>{UpdateWidth();if(!interacted){Scroll.UpdateLayout();Scroll.ScrollToHorizontalOffset(initialHorizontal);Scroll.ScrollToVerticalOffset(initialVertical);Scroll.UpdateLayout();}Notify();};
   UpdateIndicator();UpdateWidth();
  }
  static double Clamp(double value,double min,double max){return Math.Max(min,Math.Min(max,value));}
  double ViewWidth {get{return Scroll.ViewportWidth>0?Scroll.ViewportWidth:Math.Max(600,ActualWidth-SystemParameters.VerticalScrollBarWidth);}}
  void UpdateWidth(){if(Chart.IsPointerActive)return;double dayWidth=Math.Max(DefaultDayWidth,(ViewWidth-LabelWidth)/Math.Max(1,Chart.Days)),width=LabelWidth+Math.Max(1,Chart.Days)*dayWidth*Zoom;if(Math.Abs(Chart.TimelineWidth-width)>.1)Chart.SetTimelineWidth(width);UpdateHeader();}
  void UpdateHeader(){if(Header==null)return;var presenter=Scroll.Template==null?null:Scroll.Template.FindName("PART_ScrollContentPresenter",Scroll) as ScrollContentPresenter;var origin=presenter==null?new Point():presenter.TranslatePoint(new Point(),Scroll);Header.Margin=new Thickness(origin.X,origin.Y,0,0);Header.Width=Math.Max(0,Scroll.ViewportWidth);People.Margin=new Thickness(origin.X,origin.Y+64,0,0);People.Width=Math.Min(166,Math.Max(0,Scroll.ViewportWidth));People.Height=Math.Max(0,Scroll.ViewportHeight-64);Chart.DragFeedbackMinX=Scroll.HorizontalOffset+166;Chart.DragFeedbackMinY=Scroll.VerticalOffset+Header.Height;Header.InvalidateVisual();People.InvalidateVisual();}
  void UpdateIndicator(){zoomText.Text=Math.Round(Zoom*100)+"%";zoomOut.IsEnabled=Zoom>MinZoom+.0001;zoomIn.IsEnabled=Zoom<MaxZoom-.0001;}
  void Notify(){if(Changed!=null)Changed();}
  internal void WheelOverHeader(int delta,ModifierKeys modifiers,double mouseX){if(!HandleWheel(delta,modifiers,mouseX)){double lines=SystemParameters.WheelScrollLines,step=lines<0?Scroll.ViewportHeight:lines*16;Scroll.ScrollToVerticalOffset(Scroll.VerticalOffset-delta/120.0*step);}}
  internal bool HandleWheel(int delta,ModifierKeys modifiers,double mouseX){if(Chart.IsPointerActive)return true;if((modifiers&ModifierKeys.Shift)==0||delta==0)return false;SetZoom(Zoom*Math.Pow(1.15,delta/120.0),mouseX);return true;}
  internal void SetZoom(double value,double mouseX){
   if(Chart.IsPointerActive)return;
   interacted=true;double next=Clamp(value,MinZoom,MaxZoom);if(Math.Abs(next-Zoom)<.00001)return;
   double anchor=Clamp(mouseX<LabelWidth?(ViewWidth+LabelWidth)/2:mouseX,0,ViewWidth),oldWidth=Math.Max(LabelWidth+1,Chart.TimelineWidth),oldVertical=Scroll.VerticalOffset,contentX=Scroll.HorizontalOffset+anchor;
   // 以鼠标指向的日期为锚点，缩放后补偿横向偏移，避免时间轴跳到别的日期。
   double position=Clamp((contentX-LabelWidth)/(oldWidth-LabelWidth),0,1),tail=Math.Max(0,contentX-oldWidth);
   changing=true;try{Zoom=next;UpdateWidth();Scroll.UpdateLayout();Scroll.ScrollToHorizontalOffset(LabelWidth+position*(Chart.TimelineWidth-LabelWidth)+tail-anchor);Scroll.ScrollToVerticalOffset(oldVertical);Scroll.UpdateLayout();UpdateIndicator();}finally{changing=false;}Notify();
  }
 }
}
