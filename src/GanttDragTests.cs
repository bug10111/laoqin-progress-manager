// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.IO;
using System.Linq;

namespace LaoQin.ProgressManager {
 internal static class GanttDragTests {
  public static void Run(string folder,Action<bool,string> check,Action<Action,string> fails){
   var task=new TaskItem{Id="拖动验证",Start="2028-02-28",End="2028-03-02"};var move=GanttDateChange.Create(task,GanttDragMode.Move,2);
   check(move.Start==new DateTime(2028,3,1)&&move.End==new DateTime(2028,3,4)&&(move.End-move.Start).Days==3,"整体拖动按自然日跨闰日和月份，保持原计划天数");
   var left=GanttDateChange.Create(task,GanttDragMode.Start,-3);var right=GanttDateChange.Create(task,GanttDragMode.End,4);
   check(left.Start==new DateTime(2028,2,25)&&left.End==Model.Parse(task.End),"左端拖动只修改开始日期");check(right.Start==Model.Parse(task.Start)&&right.End==new DateTime(2028,3,6),"右端拖动只修改结束日期");
   check(GanttDateChange.Create(task,GanttDragMode.Start,50).Start==Model.Parse(task.End)&&GanttDateChange.Create(task,GanttDragMode.End,-50).End==Model.Parse(task.Start),"两端不会交叉，最短保留一个自然日");
   check(!GanttDateChange.Create(task,GanttDragMode.Move,0).HasChanges,"原地松开不会产生日期变更");task.Start="0001-01-01";task.End="0001-01-05";check(GanttDateChange.Create(task,GanttDragMode.Move,Int32.MinValue).Start==DateTime.MinValue.Date,"向前拖动在最小日期边界停止");task.Start="9999-12-28";task.End="9999-12-31";check(GanttDateChange.Create(task,GanttDragMode.Move,Int32.MaxValue).End==DateTime.MaxValue.Date,"向后拖动在最大日期边界停止");
   string directory=Path.Combine(folder,"drag-store"),id;using(var store=new Store(directory)){
    var original=store.Data.Tasks[0];id=original.Id;string oldRange=Model.Range(original),status=original.Status,person=original.PersonId,priority=original.Priority,note=original.Note;bool inGantt=original.InGantt;int history=original.History.Count;
    var others=Store.Decode(Store.Encode(store.Data));others.Tasks.RemoveAll(t=>t.Id==id);byte[] unchanged=Store.Encode(others);var change=GanttDateChange.Create(original,GanttDragMode.Move,-2);store.Commit(d=>Model.ApplyGanttDrag(d,change));var updated=store.Data.Tasks.Single(t=>t.Id==id);
    check(updated.Start==Model.Date(change.Start)&&updated.End==Model.Date(change.End)&&updated.Status==status&&updated.PersonId==person&&updated.Priority==priority&&updated.Note==note&&updated.InGantt==inGantt,"拖动只更新选中任务日期，状态、负责人、优先级和备注保持不变");
    check(updated.History.Count==history+1&&updated.History[0].Kind=="拖动甘特图排期"&&updated.History[0].Detail.Contains(oldRange)&&updated.History[0].Detail.Contains(Model.Range(updated))&&updated.History[0].Start==updated.Start,"自动保存记录原计划、新计划、操作方式和操作时间");
    var afterOthers=Store.Decode(Store.Encode(store.Data));afterOthers.Tasks.RemoveAll(t=>t.Id==id);check(unchanged.SequenceEqual(Store.Encode(afterOthers)),"拖动单个任务不修改其他任务和项目数据");
    byte[] before=Store.Encode(store.Data);fails(()=>store.Commit(d=>Model.ApplyGanttDrag(d,change)),"拒绝将过期拖动覆盖新的计划日期");check(before.SequenceEqual(Store.Encode(store.Data))&&before.SequenceEqual(File.ReadAllBytes(store.FilePath)),"拒绝拖动时内存与磁盘数据均保持不变");
    var invalid=GanttDateChange.Create(updated,GanttDragMode.Move,1);invalid.End=invalid.Start.AddDays(-1);fails(()=>store.Commit(d=>Model.ApplyGanttDrag(d,invalid)),"拒绝结束早于开始的拖动结果");
    invalid=GanttDateChange.Create(updated,GanttDragMode.Move,1);invalid.End=invalid.End.AddDays(1);fails(()=>store.Commit(d=>Model.ApplyGanttDrag(d,invalid)),"整体移动不能意外改变任务天数");
    invalid=GanttDateChange.Create(updated,GanttDragMode.Start,-1);invalid.End=invalid.End.AddDays(1);fails(()=>store.Commit(d=>Model.ApplyGanttDrag(d,invalid)),"调整左端时不能同时修改右端");
    invalid=GanttDateChange.Create(updated,GanttDragMode.End,1);invalid.Start=invalid.Start.AddDays(-1);fails(()=>store.Commit(d=>Model.ApplyGanttDrag(d,invalid)),"调整右端时不能同时修改左端");
    store.Commit(d=>Model.ApplyGanttDrag(d,GanttDateChange.Create(d.Tasks.Single(t=>t.Id==id),GanttDragMode.Start,1)));store.Commit(d=>Model.ApplyGanttDrag(d,GanttDateChange.Create(d.Tasks.Single(t=>t.Id==id),GanttDragMode.End,2)));
    check(store.Data.Tasks.Single(t=>t.Id==id).History.Count==history+3,"连续修改左右两端分别保存一次时间线记录");
   }
   using(var restored=new Store(directory)){check(restored.Data.Tasks.Single(t=>t.Id==id).History.Count(h=>h.Kind=="拖动甘特图排期")==3,"重启后拖动日期及三次操作记录完整保留");}
   var orderData=Model.Sample();orderData.People[0].Name="A";orderData.People[1].Name="B";var longTask=orderData.Tasks[0];var shortTask=orderData.Tasks[1];var mediumTask=orderData.Tasks[2];var otherPerson=orderData.Tasks[3];foreach(var t in new[]{longTask,shortTask,mediumTask,otherPerson}){t.Start="2026-10-01";t.PersonId=orderData.People[0].Id;}longTask.End="2026-10-05";longTask.Priority="P0";shortTask.End="2026-10-01";shortTask.Priority="P3";shortTask.Status="已完成";mediumTask.End="2026-10-03";otherPerson.End="2026-10-01";otherPerson.PersonId=orderData.People[1].Id;
   check(GanttView.Order(orderData,new[]{longTask,otherPerson,mediumTask,shortTask},true).Select(t=>t.Id).SequenceEqual(new[]{shortTask.Id,mediumTask.Id,longTask.Id,otherPerson.Id}),"同人甘特条按完整计划天数从短到长，天数优先于状态和优先级，人员分组保持不变");
   var earliest=new[]{new TaskItem{Id="a",Start="0001-01-02",End="0001-01-03"},new TaskItem{Id="b",Start="0001-01-05",End="0001-01-08"}};var bounded=GanttDateChange.CreateGroup(earliest,-100);
   check(bounded.All(c=>(c.Start-c.OriginalStart).Days==-1)&&(bounded[1].Start-bounded[0].Start).Days==3,"整组选中任务接近最小日期时统一停下，不改变相对间隔");
   earliest[0].Start="9999-12-20";earliest[0].End="9999-12-22";earliest[1].Start="9999-12-27";earliest[1].End="9999-12-30";check(GanttDateChange.CreateGroup(earliest,100).All(c=>(c.End-c.OriginalEnd).Days==1),"整组选中任务接近最大日期时统一停下");
   string batchDirectory=Path.Combine(folder,"drag-batch-store");using(var batch=new Store(batchDirectory)){
    var chosen=batch.Data.Tasks.Take(2).ToList();var changes=GanttDateChange.CreateGroup(chosen,3);var durations=chosen.Select(t=>(Model.Parse(t.End)-Model.Parse(t.Start)).Days).ToList();var personIds=chosen.Select(t=>t.PersonId).ToList();batch.Commit(d=>Model.ApplyGanttDrags(d,changes));
    check(changes.All(c=>batch.Data.Tasks.Single(t=>t.Id==c.TaskId).Start==Model.Date(c.Start))&&batch.Data.Tasks.Take(2).Select(t=>(Model.Parse(t.End)-Model.Parse(t.Start)).Days).SequenceEqual(durations)&&batch.Data.Tasks.Take(2).Select(t=>t.PersonId).SequenceEqual(personIds),"批量拖动统一保存相同位移，保留各任务天数及负责人");check(batch.Data.Tasks.Take(2).All(t=>t.History[0].Detail.Contains("批量移动（2 项）")),"每项选中任务分别记录批量移动及原、新计划");
    byte[] saved=Store.Encode(batch.Data);var stale=GanttDateChange.CreateGroup(batch.Data.Tasks.Take(2),1);stale[1].OriginalStart=stale[1].OriginalStart.AddDays(-1);fails(()=>Model.ApplyGanttDrags(batch.Data,stale),"任一选中任务计划过期时拒绝整组移动");check(saved.SequenceEqual(Store.Encode(batch.Data))&&saved.SequenceEqual(File.ReadAllBytes(batch.FilePath)),"整组校验先于写入，失败时没有任务被部分修改");
    var invalid=GanttDateChange.CreateGroup(batch.Data.Tasks.Take(2),1);invalid[1]=GanttDateChange.Create(batch.Data.Tasks[1],GanttDragMode.Move,2);fails(()=>Model.ApplyGanttDrags(batch.Data,invalid),"拒绝批量任务使用不同位移");fails(()=>Model.ApplyGanttDrags(batch.Data,new[]{invalid[0],invalid[0]}),"拒绝重复任务造成重复保存");check(saved.SequenceEqual(Store.Encode(batch.Data)),"批量校验失败不追加任何历史记录");
   }
   using(var restored=new Store(batchDirectory)){check(restored.Data.Tasks.Take(2).All(t=>t.History.Count(h=>h.Kind=="拖动甘特图排期")==1),"批量移动及每项历史记录在重启后保留");}
  }
 }
}
