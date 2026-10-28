// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace LaoQin.ProgressManager {
 public sealed class Store : IDisposable {
  public AppData Data {get;private set;}
  public string DirectoryPath {get;private set;}
  public string FilePath {get{return Path.Combine(DirectoryPath,"data.json");}}
  public string RecoveryMessage="";
  FileStream gate;
  static DataContractJsonSerializer Serializer(){return new DataContractJsonSerializer(typeof(AppData),new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=10000000});}
  public static byte[] Encode(AppData d){using(var m=new MemoryStream()){Serializer().WriteObject(m,d);return m.ToArray();}}
  public static AppData Decode(byte[] b){using(var m=new MemoryStream(b)){var d=(AppData)Serializer().ReadObject(m);Model.Check(d);return d;}}
  public Store(string directory,string legacy=null) {
   DirectoryPath=directory;Directory.CreateDirectory(directory);
   try{gate=new FileStream(Path.Combine(directory,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new InvalidOperationException("老秦已经在运行。请使用已打开的窗口。");}
   try {
    if(!File.Exists(FilePath)&&!File.Exists(FilePath+".bak")&&legacy!=null&&(File.Exists(Path.Combine(legacy,"data.json"))||File.Exists(Path.Combine(legacy,"data.json.bak")))){
     try{using(var oldGate=new FileStream(Path.Combine(legacy,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
      AppData previous;try{previous=Decode(File.ReadAllBytes(Path.Combine(legacy,"data.json")));}catch{previous=Decode(File.ReadAllBytes(Path.Combine(legacy,"data.json.bak")));}SafeOutput(FilePath,Encode(previous));RecoveryMessage="已迁移旧版数据，旧数据文件仍保留在原目录中。";
     }}catch(IOException ex){throw new InvalidOperationException("旧版数据正在使用或无法读取，请先关闭旧版程序后重试。",ex);}
    }
    if(File.Exists(FilePath)){
     try{Data=Decode(File.ReadAllBytes(FilePath));}
     catch(Exception primary){
      if(!File.Exists(FilePath+".bak"))throw new InvalidOperationException("本地数据无法读取。原文件已保留，请从备份恢复。\n"+primary.Message,primary);
      Data=Decode(File.ReadAllBytes(FilePath+".bak"));
      var damaged=Path.Combine(directory,"damaged-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");File.Copy(FilePath,damaged);
      SafeOutput(FilePath,Encode(Data));RecoveryMessage="本地数据异常，已自动从上一份备份恢复；异常文件已保留在数据目录中。";
     }
    }else if(File.Exists(FilePath+".bak")){Data=Decode(File.ReadAllBytes(FilePath+".bak"));SafeOutput(FilePath,Encode(Data));RecoveryMessage="主数据文件缺失，已自动从本地备份恢复。";}
    else{Data=Model.Sample();Write(Data);}
   }catch{Dispose();throw;}
  }
  void Write(AppData d){
   Model.Check(d);string temp=FilePath+".tmp";
   using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] b=Encode(d);f.Write(b,0,b.Length);f.Flush(true);}
   if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak",true);else File.Move(temp,FilePath);
  }
  public void Commit(Action<AppData> change){var next=Decode(Encode(Data));change(next);Model.Check(next);Write(next);Data=next;}
  public void Backup(string path){SafeOutput(path,Encode(Data));}
  public void Restore(string path){var next=Decode(File.ReadAllBytes(path));Write(next);Data=next;}
  public string ImportAll(string path){var next=Decode(File.ReadAllBytes(path));var folder=Path.Combine(DirectoryPath,"backups");Directory.CreateDirectory(folder);string safety=Path.Combine(folder,"导入前-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");Backup(safety);Write(next);Data=next;return safety;}
  public static void SafeOutput(string path,byte[] bytes){
   string full=Path.GetFullPath(path);string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null,true);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public void Dispose(){if(gate!=null){gate.Dispose();gate=null;}}
 }
}
