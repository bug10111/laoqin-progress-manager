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
 public class ChartPage {public DateTime Start;public int Days;public List<TaskItem> Tasks;public bool IncludeDetails;}
 public static class Exporter {
  public static List<ChartPage> Pages(List<TaskItem> tasks,DateTime start,DateTime end,bool includeDetails=false){
   if(start>end)throw new InvalidOperationException("导出范围不正确。");int span=(end-start).Days+1;if(span>3660)throw new InvalidOperationException("导出范围超过十年，请缩小日期范围。");var pages=new List<ChartPage>();
   for(var date=start.Date;date<=end;date=date.AddDays(14)){
    int days=Math.Min(14,(end-date).Days+1);DateTime last=date.AddDays(days-1);var visible=tasks.Where(t=>Model.Parse(t.End)>=date&&Model.Parse(t.Start)<=last).ToList();
    if(visible.Count==0)continue;for(int j=0;j<visible.Count;j+=10)pages.Add(new ChartPage{Start=date,Days=days,IncludeDetails=includeDetails,Tasks=visible.Skip(j).Take(10).ToList()});if(pages.Count>300)throw new InvalidOperationException("导出超过 300 页，请缩小日期范围或任务范围。");
   }
   if(pages.Count==0)throw new InvalidOperationException("这个日期范围内没有符合筛选条件的排期。");return pages;
  }
  public static BitmapSource Bitmap(AppData d,string name,ChartPage page,int index,int count,double scale){
   double width=1080,height=GanttView.MeasureHeight(d,page.Tasks,page.Start,page.Days,width,true,page.IncludeDetails);var visual=new DrawingVisual();using(var c=visual.RenderOpen()){GanttView.Draw(c,d,page.Tasks,page.Start,page.Days,width,true,name,index,count,true,page.IncludeDetails);}
   var bitmap=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  public static byte[] Image(BitmapSource source,bool jpeg){BitmapEncoder encoder=jpeg?(BitmapEncoder)new JpegBitmapEncoder{QualityLevel=94}:new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var m=new MemoryStream()){encoder.Save(m);return m.ToArray();}}
  public static void Png(string path,AppData d,string project,List<ChartPage> pages){
   if(pages.Count==1){Store.SafeOutput(path,Image(Bitmap(d,project,pages[0],1,1,2),false));return;}
   using(var m=new MemoryStream()){using(var zip=new ZipArchive(m,ZipArchiveMode.Create,true)){for(int i=0;i<pages.Count;i++){var entry=zip.CreateEntry("排期-"+(i+1).ToString("D3")+".png",CompressionLevel.Fastest);using(var f=entry.Open()){byte[] b=Image(Bitmap(d,project,pages[i],i+1,pages.Count,2),false);f.Write(b,0,b.Length);}}}Store.SafeOutput(path,m.ToArray());}
  }
  static byte[] Ascii(string s){return Encoding.ASCII.GetBytes(s);}
  static void Write(Stream s,string text){byte[] b=Ascii(text);s.Write(b,0,b.Length);}
  public static void Pdf(string path,AppData d,string project,List<ChartPage> pages){
   // Each page embeds a high-resolution chart. Chinese glyphs are preserved without external fonts.
   using(var output=new MemoryStream()){
    Write(output,"%PDF-1.4\n");output.Write(new byte[]{37,226,227,207,211,10},0,6);var offsets=new List<long>{0};
    Action<int,byte[]> obj=(id,body)=>{while(offsets.Count<=id)offsets.Add(0);offsets[id]=output.Position;Write(output,id+" 0 obj\n");output.Write(body,0,body.Length);Write(output,"\nendobj\n");};
    obj(1,Ascii("<< /Type /Catalog /Pages 2 0 R >>"));obj(2,Ascii("<< /Type /Pages /Count "+pages.Count+" /Kids ["+String.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>(3+i*3)+" 0 R"))+"] >>"));
    for(int i=0;i<pages.Count;i++){
     var bitmap=Bitmap(d,project,pages[i],i+1,pages.Count,3);byte[] jpg=Image(bitmap,true);int id=3+i*3;
     obj(id,Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 841.89 595.28] /Resources << /XObject << /Im0 "+(id+2)+" 0 R >> >> /Contents "+(id+1)+" 0 R >>"));
     double scale=Math.Min(793.89/bitmap.PixelWidth,547.28/bitmap.PixelHeight),w=bitmap.PixelWidth*scale,h=bitmap.PixelHeight*scale;
     string command=String.Format(CultureInfo.InvariantCulture,"q\n{0:0.###} 0 0 {1:0.###} 24 {2:0.###} cm\n/Im0 Do\nQ",w,h,571.28-h);byte[] cmd=Ascii(command);obj(id+1,Ascii("<< /Length "+cmd.Length+" >>\nstream\n"+command+"\nendstream"));
     using(var body=new MemoryStream()){Write(body,"<< /Type /XObject /Subtype /Image /Width "+bitmap.PixelWidth+" /Height "+bitmap.PixelHeight+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "+jpg.Length+" >>\nstream\n");body.Write(jpg,0,jpg.Length);Write(body,"\nendstream");obj(id+2,body.ToArray());}
    }
    long xref=output.Position;Write(output,"xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)Write(output,offsets[i].ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");Write(output,"trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");Store.SafeOutput(path,output.ToArray());
   }
  }
  public static void Xml(string path,AppData d,string projectId){
   var project=d.Projects.First(x=>x.Id==projectId);var root=new XElement("LaoQinProject",new XAttribute("schemaVersion","1"),new XAttribute("exportedAt",DateTimeOffset.Now.ToString("o")),new XAttribute("dateConvention","inclusive-calendar-days"),new XElement("Project",new XAttribute("id",project.Id),new XElement("Name",project.Name)));
   var people=new XElement("People");foreach(var p in d.People)people.Add(new XElement("Person",new XAttribute("id",p.Id),new XAttribute("deleted",p.Deleted),new XElement("Name",p.Name)));root.Add(people);
   var tasks=new XElement("Tasks");foreach(var t in d.Tasks.Where(x=>x.ProjectId==projectId)){
    var node=new XElement("Task",new XAttribute("id",t.Id),new XElement("Name",t.Name),new XElement("Status",t.Status),new XElement("PersonId",t.PersonId??""),new XElement("PersonName",Model.PersonName(d,t.PersonId)),new XElement("Start",t.Start??""),new XElement("End",t.End??""),new XElement("InCurrentGantt",t.InGantt),new XElement("Note",t.Note??""));
    var history=new XElement("Timeline");foreach(var h in t.History)history.Add(new XElement("Change",new XAttribute("at",h.At??""),new XElement("Kind",h.Kind??""),new XElement("BeforeStatus",h.Before??""),new XElement("AfterStatus",h.After??""),new XElement("TaskName",h.Name??""),new XElement("PersonName",h.PersonName??""),new XElement("Start",h.Start??""),new XElement("End",h.End??""),new XElement("Detail",h.Detail??"")));node.Add(history);tasks.Add(node);
   }
   root.Add(tasks);using(var m=new MemoryStream()){new XDocument(new XDeclaration("1.0","utf-8",null),root).Save(m);Store.SafeOutput(path,m.ToArray());}
  }
 }
}
