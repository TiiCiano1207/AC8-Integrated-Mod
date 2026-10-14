using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace AC8Settings {
 public static class WorkbenchTest {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static void Reject(Action a,string message){bool failed=false;try{a();}catch{failed=true;}Check(failed,message);}
  public static int Run(string[] args){
   string fixture=Program.Arg(args,"--fixture",null),report=Program.Arg(args,"--report",null);var results=new List<string>();
   try{
    Check(Bundle.Present,"No embedded bundle.");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fixture)));
    Reject(()=>Bundle.EntryPath(fixture,"../escape.txt"),"ZIP traversal accepted.");
    Reject(()=>Bundle.EntryPath(fixture,"C:/escape.txt"),"Absolute ZIP path accepted.");
    Bundle.Extract(fixture,true,null);Check(ConfigStore.IsPackage(fixture),"Extracted package not recognized.");
    results.Add("Embedded bundle extracted with individual SHA256 verification.");
    Reject(()=>Bundle.Extract(fixture,true,null),"Existing nonempty package overwritten.");
    results.Add("Traversal/absolute paths rejected; existing configuration protected from overwrites.");
    var request=new OperationRequest{Root=fixture,Action="CheckPackage",MissileMode="guidance",GamePath="",SteamPath=""};
    var result=Operations.Run(request,null);Check(result.Success,"Extracted package check failed: "+result.Message);results.Add("Real package validation passed through the hidden worker.");
    request.Action="Bogus";result=Operations.Run(request,null);Check(!result.Success,"Unknown worker action accepted.");
    request.Action="Check";request.GamePath=Path.Combine(Path.GetDirectoryName(fixture),"missing-game");
    result=Operations.Run(request,null);Check(!result.Success,"Invalid game path accepted.");results.Add("Unsupported actions and invalid game paths return structured errors.");
    string fake=Path.Combine(Path.GetDirectoryName(fixture),"game-fixture"),win=Path.Combine(fake,"Game","Binaries","Win64");Directory.CreateDirectory(win);
    File.WriteAllText(Path.Combine(win,"AceCombat8.exe"),"test-only-invalid-build");File.WriteAllText(Path.Combine(win,"dwmapi.dll"),"unknown-owner-sentinel");
    Reject(()=>Bundle.Extract(Path.Combine(fake,"mod-folder"),false,null),"Extracted inside a game directory.");
    request.GamePath=fake;request.Action="Save";request.SteamPath=Path.Combine(win,"steam.exe");
    result=Operations.Run(request,null);Check(!result.Success,"Invalid game build allowed installation.");
    Check(File.ReadAllText(Path.Combine(win,"dwmapi.dll"))=="unknown-owner-sentinel","Unknown loader touched.");
    File.WriteAllText(Path.Combine(fixture,"game-path.txt"),fake);
    foreach(string action in new[]{"Disable","Enable","CheckOriginal"}){
     request.Action=action;result=Operations.Run(request,null);
     Check(!result.Success,"Unsafe mode action succeeded with unknown loader: "+action);
     Check(File.ReadAllText(Path.Combine(win,"dwmapi.dll"))=="unknown-owner-sentinel","Unknown loader removed: "+action);
    }
    results.Add("Wrong game build blocks install; unknown loader survives all mode actions.");
    var py=new ProcessStartInfo(Path.Combine(fixture,"runtime","python","python.exe"),"-I -c \"import numpy,sys;print(numpy.__version__)\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    using(var p=Process.Start(py)){string output=p.StandardOutput.ReadToEnd(),error=p.StandardError.ReadToEnd();p.WaitForExit();Check(p.ExitCode==0,"Embedded Python/NumPy failed: "+error);results.Add("Relocated embedded Python/NumPy import passed: "+output.Trim());}
    request.Action="Analyze";result=Operations.Run(request,null);Check(!result.Success,"Analysis falsely succeeded without a session.");
    results.Add("No-session analysis produces an actionable error.");
    File.WriteAllText(report,"PASS\r\n"+string.Join("\r\n",results),Encoding.UTF8);return 0;
   }catch(Exception ex){File.WriteAllText(report,"FAIL\r\n"+ex+"\r\n"+string.Join("\r\n",results),Encoding.UTF8);return 1;}
  }
 }
}
