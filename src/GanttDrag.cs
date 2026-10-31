// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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
 public partial class GanttView {
  public Action<List<GanttDateChange>> ScheduleDragged;
  public event Action<bool> DragStateChanged;
  public event Action SelectionChanged;
  readonly HashSet<string> selectedTasks=new HashSet<string>();
  BarHit pressedHit,hoverHit;Point pressedPoint,pointer;GanttDragMode dragMode;TaskItem originalTask;List<TaskItem> dragOriginals;List<GanttDateChange> dragDrafts;double dragDayWidth;bool dragging,moved,shiftGesture,selectedBeforePress;
  public IEnumerable<string> SelectedTaskIds{get{return selectedTasks.ToArray();}}
  public void SetSelectedTasks(IEnumerable<string> ids){var visible=new HashSet<string>(VisibleTasks(Tasks,Start,Days).Select(t=>t.Task.Id));selectedTasks.Clear();foreach(string id in ids.Where(visible.Contains))selectedTasks.Add(id);NotifySelection();}
  void NotifySelection(){if(SelectionChanged!=null)SelectionChanged();InvalidateVisual();}
  public void ClearSelection(){selectedTasks.Clear();NotifySelection();}
  public bool IsDragging{get{return dragging;}}
  public bool IsPointerActive{get{return pressedHit!=null;}}
  internal double DragFeedbackMinX,DragFeedbackMinY;
  internal IList<BarHit> HitRegions{get{return hits.AsReadOnly();}}
  void InitializeDragging(){
   Focusable=true;MouseLeftButtonDown+=(s,e)=>{if(BeginPointer(e.GetPosition(this),Keyboard.Modifiers)){Focus();if(!CaptureMouse())CancelDrag();e.Handled=true;}};
   MouseLeftButtonUp+=(s,e)=>{if(!IsPointerActive)return;e.Handled=true;Ui.Try(()=>EndPointer(e.GetPosition(this)));};
   MouseMove+=(s,e)=>{var p=e.GetPosition(this);if(IsPointerActive)MovePointer(p);else UpdateHover(p);};
   MouseLeave+=(s,e)=>{if(IsPointerActive)return;hoverHit=null;ToolTip=null;Cursor=Cursors.Arrow;InvalidateVisual();};
   PreviewKeyDown+=(s,e)=>{if(e.Key!=Key.Escape)return;if(IsPointerActive){CancelDrag();e.Handled=true;}else if(selectedTasks.Count>0){ClearSelection();e.Handled=true;}};
   LostMouseCapture+=(s,e)=>{if(IsPointerActive)CancelDrag();};Unloaded+=(s,e)=>CancelDrag();
   LostKeyboardFocus+=(s,e)=>{if(IsPointerActive)CancelDrag();};
  }
  internal static GanttDragMode PickDragMode(BarHit hit,double x){double edge=Math.Min(7,hit.Rect.Width*.22);if(hit.StartVisible&&x-hit.Rect.Left<=edge)return GanttDragMode.Start;if(hit.EndVisible&&hit.Rect.Right-x<=edge)return GanttDragMode.End;return GanttDragMode.Move;}
  void UpdateHover(Point point){var hit=hits.FirstOrDefault(h=>h.Rect.Contains(point));if(hoverHit!=hit){hoverHit=hit;InvalidateVisual();}Cursor=hit==null?Cursors.Arrow:hit.IsLabel||ScheduleDragged==null?Cursors.Hand:PickDragMode(hit,point.X)==GanttDragMode.Move?Cursors.Hand:Cursors.SizeWE;ToolTip=hit==null?null:hit.Task.Name+"\n"+Model.Range(hit.Task)+"\nShift + 单击增减选择；拖中间移动选中任务；拖两端仅调整当前任务";}
  internal bool BeginPointer(Point point,ModifierKeys modifiers=ModifierKeys.None){
   CancelDrag();pressedHit=hits.FirstOrDefault(h=>h.Rect.Contains(point));if(pressedHit==null){ClearSelection();return false;}
   shiftGesture=(modifiers&ModifierKeys.Shift)!=0;selectedBeforePress=selectedTasks.Contains(pressedHit.Task.Id);if(!selectedBeforePress){if(!shiftGesture)selectedTasks.Clear();selectedTasks.Add(pressedHit.Task.Id);NotifySelection();}
   pressedPoint=pointer=point;moved=false;originalTask=Data.Tasks.First(t=>t.Id==pressedHit.Task.Id);dragMode=PickDragMode(pressedHit,point.X);dragDayWidth=(TimelineWidth-166)/Days;dragOriginals=dragMode==GanttDragMode.Move?Tasks.Where(t=>selectedTasks.Contains(t.Id)).ToList():new List<TaskItem>{originalTask};return true;
  }
  internal void MovePointer(Point point){
   if(pressedHit==null)return;pointer=point;if(Math.Abs(point.X-pressedPoint.X)>=SystemParameters.MinimumHorizontalDragDistance||Math.Abs(point.Y-pressedPoint.Y)>=SystemParameters.MinimumVerticalDragDistance)moved=true;
   if(!moved||pressedHit.IsLabel||ScheduleDragged==null)return;
   if(!dragging){dragging=true;ToolTip=null;if(DragStateChanged!=null)DragStateChanged(true);}
   int days=(int)Math.Max(Int32.MinValue,Math.Min(Int32.MaxValue,Math.Round((point.X-pressedPoint.X)/dragDayWidth,MidpointRounding.AwayFromZero)));dragDrafts=dragMode==GanttDragMode.Move?GanttDateChange.CreateGroup(dragOriginals,days):new List<GanttDateChange>{GanttDateChange.Create(originalTask,dragMode,days)};
   Cursor=dragMode==GanttDragMode.Move?Cursors.SizeAll:Cursors.SizeWE;Width=Math.Max(Width,RequiredWidth(RenderTasks(),Start,Days,TimelineWidth,ShowDetails));InvalidateVisual();
  }
  internal void EndPointer(Point point){
   if(pressedHit==null)return;MovePointer(point);var changes=dragDrafts;string clicked=!moved&&pressedHit.Rect.Contains(point)?pressedHit.Task.Id:null;bool selectionClick=shiftGesture&&clicked!=null,remove=selectedBeforePress;CancelDrag();
   if(changes!=null&&changes.Any(change=>change.HasChanges)){if(ScheduleDragged!=null)ScheduleDragged(changes);}else if(selectionClick){if(remove)selectedTasks.Remove(clicked);NotifySelection();}else if(clicked!=null&&TaskClicked!=null)TaskClicked(clicked);
  }
  public void CancelDrag(){bool wasDragging=dragging;pressedHit=null;hoverHit=null;originalTask=null;dragOriginals=null;dragDrafts=null;dragging=false;moved=false;shiftGesture=false;ToolTip=null;Cursor=Cursors.Arrow;if(IsMouseCaptured)ReleaseMouseCapture();if(wasDragging&&DragStateChanged!=null)DragStateChanged(false);ResizeToContent();}
  List<TaskItem> RenderTasks(){var ordered=Ordered();if(dragDrafts==null)return ordered;var drafts=dragDrafts.ToDictionary(d=>d.TaskId);return ordered.Select(t=>!drafts.ContainsKey(t.Id)?t:new TaskItem{Id=t.Id,ProjectId=t.ProjectId,Name=t.Name,PersonId=t.PersonId,Status=t.Status,Priority=t.Priority,Note=t.Note,History=t.History,Start=Model.Date(drafts[t.Id].Start),End=Model.Date(drafts[t.Id].End),InGantt=t.InGantt}).ToList();}
  void DrawDragFeedback(DrawingContext context){
   foreach(var selected in hits.Where(h=>!h.IsLabel&&selectedTasks.Contains(h.Task.Id))){var outline=selected.Rect;outline.Inflate(2,2);context.DrawRoundedRectangle(null,new Pen(Ui.Brand,2),outline,6,6);}
   var primary=dragDrafts==null?null:dragDrafts.First(d=>d.TaskId==originalTask.Id);var hit=dragging&&primary!=null?hits.FirstOrDefault(h=>!h.IsLabel&&h.Task.Id==primary.TaskId):hoverHit;
   if(hit!=null&&!hit.IsLabel&&ScheduleDragged!=null){context.DrawRoundedRectangle(null,new Pen(BarEdge(hit.Task.Status),2),hit.Rect,5,5);if(hit.Rect.Width>=16){if(hit.StartVisible)context.DrawRoundedRectangle(Brushes.White,null,new Rect(hit.Rect.Left+3,hit.Rect.Top+9,2,14),1,1);if(hit.EndVisible)context.DrawRoundedRectangle(Brushes.White,null,new Rect(hit.Rect.Right-5,hit.Rect.Top+9,2,14),1,1);}}
   if(!dragging||primary==null)return;string caption=(dragDrafts.Count>1?"批量移动 "+dragDrafts.Count+" 项":dragMode==GanttDragMode.Move?"整体移动":dragMode==GanttDragMode.Start?"调整开始日期":"调整结束日期")+"  "+Model.Date(primary.Start)+" → "+Model.Date(primary.End)+"  · "+((primary.End-primary.Start).Days+1)+"天";
   double width=Paragraph(caption,11,Ui.Ink,10000).Width+22,x=Math.Max(DragFeedbackMinX+4,Math.Min(pointer.X+12,ActualWidth-width-4)),y=pointer.Y-43;if(y<DragFeedbackMinY)y=pointer.Y+22;y=Math.Max(DragFeedbackMinY+4,y);context.DrawRoundedRectangle(Brushes.White,new Pen(Ui.Brand,1),new Rect(x,y,width,30),6,6);Text(context,caption,x+11,y+7,11,Ui.Ink,width-22);
  }
 }
}
