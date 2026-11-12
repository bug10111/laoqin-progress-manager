// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;

namespace LaoQin.ProgressManager {
 public enum GanttDragMode {Move,Start,End}
 public sealed class GanttDateChange {
  public string TaskId;public DateTime OriginalStart,OriginalEnd,Start,End;public GanttDragMode Mode;
  public bool HasChanges{get{return Start!=OriginalStart||End!=OriginalEnd;}}
  public static GanttDateChange Create(TaskItem task,GanttDragMode mode,int days){
   var a=Model.Parse(task.Start);var b=Model.Parse(task.End);var change=new GanttDateChange{TaskId=task.Id,OriginalStart=a,OriginalEnd=b,Start=a,End=b,Mode=mode};
   if(mode==GanttDragMode.Move){days=Math.Max((DateTime.MinValue.Date-a).Days,Math.Min((DateTime.MaxValue.Date-b).Days,days));change.Start=a.AddDays(days);change.End=b.AddDays(days);}
   else if(mode==GanttDragMode.Start)change.Start=a.AddDays(Math.Max((DateTime.MinValue.Date-a).Days,Math.Min((b-a).Days,days)));
   else if(mode==GanttDragMode.End)change.End=b.AddDays(Math.Max((a-b).Days,Math.Min((DateTime.MaxValue.Date-b).Days,days)));
   else throw new InvalidOperationException("不支持的排期拖动方式。");return change;
  }
  public static List<GanttDateChange> CreateGroup(IEnumerable<TaskItem> tasks,int days){var chosen=tasks.ToList();if(chosen.Count==0)throw new InvalidOperationException("请先选择任务。");int min=chosen.Max(t=>(DateTime.MinValue.Date-Model.Parse(t.Start)).Days),max=chosen.Min(t=>(DateTime.MaxValue.Date-Model.Parse(t.End)).Days);days=Math.Max(min,Math.Min(max,days));return chosen.Select(t=>Create(t,GanttDragMode.Move,days)).ToList();}
 }
}
