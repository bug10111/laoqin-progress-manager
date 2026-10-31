// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace LaoQin.ProgressManager {
 public class ChartPage {public DateTime Start;public int Days;public List<TaskItem> Tasks;public bool IncludeDetails,GroupPeople=true;public double Width,TimelineWidth;}
 public static class Exporter {
  static int DateSpan(DateTime start,DateTime end){if(start.Date>end.Date)throw new InvalidOperationException("导出范围不正确。");int span=(end.Date-start.Date).Days+1;if(span>3660)throw new InvalidOperationException("导出范围超过十年，请缩小日期范围。");return span;}
  // 直接保留当前图表的任务行、分组和列宽，不重新筛选日期或套用打印布局。
  public static ChartPage CurrentView(GanttView view){return Chart(view.Ordered(),view.Start,view.Start.AddDays(view.Days-1),view.ShowDetails,view.GroupPeople,view.TimelineWidth);}
  public static ChartPage Chart(List<TaskItem> tasks,DateTime start,DateTime end,bool includeDetails=false,bool groupPeople=true,double timelineWidth=0){
   int days=DateSpan(start,end);if(timelineWidth<=166)timelineWidth=166+days*66;
   return new ChartPage{Start=start.Date,Days=days,Tasks=tasks.ToList(),IncludeDetails=includeDetails,GroupPeople=groupPeople,TimelineWidth=timelineWidth,Width=GanttView.RequiredWidth(tasks,start.Date,days,timelineWidth,includeDetails)};
  }
  public static double Height(ChartPage page){return GanttView.MeasureHeight(null,page.Tasks,page.Start,page.Days,page.Width,false,page.IncludeDetails);}
  internal static double RasterScale(ChartPage page){
   double height=Height(page),scale=Math.Min(3,Math.Min(16000/page.Width,Math.Min(16000/height,Math.Sqrt(60000000/(page.Width*height)))));
   if(scale<1)throw new InvalidOperationException("当前甘特图过大，请缩小日期范围或减少显示的任务后再导出单个文件。");return scale;
  }
  public static BitmapSource Bitmap(AppData d,ChartPage page,double scale){
   double width=page.Width,height=Height(page);var view=new GanttView(d,page.Tasks,page.Start,page.Days,page.GroupPeople,page.IncludeDetails);view.SetTimelineWidth(page.TimelineWidth);view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var context=background.RenderOpen())context.DrawRectangle(Brushes.White,null,new Rect(0,0,bitmap.PixelWidth/scale,bitmap.PixelHeight/scale));var visual=new ContainerVisual();visual.Children.Add(background);visual.Children.Add(view);bitmap.Render(visual);
   // 白底图表统一转为不透明 RGB，避免 PNG 透明通道往返时的颜色舍入。
   var opaque=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb24,null,0);opaque.Freeze();return opaque;
  }
  public static byte[] Image(BitmapSource source,bool jpeg){BitmapEncoder encoder=jpeg?(BitmapEncoder)new JpegBitmapEncoder{QualityLevel=94}:new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var m=new MemoryStream()){encoder.Save(m);return m.ToArray();}}
  public static void Png(string path,AppData d,ChartPage page){Store.SafeOutput(path,Image(Bitmap(d,page,RasterScale(page)),false));}
  static byte[] Ascii(string s){return Encoding.ASCII.GetBytes(s);}
  static void Write(Stream s,string text){byte[] b=Ascii(text);s.Write(b,0,b.Length);}
  static byte[] PdfImage(BitmapSource bitmap){
   var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb24,null,0);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*3];rgb.CopyPixels(pixels,bitmap.PixelWidth*3,0);
   // PDF 的 FlateDecode 使用 zlib 封装；无损压缩保留细线和中文边缘。
   using(var output=new MemoryStream()){output.WriteByte(0x78);output.WriteByte(0x9c);using(var deflate=new DeflateStream(output,CompressionLevel.Optimal,true))deflate.Write(pixels,0,pixels.Length);
    uint a=1,b=0;for(int offset=0;offset<pixels.Length;){int end=Math.Min(offset+5552,pixels.Length);for(;offset<end;offset++){a+=pixels[offset];b+=a;}a%=65521;b%=65521;}uint checksum=(b<<16)|a;for(int shift=24;shift>=0;shift-=8)output.WriteByte((byte)(checksum>>shift));return output.ToArray();}
  }
  public static void Pdf(string path,AppData d,ChartPage page){Store.SafeOutput(path,PdfBytes(d,page));}
  static byte[] PdfBytes(AppData d,ChartPage page){
   // 页面随完整甘特图延展；不按 A4、日期或任务行拆页。
   var bitmap=Bitmap(d,page,RasterScale(page));byte[] pixels=PdfImage(bitmap);double width=page.Width*.75,height=Height(page)*.75;
   using(var output=new MemoryStream()){
    Write(output,"%PDF-1.4\n");output.Write(new byte[]{37,226,227,207,211,10},0,6);var offsets=new List<long>{0};
    Action<int,byte[]> obj=(id,body)=>{while(offsets.Count<=id)offsets.Add(0);offsets[id]=output.Position;Write(output,id+" 0 obj\n");output.Write(body,0,body.Length);Write(output,"\nendobj\n");};
    obj(1,Ascii("<< /Type /Catalog /Pages 2 0 R >>"));obj(2,Ascii("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"));
    obj(3,Ascii(String.Format(CultureInfo.InvariantCulture,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0:0.###} {1:0.###}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>",width,height)));
    string command=String.Format(CultureInfo.InvariantCulture,"q\n{0:0.###} 0 0 {1:0.###} 0 0 cm\n/Im0 Do\nQ",width,height);byte[] cmd=Ascii(command);obj(4,Ascii("<< /Length "+cmd.Length+" >>\nstream\n"+command+"\nendstream"));
    using(var body=new MemoryStream()){Write(body,"<< /Type /XObject /Subtype /Image /Width "+bitmap.PixelWidth+" /Height "+bitmap.PixelHeight+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length "+pixels.Length+" >>\nstream\n");body.Write(pixels,0,pixels.Length);Write(body,"\nendstream");obj(5,body.ToArray());}
    long xref=output.Position;Write(output,"xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)Write(output,offsets[i].ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");Write(output,"trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");return output.ToArray();
   }
  }
  public static void Xml(string path,AppData d,string projectId){
   var project=d.Projects.First(x=>x.Id==projectId);var root=new XElement("LaoQinProject",new XAttribute("schemaVersion","1"),new XAttribute("exportedAt",DateTimeOffset.Now.ToString("o")),new XAttribute("dateConvention","inclusive-calendar-days"),new XElement("Project",new XAttribute("id",project.Id),new XElement("Name",project.Name)));
   var people=new XElement("People");foreach(var p in d.People)people.Add(new XElement("Person",new XAttribute("id",p.Id),new XAttribute("deleted",p.Deleted),new XElement("Name",p.Name)));root.Add(people);
   var tasks=new XElement("Tasks");foreach(var t in d.Tasks.Where(x=>x.ProjectId==projectId)){
    var node=new XElement("Task",new XAttribute("id",t.Id),new XElement("Name",t.Name),new XElement("Status",t.Status),new XElement("Priority",t.Priority),new XElement("PersonId",t.PersonId??""),new XElement("PersonName",Model.PersonName(d,t.PersonId)),new XElement("Start",t.Start??""),new XElement("End",t.End??""),new XElement("InCurrentGantt",Model.IsCurrentGanttTask(t)),new XElement("Note",t.Note??""));
    var history=new XElement("Timeline");foreach(var h in t.History)history.Add(new XElement("Change",new XAttribute("at",h.At??""),new XElement("Kind",h.Kind??""),new XElement("BeforeStatus",h.Before??""),new XElement("AfterStatus",h.After??""),new XElement("Priority",h.Priority??""),new XElement("TaskName",h.Name??""),new XElement("PersonName",h.PersonName??""),new XElement("Start",h.Start??""),new XElement("End",h.End??""),new XElement("Detail",h.Detail??"")));node.Add(history);tasks.Add(node);
   }
   root.Add(tasks);using(var m=new MemoryStream()){new XDocument(new XDeclaration("1.0","utf-8",null),root).Save(m);Store.SafeOutput(path,m.ToArray());}
  }
 }
}
