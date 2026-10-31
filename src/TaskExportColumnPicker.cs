// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LaoQin.ProgressManager {
 [DataContract] public sealed class TaskExportPreferences {
  [DataMember] public List<string> Order;
  [DataMember] public List<string> Selected;

  // 用稳定字段名保存偏好；旧配置缺失的新字段自动追加，首次使用默认全选。
  internal List<TaskExportField> OrderedFields(){
   var known=TaskExcelExporter.Columns.ToDictionary(c=>c.Field.ToString(),c=>c.Field);
   var result=new List<TaskExportField>();
   foreach(string name in Order??new List<string>())if(name!=null&&known.ContainsKey(name)&&!result.Contains(known[name]))result.Add(known[name]);
   result.AddRange(TaskExcelExporter.Columns.Select(c=>c.Field).Where(f=>!result.Contains(f)));return result;
  }
  internal bool IsSelected(TaskExportField field){return Selected==null||Selected.Contains(field.ToString())||Order!=null&&!Order.Contains(field.ToString());}
 }

 // 使用独立拖动手柄，点击复选框不会意外触发排序。
 internal sealed class TaskExportColumnPicker:StackPanel {
  sealed class Entry {public TaskExportField Field;public bool Selected;}
  sealed class DraggedColumn {public TaskExportColumnPicker Owner;public TaskExportField Field;}
  readonly List<Entry> entries=new List<Entry>();readonly StackPanel rows=Ui.Stack();
  readonly List<CheckBox> boxes=new List<CheckBox>();readonly List<Border> rowBorders=new List<Border>();
  public event Action Changed;
  Point dragStart;bool dragging;TaskExportField? pressedField;
  public List<TaskExportField> SelectedFields {get{return entries.Where(e=>e.Selected).Select(e=>e.Field).ToList();}}
  public List<TaskExportField> OrderedFields {get{return entries.Select(e=>e.Field).ToList();}}
  internal TaskExportColumnPicker(TaskExportPreferences preferences){
   var saved=preferences??new TaskExportPreferences();
   foreach(var field in saved.OrderedFields())entries.Add(new Entry{Field=field,Selected=saved.IsSelected(field)});
   Children.Add(rows);RenderRows();
  }
  public TaskExportPreferences Preferences(){return new TaskExportPreferences{Order=OrderedFields.Select(f=>f.ToString()).ToList(),Selected=SelectedFields.Select(f=>f.ToString()).ToList()};}
  public void SelectAll(bool selected){foreach(var box in boxes)box.IsChecked=selected;}
  public void ResetDefaults(){entries.Clear();entries.AddRange(TaskExcelExporter.Columns.Select(c=>new Entry{Field=c.Field,Selected=true}));RenderRows();Notify();}
  internal void MoveColumn(TaskExportField field,TaskExportField target,bool after){
   var moving=entries.FirstOrDefault(e=>e.Field==field);var anchor=entries.FirstOrDefault(e=>e.Field==target);if(moving==null||anchor==null||moving==anchor)return;
   entries.Remove(moving);int index=entries.IndexOf(anchor)+(after?1:0);entries.Insert(index,moving);RenderRows();Notify();
  }
  void Notify(){if(Changed!=null)Changed();}
  void ClearInsertion(){foreach(var border in rowBorders){border.BorderBrush=Ui.Line;border.BorderThickness=new Thickness(0,0,0,1);}}
  void RenderRows(){
   rows.Children.Clear();boxes.Clear();rowBorders.Clear();
   for(int index=0;index<entries.Count;index++){
    var entry=entries[index];var column=TaskExcelExporter.Columns.First(c=>c.Field==entry.Field);
    var grid=new Grid{MinHeight=36,Background=Brushes.Transparent};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});grid.ColumnDefinitions.Add(new ColumnDefinition());
    var handle=new Border{Background=Brushes.Transparent,Cursor=Cursors.SizeAll,ToolTip="按住拖动，调整表头顺序",Child=Ui.Text("≡",19,Ui.Muted)};grid.Children.Add(handle);
    var number=Ui.Text((index+1).ToString("D2"),11,Ui.Muted);Grid.SetColumn(number,1);grid.Children.Add(number);
    var box=new CheckBox{Content=column.Header,Tag=entry.Field,IsChecked=entry.Selected,VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};Grid.SetColumn(box,2);grid.Children.Add(box);boxes.Add(box);
    box.Checked+=(s,e)=>{entry.Selected=true;Notify();};box.Unchecked+=(s,e)=>{entry.Selected=false;Notify();};
    var border=new Border{Child=grid,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1),AllowDrop=true,Tag=entry.Field};rowBorders.Add(border);rows.Children.Add(border);
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
