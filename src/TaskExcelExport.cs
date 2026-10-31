// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace LaoQin.ProgressManager {
 // 列表与导出共享查询，分页只在界面显示时应用。
 public sealed class TaskQuery {
  public readonly string ProjectId,Status,Priority,Search;
  readonly HashSet<string> statuses,priorities;
  public TaskQuery(string projectId,string status,string priority,string search):this(projectId,status=="全部"?Model.Statuses:new[]{status},priority=="全部"?Model.Priorities:new[]{priority},search){}
  public TaskQuery(string projectId,IEnumerable<string> selectedStatuses,IEnumerable<string> selectedPriorities,string search){
   ProjectId=projectId;statuses=new HashSet<string>(selectedStatuses??Model.Statuses);priorities=new HashSet<string>(selectedPriorities??Model.Priorities);Search=(search??"").Trim();
   Status=FilterSummary(Model.Statuses,statuses);Priority=FilterSummary(Model.Priorities,priorities);
  }
  static string FilterSummary(string[] options,HashSet<string> selected){return selected.SetEquals(options)?"全部":selected.Count==0?"未选择":String.Join("、",options.Where(selected.Contains));}
  public List<TaskItem> Apply(AppData data){
   return Model.OrderTaskList(data.Tasks.Where(t=>t.ProjectId==ProjectId&&statuses.Contains(t.Status)&&priorities.Contains(t.Priority)&&(Search.Length==0||Contains(t.Name)||Contains(t.Note)))).ToList();
  }
  bool Contains(string value){return !String.IsNullOrEmpty(value)&&value.IndexOf(Search,StringComparison.CurrentCultureIgnoreCase)>=0;}
  public string Summary(AppData data){var project=data.Projects.First(x=>x.Id==ProjectId);return "项目："+project.Name+"；状态："+Status+"；优先级："+Priority+"；关键词："+(Search.Length==0?"无":Search);}
 }
 public enum TaskExportField { Number,Project,Name,Priority,Status,Person,Start,End,InGantt,Note,Requirements,Created,Updated }
 public sealed class TaskExportColumn {
  public readonly TaskExportField Field;public readonly string Header;public readonly double Width;
  public TaskExportColumn(TaskExportField field,string header,double width){Field=field;Header=header;Width=width;}
 }
 public static class TaskExcelExporter {
  public static readonly TaskExportColumn[] Columns={
   new TaskExportColumn(TaskExportField.Number,"序号",8),new TaskExportColumn(TaskExportField.Project,"所属项目",20),
   new TaskExportColumn(TaskExportField.Name,"任务名称",34),new TaskExportColumn(TaskExportField.Priority,"优先级",10),
   new TaskExportColumn(TaskExportField.Status,"状态",58),new TaskExportColumn(TaskExportField.Person,"负责人",22),
   new TaskExportColumn(TaskExportField.Start,"计划开始日期",18),new TaskExportColumn(TaskExportField.End,"计划结束日期",18),
   new TaskExportColumn(TaskExportField.InGantt,"是否纳入当前排期",24),new TaskExportColumn(TaskExportField.Note,"备注",52),
   new TaskExportColumn(TaskExportField.Requirements,"追加需求",60),new TaskExportColumn(TaskExportField.Created,"创建时间",24),
   new TaskExportColumn(TaskExportField.Updated,"最近更新时间",24)
  };
  static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  static readonly XNamespace P="http://schemas.openxmlformats.org/package/2006/relationships";
  const int HeaderRow=3;
  internal const double ColumnUnitPixels=8;
  sealed class CellSpan {public int Column,First,Last;public object Value;public TaskExportField Field;}

  public static void Export(string path,AppData data,TaskQuery query,IEnumerable<TaskExportField> selected){
   var available=Columns.ToDictionary(c=>c.Field);var columns=selected.Distinct().Where(available.ContainsKey).Select(field=>available[field]).ToList();
   if(columns.Count==0)throw new InvalidOperationException("请至少勾选一个导出字段。");
   var tasks=query.Apply(data);if(tasks.Count==0)throw new InvalidOperationException("当前筛选条件下没有可导出的任务，请调整筛选条件。");
   // 少量列也为标题与导出信息保留足够宽度。
   var widths=columns.Select(c=>c.Width).ToArray();if(widths.Sum()<76)widths[0]+=76-widths.Sum();
   var rows=new XElement(S+"sheetData");var merges=new List<string>();string lastColumn=ColumnName(columns.Count-1);
   AddReportHeader(rows,merges,data.Projects.First(p=>p.Id==query.ProjectId).Name,tasks.Count,widths);
   var header=new XElement(S+"row",new XAttribute("r",HeaderRow),new XAttribute("ht",32),new XAttribute("customHeight",1));
   for(int c=0;c<columns.Count;c++)header.Add(Cell(c,HeaderRow,columns[c].Header,3));rows.Add(header);
   int nextRow=HeaderRow+1;int statusColumn=columns.FindIndex(c=>c.Field==TaskExportField.Status);var progress=new StatusProgressDrawing();
   for(int index=0;index<tasks.Count;index++){
    var task=tasks[index];var history=Model.Timeline(task);var spans=new List<CellSpan>();int count=1;
    for(int c=0;c<columns.Count;c++){
     var field=columns[c].Field;
     if(field==TaskExportField.Requirements){
      int step=0,offset=0;foreach(var change in history.Where(h=>h.Kind=="需求追加")){
       step++;string text=step+". "+Model.HistoryTime(change)+"\n"+(change.Detail??"");
       foreach(string part in SplitText(text,widths[c]))spans.Add(new CellSpan{Column=c,First=offset,Last=offset++,Value=part,Field=field});
      }
      count=Math.Max(count,offset);
     }else{
      object value=Value(data,query,task,history,field,index+1);var text=value as string;
      var parts=text==null?new List<object>{value}:SplitText(text,widths[c]).Cast<object>().ToList();
      for(int i=0;i<parts.Count;i++)spans.Add(new CellSpan{Column=c,First=i,Last=i,Value=parts[i],Field=field});
      count=Math.Max(count,parts.Count);
     }
    }
    if((long)nextRow+count-1>1048576)throw new InvalidOperationException("导出超过 Excel 行数上限，请缩小筛选范围。");
    // 每个任务独立合并，绝不跨任务合并同名、同负责人等字段。
    foreach(var group in spans.Where(s=>s.Field!=TaskExportField.Requirements).GroupBy(s=>s.Column))group.Last().Last=count-1;
    var heights=Enumerable.Repeat(24.0,count).ToArray();
    foreach(var span in spans){
     double needed=TextHeight(Display(span.Value,span.Field),widths[span.Column])+8;
     double extra=needed-heights.Skip(span.First).Take(span.Last-span.First+1).Sum();
     if(extra>0)for(int i=span.First;i<=span.Last;i++)heights[i]+=extra/(span.Last-span.First+1);
    }
    if(statusColumn>=0){double extra=StatusProgressDrawing.MinimumHeight(task)-heights.Sum();if(extra>0)for(int i=0;i<count;i++)heights[i]+=extra/count;progress.Add(task,statusColumn,nextRow,widths[statusColumn],heights.Sum());}
    var starting=spans.ToDictionary(s=>s.Column+":"+s.First);
    for(int i=0;i<count;i++){
     var row=new XElement(S+"row",new XAttribute("r",nextRow+i),new XAttribute("ht",Math.Min(409,Math.Ceiling(heights[i]))),new XAttribute("customHeight",1));
     for(int c=0;c<columns.Count;c++){
      CellSpan span;bool begins=starting.TryGetValue(c+":"+i,out span);object value=begins?span.Value:null;
      int style=BodyStyle(columns[c].Field,value,index%2==1);row.Add(Cell(c,nextRow+i,value,style));
      if(begins&&span.Last>span.First)merges.Add(ColumnName(c)+(nextRow+span.First)+":"+ColumnName(c)+(nextRow+span.Last));
     }
     rows.Add(row);
    }
    nextRow+=count;
   }
   string extent="A1:"+lastColumn+(nextRow-1);
   var sheet=new XElement(S+"worksheet",new XElement(S+"sheetPr",new XElement(S+"tabColor",new XAttribute("rgb","FF4F5FEF")),new XElement(S+"pageSetUpPr",new XAttribute("fitToPage",1))),
    new XElement(S+"dimension",new XAttribute("ref",extent)),
    new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),new XAttribute("zoomScale",85),new XElement(S+"selection",new XAttribute("activeCell","A"+(HeaderRow+1)),new XAttribute("sqref","A"+(HeaderRow+1))))),
    new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",24)),
    new XElement(S+"cols",widths.Select((width,i)=>new XElement(S+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",width),new XAttribute("customWidth",1)))),rows);
   if(merges.Count>0)sheet.Add(new XElement(S+"mergeCells",new XAttribute("count",merges.Count),merges.Select(m=>new XElement(S+"mergeCell",new XAttribute("ref",m)))));
   sheet.Add(new XElement(S+"printOptions",new XAttribute("horizontalCentered",1)),new XElement(S+"pageMargins",new XAttribute("left",0.25),new XAttribute("right",0.25),new XAttribute("top",0.4),new XAttribute("bottom",0.4),new XAttribute("header",0.2),new XAttribute("footer",0.2)),
    new XElement(S+"pageSetup",new XAttribute("paperSize",8),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)),
    new XElement(S+"headerFooter",new XElement(S+"oddFooter","&C第 &P 页 / 共 &N 页")));
   if(statusColumn>=0)sheet.Add(new XElement(S+"drawing",new XAttribute(R+"id","rIdProgress")));
   var workbook=new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),new XElement(S+"bookViews",new XElement(S+"workbookView",new XAttribute("activeTab",0))),new XElement(S+"sheets",new XElement(S+"sheet",new XAttribute("name","任务清单"),new XAttribute("sheetId",1),new XAttribute(R+"id","rId1"))),
    new XElement(S+"definedNames",new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Titles"),new XAttribute("localSheetId",0),"'任务清单'!$"+HeaderRow+":$"+HeaderRow),new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Area"),new XAttribute("localSheetId",0),"'任务清单'!$A$1:$"+lastColumn+"$"+(nextRow-1))));
   using(var output=new MemoryStream()){
    using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
     XNamespace content="http://schemas.openxmlformats.org/package/2006/content-types";
     var types=new XElement(content+"Types",new XElement(content+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(content+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),ContentType(content,"/xl/workbook.xml","sheet.main"),ContentType(content,"/xl/worksheets/sheet1.xml","worksheet"),ContentType(content,"/xl/styles.xml","styles"));
     if(statusColumn>=0){types.Add(new XElement(content+"Override",new XAttribute("PartName","/xl/drawings/progress.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.drawing+xml")));Write(zip,"xl/drawings/progress.xml",progress.Root);Write(zip,"xl/worksheets/_rels/sheet1.xml.rels",new XElement(P+"Relationships",Relationship("rIdProgress","drawing","../drawings/progress.xml")));}
     Write(zip,"[Content_Types].xml",types);
     Write(zip,"_rels/.rels",new XElement(P+"Relationships",Relationship("rId1","officeDocument","xl/workbook.xml")));
     Write(zip,"xl/_rels/workbook.xml.rels",new XElement(P+"Relationships",Relationship("rId1","worksheet","worksheets/sheet1.xml"),Relationship("rId2","styles","styles.xml")));
     Write(zip,"xl/workbook.xml",workbook);Write(zip,"xl/styles.xml",Styles());Write(zip,"xl/worksheets/sheet1.xml",sheet);
    }
    Store.SafeOutput(path,output.ToArray());
   }
  }
  static object Value(AppData data,TaskQuery query,TaskItem task,List<Change> history,TaskExportField field,int number){
   switch(field){
    case TaskExportField.Number:return number;
    case TaskExportField.Project:return data.Projects.First(p=>p.Id==query.ProjectId).Name;
    case TaskExportField.Name:return task.Name;
    case TaskExportField.Priority:return task.Priority;
    case TaskExportField.Status:return Model.ProgressStatuses.Contains(task.Status)?null:task.Status;
    case TaskExportField.Person:return Model.PersonName(data,task.PersonId);
    case TaskExportField.Start:return Model.IsDate(task.Start)?(object)Model.Parse(task.Start):null;
    case TaskExportField.End:return Model.IsDate(task.End)?(object)Model.Parse(task.End):null;
    case TaskExportField.InGantt:return Model.IsCurrentGanttTask(task)?"是":"否";
    case TaskExportField.Note:return task.Note??"";
    case TaskExportField.Created:var created=history.FirstOrDefault(h=>h.Kind=="创建任务");return created==null?null:(object)DateTimeOffset.Parse(created.At).LocalDateTime;
    case TaskExportField.Updated:return history.Count==0?null:(object)DateTimeOffset.Parse(history.Last().At).LocalDateTime;
    default:return null;
   }
  }
  static string Display(object value,TaskExportField field){if(value is DateTime)return ((DateTime)value).ToString(field==TaskExportField.Start||field==TaskExportField.End?"yyyy-MM-dd":"yyyy-MM-dd HH:mm");return Convert.ToString(value,CultureInfo.InvariantCulture);}
  static double TextHeight(string text,double width,double fontSize=44.0/3){
   if(String.IsNullOrEmpty(text))return 0;
   var formatted=new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),fontSize,Brushes.Black){MaxTextWidth=Math.Max(25,width*ColumnUnitPixels-14)};
   return formatted.Height*0.75;
  }
  // 超长说明分为连续行，保留全部文字，避免超过 Excel 单元格与行高限制。
  static List<string> SplitText(string text,double width){
   var parts=new List<string>();text=(text??"").Replace("\r\n","\n").Replace('\r','\n');
   if(text.Length==0){parts.Add("");return parts;}
   while(text.Length>0){
    int low=1,high=Math.Min(30000,text.Length),length=1;
    while(low<=high){int middle=low+(high-low)/2;int safe=middle;if(safe<text.Length&&Char.IsHighSurrogate(text[safe-1]))safe--;if(safe==0){low=middle+1;continue;}
     if(TextHeight(text.Substring(0,safe),width)<=300){length=safe;low=middle+1;}else high=middle-1;
    }
    if(length<text.Length&&Char.IsHighSurrogate(text[length-1]))length++;
    parts.Add(text.Substring(0,length));text=text.Substring(length);
   }
   return parts;
  }
  static void AddReportHeader(XElement rows,List<string> merges,string project,int taskCount,double[] widths){
   string title=project+" / 任务清单",info=taskCount+" 项任务    导出于 "+DateTime.Now.ToString("yyyy-MM-dd HH:mm");
   rows.Add(new XElement(S+"row",new XAttribute("r",1),new XAttribute("ht",10),new XAttribute("customHeight",1)));
   // 报告头限制在前几列的可见范围内，避免宽表的导出信息跑到屏幕外。
   int end=0;double total=widths[0];while(end+1<widths.Length&&total<128)total+=widths[++end];
   int split=0;double best=Double.MaxValue;
   for(int c=1;c<=end;c++){
    double left=widths.Take(c).Sum(),right=widths.Take(end+1).Skip(c).Sum();if(right<46)continue;
    double score=TextHeight(title,left,64.0/3)*10+Math.Abs(left-total*.45);if(score<best){best=score;split=c;}
   }
   if(split==0){
    // 单列或窄列导出仍保留两种字号，并合并在同一个报告头单元格中。
    var cell=Cell(0,2,null,1);cell.Add(new XAttribute("t","inlineStr"),new XElement(S+"is",new XElement(S+"r",new XElement(S+"t",ExcelText(title))),new XElement(S+"r",new XElement(S+"rPr",new XElement(S+"sz",new XAttribute("val",11)),new XElement(S+"color",new XAttribute("rgb","FF64748B"))),new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),"\n"+info))));
    var row=new XElement(S+"row",new XAttribute("r",2),new XAttribute("ht",Math.Min(409,Math.Max(54,TextHeight(title,widths.Sum(),64.0/3)+TextHeight(info,widths.Sum())+18))),new XAttribute("customHeight",1),cell);
    for(int c=1;c<widths.Length;c++)row.Add(Cell(c,2,null,1));rows.Add(row);if(widths.Length>1)merges.Add("A2:"+ColumnName(widths.Length-1)+"2");return;
   }
   double height=Math.Max(44,Math.Max(TextHeight(title,widths.Take(split).Sum(),64.0/3),TextHeight(info,widths.Take(end+1).Skip(split).Sum()))+16);
   var banner=new XElement(S+"row",new XAttribute("r",2),new XAttribute("ht",Math.Min(409,Math.Ceiling(height))),new XAttribute("customHeight",1));
   for(int c=0;c<widths.Length;c++)banner.Add(Cell(c,2,c==0?title:c==split?info:null,c<split?1:28));rows.Add(banner);
   if(split>1)merges.Add("A2:"+ColumnName(split-1)+"2");if(end>split)merges.Add(ColumnName(split)+"2:"+ColumnName(end)+"2");
  }
  static string ColumnName(int index){string result="";do{result=(char)('A'+index%26)+result;index=index/26-1;}while(index>=0);return result;}
  static XElement Cell(int column,int row,object value,int style){
   var cell=new XElement(S+"c",new XAttribute("r",ColumnName(column)+row),new XAttribute("s",style));if(value==null)return cell;
   if(value is DateTime)cell.Add(new XElement(S+"v",((DateTime)value).ToOADate().ToString("R",CultureInfo.InvariantCulture)));
   else if(value is int)cell.Add(new XElement(S+"v",value));
   else cell.Add(new XAttribute("t","inlineStr"),new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),ExcelText((string)value))));
   return cell;
  }
  static string ExcelText(string text){
   text=Regex.Replace(text,"_x[0-9a-fA-F]{4}_",m=>"_x005F_"+m.Value.Substring(1));var result=new StringBuilder();
   for(int i=0;i<text.Length;i++){char ch=text[i];if(Char.IsHighSurrogate(ch)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){result.Append(ch);result.Append(text[++i]);}else if(XmlConvert.IsXmlChar(ch))result.Append(ch);else result.Append("_x"+((int)ch).ToString("X4")+"_");}return result.ToString();
  }
  static int BodyStyle(TaskExportField field,object value,bool alternate){
   if(field==TaskExportField.Status&&((value as string)=="已暂停"||(value as string)=="已关闭"))return alternate?30:29;
   int offset=alternate?12:0;int kind=0;
   if(field==TaskExportField.Number||field==TaskExportField.InGantt)kind=1;
   else if(field==TaskExportField.Note||field==TaskExportField.Requirements)kind=2;
   else if(field==TaskExportField.Start||field==TaskExportField.End)kind=3;
   else if(field==TaskExportField.Created||field==TaskExportField.Updated)kind=4;
   else if(field==TaskExportField.Priority){kind=5+Math.Max(0,Array.IndexOf(Model.Priorities,value as string));}
   else if(field==TaskExportField.Status){kind=(value as string)=="已完成"?9:(value as string)=="执行中"?10:11;}
   return 4+offset+kind;
  }
  static XElement Styles(){
   var fonts=new XElement(S+"fonts");
   Action<string,bool,int> font=(color,bold,size)=>{var f=new XElement(S+"font",new XElement(S+"sz",new XAttribute("val",size)),new XElement(S+"color",new XAttribute("rgb","FF"+color)),new XElement(S+"name",new XAttribute("val","Microsoft YaHei")));if(bold)f.Add(new XElement(S+"b"));fonts.Add(f);};
   font("243247",false,11);font("243247",true,16);font("64748B",false,11);font("FFFFFF",true,11);
   foreach(string color in new[]{"BE3445","AE610F","3267BE","64748B","258871","4F5FEF"})font(color,true,11);fonts.SetAttributeValue("count",fonts.Elements().Count());
   var fills=new XElement(S+"fills",new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","none"))),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","gray125"))));
   foreach(string color in new[]{"354568","FFFFFF","F3F6FC"})fills.Add(new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","solid"),new XElement(S+"fgColor",new XAttribute("rgb","FF"+color)),new XElement(S+"bgColor",new XAttribute("indexed",64)))));fills.SetAttributeValue("count",fills.Elements().Count());
   // 表头与数据区使用统一的黑色细实线，报告标题区域保持无边框。
   var borders=new XElement(S+"borders",new XAttribute("count",2),new XElement(S+"border",new XElement(S+"left"),new XElement(S+"right"),new XElement(S+"top"),new XElement(S+"bottom"),new XElement(S+"diagonal")),
    new XElement(S+"border",new[]{"left","right","top","bottom"}.Select(side=>new XElement(S+side,new XAttribute("style","thin"),new XElement(S+"color",new XAttribute("rgb","FF000000")))),new XElement(S+"diagonal")));
   var styles=new XElement(S+"cellXfs");styles.Add(Format(0,0,0,0,"left","center"),Format(1,0,0,0,"left","center"),Format(2,0,0,0,"left","center"),Format(3,2,1,0,"center","center"));
   for(int stripe=0;stripe<2;stripe++)for(int kind=0;kind<12;kind++){
    int fontId=kind>=5&&kind<=8?kind-1:kind==9?8:kind==10?9:0;int number=kind==3?164:kind==4?165:0;
    styles.Add(Format(fontId,3+stripe,1,number,kind==0||kind==2?"left":"center",kind==2||kind>=9?"top":"center"));
   }
   styles.Add(Format(2,0,0,0,"right","center"),Format(2,3,1,0,"center","top"),Format(2,4,1,0,"center","top"));styles.SetAttributeValue("count",styles.Elements().Count());
   return new XElement(S+"styleSheet",new XElement(S+"numFmts",new XAttribute("count",2),new XElement(S+"numFmt",new XAttribute("numFmtId",164),new XAttribute("formatCode","yyyy-mm-dd")),new XElement(S+"numFmt",new XAttribute("numFmtId",165),new XAttribute("formatCode","yyyy-mm-dd hh:mm"))),fonts,fills,borders,
    new XElement(S+"cellStyleXfs",new XAttribute("count",1),new XElement(S+"xf",new XAttribute("numFmtId",0),new XAttribute("fontId",0),new XAttribute("fillId",0),new XAttribute("borderId",0))),styles,
    new XElement(S+"cellStyles",new XAttribute("count",1),new XElement(S+"cellStyle",new XAttribute("name","Normal"),new XAttribute("xfId",0),new XAttribute("builtinId",0))));
  }
  static XElement Format(int font,int fill,int border,int number,string horizontal,string vertical){return new XElement(S+"xf",new XAttribute("numFmtId",number),new XAttribute("fontId",font),new XAttribute("fillId",fill),new XAttribute("borderId",border),new XAttribute("xfId",0),new XAttribute("applyNumberFormat",1),new XAttribute("applyFont",1),new XAttribute("applyFill",1),new XAttribute("applyBorder",1),new XAttribute("applyAlignment",1),new XElement(S+"alignment",new XAttribute("horizontal",horizontal),new XAttribute("vertical",vertical),new XAttribute("wrapText",1)));}
  static XElement Relationship(string id,string type,string target){return new XElement(P+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/"+type),new XAttribute("Target",target));}
  static XElement ContentType(XNamespace ns,string path,string kind){return new XElement(ns+"Override",new XAttribute("PartName",path),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml."+kind+"+xml"));}
  static void Write(ZipArchive zip,string name,XElement element){using(var stream=zip.CreateEntry(name,CompressionLevel.Optimal).Open())new XDocument(new XDeclaration("1.0","utf-8","yes"),element).Save(stream);}
 }
}
