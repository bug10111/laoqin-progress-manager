// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 internal sealed class GanttFrozenHeader:FrameworkElement {
  readonly GanttView chart;readonly ScrollViewer scroll;
  public GanttFrozenHeader(GanttView chart,ScrollViewer scroll){this.chart=chart;this.scroll=scroll;Name="GanttFrozenHeader";Height=64;HorizontalAlignment=HorizontalAlignment.Left;VerticalAlignment=VerticalAlignment.Top;ClipToBounds=true;SnapsToDevicePixels=true;MouseLeftButtonDown+=(s,e)=>e.Handled=true;MouseLeftButtonUp+=(s,e)=>e.Handled=true;}
  protected override void OnRender(DrawingContext context){base.OnRender(context);context.DrawRectangle(Brushes.White,null,new Rect(0,0,ActualWidth,ActualHeight));context.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));context.PushTransform(new TranslateTransform(-scroll.HorizontalOffset,0));GanttView.DrawCalendarHeader(context,chart.Start,chart.Days,Math.Max(chart.ActualWidth,scroll.HorizontalOffset+ActualWidth),chart.TimelineWidth,0);context.Pop();GanttView.DrawPeopleHeading(context);context.Pop();}
 }
 internal sealed class GanttFrozenPeople:FrameworkElement {
  readonly GanttView chart;readonly ScrollViewer scroll;
  internal string PinnedPersonId{get;private set;}
  public GanttFrozenPeople(GanttView chart,ScrollViewer scroll){this.chart=chart;this.scroll=scroll;Name="GanttFrozenPeople";Width=166;HorizontalAlignment=HorizontalAlignment.Left;VerticalAlignment=VerticalAlignment.Top;ClipToBounds=true;SnapsToDevicePixels=true;MouseLeftButtonDown+=(s,e)=>e.Handled=true;MouseLeftButtonUp+=(s,e)=>e.Handled=true;}
  protected override void OnRender(DrawingContext context){base.OnRender(context);context.DrawRectangle(Brushes.White,null,new Rect(0,0,ActualWidth,ActualHeight));PinnedPersonId=null;var tasks=chart.Ordered();var pen=new Pen(Ui.Line,1);
   for(int i=0;i<tasks.Count;){int end=i+1;if(chart.GroupPeople)while(end<tasks.Count&&tasks[end].PersonId==tasks[i].PersonId)end++;double top=i*62-scroll.VerticalOffset,bottom=end*62-scroll.VerticalOffset;
    if(bottom>0&&top<ActualHeight){double y=top<0?Math.Min(0,bottom-62):top;if(top<=0)PinnedPersonId=tasks[i].PersonId;if(top>0)context.DrawLine(pen,new Point(0,top),new Point(166,top));GanttView.DrawPerson(context,chart.Data,tasks,tasks[i],y,chart.GroupPeople);}i=end;
   }context.DrawLine(pen,new Point(166,0),new Point(166,ActualHeight));
  }
 }
}
