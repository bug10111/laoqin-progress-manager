// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Reflection;

[assembly:AssemblyTitle("老秦进度管理软件")]
[assembly:AssemblyDescription("离线项目、任务时间线与人员甘特图")]
[assembly:AssemblyProduct("老秦进度管理软件")]
[assembly:AssemblyCompany("老秦")]
[assembly:AssemblyCopyright("Copyright 2026 老秦进度管理软件贡献者")]
[assembly:AssemblyVersion("1.1.2.0")]
[assembly:AssemblyFileVersion("1.1.2.0")]

namespace LaoQin.ProgressManager {
 public static class Program {
  [STAThread] public static int Main(string[] args){
   Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("zh-CN");Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("zh-CN");
   string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LaoQinProgress");string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Xucheng");for(int i=0;i<args.Length-1;i++)if(args[i]=="--data-dir"){directory=Path.GetFullPath(args[i+1]);legacy=null;}
   try{
    var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};Ui.Init();
    if(args.Length>1&&args[0]=="--self-test"){Tests.Run(Path.GetFullPath(args[1]));return 0;}
    if(args.Length>1&&args[0]=="--ui-check")return VisualTests.Run(app,Path.GetFullPath(args[1]));
    using(var store=new Store(directory,legacy)){
     app.DispatcherUnhandledException+=(s,e)=>{try{File.AppendAllText(Path.Combine(directory,"error.log"),DateTimeOffset.Now.ToString("o")+"\n"+e.Exception+"\n");}catch{}MessageBox.Show("操作未完成，已保存的数据不会丢失。\n"+e.Exception.Message,"老秦",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};
     var window=new MainWindow(store);window.Loaded+=(s,e)=>{if(store.RecoveryMessage.Length>0)MessageBox.Show(window,store.RecoveryMessage,"数据恢复",MessageBoxButton.OK,MessageBoxImage.Information);};return app.Run(window);
    }
   }catch(Exception e){
    if(args.Length>1&&(args[0]=="--self-test"||args[0]=="--ui-check")){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"FAILED.txt"),e.ToString());return 1;}
    MessageBox.Show(e.Message,"老秦进度管理软件启动失败",MessageBoxButton.OK,MessageBoxImage.Error);return 1;
   }
  }
 }
}
