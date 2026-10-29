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
 }
 [DataContract] public class TaskItem {
  [DataMember] public string Priority = "P0";
  [DataMember] public string Id; [DataMember] public string ProjectId; [DataMember] public string Name; [DataMember] public string Status;
  [DataMember] public string PersonId; [DataMember] public string Start; [DataMember] public string End; [DataMember] public string Note;
  [DataMember] public bool InGantt; [DataMember] public List<Change> History = new List<Change>();
 }
 [DataContract] public class AppData {
  [DataMember] public int Version = 1;
  [DataMember] public List<Project> Projects = new List<Project>();
  [DataMember] public List<Person> People = new List<Person>();
  [DataMember] public List<TaskItem> Tasks = new List<TaskItem>();
  [DataMember] public string CurrentProjectId;
 }
 public static class Model {
  public static readonly string[] Priorities = { "P0", "P1", "P2", "P3" };
  public static IOrderedEnumerable<TaskItem> OrderTasks(IEnumerable<TaskItem> tasks) { return tasks.OrderBy(t=>Array.IndexOf(Statuses,t.Status)).ThenBy(t=>Array.IndexOf(Priorities,t.Priority)).ThenBy(t=>TaskPrefix(t.Name)).ThenBy(t=>t.Start).ThenBy(t=>t.Name); }
  public static readonly string[] Statuses = { "执行中", "待执行", "待讨论", "已完成", "已暂停", "已关闭" };
  public static string TaskPrefix(string name) { if(String.IsNullOrEmpty(name)||name[0]!='【')return "";int end=name.IndexOf('】');return end>1?name.Substring(0,end+1):""; }
  public static string Id() { return Guid.NewGuid().ToString("N"); }
  public static string Date(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
  public static DateTime Parse(string d) { return DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
  public static bool IsDate(string d) { DateTime x; return DateTime.TryParseExact(d,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out x); }
  public static DateTime Monday(DateTime d) { return d.Date.AddDays(-((int)d.DayOfWeek + 6) % 7); }
  public static string PersonName(AppData d, string id) { var p=d.People.FirstOrDefault(x=>x.Id==id); return p==null ? "未分配" : p.Name+(p.Deleted?"（已删除）":""); }
  public static string Range(TaskItem t) { return IsDate(t.Start) ? t.Start.Replace('-','.')+" — "+t.End.Replace('-','.') : "尚未安排"; }
  public static List<Change> Timeline(TaskItem t) { return t.History.AsEnumerable().Reverse().OrderBy(h=>DateTimeOffset.Parse(h.At)).ToList(); }
  public static string HistoryTime(Change h) { return DateTimeOffset.Parse(h.At).LocalDateTime.ToString("yyyy-MM-dd HH:mm"); }
  public static void Record(AppData d, TaskItem t, string kind, string before, string detail) {
   t.History.Insert(0,new Change { At=DateTimeOffset.Now.ToString("o"), Kind=kind, Before=before??"",After=t.Status,Priority=t.Priority,Name=t.Name,PersonName=PersonName(d,t.PersonId),Start=t.Start??"",End=t.End??"",Detail=detail??"" });
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
    foreach(var h in t.History){DateTimeOffset at;if(h==null||!DateTimeOffset.TryParse(h.At,out at)||!Statuses.Contains(h.After))throw new InvalidOperationException("任务时间线缺少有效的时间或状态。");}
    if(!String.IsNullOrEmpty(t.PersonId)&&!d.People.Any(p=>p.Id==t.PersonId)) throw new InvalidOperationException("任务负责人不存在。");
    bool any=!String.IsNullOrEmpty(t.Start)||!String.IsNullOrEmpty(t.End);
    if(any&&(!IsDate(t.Start)||!IsDate(t.End)||Parse(t.Start)>Parse(t.End))) throw new InvalidOperationException("任务起止日期不正确。");
    if((t.Status=="执行中"||t.InGantt)&&(!any||String.IsNullOrEmpty(t.PersonId))) throw new InvalidOperationException("执行中或纳入甘特图的任务必须有负责人和完整时间范围。");
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
    var t=new TaskItem {Id=Id(),ProjectId=p.Id,Name=names[i],Status=i<5?"执行中":i<10?"待执行":i<14?"待讨论":"已完成",PersonId="",Start="",End="",Note="示例任务，可直接编辑或删除。",InGantt=i<5};
    if(i<5){t.PersonId=d.People[persons[i]].Id;t.Start=Date(monday.AddDays(starts[i]));t.End=Date(monday.AddDays(ends[i]));}
    t.History.Add(new Change{At=DateTimeOffset.Now.ToString("o"),Kind="创建任务",Before="",After=t.Status,Name=t.Name,PersonName=PersonName(d,t.PersonId),Start=t.Start,End=t.End,Detail="首次启动生成的示例任务"});d.Tasks.Add(t);
   }
   return d;
  }
  // A task has at most one current bar. Replacing a bar does not delete the original task.
  public static void ReplaceSchedule(AppData d,string originalId,IEnumerable<string> selectedIds,DateTime start,DateTime end,string personId,string priority=null) {
   if(priority!=null&&!Priorities.Contains(priority))throw new InvalidOperationException("请选择有效的优先级。");
   var ids=new HashSet<string>(selectedIds); if(ids.Count==0)throw new InvalidOperationException("请至少选择一项任务。");
   if(start.Date>end.Date)throw new InvalidOperationException("结束日期不能早于开始日期。");
   var original=d.Tasks.FirstOrDefault(t=>t.Id==originalId); string project=original==null?d.CurrentProjectId:original.ProjectId;
   var chosen=d.Tasks.Where(t=>ids.Contains(t.Id)).ToList();
   if(chosen.Count!=ids.Count||chosen.Any(t=>t.ProjectId!=project))throw new InvalidOperationException("只能选择当前项目中的任务。");
   foreach(var t in chosen) if(String.IsNullOrEmpty(personId)&&String.IsNullOrEmpty(t.PersonId))throw new InvalidOperationException("选中的任务含未分配人员的任务，请选择负责人。");
   if(!String.IsNullOrEmpty(personId)&&!d.People.Any(p=>p.Id==personId&&!p.Deleted))throw new InvalidOperationException("请选择有效的负责人。");
   if(original!=null&&!ids.Contains(original.Id)){original.InGantt=false;Record(d,original,"退出本次排期",original.Status,"甘特条已替换，任务与原有日期保留。");}
   foreach(var t in chosen){string previous=Range(t),oldPriority=t.Priority;if(priority!=null)t.Priority=priority;if(!String.IsNullOrEmpty(personId))t.PersonId=personId;t.Start=Date(start);t.End=Date(end);t.InGantt=true;Record(d,t,"调整甘特图排期",t.Status,"原计划："+previous+"。任务状态保持不变。"+(oldPriority!=t.Priority?"优先级："+oldPriority+" → "+t.Priority+"。":""));}
  }
 }
}
