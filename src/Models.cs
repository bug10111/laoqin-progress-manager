// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace LaoQin.ProgressManager {
 [DataContract] public class Project { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public bool Example; }
 [DataContract] public class Person { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public bool Deleted; }
 [DataContract] public class Change {
  [DataMember] public string Priority;
  [DataMember] public string At; [DataMember] public string Kind; [DataMember] public string Before; [DataMember] public string After;
  [DataMember] public string Name; [DataMember] public string PersonName; [DataMember] public string Start; [DataMember] public string End; [DataMember] public string Detail;
  /// <summary>操作时填写的备注；与自动生成的变更明细分开保存。</summary>
  [DataMember(EmitDefaultValue=false)] public string Note;
  internal Change WithNote(string note,string detail){
   var entry=(Change)MemberwiseClone();entry.Note=note;entry.Detail=detail;return entry;
  }
 }
 [DataContract] public class TaskItem {
  [DataMember] public string Priority = "P0";
  [DataMember] public string Id; [DataMember] public string ProjectId; [DataMember] public string Name; [DataMember] public string Status;
  [DataMember] public string PersonId; [DataMember] public string Start; [DataMember] public string End;
  // 仅用于兼容旧版数据。新备注保存到对应的时间线节点。
  [DataMember(EmitDefaultValue=false)] public string Note;
  [DataMember] public bool InGantt; [DataMember] public List<Change> History = new List<Change>();
 }
 [DataContract] public class AppData {
  [DataMember(EmitDefaultValue=false)] public TaskExportPreferences TaskExcelPreferences;
  [DataMember] public int Version = 1;
  [DataMember] public List<Project> Projects = new List<Project>();
  [DataMember] public List<Person> People = new List<Person>();
  [DataMember] public List<TaskItem> Tasks = new List<TaskItem>();
  [DataMember] public string CurrentProjectId;
 }
 public static class Model {
  internal const int ChangeNoteMaxLength=50000;
  public static readonly string[] Priorities = { "P0", "P1", "P2", "P3" };
  public static IOrderedEnumerable<TaskItem> OrderTasks(IEnumerable<TaskItem> tasks) { return tasks.OrderBy(t=>Array.IndexOf(Statuses,t.Status)).ThenBy(t=>Array.IndexOf(Priorities,t.Priority)).ThenBy(t=>TaskPrefix(t.Name)).ThenBy(t=>t.Start).ThenBy(t=>t.Name); }
  // 列表优先展示正在推进的任务；流程节点仍按独立的正常阶段顺序显示。
  public static readonly string[] ListStatusOrder = { "执行中", "测试中", "待执行", "待讨论", "已暂停", "已完成", "已关闭" };
  public static IOrderedEnumerable<TaskItem> OrderTaskList(IEnumerable<TaskItem> tasks){return tasks.OrderBy(t=>Array.IndexOf(ListStatusOrder,t.Status)).ThenBy(t=>Array.IndexOf(Priorities,t.Priority)).ThenByDescending(t=>TaskCreatedAt(t)??DateTimeOffset.MinValue);}
  // 只取创建节点，后续追加与修改不会改变任务的创建顺序；未记录创建时间的旧数据排后。
  public static DateTimeOffset? TaskCreatedAt(TaskItem task){return task.History.Where(h=>h.Kind=="创建任务").Select(h=>(DateTimeOffset?)DateTimeOffset.Parse(h.At)).OrderBy(at=>at).FirstOrDefault();}
  public static readonly string[] ProgressStatuses = { "待讨论", "待执行", "执行中", "测试中", "已完成" };
  public static readonly string[] Statuses = ProgressStatuses.Concat(new[]{"已暂停","已关闭"}).ToArray();
  public static int ProgressIndex(TaskItem task){
   int current=Array.IndexOf(ProgressStatuses,task.Status);if(current>=0)return current;
   var last=Timeline(task).LastOrDefault(h=>ProgressStatuses.Contains(h.After));return last==null?-1:Array.IndexOf(ProgressStatuses,last.After);
  }
  public static string TaskPrefix(string name) { if(String.IsNullOrEmpty(name)||name[0]!='【')return "";int end=name.IndexOf('】');return end>1?name.Substring(0,end+1):""; }
  public static string Id() { return Guid.NewGuid().ToString("N"); }
  public static string Date(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
  public static DateTime Parse(string d) { return DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
  public static bool IsDate(string d) { DateTime x; return DateTime.TryParseExact(d,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out x); }
  public static DateTime Monday(DateTime d) { return d.Date.AddDays(-((int)d.DayOfWeek + 6) % 7); }
  public static string PersonName(AppData d, string id) { var p=d.People.FirstOrDefault(x=>x.Id==id); return p==null ? "未分配" : p.Name+(p.Deleted?"（已删除）":""); }
  public static string Range(TaskItem t) { return IsDate(t.Start) ? t.Start.Replace('-','.')+" — "+t.End.Replace('-','.') : "尚未安排"; }
  public static bool IsCurrentGanttTask(TaskItem t) { if(!IsDate(t.Start)||!IsDate(t.End))return false;if(t.InGantt||t.Status=="已完成")return true;if(String.IsNullOrEmpty(t.PersonId))return false;return !t.History.Any(h=>h.Kind=="退出本次排期"); }
  public static List<Change> Timeline(TaskItem t) { return t.History.AsEnumerable().Reverse().OrderBy(h=>DateTimeOffset.Parse(h.At)).ToList(); }
  public static bool HasTimelineNotes(TaskItem t) { return !String.IsNullOrWhiteSpace(t.Note)||t.History.Any(h=>!String.IsNullOrWhiteSpace(h.Note)||UsesLegacyRequirementDetail(h)); }
  static bool HasLegacyNote(TaskItem t) { return !String.IsNullOrWhiteSpace(t.Note)&&!t.History.Any(h=>h.Kind=="创建任务"); }
  public static int TimelineNodeCount(TaskItem t) { return t.History.Count+(HasLegacyNote(t)?1:0); }
  public static string HistoryTime(Change h) { return String.IsNullOrEmpty(h.At)?"创建时间未记录":DateTimeOffset.Parse(h.At).LocalDateTime.ToString("yyyy-MM-dd HH:mm"); }
  static bool UsesLegacyRequirementDetail(Change entry){
   return entry.Kind=="需求追加"&&String.IsNullOrWhiteSpace(entry.Note)&&!String.IsNullOrWhiteSpace(entry.Detail);
  }
  internal static List<Change> TimelineEntries(TaskItem task){
   var entries=Timeline(task);
   for(int i=0;i<entries.Count;i++){
    var entry=entries[i];
    if(UsesLegacyRequirementDetail(entry))
     entries[i]=entry.WithNote(entry.Detail,"");
   }
   if(String.IsNullOrWhiteSpace(task.Note))return entries;
   int creation=entries.FindIndex(h=>h.Kind=="创建任务");
   if(creation<0)entries.Insert(0,new Change{Kind="旧版备注",Note=task.Note});
   else{
    var entry=entries[creation];
    string note=String.IsNullOrWhiteSpace(entry.Note)||entry.Note==task.Note?task.Note:task.Note+"\n\n"+entry.Note;
    entries[creation]=entry.WithNote(note,entry.Detail);
   }
   return entries;
  }
  internal static string DisplayNote(Change entry){return String.IsNullOrWhiteSpace(entry.Note)?"无":entry.Note;}
  internal static string ChangeLine(string field,string before,string after){
   return before==after?"":field+"："+(String.IsNullOrEmpty(before)?"无":before)+" → "+(String.IsNullOrEmpty(after)?"无":after);
  }
  internal static string TimelineDetails(Change entry){
   var lines=new List<string>();
   if(!String.IsNullOrEmpty(entry.After)){
    if(!String.IsNullOrEmpty(entry.Before)&&entry.Before!=entry.After)lines.Add(ChangeLine("状态",entry.Before,entry.After));
    else if(entry.Kind=="创建任务")lines.Add("状态："+entry.After);
   }
   if(entry.Kind=="创建任务"){
    if(!String.IsNullOrEmpty(entry.Priority))lines.Add("优先级："+entry.Priority);
    if(!String.IsNullOrEmpty(entry.PersonName))lines.Add("负责人："+entry.PersonName);
    lines.Add("计划时间："+(IsDate(entry.Start)?entry.Start+" — "+entry.End:"尚未安排"));
   }
   if(!String.IsNullOrWhiteSpace(entry.Detail)){
    lines.Add(entry.Detail);
    // 旧版自动明细只保存了部分原值，补显示该节点保存的新值。
    if(entry.Detail.StartsWith("原任务名称：",StringComparison.Ordinal))lines.Add("变更后任务名称："+entry.Name);
    if(entry.Detail.StartsWith("原计划：",StringComparison.Ordinal)||entry.Detail.Contains("；原计划："))
     if(!entry.Detail.Contains("新计划："))lines.Add("变更后计划："+(IsDate(entry.Start)?entry.Start+" — "+entry.End:"尚未安排"));
   }
   return String.Join("\n",lines);
  }
  internal static string TimelineHeading(Change entry){return HistoryTime(entry)+" · "+entry.Kind;}
  internal static string TimelineNoteLine(Change entry){return "备注："+DisplayNote(entry);}
  static void ValidateChangeNote(string note){
   if(note!=null&&note.Length>ChangeNoteMaxLength)throw new InvalidOperationException("变更备注不能超过 "+ChangeNoteMaxLength+" 个字符。");
  }
  internal static void EditTimelineNote(TaskItem task,int position,string note){
   note=String.IsNullOrWhiteSpace(note)?null:note.Trim();
   ValidateChangeNote(note);
   if(position<0||position>=TimelineNodeCount(task))throw new InvalidOperationException("该时间线记录已不存在。");

   // 没有创建节点的旧版备注仍保存到任务原有的备注字段。
   bool hasLegacyNote=HasLegacyNote(task);
   if(hasLegacyNote&&position==0){task.Note=note;return;}

   var entries=Timeline(task);
   var entry=entries[hasLegacyNote?position-1:position];
   // 需求追加本身以补充内容为主体，沿用追加时的非空要求。
   if(entry.Kind=="需求追加"&&note==null)throw new InvalidOperationException("请输入本次补充内容。");
   // 编辑合并显示的备注时，清除对应旧字段，避免原文再次出现在时间线中。
   if(entry==entries.Find(h=>h.Kind=="创建任务"))task.Note=null;
   if(UsesLegacyRequirementDetail(entry))entry.Detail="";
   entry.Note=note;
  }
  /// <summary>将操作、当前任务快照及可选备注加入时间线。</summary>
  /// <param name="d">包含负责人的工作区数据。</param>
  /// <param name="t">已应用本次操作的任务。</param>
  /// <param name="kind">操作类型。</param>
  /// <param name="before">变更前的状态。</param>
  /// <param name="detail">操作生成的明细。</param>
  /// <param name="note">用户填写的变更备注。</param>
  public static void Record(AppData d, TaskItem t, string kind, string before, string detail,string note=null) {
   note=String.IsNullOrWhiteSpace(note)?null:note.Trim();ValidateChangeNote(note);
   t.History.Insert(0,new Change { At=DateTimeOffset.Now.ToString("o"), Kind=kind, Before=before??"",After=t.Status,Priority=t.Priority,Name=t.Name,PersonName=PersonName(d,t.PersonId),Start=t.Start??"",End=t.End??"",Detail=detail??"",Note=note });
  }
  public static void Check(AppData d) {
   if(d==null || d.Version!=1 || d.Projects==null || d.People==null || d.Tasks==null) throw new InvalidOperationException("数据格式或版本不受支持。");
   if(d.Projects.Any(x=>x==null)||d.People.Any(x=>x==null)||d.Tasks.Any(x=>x==null))throw new InvalidOperationException("导入数据包含空的项目、人员或任务记录。");
   if(d.Projects.Any(x=>String.IsNullOrWhiteSpace(x.Id)||String.IsNullOrWhiteSpace(x.Name)) || d.Projects.Select(x=>x.Id).Distinct().Count()!=d.Projects.Count) throw new InvalidOperationException("项目数据不完整或重复。");
   if(d.People.Any(x=>String.IsNullOrWhiteSpace(x.Id)||String.IsNullOrWhiteSpace(x.Name)) || d.People.Select(x=>x.Id).Distinct().Count()!=d.People.Count) throw new InvalidOperationException("人员数据不完整或重复。");
   if(d.Tasks.Select(x=>x.Id).Distinct().Count()!=d.Tasks.Count) throw new InvalidOperationException("存在重复的任务标识。");
   foreach(var t in d.Tasks) {
    if(String.IsNullOrEmpty(t.Priority))t.Priority="P0";
    if(!Priorities.Contains(t.Priority))throw new InvalidOperationException("任务优先级必须为 P0、P1、P2 或 P3。");
    if(String.IsNullOrWhiteSpace(t.Id)||String.IsNullOrWhiteSpace(t.Name)||!d.Projects.Any(p=>p.Id==t.ProjectId)||!Statuses.Contains(t.Status)||t.History==null) throw new InvalidOperationException("任务数据不完整。");
    foreach(var h in t.History){DateTimeOffset at;if(h==null||!DateTimeOffset.TryParse(h.At,out at)||!Statuses.Contains(h.After))throw new InvalidOperationException("任务时间线缺少有效的时间或状态。");ValidateChangeNote(h.Note);}
    if(!String.IsNullOrEmpty(t.PersonId)&&!d.People.Any(p=>p.Id==t.PersonId)) throw new InvalidOperationException("任务负责人不存在。");
    bool any=!String.IsNullOrEmpty(t.Start)||!String.IsNullOrEmpty(t.End);
    if(any&&(!IsDate(t.Start)||!IsDate(t.End)||Parse(t.Start)>Parse(t.End))) throw new InvalidOperationException("任务起止日期不正确。");
    if(t.Status=="执行中"&&String.IsNullOrEmpty(t.PersonId))throw new InvalidOperationException("执行中的任务必须有负责人。");
    if(t.InGantt&&(!any||String.IsNullOrEmpty(t.PersonId)))throw new InvalidOperationException("纳入甘特图的任务必须有负责人和完整时间范围。");
   }
   if(!d.Projects.Any(x=>x.Id==d.CurrentProjectId)) d.CurrentProjectId=d.Projects.Count==0?"":d.Projects[0].Id;
  }
  public static void DeleteProject(AppData d,string id){
   if(!d.Projects.Any(p=>p.Id==id))throw new InvalidOperationException("项目不存在或已被删除。");
   d.Tasks.RemoveAll(t=>t.ProjectId==id);d.Projects.RemoveAll(p=>p.Id==id);
   if(d.CurrentProjectId==id||!d.Projects.Any(p=>p.Id==d.CurrentProjectId))d.CurrentProjectId=d.Projects.Count==0?"":d.Projects[0].Id;
  }
  public static AppData Sample() {
   var d=new AppData(); var p=new Project {Id=Id(),Name="智能业务平台",Example=true};d.Projects.Add(p);d.CurrentProjectId=p.Id;
   foreach(var n in new[]{"李明","王悦","陈亦","周宁"}) d.People.Add(new Person{Id=Id(),Name=n});
   string[] names={"知识库对接","SaaS 测试","小程序备案","计费报价","录音设备调研","小程序订阅","阿里云短信申请","SSL 证书申请","考题题型配置","方案准确度测试","对接企微与任务统计","充值方案","新品知识库生命周期","多租户数据权限扩展","任务模块原型确认","人员管理方案确认"};
   int[] starts={0,3,1,2,7},ends={4,9,5,7,11},persons={0,0,1,2,3};var monday=Monday(DateTime.Today);
   for(int i=0;i<names.Length;i++) {
    var t=new TaskItem {Id=Id(),ProjectId=p.Id,Name=names[i],Status=i<5?"执行中":i<10?"待执行":i<14?"待讨论":"已完成",PersonId="",Start="",End="",InGantt=i<5};
    if(i<5){t.PersonId=d.People[persons[i]].Id;t.Start=Date(monday.AddDays(starts[i]));t.End=Date(monday.AddDays(ends[i]));}
    Record(d,t,"创建任务","","首次启动生成的示例任务","示例任务，可直接编辑或删除。");d.Tasks.Add(t);
   }
   return d;
  }
  static TaskItem CheckGanttDrag(AppData data,GanttDateChange change){
   if(change==null)throw new InvalidOperationException("缺少拖动后的计划日期。");var task=data.Tasks.SingleOrDefault(t=>t.Id==change.TaskId);
   if(task==null||!IsCurrentGanttTask(task))throw new InvalidOperationException("任务已退出排期，请刷新后重试。");
   if(task.Start!=Date(change.OriginalStart)||task.End!=Date(change.OriginalEnd))throw new InvalidOperationException("任务计划已发生变化，请刷新后重试。");
   if(change.Start.Date>change.End.Date)throw new InvalidOperationException("结束日期不能早于开始日期。");
   if(change.Mode==GanttDragMode.Move){if(change.End.Date-change.Start.Date!=change.OriginalEnd.Date-change.OriginalStart.Date)throw new InvalidOperationException("整体移动必须保持原计划天数。");}
   else if(change.Mode==GanttDragMode.Start){if(change.End.Date!=change.OriginalEnd.Date)throw new InvalidOperationException("拖动开始日期不能改变结束日期。");}
   else if(change.Mode==GanttDragMode.End){if(change.Start.Date!=change.OriginalStart.Date)throw new InvalidOperationException("拖动结束日期不能改变开始日期。");}
   else throw new InvalidOperationException("不支持的排期拖动方式。");
   return task;
  }
  static void WriteGanttDrag(AppData data,TaskItem task,GanttDateChange change,int count){if(!change.HasChanges)return;string previous=Range(task);task.Start=Date(change.Start);task.End=Date(change.End);Record(data,task,"拖动甘特图排期",task.Status,(count>1?"批量移动（"+count+" 项）":change.Mode==GanttDragMode.Move?"整体移动":change.Mode==GanttDragMode.Start?"调整开始日期":"调整结束日期")+"\n"+ChangeLine("计划时间",previous,Range(task)));}
  public static void ApplyGanttDrag(AppData data,GanttDateChange change){WriteGanttDrag(data,CheckGanttDrag(data,change),change,1);}
  public static void ApplyGanttDrags(AppData data,IEnumerable<GanttDateChange> changes){
   var chosen=changes.ToList();if(chosen.Count==0||chosen.Any(c=>c==null)||chosen.Select(c=>c.TaskId).Distinct().Count()!=chosen.Count)throw new InvalidOperationException("请选择有效且不重复的任务。");
   // 先检查整组，再修改日期，任何任务已被改动时均不写入其余任务。
   var tasks=chosen.Select(c=>CheckGanttDrag(data,c)).ToList();if(tasks.Select(t=>t.ProjectId).Distinct().Count()!=1)throw new InvalidOperationException("只能一起移动同一项目中的任务。");
   if(chosen.Count>1&&(chosen.Any(c=>c.Mode!=GanttDragMode.Move)||chosen.Select(c=>c.Start.Date-c.OriginalStart.Date).Distinct().Count()!=1))throw new InvalidOperationException("批量拖动必须保持所有任务移动相同天数。");
   for(int i=0;i<chosen.Count;i++)WriteGanttDrag(data,tasks[i],chosen[i],chosen.Count);
  }
  /// <summary>批量调整排期；替换甘特条时保留原任务与历史记录。</summary>
  /// <param name="d">待更新的工作区数据。</param>
  /// <param name="originalId">被替换的任务标识；安排现有任务时为空。</param>
  /// <param name="selectedIds">本次选中的任务标识。</param>
  /// <param name="start">计划开始日期；清空排期时为空。</param>
  /// <param name="end">计划结束日期；清空排期时为空。</param>
  /// <param name="personId">本次选择的负责人标识。</param>
  /// <param name="priority">新优先级；为空时保持原值。</param>
  /// <param name="status">新状态；为空时保持原值。</param>
  /// <param name="note">为每个选中任务记录一次的变更备注。</param>
  public static void ReplaceSchedule(AppData d,string originalId,IEnumerable<string> selectedIds,DateTime? start,DateTime? end,string personId,string priority=null,string status=null,string note=null) {
   ValidateChangeNote(note);
   if(priority!=null&&!Priorities.Contains(priority))throw new InvalidOperationException("请选择有效的优先级。");
   if(status!=null&&!Statuses.Contains(status))throw new InvalidOperationException("请选择有效的任务状态。");
   var ids=new HashSet<string>(selectedIds); if(ids.Count==0)throw new InvalidOperationException("请至少选择一项任务。");
   if(start.HasValue!=end.HasValue)throw new InvalidOperationException("请填写完整的起止日期，或同时清空两个日期。");
   if(start.HasValue&&start.Value.Date>end.Value.Date)throw new InvalidOperationException("结束日期不能早于开始日期。");
   var original=d.Tasks.FirstOrDefault(t=>t.Id==originalId); string project=original==null?d.CurrentProjectId:original.ProjectId;
   var chosen=d.Tasks.Where(t=>ids.Contains(t.Id)).ToList();
   if(chosen.Count!=ids.Count||chosen.Any(t=>t.ProjectId!=project))throw new InvalidOperationException("只能选择当前项目中的任务。");
   foreach(var t in chosen)if(String.IsNullOrEmpty(personId)&&String.IsNullOrEmpty(t.PersonId)&&(start.HasValue||(status??t.Status)=="执行中"))throw new InvalidOperationException("排期或执行中的任务需要负责人，请先选择负责人。");
   if(!String.IsNullOrEmpty(personId)&&!d.People.Any(p=>p.Id==personId&&!p.Deleted))throw new InvalidOperationException("请选择有效的负责人。");
   if(original!=null&&!ids.Contains(original.Id)){original.InGantt=false;Record(d,original,"退出本次排期",original.Status,"当前排期：已纳入 → 未纳入\n甘特条已替换，任务与原有日期保留。");}
   foreach(var t in chosen){
    string previous=Range(t),oldPriority=t.Priority,oldStatus=t.Status,oldPerson=t.PersonId;bool wasIn=IsCurrentGanttTask(t);
    if(priority!=null)t.Priority=priority;
    if(status!=null)t.Status=status;
    if(!String.IsNullOrEmpty(personId))t.PersonId=personId;
    t.Start=start.HasValue?Date(start.Value):"";t.End=end.HasValue?Date(end.Value):"";t.InGantt=start.HasValue;
    string range=Range(t);
    var changes=new[]{
     ChangeLine("计划时间",previous,range),
     oldPerson==t.PersonId?"":ChangeLine("负责人",PersonName(d,oldPerson),PersonName(d,t.PersonId)),
     ChangeLine("优先级",oldPriority,t.Priority),
     ChangeLine("当前排期",wasIn?"已纳入":"未纳入",IsCurrentGanttTask(t)?"已纳入":"未纳入")
    };
    string detail=String.Join("\n",changes.Where(line=>line.Length>0));
    if(oldStatus!=t.Status)Record(d,t,"状态变更",oldStatus,"",note);
    if(detail.Length>0)
     Record(d,t,!start.HasValue&&previous!=range?"清空计划时间":"调整甘特图排期",t.Status,detail,oldStatus==t.Status?note:null);
    else if(oldStatus==t.Status&&!String.IsNullOrWhiteSpace(note))Record(d,t,"追加备注",t.Status,"",note);
   }
  }
 }
}
