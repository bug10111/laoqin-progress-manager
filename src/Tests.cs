// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace LaoQin.ProgressManager {
 public static class Tests {
  static List<string> passed=new List<string>();
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed.Add("PASS: "+name);}
  static void Fails(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
  public static void Run(string folder){
   Directory.CreateDirectory(folder);string data=Path.Combine(folder,"local-data-"+Guid.NewGuid().ToString("N"));string originalId,chosenId,pendingId,originalStart;
   var priorityData=Model.Sample();
   string legacyJson=System.Text.RegularExpressions.Regex.Replace(Encoding.UTF8.GetString(Store.Encode(priorityData)),"\"Priority\":(?:\"[^\"]*\"|null),?", "");
   Check(Store.Decode(Encoding.UTF8.GetBytes(legacyJson)).Tasks.All(t=>t.Priority=="P0"),"legacy JSON without priority defaults all tasks to P0");
   var priorityTasks=priorityData.Tasks.Take(4).ToList();
   for(int i=0;i<4;i++)priorityTasks[i].Priority=Model.Priorities[3-i];
   Check(Model.OrderTasks(priorityTasks).Select(t=>t.Priority).SequenceEqual(Model.Priorities),"same status sorts P0 through P3 ahead of prefix and dates");
   priorityTasks[0].Status="待执行";priorityTasks[0].Priority="P0";
   Check(Model.OrderTasks(priorityTasks).Last()==priorityTasks[0],"status sorting takes precedence over priority");
   var priorityTarget=priorityTasks[1];Model.ReplaceSchedule(priorityData,priorityTarget.Id,new[]{priorityTarget.Id},DateTime.Today,DateTime.Today,priorityData.People[0].Id,"P2");
   Check(priorityTarget.Priority=="P2"&&priorityTarget.History[0].Priority=="P2"&&Store.Decode(Store.Encode(priorityData)).Tasks.First(t=>t.Id==priorityTarget.Id).Priority=="P2","Gantt priority edits persist in task, history and JSON");
   Model.ReplaceSchedule(priorityData,priorityTarget.Id,new[]{priorityTarget.Id},DateTime.Today,DateTime.Today,priorityData.People[0].Id);
   Check(priorityTarget.Priority=="P2","rescheduling preserves priority by default");
   Fails(()=>{var invalid=Store.Decode(Store.Encode(priorityData));invalid.Tasks[0].Priority="P4";Model.Check(invalid);},"reject priority outside P0 to P3");
   var timelineData=Model.Sample();var timelineTask=timelineData.Tasks[0];timelineTask.Note="";timelineTask.History.Clear();Model.Record(timelineData,timelineTask,"创建任务","","");timelineTask.History[0].At="2026-10-20T09:00:00+08:00";
   Model.Record(timelineData,timelineTask,"需求追加",timelineTask.Status,"核实是否通过，进度如何");timelineTask.History[0].At="2026-10-21T11:00:00+08:00";
   Model.Record(timelineData,timelineTask,"状态变更","待执行","");timelineTask.History[0].At="2026-10-21T02:00:00+00:00";
   var chronological=Model.Timeline(timelineTask);Check(chronological.Select(h=>h.Kind).SequenceEqual(new[]{"创建任务","状态变更","需求追加"}),"timeline orders creation, status and requirement records by timestamp including offsets");
   Check(chronological[0].Kind=="创建任务"&&String.IsNullOrEmpty(timelineTask.Note),"creation remains in timeline without an initial requirement");
   var restoredTimeline=Store.Decode(Store.Encode(timelineData)).Tasks[0];Check(Model.Timeline(restoredTimeline).Last().Detail=="核实是否通过，进度如何"&&restoredTimeline.History.Count==3,"combined timeline preserves original and appended records through JSON round-trip");
   using(var s=new Store(data)){
    Check(s.Data.Tasks.Count==16,"first-run sample and all local entities");Check(s.Data.Tasks.Count(t=>t.InGantt)==5,"five initial bars");
    originalId=s.Data.Tasks[0].Id;chosenId=s.Data.Tasks[1].Id;pendingId=s.Data.Tasks[5].Id;originalStart=s.Data.Tasks[0].Start;var a=DateTime.Today.AddDays(2);var b=a.AddDays(5);var person=s.Data.People[1].Id;
    Fails(()=>{using(var second=new Store(data)){}} ,"single-writer lock prevents lost updates");
    s.Commit(d=>Model.ReplaceSchedule(d,originalId,new[]{chosenId,pendingId},a,b,person));
    Check(!s.Data.Tasks[0].InGantt&&s.Data.Tasks[0].Start==originalStart,"replace removes original bar but preserves task and dates");
    Check(s.Data.Tasks.Count==16,"replacement does not delete any task");
    Check(s.Data.Tasks.First(t=>t.Id==pendingId).Status=="待执行","choosing pending task preserves its status");
    Check(s.Data.Tasks.Where(t=>t.Id==chosenId||t.Id==pendingId).All(t=>t.InGantt&&t.Start==Model.Date(a)&&t.End==Model.Date(b)&&t.PersonId==person),"multi-select applies inclusive dates and person to all chosen tasks");
    Check(s.Data.Tasks[0].History[0].Kind=="退出本次排期","original exit recorded in timeline");
    Check(s.Data.Tasks.First(t=>t.Id==pendingId).History[0].Kind=="调整甘特图排期","selected tasks receive schedule history");
    Check(s.Data.Tasks.All(t=>t.History.All(h=>DateTimeOffset.Parse(h.At)!=default(DateTimeOffset))),"timestamps contain parseable offset and date-time");
    byte[] before=Store.Encode(s.Data);Fails(()=>s.Commit(d=>Model.ReplaceSchedule(d,chosenId,new[]{pendingId},b,a,person)),"reject reversed date range");Check(before.SequenceEqual(Store.Encode(s.Data))&&before.SequenceEqual(File.ReadAllBytes(s.FilePath)),"rejected transaction leaves memory and disk unchanged");
    Fails(()=>s.Commit(d=>Model.ReplaceSchedule(d,chosenId,new string[0],a,b,person)),"reject empty task selection");
    Fails(()=>s.Commit(d=>{d.Tasks[0].Status="invalid";}),"reject unknown status");
    string backup=Path.Combine(folder,"完整备份.json");s.Backup(backup);s.Commit(d=>d.Tasks[0].Name="A < & B \" 中文任务");Check(s.Data.Tasks[0].Name.Contains("中文"),"unicode and XML-sensitive characters round-trip in JSON");s.Restore(backup);Check(s.Data.Tasks[0].Name=="知识库对接","complete backup restore");
    s.Commit(d=>{var t=d.Tasks[2];string old=t.Status;t.Status="已暂停";Model.Record(d,t,"状态变更",old,"测试状态变更记录");});Check(s.Data.Tasks[2].History[0].Before=="执行中"&&s.Data.Tasks[2].History[0].After=="已暂停","before and after status retained in timeline");
    Exporter.Xml(Path.Combine(folder,"项目导出.xml"),s.Data,s.Data.CurrentProjectId);var xml=XDocument.Load(Path.Combine(folder,"项目导出.xml"));Check(xml.Descendants("Task").Count()==16&&xml.Descendants("Change").Count()>16,"XML contains all tasks and complete timelines");
    var tasks=GanttView.Order(s.Data,s.Data.Tasks.Where(t=>t.InGantt),true);var pages=Exporter.Pages(tasks,tasks.Min(t=>Model.Parse(t.Start)),tasks.Max(t=>Model.Parse(t.End)));Exporter.Pdf(Path.Combine(folder,"甘特图.pdf"),s.Data,"智能业务平台",pages);Exporter.Png(Path.Combine(folder,"甘特图.png"),s.Data,"智能业务平台",new List<ChartPage>{pages[0]});Check(File.ReadAllBytes(Path.Combine(folder,"甘特图.pdf")).Take(5).SequenceEqual(Encoding.ASCII.GetBytes("%PDF-")),"PDF file header");Check(new FileInfo(Path.Combine(folder,"甘特图.png")).Length>20000,"high-resolution PNG");
    var large=Enumerable.Range(0,25).Select(i=>new TaskItem{Id=Model.Id(),Name="跨月长任务 "+(i+1),PersonId=person,Status="执行中",Start=Model.Date(a),End=Model.Date(a.AddDays(35)),InGantt=true}).ToList();var multi=Exporter.Pages(large,a,a.AddDays(35));Check(multi.Count==9,"pagination splits both dates and task rows");Exporter.Pdf(Path.Combine(folder,"多页甘特图.pdf"),s.Data,"跨月与多任务验证",multi);Exporter.Png(Path.Combine(folder,"多页图片.zip"),s.Data,"跨月与多任务验证",multi);
    s.Commit(d=>d.Tasks[0].Note="重启持久化验证");
   }
   using(var reopened=new Store(data)){Check(reopened.Data.Tasks[0].Note=="重启持久化验证","restart reloads persisted edits");Check(!reopened.Data.Tasks[0].InGantt,"excluded original remains excluded after restart");}
   File.WriteAllText(Path.Combine(data,"data.json"),"damaged-json");using(var recovered=new Store(data)){Check(recovered.RecoveryMessage.Length>0,"recover corrupt primary from automatic backup");Check(Directory.GetFiles(data,"damaged-*.json").Length==1,"damaged file preserved for recovery");Check(Store.Decode(File.ReadAllBytes(Path.Combine(data,"data.json.bak"))).Tasks.Count==16,"recovery preserves usable backup");}
   File.Delete(Path.Combine(data,"data.json"));using(var missing=new Store(data)){Check(missing.RecoveryMessage.Length>0&&missing.Data.Tasks.Count==16,"missing primary recovered without creating fresh data");}
   string all=Path.Combine(folder,"全部数据.json"),sourceDir=Path.Combine(folder,"import-source"),targetDir=Path.Combine(folder,"import-target"),migratedDir=Path.Combine(folder,"migrated-data");byte[] sourceBytes;
   using(var source=new Store(sourceDir)){
    source.Commit(d=>{var project=new Project{Id=Model.Id(),Name="第二个项目"};d.Projects.Add(project);d.CurrentProjectId=project.Id;d.People.Add(new Person{Id=Model.Id(),Name="历史人员",Deleted=true});var task=new TaskItem{Id=Model.Id(),ProjectId=project.Id,Name="额外项目任务",Status="待讨论",PersonId="",Start="",End="",Note="完整数据导入验证",InGantt=false};Model.Record(d,task,"创建任务","","跨电脑完整迁移");d.Tasks.Add(task);});source.Backup(all);sourceBytes=File.ReadAllBytes(source.FilePath);
    using(var target=new Store(targetDir)){
     target.Commit(d=>d.Tasks[0].Name="目标电脑原有任务");string safety=target.ImportAll(all);
     Check(target.Data.Projects.Count==2&&target.Data.Tasks.Count==17,"full import includes tasks across every project");Check(target.Data.People.Count==5&&target.Data.People.Any(p=>p.Deleted),"full import preserves historical people records");Check(target.Data.Tasks.Sum(t=>t.History.Count)==17&&target.Data.Tasks.Count(t=>t.InGantt)==5,"full import preserves complete histories and active schedules");Check(target.Data.Projects.First(p=>p.Id==target.Data.CurrentProjectId).Name=="第二个项目","full import retains active project");Check(Store.Decode(File.ReadAllBytes(safety)).Tasks[0].Name=="目标电脑原有任务","import creates independent complete pre-import backup");Check(Store.Encode(target.Data).SequenceEqual(Store.Encode(source.Data)),"complete export-import round-trip preserves every serialized field");
     string invalid=Path.Combine(folder,"invalid-import.json");File.WriteAllText(invalid,"not-json");byte[] unchanged=Store.Encode(target.Data);Fails(()=>target.ImportAll(invalid),"invalid full import rejected");Check(unchanged.SequenceEqual(Store.Encode(target.Data))&&unchanged.SequenceEqual(File.ReadAllBytes(target.FilePath)),"invalid import leaves current data untouched");
    }
    Fails(()=>{using(var locked=new Store(migratedDir,sourceDir)){}},"legacy migration refuses a workspace still being edited");
   }
   using(var migrated=new Store(migratedDir,sourceDir)){Check(migrated.Data.Tasks.Count==17&&migrated.RecoveryMessage.Length>0,"legacy workspace migrated on first launch");Check(sourceBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(sourceDir,"data.json"))),"legacy migration leaves original data file untouched");}
   var many=Model.Sample();many.Tasks.Clear();for(int i=0;i<3000;i++){var t=new TaskItem{Id=Model.Id(),ProjectId=many.CurrentProjectId,Name="大数据量任务 "+i,Status="待讨论",PersonId="",Start="",End="",Note="本地完整导入导出容量验证"};Model.Record(many,t,"创建任务","","");many.Tasks.Add(t);}Check(Store.Decode(Store.Encode(many)).Tasks.Count==3000,"large full-data round-trip with 3000 tasks and timelines");
   var invalidHistory=Model.Sample();invalidHistory.Tasks[0].History[0].At="invalid timestamp";Fails(()=>Store.Decode(Store.Encode(invalidHistory)),"reject imported timeline with invalid timestamp");
   string zeroDir=Path.Combine(folder,"zero-projects"),emptyFile=Path.Combine(folder,"零项目全部数据.json"),retainedId="";
   using(var zero=new Store(zeroDir)){
    string removed=zero.Data.CurrentProjectId;var removedTasks=new HashSet<string>(zero.Data.Tasks.Select(t=>t.Id));retainedId=Model.Id();string retainedTask=Model.Id();zero.Commit(d=>{d.Projects.Add(new Project{Id=retainedId,Name="保留项目"});var task=new TaskItem{Id=retainedTask,ProjectId=retainedId,Name="不能被误删的任务",Status="执行中",PersonId=d.People[0].Id,Start=Model.Date(DateTime.Today),End=Model.Date(DateTime.Today.AddDays(3)),InGantt=true,Note="跨项目删除隔离验证"};Model.Record(d,task,"创建任务","","");d.Tasks.Add(task);});
    zero.Commit(d=>Model.DeleteProject(d,removed));Check(zero.Data.Projects.Count==1&&!zero.Data.Tasks.Any(t=>removedTasks.Contains(t.Id)),"project deletion cascades to every owned task");Check(zero.Data.Tasks.Count==1&&zero.Data.Tasks[0].Id==retainedTask&&zero.Data.Tasks[0].InGantt&&zero.Data.Tasks[0].History.Count==1,"project deletion preserves other project schedules and histories");Check(zero.Data.CurrentProjectId==retainedId,"deleting current project selects a remaining project");Check(zero.Data.People.Count==4,"global shared people preserved after deleting a project");
    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(zero.FilePath)).Contains(removed),"deleted project and embedded data removed from current data file");Fails(()=>zero.Commit(d=>Model.DeleteProject(d,removed)),"reject deleting nonexistent project");
    zero.Commit(d=>Model.DeleteProject(d,retainedId));Check(zero.Data.Projects.Count==0&&zero.Data.Tasks.Count==0&&String.IsNullOrEmpty(zero.Data.CurrentProjectId),"last project can be deleted with no orphan tasks or active project");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(zero.FilePath)).Contains(retainedTask),"last project task schedule and timeline removed from current file");zero.Backup(emptyFile);Check(Store.Decode(File.ReadAllBytes(emptyFile)).Projects.Count==0,"zero-project complete export remains a valid file");
   }
   using(var reopenedEmpty=new Store(zeroDir)){Check(reopenedEmpty.Data.Projects.Count==0&&reopenedEmpty.Data.Tasks.Count==0,"empty workspace persists after restart without reseeding sample");reopenedEmpty.Commit(d=>{var p=new Project{Id=Model.Id(),Name="从零重新开始"};d.Projects.Add(p);d.CurrentProjectId=p.Id;});Check(reopenedEmpty.Data.Projects.Count==1&&reopenedEmpty.Data.CurrentProjectId==reopenedEmpty.Data.Projects[0].Id,"new project can be added after all projects are removed");}
   using(var emptyImport=new Store(Path.Combine(folder,"empty-import-target"))){emptyImport.ImportAll(emptyFile);Check(emptyImport.Data.Projects.Count==0&&emptyImport.Data.Tasks.Count==0,"complete import accepts zero-project workspace");var bad=Store.Decode(Store.Encode(emptyImport.Data));var orphan=new TaskItem{Id=Model.Id(),ProjectId="missing",Name="孤立任务",Status="待讨论",PersonId="",Start="",End=""};Model.Record(bad,orphan,"创建任务","","");bad.Tasks.Add(orphan);Fails(()=>Model.Check(bad),"zero-project schema rejects orphan tasks");}
   File.WriteAllLines(Path.Combine(folder,"测试结果.txt"),passed,Encoding.UTF8);
  }
 }
}
