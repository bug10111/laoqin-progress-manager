// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LaoQin.ProgressManager {
 public sealed class Store : IDisposable {
  public AppData Data {get;private set;}
  public string DirectoryPath {get;private set;}
  public string FilePath {get{return Path.Combine(DirectoryPath,"workspace.sqlite3");}}
  public string RecoveryMessage="";
  internal bool IsCloud {get{return cloud!=null;}}
  internal bool WorkspaceReady {get{return Data!=null;}}
  internal DateTime LastSavedAt {get{return cloud==null?File.GetLastWriteTime(FilePath):lastCloudAccess;}}
  FileStream gate;
  readonly CloudClient cloud;
  readonly SemaphoreSlim cloudGate;
  DateTime lastCloudAccess;
  public static byte[] Encode(AppData d){return WorkspaceJson.Encode(d);}
  public static AppData Decode(byte[] b){return WorkspaceJson.Decode(b);}
  public Store(string directory,string legacy=null) {
   DirectoryPath=directory;Directory.CreateDirectory(directory);
   try{gate=new FileStream(Path.Combine(directory,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new InvalidOperationException("老秦已经在运行。请使用已打开的窗口。");}
   try {
    if(File.Exists(FilePath)){
     try{Data=LocalWorkspace.Read(FilePath);}
     catch(Exception error){throw new InvalidOperationException("无法读取本地 SQLite 数据库。原文件已保留。\n"+error.Message,error);}
    }else{
     Data=ReadLegacyJson(directory)??ReadPreviousDirectory(legacy)??Model.Sample();
     LocalWorkspace.Create(FilePath,Data);
    }
   }catch{Dispose();throw;}
  }
  AppData ReadLegacyJson(string directory){
   var path=Path.Combine(directory,"data.json");
   if(!File.Exists(path)&&!File.Exists(path+".bak"))return null;
   try{
    var data=Decode(File.ReadAllBytes(path));
    RecoveryMessage="旧版数据已迁移到本地数据库，原 JSON 文件已保留。";
    return data;
   }catch(Exception primary){
    if(!File.Exists(path+".bak"))throw new InvalidOperationException("无法迁移旧版数据。原 JSON 文件已保留。\n"+primary.Message,primary);
    var data=Decode(File.ReadAllBytes(path+".bak"));
    RecoveryMessage="已从旧版 JSON 备份迁移，原文件已保留。";
    return data;
   }
  }
  AppData ReadPreviousDirectory(string directory){
   if(directory==null||(!File.Exists(Path.Combine(directory,"data.json"))&&!File.Exists(Path.Combine(directory,"data.json.bak"))))return null;
   try{using var oldGate=new FileStream(Path.Combine(directory,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);return ReadLegacyJson(directory);}
   catch(IOException error){throw new InvalidOperationException("旧版数据正在使用或无法读取，请关闭旧版程序后重试。",error);}
  }
  Store(CloudClient client,AppData snapshot,string localDirectory){
   cloud=client;
   cloudGate=new SemaphoreSlim(1,1);
   DirectoryPath=client.BackupDirectory(localDirectory);
   UseSnapshot(snapshot);
  }
  internal static async Task<Store> OpenCloudAsync(string connection,string password,string localDirectory){
   var client=await CloudClient.LoginAsync(connection,password);
   var snapshot=await client.ReadWorkspaceAsync();
   return new Store(client,snapshot,localDirectory);
  }
  void UseSnapshot(AppData snapshot){
   Data=snapshot;
   lastCloudAccess=DateTime.Now;
  }
  internal async Task ReloadCloudAsync(){
   await cloudGate.WaitAsync();
   try{UseSnapshot(await cloud.ReadWorkspaceAsync());}
   finally{cloudGate.Release();}
  }
  internal async Task ChangeCloudPasswordAsync(string current,string next){
   await cloudGate.WaitAsync();
   try{await cloud.ChangePasswordAsync(current,next);}
   finally{cloudGate.Release();}
  }
  void Write(AppData d){
   if(IsCloud)throw new InvalidOperationException("请通过 PostgreSQL 保存云端工作区。");
   LocalWorkspace.Save(FilePath,Data,d);
  }
  AppData ApplyChange(Action<AppData> change){
   var next=Data.Copy();
   change(next);
   return next;
  }
  public void Commit(Action<AppData> change){var next=ApplyChange(change);Write(next);Data=next;}
  async Task SaveCloudAsync(AppData next){
   if(!WorkspaceReady)throw new InvalidOperationException("请先配置 PostgreSQL 数据库。");
   await cloud.SaveChangesAsync(Data,next);
   UseSnapshot(next);
  }
  internal async Task CommitAsync(Action<AppData> change){
   if(!IsCloud){Commit(change);return;}
   await cloudGate.WaitAsync();
   try{await SaveCloudAsync(ApplyChange(change));}
   finally{cloudGate.Release();}
  }
  public void Backup(string path){SafeOutput(path,Encode(Data));}
  internal async Task BackupAsync(string path){
   if(!IsCloud){Backup(path);return;}
   await cloudGate.WaitAsync();
   try{SafeOutput(path,Encode(await cloud.ReadWorkspaceAsync()));}
   finally{cloudGate.Release();}
  }
  public void Restore(string path){var next=Decode(File.ReadAllBytes(path));Write(next);Data=next;}
  string SaveImportBackup(AppData before){
   var folder=Path.Combine(DirectoryPath,"backups");
   Directory.CreateDirectory(folder);
   string safety=Path.Combine(folder,"导入前-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");
   SafeOutput(safety,Encode(before));
   return safety;
  }
  public string ImportAll(string path){
   return ReplaceLocal(Decode(File.ReadAllBytes(path)));
  }
  string ReplaceLocal(AppData next){
   string safety=SaveImportBackup(Data);
   Write(next);
   Data=next;
   return safety;
  }
  internal async Task<string> ImportAllAsync(string path){
   if(!IsCloud)return ImportAll(path);
   return await ImportCloudSnapshotAsync(Decode(File.ReadAllBytes(path)));
  }
  internal Task<string> MigrateLocalAsync(AppData source){
   return ImportCloudSnapshotAsync(Decode(Encode(source)));
  }
  internal async Task<string> MigrateCloudToLocalAsync(Store local){
   await cloudGate.WaitAsync();
   try{
    var source=await cloud.ReadWorkspaceAsync();
    return local.ReplaceLocal(Decode(Encode(source)));
   }finally{cloudGate.Release();}
  }
  async Task<string> ImportCloudSnapshotAsync(AppData next){
   await cloudGate.WaitAsync();
   try{
    if(!WorkspaceReady)throw new InvalidOperationException("请先配置 PostgreSQL 数据库。");
    string safety=null;
    await cloud.ReplaceAsync(next,current=>safety=SaveImportBackup(current));
    UseSnapshot(next);
    return safety;
   }finally{cloudGate.Release();}
  }
  public static void SafeOutput(string path,byte[] bytes){
   string full=Path.GetFullPath(path);string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null,true);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public void Dispose(){
   if(cloud!=null)cloudGate.Dispose();
   if(gate!=null){gate.Dispose();gate=null;}
  }
 }
}
