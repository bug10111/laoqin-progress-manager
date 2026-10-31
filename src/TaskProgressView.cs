// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 internal sealed class TaskProgressView:FrameworkElement {
  readonly TaskItem task;
  public TaskProgressView(TaskItem item){task=item;Height=76;MinWidth=320;}
  protected override void OnRender(DrawingContext drawing){
   int current=Model.ProgressIndex(task);bool special=Array.IndexOf(Model.ProgressStatuses,task.Status)<0;double step=ActualWidth/Model.ProgressStatuses.Length;const double y=22,radius=9;
   var completed=Ui.B("#8CB7A6");var pending=Ui.B("#CBD5E1");var active=special?Ui.B("#A8742B"):Ui.Brand;
   for(int i=0;i<Model.ProgressStatuses.Length-1;i++)drawing.DrawLine(new Pen(i<current?completed:pending,2),new Point(step*(i+.5)+radius,y),new Point(step*(i+1.5)-radius,y));
   for(int i=0;i<Model.ProgressStatuses.Length;i++){
    bool selected=i==current;var color=selected?active:i<current?completed:pending;drawing.DrawEllipse(i<=current?color:Brushes.White,new Pen(color,1.5),new Point(step*(i+.5),y),radius,radius);
    var label=new FormattedText(Model.ProgressStatuses[i],CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,selected?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),11,selected?active:Ui.Muted);drawing.DrawText(label,new Point(step*(i+.5)-label.Width/2,42));
   }
  }
 }
}
