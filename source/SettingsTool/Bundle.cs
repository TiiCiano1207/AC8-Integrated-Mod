using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Forms=System.Windows.Forms;
namespace AC8Settings {
 public static class Bundle {
  public static bool Present{get{return Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("AC8.Bundle.zip");}}
  public static bool Compatible(string root){
   try{
    if(!ConfigStore.IsPackage(root))return false;
    var info=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"package-info.json")));
    object version;return info.TryGetValue("settings_tool_version",out version)&&Convert.ToString(version)=="1.0";
   }catch{return false;}
  }
  public static string Remembered(){
   try{var root=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\AC8Workbench","PackageRoot",null) as string;return root!=null&&Compatible(root)&&File.Exists(Path.Combine(root,".ac8-bundle.txt"))?root:null;}catch{return null;}
  }
  static void Remember(string root){try{using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\AC8Workbench"))key.SetValue("PackageRoot",root);}catch{}}
  public static Window BootstrapWindow(){
   Window window;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Bootstrap.xaml"))window=(Window)XamlReader.Load(s);
   Window theme;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))theme=(Window)XamlReader.Load(s);
   foreach(System.Collections.DictionaryEntry item in theme.Resources)window.Resources[item.Key]=item.Value;
   return window;
  }
  public static Dictionary<string,string> Hashes(){
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("AC8.Bundle.hashes.json"))
   using(var reader=new StreamReader(s))return new JavaScriptSerializer{MaxJsonLength=10000000}.Deserialize<Dictionary<string,string>>(reader.ReadToEnd());
  }
  public static string EntryPath(string root,string entry){
   if(string.IsNullOrWhiteSpace(entry)||entry.Contains(":")||entry.StartsWith("/")||entry.StartsWith("\\")||entry.Split('/','\\').Any(x=>x==".."||x==""))throw new InvalidDataException("不安全的资源路径："+entry);
   string basePath=Path.GetFullPath(root).TrimEnd('\\')+"\\",path=Path.GetFullPath(Path.Combine(basePath,entry.Replace('/',Path.DirectorySeparatorChar)));
   if(!path.StartsWith(basePath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("资源路径越过了解包目录。");
   return path;
  }
  public static void NoLinks(string path){
   string current=Path.GetFullPath(path);
   while(!string.IsNullOrEmpty(current)){
    if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("解包位置包含目录链接，请选择普通文件夹："+current);
    current=Path.GetDirectoryName(current);
   }
  }
  public static void Extract(string destination,bool copyExe,Action<int,int> progress){
   string target=Path.GetFullPath(destination).TrimEnd('\\');
   if(target==Path.GetPathRoot(target).TrimEnd('\\'))throw new IOException("请选择专用子文件夹，不要选择磁盘根目录。");
   NoLinks(target);
   for(string ancestor=target;!string.IsNullOrEmpty(ancestor);ancestor=Path.GetDirectoryName(ancestor))
    if(File.Exists(Path.Combine(ancestor,"Game","Binaries","Win64","AceCombat8.exe")))throw new IOException("MOD 应解包到游戏目录之外，请选择其他文件夹。");
   if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new IOException("目标文件夹不是空的。请选择一个新的 MOD 文件夹，避免覆盖已有设置或记录。");
   string parent=Path.GetDirectoryName(target);Directory.CreateDirectory(parent);
   string staging=target+".extract-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(staging);
   var expected=Hashes();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   int count=0;long total=0;
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AC8.Bundle.zip"))
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
    if(zip.Entries.Count!=expected.Count||zip.Entries.Count>20000)throw new InvalidDataException("资源数量校验失败。");
    foreach(var entry in zip.Entries){
     string name=entry.FullName;string hash;
     if(!seen.Add(name)||!expected.TryGetValue(name,out hash))throw new InvalidDataException("资源清单不匹配："+name);
     total+=entry.Length;if(total>750000000)throw new InvalidDataException("资源总大小超过预期。");
     string file=EntryPath(staging,name);Directory.CreateDirectory(Path.GetDirectoryName(file));
     using(var input=entry.Open())using(var output=File.Create(file))input.CopyTo(output);
     using(var input=File.OpenRead(file))using(var alg=SHA256.Create()){
      string actual=BitConverter.ToString(alg.ComputeHash(input)).Replace("-","");
      if(!actual.Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("资源校验失败："+name);
     }
     count++;if(progress!=null)progress(count,expected.Count);
    }
   }
   File.WriteAllText(Path.Combine(staging,".ac8-bundle.txt"),"AC8 Workbench 1.0\r\n"+DateTime.UtcNow.ToString("o"),Encoding.UTF8);
   if(copyExe)File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(staging,"AC8Workbench.exe"),true);
   if(Directory.Exists(target)){if(Directory.EnumerateFileSystemEntries(target).Any())throw new IOException("解包过程中目标文件夹被修改，已停止覆盖。");Directory.Delete(target);}
   Directory.Move(staging,target);
  }
  public static string ChooseAndExtract(){
   Window window=BootstrapWindow();
   var folder=(TextBox)window.FindName("Folder");var status=(TextBlock)window.FindName("Status");var bar=(ProgressBar)window.FindName("Progress");
   var browse=(Button)window.FindName("Browse");var extract=(Button)window.FindName("Extract");var cancel=(Button)window.FindName("Cancel");
   folder.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"AC8-Workbench-1.0");
   bool busy=false;string result=null;
   folder.TextChanged+=(s,e)=>{if(!busy){bool exists=Compatible(folder.Text)&&File.Exists(Path.Combine(folder.Text,".ac8-bundle.txt"));extract.Content=exists?"打开已有工具":"解包并打开工具";status.Text=exists?"已找到此前解包的 MOD，将保留现有配置和记录。":"解包会校验资源完整性，不会在这一步修改游戏目录。";}};
   browse.Click+=(s,e)=>{using(var d=new Forms.FolderBrowserDialog()){
    d.Description="选择父文件夹；工具会创建 AC8-Workbench 子文件夹";
    if(d.ShowDialog()==Forms.DialogResult.OK)folder.Text=Path.Combine(d.SelectedPath,"AC8-Workbench-1.0");
   }};
   cancel.Click+=(s,e)=>window.Close();window.Closing+=(s,e)=>{if(busy)e.Cancel=true;};
   extract.Click+=async(s,e)=>{
    string target=folder.Text.Trim().Trim('"');
    if(Compatible(target)&&File.Exists(Path.Combine(target,".ac8-bundle.txt"))){result=Path.GetFullPath(target);Remember(result);window.Close();return;}
    busy=true;folder.IsEnabled=browse.IsEnabled=extract.IsEnabled=cancel.IsEnabled=false;bar.Visibility=Visibility.Visible;
    status.Text="正在解包并逐个校验资源，请稍候…";
    try{
     await Task.Run(()=>Extract(target,true,(done,total)=>{if(done%40==0||done==total)window.Dispatcher.BeginInvoke(new Action(()=>bar.Value=done*100.0/total));}));
     result=Path.GetFullPath(target);Remember(result);busy=false;window.Close();
    }catch(Exception ex){busy=false;status.Text=ex.Message+" 若存在未完成解包，文件保留在名称含 .extract- 的同级文件夹中。";folder.IsEnabled=browse.IsEnabled=extract.IsEnabled=cancel.IsEnabled=true;bar.Visibility=Visibility.Collapsed;}
   };
   window.ShowDialog();return result;
  }
 }
}
