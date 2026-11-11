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
  static void VerifyProgressAnchors(string path,XDocument sheet,Action<bool,string> check){
   XNamespace drawing="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
   var shapes=Part(path,"xl/drawings/progress.xml").Root.Elements(drawing+"oneCellAnchor").ToList();
   var heights=sheet.Descendants(S+"row").Where(r=>(int)r.Attribute("r")>=4).ToDictionary(r=>(int)r.Attribute("r"),r=>(double)r.Attribute("ht"));
   Func<XElement,int> physicalRow=anchor=>1+(int)anchor.Element(drawing+"from").Element(drawing+"row");
   Func<XElement,double> offset=anchor=>(double)anchor.Element(drawing+"from").Element(drawing+"rowOff")/12700;
   check(shapes.All(anchor=>heights.ContainsKey(physicalRow(anchor))&&offset(anchor)>=0&&offset(anchor)<heights[physicalRow(anchor)])&&shapes.Any(anchor=>physicalRow(anchor)>4),"八行合并的状态图锚点都在实际物理行内");
   Func<XElement,double> absolute=anchor=>heights.Where(r=>r.Key<physicalRow(anchor)).Sum(r=>r.Value)+offset(anchor);
   Func<string,XElement> shape=prefix=>shapes.First(a=>((string)a.Descendants(drawing+"cNvPr").Single().Attribute("name")).StartsWith(prefix,StringComparison.Ordinal));
   double circleTop=absolute(shape("节点-"));
   double labelTop=absolute(shape("步骤-"));
   check(labelTop-circleTop>14&&Math.Abs(circleTop+(41*0.75/2)-heights.Values.Sum()/2)<1,"多行状态圆点和标签保持间隔，整体在任务行组居中");
  }
  static void VerifyCompleteTimeline(string folder,Action<bool,string> check){
   var data=Model.Sample();var task=data.Tasks[0];data.Tasks=new List<TaskItem>{task};
   task.History.Clear();task.Note="任务创建时的说明";
   var kinds=new[]{"创建任务","状态变更","调整甘特图排期","拖动甘特图排期","任务修改","调整甘特图排期","清空计划时间","调整甘特图排期"};
   var at=new DateTimeOffset(2026,10,31,13,44,0,TimeSpan.FromHours(8));
   for(int i=0;i<kinds.Length;i++){
    task.Status=i==0?"待执行":"执行中";
    string before=i==0?"":i==1?"待执行":task.Status;
    string note=i==5?"等待联调资料":i==7?"资料到齐，恢复排期":null;
    Model.Record(data,task,kinds[i],before,i==3?"由 11 月 2 日移至 11 月 3 日":"",note);
    task.History[0].At=at.AddMinutes(i).ToString("o");
   }
   var query=new TaskQuery(data.CurrentProjectId,"全部","全部","");
   string path=Path.Combine(folder,"任务导出-8条时间线.xlsx");
   var fields=TaskExcelExporter.Columns.Select(c=>c.Field);
   TaskExcelExporter.Export(path,data,query,fields);
   var sheet=Part(path,"xl/worksheets/sheet1.xml");
   int exported=sheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4);
   check(Model.TimelineNodeCount(task)==8&&exported==8,"详情有 8 条时间线且只有 2 条手填备注，Excel 实际展开 "+exported+" 条，应为 8 条");
   for(int i=0;i<kinds.Length;i++){
    string cell=Text(sheet,"J"+(4+i));
    string title=at.AddMinutes(i).LocalDateTime.ToString("yyyy-MM-dd HH:mm")+" · "+kinds[i];
    check(cell.StartsWith(title,StringComparison.Ordinal),"时间线第 "+(i+1)+" 行保留事件与时间："+kinds[i]);
   }
   check(Text(sheet,"J4").Contains(task.Note)&&Text(sheet,"J5").Contains("待执行 → 执行中")&&Text(sheet,"J7").Contains("由 11 月 2 日移至 11 月 3 日")&&Text(sheet,"J9").Contains("等待联调资料")&&Text(sheet,"J11").Contains("资料到齐，恢复排期"),"创建备注、状态变化、排期明细和两条变更备注完整写入同一列");
   VerifyProgressAnchors(path,sheet,check);

   var timelineColumn=new[]{TaskExportField.Timeline};
   string statuses=Path.Combine(folder,"任务导出-仅状态变更.xlsx");
   TaskExcelExporter.Export(statuses,data,query,timelineColumn,new[]{"状态变更"});
   var statusSheet=Part(statuses,"xl/worksheets/sheet1.xml");
   check(Text(statusSheet,"A4").Contains("待执行 → 执行中")&&Text(statusSheet,"A4").EndsWith("备注：无")&&statusSheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==1,"仅勾选状态变更时只导出匹配事件，空备注显示无");
   string notes=Path.Combine(folder,"任务导出-仅备注记录.xlsx");
   TaskExcelExporter.Export(notes,data,query,timelineColumn,new[]{TaskTimelineTypes.Notes});
   var noteSheet=Part(notes,"xl/worksheets/sheet1.xml");
   var noteContents=new[]{task.Note,"等待联调资料","资料到齐，恢复排期"};
   var noteEntries=Model.TimelineEntries(task).Where(h=>h.Note!=null).ToList();
   for(int i=0;i<noteContents.Length;i++){
    string cell=Text(noteSheet,"A"+(4+i));
    check(cell.StartsWith(Model.TimelineHeading(noteEntries[i])+"\n")&&cell.EndsWith("备注："+noteContents[i]),"仅备注记录的第 "+(i+1)+" 行保留原操作类型和备注");
   }
   check(noteSheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==3,"仅备注记录时只导出三条实际备注");
   string mixed=Path.Combine(folder,"任务导出-排期与备注.xlsx");
   TaskExcelExporter.Export(mixed,data,query,timelineColumn,new[]{"调整甘特图排期",TaskTimelineTypes.Notes});
   var mixedSheet=Part(mixed,"xl/worksheets/sheet1.xml");
   check(mixedSheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==4&&Text(mixedSheet,"A4").Contains(" · 创建任务\n")&&Text(mixedSheet,"A5").EndsWith("备注：无")&&Text(mixedSheet,"A6").Contains(" · 调整甘特图排期\n备注：等待联调资料"),"同时选择排期和备注时，重合节点只写一次，每条都保留事件类型与备注");
   string noMatch=Path.Combine(folder,"任务导出-无匹配时间线.xlsx");
   TaskExcelExporter.Export(noMatch,data,query,new[]{TaskExportField.Name,TaskExportField.Timeline},new[]{"需求追加"});
   var noMatchSheet=Part(noMatch,"xl/worksheets/sheet1.xml");
   check(Text(noMatchSheet,"A4")==task.Name&&Text(noMatchSheet,"B4")==""&&noMatchSheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==1,"没有匹配类型时保留任务行，时间线为空");
   task.History.RemoveAll(h=>h.Kind=="创建任务");
   string legacy=Path.Combine(folder,"任务导出-旧版备注.xlsx");TaskExcelExporter.Export(legacy,data,query,new[]{TaskExportField.Timeline});
   var legacySheet=Part(legacy,"xl/worksheets/sheet1.xml");
   check(Text(legacySheet,"A4")=="创建时间未记录 · 旧版备注\n备注："+task.Note&&legacySheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==8,"没有创建节点的旧任务仍导出原备注和所有历史，不虚构创建时间");
  }
  public static void Run(string folder,Action<bool,string> check,Action<Action,string> fails){
   VerifyCompleteTimeline(folder,check);
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
   Model.Record(data,first,"状态变更","待讨论","通过任务编辑更新状态。","等待审批。\n审批后恢复。");first.History[0].At="2026-10-22T09:00:00+08:00";
   Model.Record(data,first,"调整甘特图排期",first.Status,"自动明细","审批通过，恢复排期。");first.History[0].At="2026-10-22T12:00:00+08:00";
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
   check(header.Descendants(S+"t").Select(t=>t.Value).SequenceEqual(new[]{"序号","所属项目","任务名称","优先级","状态","负责人","计划开始日期","计划结束日期","是否纳入当前排期","任务时间线","创建时间","最近更新时间"}),"导出只提供一个任务时间线表头，三类历史内容不再分列");
   check(sheet.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==31,"26 项匹配任务共展开 31 行，不受列表分页限制");
   check(Text(sheet,"J6").Contains("需求追加")&&Text(sheet,"J6").Contains("补充日志记录")&&Text(sheet,"J8").Contains("核对导出文件内容")&&Text(sheet,"J9").Contains("完成回归验收"),"追加需求在统一时间线中独占一行并保留全文");
   check(Text(sheet,"J5").Contains("待讨论 → 执行中")&&Text(sheet,"J5").Contains("通过任务编辑更新状态。\n备注：等待审批。\n审批后恢复。")&&Text(sheet,"J7").Contains("自动明细\n备注：审批通过，恢复排期。"),"状态和排期的自动明细及手填备注保留在对应事件行");
   var merged=sheet.Descendants(S+"mergeCell").Select(m=>(string)m.Attribute("ref")).ToList();
   check(merged.Contains("A4:A9")&&merged.Contains("C4:C9")&&merged.Contains("K4:K9")&&merged.Contains("L4:L9")&&!merged.Any(m=>m.StartsWith("J4:")),"同一任务的基础信息纵向合并，六条时间线独立占行");
   check(!sheet.Descendants(S+"f").Any()&&Text(sheet,"C4")=="=SUM(1,2) <&> _x005F_x0041_ 中文😀"&&Text(sheet,"J4").Contains("\n验收要求"),"任务文本以字符串写入，保留中文、emoji、换行及 OOXML 转义字面量且不执行公式");
   check(sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="G4").Element(S+"v").Value==new DateTime(2026,10,20).ToOADate().ToString("R",CultureInfo.InvariantCulture),"计划日期存为可识别的 Excel 日期数值");
   var created=DateTimeOffset.Parse("2026-10-20T09:00:00+08:00").LocalDateTime.ToOADate();var updated=DateTimeOffset.Parse("2026-10-24T16:00:00+08:00").LocalDateTime.ToOADate();
   check((double)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="K4").Element(S+"v")==created&&(double)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="L4").Element(S+"v")==updated,"创建时间与最近更新时间来自历史记录，按实际时间排序");
   int secondRow=(int)sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")!="C3"&&String.Concat(c.Descendants(S+"t").Select(t=>t.Value))==second.Name).Parent.Attribute("r");
   check(Text(sheet,"F"+secondRow)=="历史负责人（已删除）"&&Text(sheet,"J"+secondRow)==""&&!sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")=="K"+secondRow).Elements().Any(),"无历史内容也保留任务行，已删除负责人正确显示且缺失创建时间留空");

   var info=sheet.Descendants(S+"row").Single(r=>(int)r.Attribute("r")==2);string infoText=String.Concat(info.Descendants(S+"t").Select(t=>t.Value));check(Text(sheet,"A2")=="销冠项目 / 任务清单"&&infoText.Contains("26 项任务")&&infoText.Contains("导出于 ")&&!infoText.Contains("状态：")&&!infoText.Contains("优先级：")&&!infoText.Contains("关键词：")&&!sheet.Descendants(S+"pane").Any(),"项目并入标题、任务数与导出时间在同一报告头，表头不冻结");
   string filtered=Path.Combine(folder,"任务导出-筛选与选列.xlsx");TaskExcelExporter.Export(filtered,data,combined,new[]{TaskExportField.Timeline,TaskExportField.Name});var small=Part(filtered,"xl/worksheets/sheet1.xml");
   check(Text(small,"A3")=="任务时间线"&&Text(small,"B3")=="任务名称"&&small.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==1&&Text(small,"B4")=="功能验收 03","按用户顺序导出勾选列与匹配任务，未勾选列不占位");
   string reordered=Path.Combine(folder,"任务导出-调整表头顺序.xlsx");
   TaskExcelExporter.Export(reordered,data,all,new[]{TaskExportField.Timeline,TaskExportField.Name,TaskExportField.Start,TaskExportField.End,TaskExportField.Status,TaskExportField.Name});var ordered=Part(reordered,"xl/worksheets/sheet1.xml");
   check(Text(ordered,"A3")=="任务时间线"&&Text(ordered,"B3")=="任务名称"&&Text(ordered,"C3")=="计划开始日期"&&Text(ordered,"E3")=="状态"&&ordered.Descendants(S+"row").Single(r=>(int)r.Attribute("r")==3).Elements(S+"c").Count()==5,"任意调整表头顺序并去重，Excel 列顺序与选择顺序一致");
   check(Text(ordered,"A6").Contains("补充日志记录")&&Text(ordered,"B4")==first.Name.Replace("_x0041_","_x005F_x0041_")&&ordered.Descendants(S+"mergeCell").Any(m=>(string)m.Attribute("ref")=="B4:B9")&&!ordered.Descendants(S+"mergeCell").Any(m=>((string)m.Attribute("ref")).StartsWith("A4:")),"时间线移到第一列后仍逐行展示，基础信息按新位置正确合并");
   check(Part(reordered,"xl/drawings/progress.xml").Descendants(drawingNs+"from").All(anchor=>(int)anchor.Element(drawingNs+"col")==4),"表头拖动后进度图跟随状态列的位置");
   var restoredOrder=every.Reverse().Select(f=>f.ToString()).ToList();
   var preferences=new TaskExportPreferences{Order=restoredOrder,Selected=new List<string>{"Name","Timeline"},TimelineTypes=new List<string>{"状态变更",TaskTimelineTypes.Notes}};
   string preferenceFolder=Path.Combine(folder,"export-preferences");using(var preferenceStore=new Store(preferenceFolder))preferenceStore.Commit(d=>d.TaskExcelPreferences=preferences);
   using(var preferenceStore=new Store(preferenceFolder)){
    var saved=preferenceStore.Data.TaskExcelPreferences;check(saved.OrderedFields().SequenceEqual(every.Reverse())&&saved.IsSelected(TaskExportField.Name)&&saved.IsSelected(TaskExportField.Timeline)&&!saved.IsSelected(TaskExportField.Number)&&saved.TimelineTypes.SequenceEqual(preferences.TimelineTypes),"表头顺序、勾选状态和时间线类型保存到工作区，重启后恢复");
    check(Store.Decode(Store.Encode(preferenceStore.Data)).TaskExcelPreferences.Order.SequenceEqual(restoredOrder),"表头偏好随完整备份与恢复保留");
   }
   var compatible=new TaskExportPreferences{Order=new List<string>{"Requirements","Name","Note","ChangeNotes","Unknown"},Selected=new List<string>{"Name","ChangeNotes"}};
   check(compatible.OrderedFields().Count==12&&compatible.OrderedFields().Take(2).SequenceEqual(new[]{TaskExportField.Timeline,TaskExportField.Name})&&compatible.IsSelected(TaskExportField.Timeline),"旧三列映射为一列，保留最前位置，任一旧列选中即选中时间线");
   compatible.Selected.Remove("ChangeNotes");
   check(!compatible.IsSelected(TaskExportField.Timeline),"旧三列均未选中时保持精简导出偏好");
   string compactFile=Path.Combine(folder,"任务导出-不含时间线.xlsx");TaskExcelExporter.Export(compactFile,data,all,new[]{TaskExportField.Name});var compact=Part(compactFile,"xl/worksheets/sheet1.xml");
   check(compact.Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==26&&!compact.Descendants(S+"mergeCell").Any(),"不选时间线时，每个普通任务仅一行，单列导出不生成无效合并");
   fails(()=>TaskExcelExporter.Export(Path.Combine(folder,"不应生成.xlsx"),data,all,new TaskExportField[0]),"拒绝零选列导出");
   fails(()=>TaskExcelExporter.Export(Path.Combine(folder,"不应生成.xlsx"),data,all,new[]{TaskExportField.Timeline},new string[0]),"勾选时间线时拒绝空的类型选择");
   fails(()=>TaskExcelExporter.Export(Path.Combine(folder,"不应生成.xlsx"),data,new TaskQuery("current","全部","全部","不存在"),every),"拒绝无结果导出");
   check(!File.Exists(Path.Combine(folder,"不应生成.xlsx"))&&unchanged.SequenceEqual(Store.Encode(data)),"导出与校验失败均不修改项目数据，不生成失败文件");
   // 小样用于导出样式检查，避免示例数据过多影响视觉核对。
   var preview=Store.Decode(unchanged);preview.Tasks=preview.Tasks.Take(3).ToList();preview.Tasks[0].Name="功能权限与数据导出验收";preview.Tasks[2].Name="录音设备调研";preview.Tasks[2].Status="已完成";preview.Tasks[2].Priority="P2";
   TaskExcelExporter.Export(Path.Combine(folder,"任务导出-样式示例.xlsx"),preview,all,every);
   var longData=Store.Decode(unchanged);longData.Tasks=longData.Tasks.Take(1).ToList();longData.Tasks[0].Note=String.Concat(Enumerable.Repeat("超长说明与😀中文内容。\n",4000));
   string longFile=Path.Combine(folder,"任务导出-超长详情.xlsx");TaskExcelExporter.Export(longFile,longData,all,new[]{TaskExportField.Name,TaskExportField.Timeline});var longSheet=Part(longFile,"xl/worksheets/sheet1.xml");
   string recovered=String.Concat(longSheet.Descendants(S+"c").Where(c=>((string)c.Attribute("r")).StartsWith("B")&&(int)c.Parent.Attribute("r")>=4).Descendants(S+"t").Select(t=>t.Value));
   check(recovered.Contains("备注："+longData.Tasks[0].Note)&&longSheet.Descendants(S+"row").All(r=>(double?)r.Attribute("ht")<=409)&&longSheet.Descendants(S+"t").All(t=>t.Value.Length<=32767),"超长多行说明分为续行，完整保留内容与 emoji 且符合 Excel 行高和单元格限制");
   TaskExcelExporter.Export(Path.Combine(folder,"任务导出-大量任务.xlsx"),data,status,new[]{TaskExportField.Number,TaskExportField.Name,TaskExportField.Timeline});
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

   var dated=Store.Decode(unchanged);
   for(int i=0;i<=20;i++){
    dated.Tasks[i].Start=i==20?"2026-11-03":"2026-10-20";
    dated.Tasks[i].End=i==20?"2026-11-10":"2026-10-31";
   }
   dated.Tasks[21].Start="2026-10-20";dated.Tasks[21].End="2026-10-30";
   dated.Tasks[22].Start="2026-11-04";dated.Tasks[22].End="2026-11-10";
   dated.Tasks[26].Start="2026-10-31";dated.Tasks[26].End="2026-11-03";
   var plan=new TaskQuery("current",Model.Statuses,Model.Priorities,"",new DateTime(2026,10,31),new DateTime(2026,11,3));
   var matches=plan.Apply(dated);
   check(matches.Count==21&&new HashSet<string>(matches.Select(t=>t.Id)).SetEquals(dated.Tasks.Take(21).Select(t=>t.Id)),"计划时间筛选包含两端重叠任务，排除范围外、无日期及其他项目任务");
   string planFile=Path.Combine(folder,"任务导出-计划时间.xlsx");
   TaskExcelExporter.Export(planFile,dated,plan,new[]{TaskExportField.Name});
   check(Part(planFile,"xl/worksheets/sheet1.xml").Descendants(S+"row").Count(r=>(int)r.Attribute("r")>=4)==21,"Excel 导出跨页的全部计划时间匹配任务");
   string planXml=Path.Combine(folder,"项目导出-计划时间.xml");
   Exporter.Xml(planXml,dated,plan.ProjectId,matches);
   var exported=XDocument.Load(planXml).Descendants("Task").ToList();
   check(exported.Select(t=>(string)t.Attribute("id")).SequenceEqual(matches.Select(t=>t.Id))&&exported.Select(t=>(string)t.Element("Start")).SequenceEqual(matches.Select(t=>t.Start)),"筛选后的 XML 保留匹配任务和原始计划日期");
  }
 }
}
