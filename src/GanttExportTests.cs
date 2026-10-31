// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaoQin.ProgressManager {
 internal static class GanttExportTests {
  static Color Pixel(BitmapSource image,double x,double y){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var bytes=new byte[3];rgb.CopyPixels(new Int32Rect((int)x,(int)y,1,1),bytes,3,0);return Color.FromRgb(bytes[0],bytes[1],bytes[2]);}
  static double Luminance(Color color){Func<byte,double> linear=value=>value/255.0<=.04045?value/255.0/12.92:Math.Pow((value/255.0+.055)/1.055,2.4);return .2126*linear(color.R)+.7152*linear(color.G)+.0722*linear(color.B);}
  static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
  internal static byte[] Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var bytes=new byte[image.PixelWidth*image.PixelHeight*3];rgb.CopyPixels(bytes,image.PixelWidth*3,0);return bytes;}
  public static void Run(string folder,Action<bool,string> check){
   var data=Model.Sample();var start=new DateTime(2026,10,26);var tasks=data.Tasks.Take(3).ToList();string[] names={"小程序备案","合并租户权限体系","录音设备调研"};
   for(int i=0;i<tasks.Count;i++){tasks[i].Name=names[i];tasks[i].Start=Model.Date(start.AddDays(i*4));tasks[i].End=Model.Date(start.AddDays(i*4+16));tasks[i].Status="执行中";}
   var items=tasks.Select((t,i)=>new TaskLegendItem{Number=i+1,Task=t}).ToList();var layout=GanttView.LayoutLegend(items,900);
   check(layout.Count==3&&layout.All(s=>s.Bounds.Top==layout[0].Bounds.Top)&&layout[2].Bounds.Right<=900,"任务名称说明优先横排，编号和名称保持完整项");
   tasks[1].Name="合并租户权限体系，检查不同角色下的数据权限、功能权限和历史记录。"+new string('测',180);
   foreach(double width in new[]{210.0,650.0,900.0}){layout=GanttView.LayoutLegend(items,width);check(layout.All(s=>s.Bounds.Left>=0&&s.Bounds.Right<=width+.01)&&!layout.Any(a=>layout.Any(b=>a!=b&&Rect.Intersect(a.Bounds,b.Bounds).Width>0&&Rect.Intersect(a.Bounds,b.Bounds).Height>0)),"任务说明在宽度 "+width+" 下自动换行，不重叠或超出右边界");}
   var gridItems=Enumerable.Range(0,20).Select(i=>new TaskLegendItem{Number=i+1,Task=new TaskItem{Name=i%3==0?"不同长度的任务说明，编号应该在多行之间对齐":"任务 "+(i+1)}}).ToList();layout=GanttView.LayoutLegend(gridItems,948);check(layout.Count==20&&layout.Select(p=>p.Bounds.X).Distinct().Count()==4&&layout.All(p=>p.Bounds.X==layout[(p.Item.Number-1)%4].Bounds.X)&&layout.Select(p=>p.Item.Number).SequenceEqual(Enumerable.Range(1,20)),"20 项说明按编号横向顺序填入四个等宽列，各行数字纵向对齐");
   var before=GanttView.LayoutBarLabel(new Rect(980,79,55,32),170,1080,"录音设备调研","1天",true);var after=GanttView.LayoutBarLabel(new Rect(170,79,55,32),170,1080,"首日任务","1天",false);var above=GanttView.LayoutBarLabel(new Rect(170,79,900,32),170,1080,"覆盖整个日期范围的任务","14天",true);var preferred=GanttView.LayoutBarLabel(new Rect(600,79,55,32),170,1080,"一天任务","1天",true);
   check(before.Label.Left>before.Bar.Right&&before.Bar.Width==55&&before.TitleWidth>=72,"最后一天的名称仍放在条后，保持真实条宽和完整名称");check(after.Label.Left>after.Bar.Right,"首日任务名称统一放条后");check(preferred.Label.Left>preferred.Bar.Right,"两侧空间都足够时名称仍放条后");check(above.Label.Left>above.Bar.Right&&above.Bar.Width==900&&above.Label.Top>above.Bar.Top,"覆盖整个日期范围的任务名称也放条后，不换到上方");
   foreach(var placement in new[]{before,after,above,GanttView.LayoutBarLabel(new Rect(1078,79,2,32),170,1080,"右边界任务","100天",true)})check(placement.Label.Left>placement.Bar.Right&&placement.Duration.Left>=170&&(!placement.Label.IntersectsWith(placement.Bar))&&!placement.Label.IntersectsWith(placement.Duration),"名称和时长始终留在条后，互不重叠，右侧画布可扩展");
   tasks[1].Name=names[1];tasks[2].Status="测试中";tasks[2].Start=Model.Date(start.AddDays(-20));tasks[2].End=Model.Date(start.AddDays(-15));byte[] saved=Store.Encode(data);
   foreach(bool group in new[]{true,false})foreach(bool details in new[]{false,true}){
    var view=new GanttView(data,tasks,start,14,group,details);view.SetTimelineWidth(166+14*66*.87);view.Measure(new Size(view.Width,view.Height));view.Arrange(new Rect(0,0,view.Width,view.Height));view.UpdateLayout();var chart=Exporter.CurrentView(view);
    check(chart.Start==start&&chart.Days==14&&chart.Tasks.SequenceEqual(view.Ordered())&&chart.Tasks.Count==3&&chart.GroupPeople==group&&chart.IncludeDetails==details&&chart.TimelineWidth==view.TimelineWidth&&chart.Width==view.Width&&Exporter.Height(chart)==view.Height,"导出完整保留当前日期、筛选任务行、分组、顺序、87% 列宽和详情："+group+" / "+details);
    var actual=new RenderTargetBitmap((int)Math.Ceiling(view.Width),(int)Math.Ceiling(view.Height),96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var context=background.RenderOpen())context.DrawRectangle(Brushes.White,null,new Rect(0,0,actual.PixelWidth,actual.PixelHeight));var referenceVisual=new ContainerVisual();referenceVisual.Children.Add(background);referenceVisual.Children.Add(view);actual.Render(referenceVisual);check(Pixels(actual).SequenceEqual(Pixels(Exporter.Bitmap(data,chart,1))),"导出与原生甘特图逐像素一致："+group+" / "+details);
   }
   check(saved.SequenceEqual(Store.Encode(data)),"捕获和绘制导出不修改任务或已保存数据");
   var crowded=Enumerable.Range(0,25).Select(i=>new TaskItem{Id=Model.Id(),Name="任务 "+(i+1)+"：验证完整任务说明与日期分页",PersonId=data.People[i%data.People.Count].Id,Status="执行中",Priority="P0",Start=Model.Date(start),End=Model.Date(start.AddDays(35))}).ToList();
   var dense=Exporter.Chart(GanttView.Order(data,crowded,true),start,start.AddDays(35),true);check(dense.Days==36&&dense.Tasks.Count==25&&dense.Tasks.Select(t=>t.Id).Distinct().Count()==25,"跨月 36 天、25 项任务保留为一张完整甘特图，任务不遗漏或重复");
   string pdfPath=Path.Combine(folder,"单页完整甘特图.pdf");Exporter.Pdf(pdfPath,data,dense);string pdf=Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath));check(pdf.StartsWith("%PDF-")&&pdf.Contains("/Count 1 /Kids [3 0 R]")&&pdf.Contains(String.Format(CultureInfo.InvariantCulture,"/MediaBox [0 0 {0:0.###} {1:0.###}]",dense.Width*.75,Exporter.Height(dense)*.75))&&pdf.Contains("/FlateDecode"),"PDF 为单个文件、单张连续页面，按原图比例无损嵌入，保留全部日期与任务");
   var imageChart=Exporter.Chart(tasks,start,start.AddDays(27),true);string imagePath=Path.Combine(folder,"完整高清甘特图.png");Exporter.Png(imagePath,data,imageChart);using(var stream=File.OpenRead(imagePath)){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);var frame=decoder.Frames[0];check(decoder.Frames.Count==1&&frame.PixelWidth==(int)Math.Ceiling(imageChart.Width*3)&&frame.DpiX>=287&&frame.PixelHeight==(int)Math.Ceiling(Exporter.Height(imageChart)*3),"PNG 为单张完整图片，默认使用无损 3 倍分辨率");var actual=Pixels(new FormatConvertedBitmap(frame,PixelFormats.Pbgra32,null,0));var reference=Exporter.Bitmap(data,imageChart,3);var expected=Pixels(reference);bool same=actual.SequenceEqual(expected);if(!same){Store.SafeOutput(Path.Combine(folder,"PNG-参考.png"),Exporter.Image(reference,false));int first=Enumerable.Range(0,actual.Length).First(i=>actual[i]!=expected[i]);File.WriteAllText(Path.Combine(folder,"PNG-差异.txt"),"首个差异："+(first/3%frame.PixelWidth)+","+(first/3/frame.PixelWidth)+"，通道 "+first%3+"，实际 "+actual[first]+"，参考 "+expected[first]+"\n差异字节数："+Enumerable.Range(0,actual.Length).Count(i=>actual[i]!=expected[i]));}check(same,"PNG 文件解码后与当前布局渲染逐像素一致");}
   var reduced=Exporter.Chart(crowded,start,start.AddDays(60),true);check(Exporter.RasterScale(reduced)>=1&&Exporter.RasterScale(reduced)<3&&reduced.Days==61&&reduced.Tasks.Count==25,"大图仅调整输出像素密度，保留日期、列宽和所有任务，不自动拆分");
   bool oversized=false;try{Exporter.RasterScale(Exporter.Chart(crowded,start,start.AddDays(999),true));}catch(InvalidOperationException){oversized=true;}check(oversized,"超出单图安全尺寸时提示缩小视图，不生成多个文件");
   var empty=Exporter.Chart(new List<TaskItem>(),start,start.AddDays(13));check(Exporter.Bitmap(data,empty,1).PixelHeight>300,"空筛选沿用界面空状态，可导出当前空甘特图");
   var aligned=gridItems.Select((item,i)=>new TaskItem{Id=Model.Id(),Name=item.Task.Name,PersonId=data.People[0].Id,Status="执行中",Priority=i%2==0?"P0":"P1",Start=Model.Date(start.AddDays(i%4==0?0:i%4==1?6:i%4==2?13:0)),End=Model.Date(start.AddDays(i%4==0?0:i%4==1?6:13))}).ToList();
   var preview=Exporter.Chart(aligned,start,start.AddDays(13),true,true,1136);Exporter.Png(Path.Combine(folder,"编号对齐与短任务名称.png"),data,preview);Exporter.Pdf(Path.Combine(folder,"编号对齐与短任务名称.pdf"),data,preview);
   double canvasWidth=GanttView.RequiredWidth(aligned,start,14,1136,true);var visual=new DrawingVisual();List<BarHit> hits;using(var context=visual.RenderOpen()){hits=GanttView.Draw(context,data,aligned,start,14,canvasWidth,false,"",1,1,true,true,1136);}check(aligned.All(t=>hits.Count(h=>h.Task==t)==2),"移到条外的名称和时间条都保留任务点击区域");check(aligned.All(t=>{var pair=hits.Where(h=>h.Task==t).ToList();return pair[1].Rect.Left>pair[0].Rect.Right&&pair[1].Rect.Right<=canvasWidth-11;}),"预览画布包含最后一天之后的完整任务名称，条后标识不被裁切");using(var context=visual.RenderOpen()){hits=GanttView.Draw(context,data,aligned,start,14,1080,true,"",1,1,true,true);}check(aligned.All(t=>{var pair=hits.Where(h=>h.Task==t).ToList();return pair[1].Rect.Left>pair[0].Rect.Right&&pair[1].Rect.Right<=1080;}),"横向 PDF 预留右侧标识区域，全部名称统一在条后且不越出页面");
   var weekendTasks=Model.Statuses.Select(status=>new TaskItem{Id=Model.Id(),Name=status+" · 周末一天任务",PersonId=data.People[0].Id,Status=status,Priority="P1",Start=Model.Date(start.AddDays(5)),End=Model.Date(start.AddDays(5))}).ToList();var weekendPage=Exporter.Chart(weekendTasks,start,start.AddDays(13),true,true,1080);var weekendImage=Exporter.Bitmap(data,weekendPage,1);double barX=166+5*(1080-166)/14.0+4;
   for(int i=0;i<weekendTasks.Count;i++){double rowTop=64+i*62;Color background=Pixel(weekendImage,barX+14,rowTop+3),fill=Pixel(weekendImage,barX+14,rowTop+23),edge=Pixel(weekendImage,barX+4,rowTop+25),ink=((SolidColorBrush)GanttView.BarInk(weekendTasks[i].Status)).Color;check(Contrast(edge,background)>=2.4&&Contrast(ink,fill)>=4.5,"周末上的"+weekendTasks[i].Status+"短任务轮廓及条内时长保持清晰");}
   Exporter.Png(Path.Combine(folder,"周末短任务-配色与名称.png"),data,weekendPage);Exporter.Pdf(Path.Combine(folder,"周末短任务-配色与名称.pdf"),data,weekendPage);
  }
 }
}
