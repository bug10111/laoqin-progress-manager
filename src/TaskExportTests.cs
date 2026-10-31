// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace LaoQin.ProgressManager {
 internal static class TaskExportTests {
  static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  static XDocument Part(string path,string part){using(var zip=ZipFile.OpenRead(path))using(var stream=zip.GetEntry(part).Open())return XDocument.Load(stream);}
  static string Text(XDocument sheet,string address){var cell=sheet.Descendants(S+"c").FirstOrDefault(c=>(string)c.Attribute("r")==address);return cell==null?"":String.Concat(cell.Descendants(S+"t").Select(t=>t.Value));}
  public static void Run(string folder,Action<bool,string> check,Action<Action,string> fails){
   var data=new AppData();data.CurrentProjectId="current";data.Projects.Add(new Project{Id="current",Name="销冠项目"});data.Projects.Add(new Project{Id="other",Name="其他项目"});
   data.People.Add(new Person{Id="person",Name="李明"});data.People.Add(new Person{Id="deleted",Name="历史负责人",Deleted=true});
   for(int i=0;i<27;i++){
    var task=new TaskItem{Id="task-"+i,ProjectId=i==26?"other":"current",Name="功能验收 "+i.ToString("D2"),Priority=i%2==0?"P0":"P1",Status=i<25?"待执行":"已完成",PersonId=i==0?"person":"",Start=i==0?"2026-10-20":"",End=i==0?"2026-10-31":"",Note=i==3?"说明包含筛选词：验收目标":"初始说明",InGantt=i==0};
    Model.Record(data,task,"创建任务","","");task.History[0].At="2026-10-20T09:00:00+08:00";data.Tasks.Add(task);
   }
   var all=new TaskQuery("current","全部","全部","");var status=new TaskQuery("current","待执行","全部","");var combined=new TaskQuery("current","待执行","P1"," 验收目标 ");
   check(all.Apply(data).Count==26&&status.Apply(data).Count==25,"任务导出按当前项目查询全部结果，包含超过一页的任务且隔离其他项目");
   check(combined.Apply(data).Count==1&&combined.Apply(data)[0].Id=="task-3","状态、优先级与关键词组合筛选并支持说明搜索");
   check(new TaskQuery("current","已完成","P1","功能").Apply(data).Single().Id=="task-25","组合筛选支持任务名称");
   check(new TaskQuery("current","全部","全部","不存在").Apply(data).Count==0,"无匹配任务时查询返回空集合");
   var first=data.Tasks[0];first.Status="执行中";first.Name="=SUM(1,2) <&> _x0041_ 中文😀";first.Note="初始需求：完成接口权限校验。\n验收要求：保留中文、换行和特殊字符。";
   Model.Record(data,first,"需求追加",first.Status,"补充日志记录。\n包含操作人和操作时间。");first.History[0].At="2026-10-22T10:30:00+08:00";
   Model.Record(data,first,"状态变更","待讨论","不应混入追加需求");first.History[0].At="2026-10-22T09:00:00+08:00";
   Model.Record(data,first,"需求追加",first.Status,"核对导出文件内容，确认所有筛选结果完整。");first.History[0].At="2026-10-23T14:00:00+08:00";
   Model.Record(data,first,"需求追加",first.Status,"完成回归验收并归档。");first.History[0].At="2026-10-24T16:00:00+08:00";
   var second=data.Tasks[1];second.PersonId="deleted";second.Priority="P0";second.Name="00 没有追加需求的任务";second.Note="";second.History.Clear();
   byte[] unchanged=Store.Encode(data);var every=TaskExcelExporter.Columns.Select(c=>c.Field).ToArray();
   string full=Path.Combine(folder,"任务导出-全部字段.xlsx");TaskExcelExporter.Export(full,data,all,every);var sheet=Part(full,"xl/worksheets/sheet1.xml");
   using(var zip=ZipFile.OpenRead(full))check(zip.Entries.Count==8&&zip.Entries.All(e=>{using(var stream=e.Open()){XDocument.Load(stream);return true;}}),"生成标准 XLSX 包，所有关系、样式、进度图与工作表 XML 可读取");
   XNamespace drawingNs="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing",shapeNs="http://schemas.openxmlformats.org/drawingml/2006/main";var drawing=Part(full,"xl/drawings/progress.xml");
   var circles=drawing.Descendants(shapeNs+"prstGeom").Where(g=>(string)g.Attribute("prst")=="ellipse").ToList();
   check(circles.Count==26*5&&(string)circles[2].Parent.Element(shapeNs+"solidFill").Element(shapeNs+"srgbClr").Attribute("val")=="4D65E6"&&circles.Skip(3).Take(2).All(c=>c.Parent.Element(shapeNs+"noFill")!=null),"Excel 每项任务包含五个矢量圆点，当前步骤高亮、未来步骤空心");
   check(drawing.Descendants(shapeNs+"t").Take(5).Select(t=>t.Value).SequenceEqual(Model.ProgressStatuses)&&drawing.Descendants(drawingNs+"cNvPr").Count(n=>((string)n.Attribute("name")).StartsWith("连接线"))==26*4,"步骤文字与全局状态顺序一致，每相邻圆点通过线段连接");
   var header=sheet.Descendants(S+"row").Single(r=>(int)r.Attribute("r")==3);
   check(header.Descendants(S+"t").Select(t=>t.Value).SequenceEqual(new[]{"序号","所属项目","任务名称","优先级","状态","负责人","计划开始日期","计划结束日期","是否纳入当前排期","备注","追加需求","创建时间","最近更新时间"}),"导出包含确认的全部 13 个表头且顺序稳定");
   check(sheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==28,"26 项任务按三次追加展开为 28 行，不受列表分页限制");
   check(Text(sheet,"K4").StartsWith("1. ")&&Text(sheet,"K4").Contains("补充日志记录")&&Text(sheet,"K5").Contains("核对导出文件内容")&&Text(sheet,"K6").Contains("完成回归验收"),"每次追加独占一行，按时间顺序保留序号、时间及完整内容");
   var merged=sheet.Descendants(S+"mergeCell").Select(m=>(string)m.Attribute("ref")).ToList();
   check(merged.Contains("A4:A6")&&merged.Contains("C4:C6")&&merged.Contains("J4:J6")&&merged.Contains("L4:L6")&&merged.Contains("M4:M6")&&!merged.Any(m=>m.StartsWith("K4:")),"同一任务的基础信息纵向合并，追加需求列逐行独立保留");
   check(!sheet.Descendants(S+"f").Any()&&Text(sheet,"C4")=="=SUM(1,2) <&> _x005F_x0041_ 中文😀"&&Text(sheet,"J4").Contains("\n验收要求"),"任务文本以字符串写入，保留中文、emoji、换行及 OOXML 转义字面量且不执行公式");
   check(sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="G4").Element(S+"v").Value==new DateTime(2026,10,20).ToOADate().ToString("R",CultureInfo.InvariantCulture),"计划日期存为可识别的 Excel 日期数值");
   var created=DateTimeOffset.Parse("2026-10-20T09:00:00+08:00").LocalDateTime.ToOADate();var updated=DateTimeOffset.Parse("2026-10-24T16:00:00+08:00").LocalDateTime.ToOADate();
   check((double)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="L4").Element(S+"v")==created&&(double)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="M4").Element(S+"v")==updated,"创建时间与最近更新时间来自历史记录，按实际时间排序");
   int secondRow=(int)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")!="C3"&&String.Concat(c.Descendants(S+"t").Select(t=>t.Value))==second.Name).Parent.Attribute("r");
   check(Text(sheet,"F"+secondRow)=="历史负责人（已删除）"&&Text(sheet,"K"+secondRow)==""&&!sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="L"+secondRow).Elements().Any(),"无追加需求也保留任务行，已删除负责人正确显示且缺失创建时间留空");

   var info=sheet.Descendants(S+"row").Single(r=>(int)r.Attribute("r")==2);string infoText=String.Concat(info.Descendants(S+"t").Select(t=>t.Value));check(Text(sheet,"A2")=="销冠项目 / 任务清单"&&infoText.Contains("26 项任务")&&infoText.Contains("导出于 ")&&!infoText.Contains("状态：")&&!infoText.Contains("优先级：")&&!infoText.Contains("关键词：")&&!sheet.Descendants(S+"pane").Any(),"项目并入标题、任务数与导出时间在同一报告头，表头不冻结");
   string filtered=Path.Combine(folder,"任务导出-筛选与选列.xlsx");TaskExcelExporter.Export(filtered,data,combined,new[]{TaskExportField.Requirements,TaskExportField.Name});var small=Part(filtered,"xl/worksheets/sheet1.xml");
   check(Text(small,"A3")=="追加需求"&&Text(small,"B3")=="任务名称"&&small.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==1&&Text(small,"B4")=="功能验收 03","按用户顺序导出勾选列与匹配任务，未勾选列不占位");
   string reordered=Path.Combine(folder,"任务导出-调整表头顺序.xlsx");
   TaskExcelExporter.Export(reordered,data,all,new[]{TaskExportField.Requirements,TaskExportField.Name,TaskExportField.Start,TaskExportField.End,TaskExportField.Status,TaskExportField.Name});var ordered=Part(reordered,"xl/worksheets/sheet1.xml");
   check(Text(ordered,"A3")=="追加需求"&&Text(ordered,"B3")=="任务名称"&&Text(ordered,"C3")=="计划开始日期"&&Text(ordered,"E3")=="状态"&&ordered.Descendants(S+"row").Single(r=>(int)r.Attribute("r")==3).Elements(S+"c").Count()==5,"任意调整表头顺序并去重，Excel 列顺序与选择顺序一致");
   check(Text(ordered,"A4").Contains("补充日志记录")&&Text(ordered,"B4")==first.Name.Replace("_x0041_","_x005F_x0041_")&&ordered.Descendants(S+"mergeCell").Any(m=>(string)m.Attribute("ref")=="B4:B6")&&!ordered.Descendants(S+"mergeCell").Any(m=>((string)m.Attribute("ref")).StartsWith("A4:")),"追加需求移到第一列后仍逐行展示，基础信息按新位置正确合并");
   check(Part(reordered,"xl/drawings/progress.xml").Descendants(drawingNs+"from").All(anchor=>(int)anchor.Element(drawingNs+"col")==4),"表头拖动后进度图跟随状态列的位置");
   var restoredOrder=every.Reverse().Select(f=>f.ToString()).ToList();
   var preferences=new TaskExportPreferences{Order=restoredOrder,Selected=new List<string>{"Name","Requirements"}};
   string preferenceFolder=Path.Combine(folder,"export-preferences");using(var preferenceStore=new Store(preferenceFolder))preferenceStore.Commit(d=>d.TaskExcelPreferences=preferences);
   using(var preferenceStore=new Store(preferenceFolder)){
    var saved=preferenceStore.Data.TaskExcelPreferences;check(saved.OrderedFields().SequenceEqual(every.Reverse())&&saved.IsSelected(TaskExportField.Name)&&saved.IsSelected(TaskExportField.Requirements)&&!saved.IsSelected(TaskExportField.Number),"表头顺序和勾选状态保存到工作区，重启后恢复");
    check(Store.Decode(Store.Encode(preferenceStore.Data)).TaskExcelPreferences.Order.SequenceEqual(restoredOrder),"表头偏好随完整备份与恢复保留");
   }
   var compatible=new TaskExportPreferences{Order=new List<string>{"Requirements","Name","Name","Unknown"},Selected=new List<string>{"Name"}};
   check(compatible.OrderedFields().Count==13&&compatible.OrderedFields().Take(2).SequenceEqual(new[]{TaskExportField.Requirements,TaskExportField.Name})&&!compatible.IsSelected(TaskExportField.Requirements)&&compatible.IsSelected(TaskExportField.Number),"字段偏好去重、忽略未知字段并自动补充新增字段");
   string noAppend=Path.Combine(folder,"任务导出-不选追加.xlsx");TaskExcelExporter.Export(noAppend,data,all,new[]{TaskExportField.Name});var compact=Part(noAppend,"xl/worksheets/sheet1.xml");
   check(compact.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==26&&!compact.Descendants(S+"mergeCell").Any(),"不选追加需求时，每个普通任务仅一行，单列导出不生成无效合并");
   fails(()=>TaskExcelExporter.Export(Path.Combine(folder,"不应生成.xlsx"),data,all,new TaskExportField[0]),"拒绝零选列导出");
   fails(()=>TaskExcelExporter.Export(Path.Combine(folder,"不应生成.xlsx"),data,new TaskQuery("current","全部","全部","不存在"),every),"拒绝无结果导出");
   check(!File.Exists(Path.Combine(folder,"不应生成.xlsx"))&&unchanged.SequenceEqual(Store.Encode(data)),"导出与校验失败均不修改项目数据，不生成失败文件");
   // 小样用于导出样式检查，避免示例数据过多影响视觉核对。
   var preview=Store.Decode(unchanged);preview.Tasks=preview.Tasks.Take(3).ToList();preview.Tasks[0].Name="功能权限与数据导出验收";preview.Tasks[2].Name="录音设备调研";preview.Tasks[2].Status="已完成";preview.Tasks[2].Priority="P2";
   TaskExcelExporter.Export(Path.Combine(folder,"任务导出-样式示例.xlsx"),preview,all,every);
   var longData=Store.Decode(unchanged);longData.Tasks=longData.Tasks.Take(1).ToList();longData.Tasks[0].Note=String.Concat(Enumerable.Repeat("超长说明与😀中文内容。\n",4000));
   string longFile=Path.Combine(folder,"任务导出-超长详情.xlsx");TaskExcelExporter.Export(longFile,longData,all,new[]{TaskExportField.Name,TaskExportField.Note,TaskExportField.Requirements});var longSheet=Part(longFile,"xl/worksheets/sheet1.xml");
   string recovered=String.Concat(longSheet.Descendants(S+"c").Where(c=>((string)c.Attribute("r")).StartsWith("B")&&(int)c.Parent.Attribute("r")>=4).Descendants(S+"t").Select(t=>t.Value));
   check(recovered==longData.Tasks[0].Note&&longSheet.Descendants(S+"row").All(r=>(double?)r.Attribute("ht")<=409)&&longSheet.Descendants(S+"t").All(t=>t.Value.Length<=32767),"超长多行说明分为续行，完整保留内容与 emoji 且符合 Excel 行高和单元格限制");
   TaskExcelExporter.Export(Path.Combine(folder,"任务导出-大量任务.xlsx"),data,status,new[]{TaskExportField.Number,TaskExportField.Name,TaskExportField.Requirements});
   var testingData=Store.Decode(unchanged);var testingTask=testingData.Tasks[0];testingTask.Status="测试中";Model.Record(testingData,testingTask,"状态变更","执行中","");testingData=Store.Decode(Store.Encode(testingData));
   var testingQuery=new TaskQuery("current","测试中","全部","");check(testingQuery.Apply(testingData).Single().Id==testingTask.Id&&Model.OrderTasks(new[]{testingData.Tasks[25],testingData.Tasks[0]}).First().Status=="测试中","测试中任务与历史可完整保存恢复，筛选准确且排在已完成前");
   string testingFile=Path.Combine(folder,"任务导出-测试中.xlsx");TaskExcelExporter.Export(testingFile,testingData,testingQuery,new[]{TaskExportField.Name,TaskExportField.Status});check(Text(Part(testingFile,"xl/worksheets/sheet1.xml"),"B4")==""&&Part(testingFile,"xl/drawings/progress.xml").Descendants(drawingNs+"cNvPr").Any(n=>((string)n.Attribute("name")).StartsWith("节点-测试中-当前")),"Excel 支持测试中状态筛选，进度图高亮当前步骤且不再重复显示状态文字");
   var testingDrawing=Part(testingFile,"xl/drawings/progress.xml");var testingCircles=testingDrawing.Descendants(shapeNs+"prstGeom").Where(g=>(string)g.Attribute("prst")=="ellipse").ToList();check(testingCircles.Take(3).All(c=>(string)c.Parent.Element(shapeNs+"solidFill").Element(shapeNs+"srgbClr").Attribute("val")=="8CB7A6")&&testingCircles[4].Parent.Element(shapeNs+"noFill")!=null,"当前测试中时，前面三步实心、已完成为空心");
   var paused=Store.Decode(Store.Encode(testingData)).Tasks[0];paused.Status="已暂停";Model.Record(testingData,paused,"状态变更","测试中","");check(Model.ProgressIndex(paused)==3&&Model.ProgressIndex(new TaskItem{Status="已关闭"})==-1,"暂停保留最近正常进度，无进度记录的关闭任务不虚构完成步骤");
   var selectedStatuses=new List<string>{"执行中","已完成"};var selectedPriorities=new List<string>{"P0","P1"};var multi=new TaskQuery("current",selectedStatuses,selectedPriorities,"");selectedStatuses.Clear();selectedPriorities.Clear();
   check(multi.Apply(data).Select(t=>t.Id).SequenceEqual(new[]{"task-0","task-25"}),"多选状态内按任意匹配，与多选优先级组合生效，查询独立保存筛选快照");
   check(new TaskQuery("current",new[]{"执行中","已完成"},new[]{"P0","P1"},"功能").Apply(data).Single().Id=="task-25","多选状态、优先级与关键词共同筛选且隔离其他项目");
   check(new TaskQuery("current",new string[0],Model.Priorities,"").Apply(data).Count==0&&new TaskQuery("current",Model.Statuses,new string[0],"").Apply(data).Count==0&&new TaskQuery("current",Model.Statuses,Model.Priorities,"").Apply(data).Count==26,"清空任一筛选得到空结果，全选两个筛选恢复当前项目全部任务");
   string multiFile=Path.Combine(folder,"任务导出-多选筛选.xlsx");TaskExcelExporter.Export(multiFile,data,multi,new[]{TaskExportField.Name,TaskExportField.Status,TaskExportField.Priority});var multiSheet=Part(multiFile,"xl/worksheets/sheet1.xml");
   check(Text(multiSheet,"A4")==first.Name.Replace("_x0041_","_x005F_x0041_")&&Text(multiSheet,"A5")==data.Tasks[25].Name&&multiSheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==2&&!String.Concat(multiSheet.Descendants(S+"t").Select(t=>t.Value)).Contains("状态：执行中、已完成"),"Excel 仅导出多选匹配结果且保持列表排序，不显示筛选摘要");
   string multiPages=Path.Combine(folder,"任务导出-多选全部分页.xlsx");TaskExcelExporter.Export(multiPages,data,new TaskQuery("current",new[]{"执行中","待执行"},new[]{"P0","P1"},""),new[]{TaskExportField.Name});check(Part(multiPages,"xl/worksheets/sheet1.xml").Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==25,"多选筛选导出全部 25 项匹配任务，包含所有分页");
  }
 }
}
