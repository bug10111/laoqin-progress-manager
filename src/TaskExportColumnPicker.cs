// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 public sealed partial class TaskExportPreferences {
  static readonly string TimelineFieldName=TaskExportField.Timeline.ToString();

  // 合并旧导出字段时沿用它们首次出现的位置和勾选状态。
  static string CurrentFieldName(string name){
   switch(name){
    case "Note":case "Requirements":case "ChangeNotes":return TimelineFieldName;
    default:return name;
   }
  }
  internal List<TaskExportField> OrderedFields(){
   var known=TaskExcelExporter.Columns.ToDictionary(c=>c.Field.ToString(),c=>c.Field);
   var result=new List<TaskExportField>();
   foreach(string name in Order??new List<string>()){
    string current=CurrentFieldName(name);TaskExportField field;
    if(current!=null&&known.TryGetValue(current,out field)&&!result.Contains(field))result.Add(field);
   }
   result.AddRange(TaskExcelExporter.Columns.Select(c=>c.Field).Where(f=>!result.Contains(f)));return result;
  }
  internal bool IsSelected(TaskExportField field){
   string name=field.ToString();
   return Selected==null||Selected.Any(value=>CurrentFieldName(value)==name)||Order!=null&&!Order.Any(value=>CurrentFieldName(value)==name);
  }
 }

 // 使用独立拖动手柄，点击复选框不会意外触发排序。
 internal sealed class TaskExportColumnPicker:StackPanel {
  sealed class Entry {public TaskExportField Field;public bool Selected;}
  sealed class DraggedColumn {public TaskExportColumnPicker Owner;public TaskExportField Field;}
  readonly List<Entry> entries=new List<Entry>();readonly StackPanel rows=Ui.Stack();
  readonly List<CheckBox> boxes=new List<CheckBox>();readonly List<Border> rowBorders=new List<Border>();
  readonly List<CheckBox> typeBoxes=new List<CheckBox>();
  readonly HashSet<string> timelineTypes;
  public event Action Changed;
  Point dragStart;bool dragging;TaskExportField? pressedField;
  public List<TaskExportField> SelectedFields {get{return entries.Where(e=>e.Selected).Select(e=>e.Field).ToList();}}
  public List<TaskExportField> OrderedFields {get{return entries.Select(e=>e.Field).ToList();}}
  internal bool NeedsTimelineType {get{return timelineTypes.Count==0&&entries.Any(e=>e.Field==TaskExportField.Timeline&&e.Selected);}}
  internal TaskExportColumnPicker(TaskExportPreferences preferences){
   var saved=preferences??new TaskExportPreferences();
   timelineTypes=TaskTimelineTypes.Select(saved.TimelineTypes);
   foreach(var field in saved.OrderedFields())entries.Add(new Entry{Field=field,Selected=saved.IsSelected(field)});
   Children.Add(rows);RenderRows();
  }
  public TaskExportPreferences Preferences(){
   return new TaskExportPreferences{
    Order=OrderedFields.Select(f=>f.ToString()).ToList(),
    Selected=SelectedFields.Select(f=>f.ToString()).ToList(),
    TimelineTypes=TaskTimelineTypes.All.Where(timelineTypes.Contains).ToList()
   };
  }
  public void SelectAll(bool selected){
   if(selected)SetTimelineTypes(true);
   foreach(var box in boxes)box.IsChecked=selected;
  }
  public void ResetDefaults(){
   entries.Clear();entries.AddRange(TaskExcelExporter.Columns.Select(c=>new Entry{Field=c.Field,Selected=true}));
   timelineTypes.UnionWith(TaskTimelineTypes.All);RenderRows();Notify();
  }
  internal void MoveColumn(TaskExportField field,TaskExportField target,bool after){
   var moving=entries.FirstOrDefault(e=>e.Field==field);var anchor=entries.FirstOrDefault(e=>e.Field==target);if(moving==null||anchor==null||moving==anchor)return;
   entries.Remove(moving);int index=entries.IndexOf(anchor)+(after?1:0);entries.Insert(index,moving);RenderRows();Notify();
  }
  void Notify(){if(Changed!=null)Changed();}
  void SetTimelineTypes(bool selected){
   timelineTypes.Clear();
   if(selected)timelineTypes.UnionWith(TaskTimelineTypes.All);
   foreach(var box in typeBoxes)box.IsChecked=selected;
   Notify();
  }
  FrameworkElement TimelineTypePanel(bool selected){
   var panel=Ui.Stack();
   panel.Margin=new Thickness(62,0,8,12);
   panel.Visibility=selected?Visibility.Visible:Visibility.Collapsed;
   var actions=Ui.Stack(true);
   actions.Margin=new Thickness(0,0,0,4);
   var select=Ui.Button("全选类型",()=>SetTimelineTypes(true));
   select.Margin=new Thickness(0,0,8,0);
   actions.Children.Add(select);
   actions.Children.Add(Ui.Button("清空类型",()=>SetTimelineTypes(false)));
   panel.Children.Add(actions);

   var grid=new UniformGrid{Columns=3};
   foreach(string type in TaskTimelineTypes.All){
    var box=new CheckBox{Content=type,Tag=type,IsChecked=timelineTypes.Contains(type),MinHeight=26,VerticalContentAlignment=VerticalAlignment.Center};
    box.Checked+=(s,e)=>{timelineTypes.Add(type);Notify();};
    box.Unchecked+=(s,e)=>{timelineTypes.Remove(type);Notify();};
    grid.Children.Add(box);typeBoxes.Add(box);
   }
   panel.Children.Add(grid);
   return panel;
  }
  void ClearInsertion(){foreach(var border in rowBorders){border.BorderBrush=Ui.Line;border.BorderThickness=new Thickness(0,0,0,1);}}
  void RenderRows(){
   rows.Children.Clear();boxes.Clear();typeBoxes.Clear();rowBorders.Clear();
   for(int index=0;index<entries.Count;index++){
    var entry=entries[index];var column=TaskExcelExporter.Columns.First(c=>c.Field==entry.Field);
    var grid=new Grid{MinHeight=36,Background=Brushes.Transparent};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});grid.ColumnDefinitions.Add(new ColumnDefinition());
    var handle=new Border{Background=Brushes.Transparent,Cursor=Cursors.SizeAll,ToolTip="按住拖动，调整表头顺序",Child=Ui.Text("≡",19,Ui.Muted)};grid.Children.Add(handle);
    var number=Ui.Text((index+1).ToString("D2"),11,Ui.Muted);Grid.SetColumn(number,1);grid.Children.Add(number);
    var box=new CheckBox{Content=column.Header,Tag=entry.Field,IsChecked=entry.Selected,VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};Grid.SetColumn(box,2);grid.Children.Add(box);boxes.Add(box);
    var content=Ui.Stack();content.Children.Add(grid);
    FrameworkElement types=null;
    if(entry.Field==TaskExportField.Timeline){types=TimelineTypePanel(entry.Selected);content.Children.Add(types);}
    box.Checked+=(s,e)=>{entry.Selected=true;if(types!=null)types.Visibility=Visibility.Visible;Notify();};
    box.Unchecked+=(s,e)=>{entry.Selected=false;if(types!=null)types.Visibility=Visibility.Collapsed;Notify();};
    var border=new Border{Child=content,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1),AllowDrop=true,Tag=entry.Field};rowBorders.Add(border);rows.Children.Add(border);
    handle.MouseLeftButtonDown+=(s,e)=>{pressedField=entry.Field;dragStart=e.GetPosition(this);};
    handle.MouseLeftButtonUp+=(s,e)=>pressedField=null;
    handle.MouseMove+=(s,e)=>{
     if(e.LeftButton!=MouseButtonState.Pressed){pressedField=null;return;}
     if(dragging||pressedField!=entry.Field)return;
     var position=e.GetPosition(this);if(Math.Abs(position.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(position.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
     dragging=true;try{DragDrop.DoDragDrop(handle,new DataObject(typeof(DraggedColumn),new DraggedColumn{Owner=this,Field=entry.Field}),DragDropEffects.Move);}finally{dragging=false;pressedField=null;ClearInsertion();}
    };
    border.DragOver+=(s,e)=>{
     var item=e.Data.GetData(typeof(DraggedColumn)) as DraggedColumn;e.Handled=true;e.Effects=item!=null&&item.Owner==this?DragDropEffects.Move:DragDropEffects.None;ClearInsertion();if(e.Effects==DragDropEffects.None)return;
     bool after=e.GetPosition(border).Y>=border.ActualHeight/2;border.BorderBrush=Ui.Brand;border.BorderThickness=after?new Thickness(0,0,0,2):new Thickness(0,2,0,0);
     var scroll=Parent as ScrollViewer;if(scroll!=null){double y=e.GetPosition(scroll).Y;if(y<30)scroll.ScrollToVerticalOffset(scroll.VerticalOffset-18);else if(y>scroll.ViewportHeight-30)scroll.ScrollToVerticalOffset(scroll.VerticalOffset+18);}
    };
    border.DragLeave+=(s,e)=>{border.BorderBrush=Ui.Line;border.BorderThickness=new Thickness(0,0,0,1);};
    border.Drop+=(s,e)=>{
     var item=e.Data.GetData(typeof(DraggedColumn)) as DraggedColumn;e.Handled=true;if(item==null||item.Owner!=this){e.Effects=DragDropEffects.None;return;}
     bool after=e.GetPosition(border).Y>=border.ActualHeight/2;ClearInsertion();MoveColumn(item.Field,entry.Field,after);e.Effects=DragDropEffects.Move;
    };
   }
  }
 }
}
