// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Automation;

namespace LaoQin.ProgressManager {
 public static class Ui {
  internal static Action<Window> TestDialogReady;
  public static Brush Ink=B("#202B40"),Muted=B("#7B8597"),Line=B("#E8ECF3"),Brand=B("#4D65E6"),BrandSoft=B("#EDF0FF"),Bg=B("#F7F8FC"),Side=B("#F0F3F9");
  public static Brush B(string c){return (Brush)new BrushConverter().ConvertFromString(c);}
  public static void Init(){
   string x=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
   <Style TargetType='Window'><Setter Property='FontFamily' Value='Microsoft YaHei UI, Segoe UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#202B40'/><Setter Property='Background' Value='#F7F8FC'/></Style>
   <Style TargetType='Button'><Setter Property='Padding' Value='14,9'/><Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='#202B40'/><Setter Property='BorderBrush' Value='#E3E8F2'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.83'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#4D65E6'/><Setter TargetName='b' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='TextBox'><Setter Property='Padding' Value='10,9'/><Setter Property='BorderBrush' Value='#E3E8F2'/><Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='#202B40'/><Setter Property='SelectionBrush' Value='#B9C4FF'/></Style>
   <Style TargetType='ComboBox'><Setter Property='Padding' Value='10,8'/><Setter Property='MinHeight' Value='36'/><Setter Property='Background' Value='White'/><Setter Property='BorderBrush' Value='#E3E8F2'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='White' BorderBrush='#E3E8F2' BorderThickness='1' CornerRadius='6'><Grid><TextBlock Text='⌄' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,11,3' Foreground='#7B8597'/></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='11,7,29,7' VerticalAlignment='Center'/><Popup Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'><Border Background='White' BorderBrush='#E3E8F2' BorderThickness='1' CornerRadius='6' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' Padding='5'><ScrollViewer MaxHeight='300'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ComboBoxItem'><Setter Property='Padding' Value='10,8'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='b' Padding='{TemplateBinding Padding}' Background='White' CornerRadius='4'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='b' Property='Background' Value='#EDF0FF'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='#EDF0FF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='DatePicker'><Setter Property='Padding' Value='6'/><Setter Property='BorderBrush' Value='#E3E8F2'/><Setter Property='Background' Value='White'/><Setter Property='Language' Value='zh-CN'/><Setter Property='SelectedDateFormat' Value='Short'/></Style>
   <Style TargetType='CheckBox'><Setter Property='Margin' Value='0,6'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/></Style>
   </ResourceDictionary>";
   Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(x));
  }
  public static TextBlock Text(string s,double size=13,Brush color=null,bool bold=false){return new TextBlock{Text=s??"",FontSize=size,Foreground=color??Ink,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};}
  public static TextBlock Ellipsis(string s,double size=13,Brush color=null,bool bold=false){var t=Text(s,size,color,bold);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;t.ToolTip=s;return t;}
  public static Button Button(string text,Action action,bool primary=false){var b=new Button{Content=text,Background=primary?Brand:Brushes.White,Foreground=primary?Brushes.White:Ink,BorderBrush=primary?Brand:Line,Margin=new Thickness(0),VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(b,text);b.Click+=(s,e)=>Try(action);return b;}
  public static void Try(Action a){try{a();}catch(Exception ex){MessageBox.Show(ex.Message,"老秦",MessageBoxButton.OK,MessageBoxImage.Warning);}}
  public static StackPanel Stack(bool horizontal=false){return new StackPanel{Orientation=horizontal?Orientation.Horizontal:Orientation.Vertical};}
  public static Border Card(UIElement child,double pad=16){return new Border{Child=child,Background=Brushes.White,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(pad)};}
  public static Brush StatusInk(string s){return s=="执行中"?Brand:s=="待执行"?B("#A8742B"):s=="已完成"?B("#258871"):s=="已暂停"?B("#8065AC"):Muted;}
  public static Brush StatusFill(string s){return s=="执行中"?BrandSoft:s=="待执行"?B("#FCF3E4"):s=="已完成"?B("#EAF6F0"):s=="已暂停"?B("#F0ECF9"):Side;}
  public static Border Badge(string status){return new Border{Child=Text("●  "+status,11,StatusInk(status)),Background=StatusFill(status),CornerRadius=new CornerRadius(5),Padding=new Thickness(7,4,7,4),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};}
  public static Border Rule(){return new Border{Height=1,Background=Line,Margin=new Thickness(0,15,0,15)};}
  public static void Margin(FrameworkElement el,double bottom=12){el.Margin=new Thickness(0,0,0,bottom);}
  public static void Label(Panel p,string caption,UIElement field){var t=Text(caption,12);t.Margin=new Thickness(0,16,0,7);p.Children.Add(t);p.Children.Add(field);}
  public static Window Dialog(Window owner,string title,double width=480){var w=new Window{Owner=owner,Title=title,Width=width,SizeToContent=SizeToContent.Height,MaxHeight=850,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Background=Brushes.White};w.Loaded+=(s,e)=>{if(TestDialogReady!=null)w.Dispatcher.BeginInvoke(new Action(()=>TestDialogReady(w)),System.Windows.Threading.DispatcherPriority.ApplicationIdle);};return w;}
  public static string Prompt(Window owner,string title,string label,string initial=""){
   var w=Dialog(owner,title,400);var panel=Stack();panel.Margin=new Thickness(24);var input=new TextBox{Text=initial,MaxLength=120};Label(panel,label,input);string result=null;var row=Stack(true);row.HorizontalAlignment=HorizontalAlignment.Right;row.Margin=new Thickness(0,20,0,0);var cancel=Button("取消",()=>w.Close());cancel.Margin=new Thickness(0,0,8,0);row.Children.Add(cancel);row.Children.Add(Button("确定",()=>{if(String.IsNullOrWhiteSpace(input.Text))throw new InvalidOperationException("请输入名称。");result=input.Text.Trim();w.DialogResult=true;},true));panel.Children.Add(row);w.Content=panel;w.Loaded+=(s,e)=>{input.Focus();input.SelectAll();};w.ShowDialog();return result;
  }
  public static Popup Popup(Button anchor,UIElement child,double width=300){var p=new Popup{PlacementTarget=anchor,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,HorizontalOffset=0,VerticalOffset=5};p.Child=new Border{Background=Brushes.White,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Width=width,Padding=new Thickness(9),Child=child};return p;}
 }
 public class Choice {public string Id;public string Label;}
 public class MultiPicker:Grid {
  public HashSet<string> Selected=new HashSet<string>();public Action Changed;List<Choice> choices;Button button;Popup popup;string label;StackPanel rows;
  public MultiPicker(string caption,IEnumerable<Choice> options,IEnumerable<string> selected){label=caption;choices=options.ToList();Selected=new HashSet<string>(selected??new string[0]);button=Ui.Button("",()=>{Build();popup.IsOpen=!popup.IsOpen;});button.HorizontalContentAlignment=HorizontalAlignment.Left;button.MinWidth=140;Children.Add(button);popup=Ui.Popup(button,new Grid(),360);Update();}
  void Update(){string names=String.Join("、",choices.Where(x=>Selected.Contains(x.Id)).Select(x=>x.Label));button.Content=label+"："+(Selected.Count==0?"未选择":Selected.Count<=2&&names.Length<16?names:"已选 "+Selected.Count+" 项")+"  ⌄";AutomationProperties.SetName(button,label+"多选下拉");}
  public void SetOptions(IEnumerable<Choice> options,bool restrict){choices=options.ToList();if(restrict)Selected.IntersectWith(choices.Select(x=>x.Id));Update();if(popup.IsOpen)Build();}
  public void SelectAll(){Selected=new HashSet<string>(choices.Select(x=>x.Id));Update();if(Changed!=null)Changed();}
  internal FrameworkElement Preview(){Build();return (FrameworkElement)popup.Child;}
  void Build(){
   var panel=Ui.Stack();var head=Ui.Stack(true);var all=Ui.Button("全选",()=>{Selected=new HashSet<string>(choices.Select(x=>x.Id));Build();Update();if(Changed!=null)Changed();});var none=Ui.Button("清空",()=>{Selected.Clear();Build();Update();if(Changed!=null)Changed();});all.Padding=none.Padding=new Thickness(9,5,9,5);none.Margin=new Thickness(6,0,0,0);head.Children.Add(all);head.Children.Add(none);panel.Children.Add(head);rows=Ui.Stack();
   foreach(var item in choices){var local=item;var cb=new CheckBox{Content=Ui.Text(item.Label,12),IsChecked=Selected.Contains(item.Id),Padding=new Thickness(3,5,3,5)};AutomationProperties.SetName(cb,item.Label);cb.Click+=(s,e)=>{if(cb.IsChecked==true)Selected.Add(local.Id);else Selected.Remove(local.Id);Update();if(Changed!=null)Changed();};rows.Children.Add(cb);}
   if(choices.Count==0)rows.Children.Add(Ui.Text("没有符合筛选条件的任务",12,Ui.Muted));panel.Children.Add(new ScrollViewer{Content=rows,MaxHeight=270,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var done=Ui.Button("完成",()=>popup.IsOpen=false,true);done.Margin=new Thickness(0,8,0,0);panel.Children.Add(done);((Border)popup.Child).Child=panel;
  }
 }
 public class EntityPicker:Grid {
  Store store;Window owner;bool project;string current;Button button;Popup popup;Action<string> changed;bool manager;
  public string SelectedId {get{return current;}}
  public void AddNew(){Add();}
  internal FrameworkElement Preview(){Build();return (FrameworkElement)popup.Child;}
  public EntityPicker(Store s,Window w,bool isProject,string selected,Action<string> onChanged,bool manageOnly=false){store=s;owner=w;project=isProject;current=selected;changed=onChanged;manager=manageOnly;button=Ui.Button("",()=>{Build();popup.IsOpen=!popup.IsOpen;});button.HorizontalContentAlignment=HorizontalAlignment.Left;Children.Add(button);popup=Ui.Popup(button,new Grid(),285);Update();}
  void Update(){string label=project?store.Data.Projects.Where(x=>x.Id==current).Select(x=>x.Name).FirstOrDefault():store.Data.People.Where(x=>x.Id==current).Select(x=>x.Name+(x.Deleted?"（已删除）":"")).FirstOrDefault();button.Content=manager?"♧  人员  ·  "+store.Data.People.Count(x=>!x.Deleted)+"   ⌄":(label??(project?(store.Data.Projects.Count==0?"暂无项目":"选择项目"):"选择负责人"))+"  ⌄";button.ToolTip=label;AutomationProperties.SetName(button,project?"项目下拉管理":manager?"人员下拉管理":"负责人下拉选择");}
  void Build(){
   var panel=Ui.Stack();var list=Ui.Stack();var items=project?store.Data.Projects.Select(x=>new Choice{Id=x.Id,Label=x.Name}).ToList():store.Data.People.Where(x=>!x.Deleted).Select(x=>new Choice{Id=x.Id,Label=x.Name}).ToList();
   foreach(var item in items){var local=item;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});var pick=Ui.Button(item.Label,()=>{current=local.Id;popup.IsOpen=false;Update();if(changed!=null)changed(current);});pick.HorizontalContentAlignment=HorizontalAlignment.Left;pick.BorderThickness=new Thickness(0);if(item.Id==current)pick.Background=Ui.BrandSoft;row.Children.Add(pick);var del=Ui.Button("×",()=>Delete(local.Id,local.Label));del.Padding=new Thickness(6);del.BorderThickness=new Thickness(0);del.ToolTip="删除"+item.Label;AutomationProperties.SetName(del,"删除"+item.Label);Grid.SetColumn(del,1);row.Children.Add(del);list.Children.Add(row);}
   panel.Children.Add(new ScrollViewer{Content=list,MaxHeight=300,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var add=Ui.Button("＋ 增加"+(project?"项目":"人员"),()=>Add());add.Margin=new Thickness(0,8,0,0);add.Foreground=Ui.Brand;panel.Children.Add(add);((Border)popup.Child).Child=panel;
  }
  void Add(){popup.IsOpen=false;string name=Ui.Prompt(owner,"增加"+(project?"项目":"人员"),project?"项目名称":"人员姓名");if(name==null)return;string id=Model.Id();store.Commit(d=>{if(project){if(d.Projects.Any(x=>x.Name==name))throw new InvalidOperationException("项目名称已存在。");d.Projects.Add(new Project{Id=id,Name=name});d.CurrentProjectId=id;}else{if(d.People.Any(x=>!x.Deleted&&x.Name==name))throw new InvalidOperationException("人员姓名已存在。");d.People.Add(new Person{Id=id,Name=name});}});current=id;Update();if(changed!=null)changed(id);}
  void Delete(string id,string name){
   popup.IsOpen=false;int count=project?store.Data.Tasks.Count(t=>t.ProjectId==id):store.Data.Tasks.Count(t=>t.PersonId==id);
   string message=project?"删除项目“"+name+"”？\n该项目的全部 "+count+" 项任务、甘特图排期及时间线将一并清除。\n共享人员档案和独立备份文件会保留。允许删除最后一个项目。":"删除人员“"+name+"”？\n已关联的任务和时间线会保留该人员的记录。";
   if(MessageBox.Show(owner,message,"删除确认",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
   store.Commit(d=>{if(project){Model.DeleteProject(d,id);}else{var p=d.People.First(x=>x.Id==id);p.Deleted=true;}});
   if(current==id)current=project?store.Data.CurrentProjectId:"";Update();if(changed!=null)changed(current);
  }
 }
}
