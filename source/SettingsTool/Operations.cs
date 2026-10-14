using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading;
namespace AC8Settings {
 public static class RootOwnership {
  static Mutex current;static string owned;
  public static bool Acquire(string root){
   string canonical=Path.GetFullPath(root).TrimEnd('\\','/').ToLowerInvariant();
   if(current!=null&&owned==canonical)return true;
   bool created;var next=new Mutex(true,"Local\\AC8Gui-"+ConfigStore.Hash(Encoding.UTF8.GetBytes(canonical)),out created);
   if(!created){next.Dispose();return false;}
   Release();current=next;owned=canonical;return true;
  }
  public static void Release(){if(current!=null){current.ReleaseMutex();current.Dispose();current=null;owned=null;}}
 }
 public sealed class OperationRequest {
  public string Action,Root,GamePath,SteamPath,MissileMode;
  public bool ConfirmRecovery;
 }
 public sealed class OperationResult {
  public bool Success;public string Title,Message,GameRoot,SteamPath,Option;
  public string[] GameCandidates,SteamCandidates;
 }
 public static class Operations {
  public static string Quote(string arg){
   if(arg==null)return "\"\"";
   var b=new StringBuilder("\"");int slashes=0;
   foreach(char c in arg){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
   b.Append('\\',slashes*2);b.Append('"');return b.ToString();
  }
  public static OperationResult Run(OperationRequest r,Action<string> output){
   if(!ConfigStore.IsPackage(r.Root))throw new InvalidOperationException("MOD 文件夹不完整，请选择完整解包后的目录。");
   string folder=Path.Combine(r.Root,"diagnostics","workbench-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
   Directory.CreateDirectory(folder);
   string request=Path.Combine(folder,"request.json"),response=Path.Combine(folder,"response.json");
   var json=new JavaScriptSerializer();File.WriteAllText(request,json.Serialize(r),Encoding.UTF8);
   string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
   var start=new ProcessStartInfo(ps,"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(r.Root,"Workbench-Worker.ps1"))+" -Request "+Quote(request)+" -Response "+Quote(response));
   start.UseShellExecute=false;start.CreateNoWindow=true;start.WindowStyle=ProcessWindowStyle.Hidden;
   start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.StandardOutputEncoding=Encoding.UTF8;start.StandardErrorEncoding=Encoding.UTF8;
   var log=new StringBuilder();object gate=new object();
   Action<string> line=s=>{if(s==null)return;lock(gate){log.AppendLine(s);}if(output!=null)output(s);};
   using(var p=new Process{StartInfo=start}){
    p.OutputDataReceived+=(s,e)=>line(e.Data);p.ErrorDataReceived+=(s,e)=>line(e.Data);
    p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();p.WaitForExit();
    File.WriteAllText(Path.Combine(folder,"operation.log"),log.ToString(),Encoding.UTF8);
    if(!File.Exists(response))return new OperationResult{Success=false,Title="操作未完成",Message="后台脚本提前结束。请查看日志："+Path.Combine(folder,"operation.log")};
   }
   return json.Deserialize<OperationResult>(File.ReadAllText(response,Encoding.UTF8));
  }
 }
}
