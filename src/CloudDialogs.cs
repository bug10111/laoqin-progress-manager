// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LaoQin.ProgressManager {
 public static partial class Dialogs {
  static PasswordBox CloudSecret(Panel panel,string label,int maxLength=0){
   var field=new PasswordBox{MaxLength=maxLength,Padding=new Thickness(10,9,10,9),BorderBrush=Ui.Line};
   System.Windows.Automation.AutomationProperties.SetName(field,label);
   Ui.Label(panel,label,field);
   return field;
  }

  static TextBox CloudTextField(Panel panel,string label,int maxLength,string value=""){
   var field=new TextBox{Text=value,MaxLength=maxLength};
   System.Windows.Automation.AutomationProperties.SetName(field,label);
   Ui.Label(panel,label,field);
   return field;
  }

  static (Panel Left,Panel Right) CloudFieldsRow(Panel panel,GridLength rightWidth){
   var row=new Grid();
   row.ColumnDefinitions.Add(new ColumnDefinition());
   row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});
   row.ColumnDefinitions.Add(new ColumnDefinition{Width=rightWidth});
   var left=Ui.Stack();
   var right=Ui.Stack();
   Grid.SetColumn(right,2);
   row.Children.Add(left);
   row.Children.Add(right);
   panel.Children.Add(row);
   return (left,right);
  }

  internal static Store ConnectCloud(Window owner,string localDirectory){
   var window=Ui.Dialog(owner,"连接 PostgreSQL 工作区",630);
   var panel=Ui.Stack();
   panel.Margin=new Thickness(24);
   panel.Children.Add(Ui.Text("连接 PostgreSQL 工作区",21,null,true));
   var (addressField,portField)=CloudFieldsRow(panel,new GridLength(124));
   var address=CloudTextField(addressField,"数据库地址",255);
   var port=CloudTextField(portField,"端口",5,CloudClient.DefaultPort.ToString(CultureInfo.InvariantCulture));
   var (userField,secretField)=CloudFieldsRow(panel,new GridLength(1,GridUnitType.Star));
   var username=CloudTextField(userField,"数据库账号",256);
   var databasePassword=CloudSecret(secretField,"数据库密码",3500);
   var unencrypted=new CheckBox{
    Content="允许非加密连接（仅限可信内网）",Margin=new Thickness(0,8,0,0),
    ToolTip="仅在确认服务器位于可信内网且未启用 TLS 时勾选。"
   };
   string BuildConnection()=>CloudClient.BuildConnection(address.Text,port.Text,username.Text,databasePassword.Password,unencrypted.IsChecked==true);
   var profile=Path.Combine(localDirectory,"postgres-connection.dat");
   var database=Ui.Text("固定数据库  "+CloudClient.DatabaseName,12,Ui.Muted);
   database.Margin=new Thickness(0,8,0,0);
   panel.Children.Add(database);
   panel.Children.Add(unencrypted);
   var status=Ui.Text("",12,Ui.Muted);
   status.Margin=new Thickness(0,6,0,0);
   status.Visibility=Visibility.Collapsed;
   panel.Children.Add(status);
   void SetStatus(string message){status.Text=message;status.Visibility=Visibility.Visible;}
   if(File.Exists(profile)){
    try{
     var bytes=ProtectedData.Unprotect(File.ReadAllBytes(profile),null,DataProtectionScope.CurrentUser);
     try{
      var saved=CloudClient.RestoreConnection(Encoding.UTF8.GetString(bytes));
      address.Text=saved.Host;
      port.Text=saved.Port.ToString(CultureInfo.InvariantCulture);
      username.Text=saved.Username;
      databasePassword.Password=saved.Password;
      unencrypted.IsChecked=saved.AllowUnencrypted;
     }finally{CryptographicOperations.ZeroMemory(bytes);}
    }catch(InvalidOperationException error){
     SetStatus(error.Message);
    }catch(Exception error) when(error is CryptographicException||error is IOException||error is UnauthorizedAccessException){
     SetStatus("无法读取此 Windows 账号保存的数据库设置，请重新填写。");
    }
   }
   address.TextChanged+=(s,e)=>unencrypted.IsChecked=false;
   port.TextChanged+=(s,e)=>unencrypted.IsChecked=false;
   var databaseActions=Ui.Stack(true);
   databaseActions.Margin=new Thickness(0,12,0,0);
   var init=Ui.ButtonAsync("初始化并保存",window,async ()=>{
    var candidate=BuildConnection();
    var target=await CloudClient.PreviewDatabaseAsync(candidate);
    if(!window.IsVisible)return;
    var action=target.NeedsMigration?"将旧版工作区迁移到拆分后的表，旧数据会保留为备份。"
     :target.TableExists?"已确认是本软件的表，现有数据会保留。":"将创建工作区所需的表。";
    var details="服务器："+target.Server+"\n数据库："+target.Database+"\n操作的表：\n"+target.Table.Replace(", ","\n")+"\n\n"+action;
    if(MessageBox.Show(window,details,"确认初始化数据库",MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return;
    await CloudClient.InitializeDatabaseAsync(candidate,target);
    SaveConnection(profile,candidate);
    SetStatus("数据库已初始化。请输入管理员密码登录。");
   });
   init.Margin=new Thickness(0,0,8,0);
   databaseActions.Children.Add(init);
   databaseActions.Children.Add(Ui.Button("下载初始化 SQL",()=>{
    var file=new SaveFileDialog{Title="保存 PostgreSQL 初始化 SQL",FileName="laoqin-init.sql",Filter="SQL 脚本 (*.sql)|*.sql",AddExtension=true,OverwritePrompt=true};
    if(file.ShowDialog(window)==true)Store.SafeOutput(file.FileName,CloudClient.SchemaBytes());
   }));
   panel.Children.Add(databaseActions);
   var account=Ui.Text("工作区管理员  "+CloudClient.Username,12,Ui.Muted);
   account.Margin=new Thickness(0,16,0,0);
   panel.Children.Add(account);
   var password=CloudSecret(panel,"管理员密码");
   Store result=null;
   FooterAsync(panel,window,async ()=>{
    var candidate=BuildConnection();
    var opened=await Store.OpenCloudAsync(candidate,password.Password,localDirectory);
    try{SaveConnection(profile,candidate);}
    catch{opened.Dispose();throw;}
    result=opened;
    password.Clear();
    window.DialogResult=true;
   },"登录");
   window.Content=panel;
   window.Loaded+=(s,e)=>{if(String.IsNullOrWhiteSpace(address.Text))address.Focus();else password.Focus();};
   window.ShowDialog();
   return result;
  }

  static void SaveConnection(string path,string connection){
   var bytes=Encoding.UTF8.GetBytes(connection);
   try{Store.SafeOutput(path,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}
   finally{CryptographicOperations.ZeroMemory(bytes);}
  }

  internal static void ChangeCloudPassword(Window owner,Store store){
   var window=Ui.Dialog(owner,"修改管理员密码",450);
   var panel=Ui.Stack();
   panel.Margin=new Thickness(24);
   panel.Children.Add(Ui.Text("修改管理员密码",21,null,true));
   var current=CloudSecret(panel,"当前密码");
   var next=CloudSecret(panel,"新密码");
   var confirmation=CloudSecret(panel,"确认新密码");
   FooterAsync(panel,window,async ()=>{
    if(next.Password!=confirmation.Password)throw new InvalidOperationException("两次输入的新密码不一致。");
    await store.ChangeCloudPasswordAsync(current.Password,next.Password);
    window.DialogResult=true;
   },"保存密码");
   window.Content=panel;
   window.Loaded+=(s,e)=>current.Focus();
   if(window.ShowDialog()==true)
    MessageBox.Show(owner,"管理员密码已更新，其他设备需要重新登录。","老秦进度管理软件");
  }

  internal static async Task<bool> MigrateCloudAsync(Window owner,Store cloud,Store local){
   string counts=local.Data.Projects.Count+" 个项目、"+local.Data.People.Count+" 位员工和 "+local.Data.Tasks.Count+" 项任务";
   if(MessageBox.Show(owner,"将 "+counts+" 及时间线复制到当前云端数据库，覆盖云端现有工作区。操作前会把云端数据备份到本机。是否继续？","迁移到云端",MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return false;
   var backup=await cloud.MigrateLocalAsync(local.Data);
   MessageBox.Show(owner,"迁移完成。原云端数据已备份至：\n"+backup,"老秦进度管理软件");
   return true;
  }

  internal static async Task MigrateCloudToLocalAsync(Window owner,Store cloud,Store local){
   var message="将最新的云端数据完整覆盖到本机。操作前会将当前本机工作区备份成 JSON。是否继续？";
   if(MessageBox.Show(owner,message,"用云端数据覆盖本机",MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return;
   var backup=await cloud.MigrateCloudToLocalAsync(local);
   MessageBox.Show(owner,"本机数据已覆盖。退出云端可查看本机工作区。\n原本机数据备份：\n"+backup,"老秦进度管理软件");
  }
 }
}
