using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AC8Settings {
 public sealed class Rule {
  public string Key,Label; public double Min,Max,Default; public bool Integer;
  public Rule(string key,string label,double min,double max,double value,bool integer=false){Key=key;Label=label;Min=min;Max=max;Default=value;Integer=integer;}
 }
 public sealed class ActionSpec { public string Name,Label,Default; }
 public sealed class KeySpec { public string Token,Label;public int VK;public override string ToString(){return Label;} }
 public sealed class ConfigState {
  public Dictionary<string,double> Values=new Dictionary<string,double>();
  public Dictionary<string,string> Keys=new Dictionary<string,string>();
  public string Color="#70FF90";
  public ConfigState Copy(){return new ConfigState{Values=new Dictionary<string,double>(Values),Keys=new Dictionary<string,string>(Keys),Color=Color};}
  public string Signature(){return string.Join("|",Values.OrderBy(x=>x.Key).Select(x=>x.Key+"="+x.Value.ToString("R",CultureInfo.InvariantCulture)))+";"+string.Join("|",Keys.OrderBy(x=>x.Key).Select(x=>x.Key+"="+x.Value))+";"+Color.ToUpperInvariant();}
 }
 public sealed class ConfigStore {
  public static readonly Rule[] Rules={
   new Rule("sensitivity","鼠标灵敏度",.01,1,.10),
   new Rule("free_look_zoom","自由观察放大",1,3,1.75),
   new Rule("hud_renderer","准星渲染方式",0,3,3,true),
   new Rule("hud_connector","方向刻度",0,1,1,true),
   new Rule("hud_link_always","持续显示方向刻度",0,1,0,true),
   new Rule("hud_opacity","准星不透明度",.15,1,1),
   new Rule("hud_boresight","机头参考十字",0,1,1,true),
   new Rule("control_mode","默认飞控模式",0,3,0,true),
   new Rule("model_assist","模型辅助",0,1,1,true),
   new Rule("model_assist_strength","模型辅助强度",0,.35,.2),
   new Rule("mouse_reference_fov","鼠标参考视野",30,150,62),
   new Rule("arrival_braking","备用制动参数",1,1.35,1.15),
   new Rule("hud_fps","准星更新目标",60,240,120,true),
   new Rule("follow_rate","相机响应速度",2,20,8),
   new Rule("level_rate","相机水平回正",0,10,3),
   new Rule("camera_mode","默认相机模式",0,2,1,true),
   new Rule("camera_distance_m","机外跟随距离",10,100,36),
   new Rule("camera_height_m","机外跟随高度",0,20,6),
   new Rule("first_person_follow","驾驶舱跟随圆圈",0,1,1,true),
   new Rule("first_person_response","驾驶舱响应速度",2,20,6)
  };
  public static readonly Rule[] FovRules={
   new Rule("fov_cockpit","驾驶舱 FOV",0,130,0),
   new Rule("fov_hud","第一人称 FOV",0,130,0),
   new Rule("fov_third_person","机外 FOV",0,130,0)
  };
  public static readonly string[] FovTokens={"Cockpit","HUD","ThirdPerson"};
  public static readonly string[] Files={"MouseAim-Settings.ini","Keybindings.ini","Hud-Style.ini","FOV.ini"};
  public string Root;
  public List<ActionSpec> Actions=new List<ActionSpec>();
  public List<KeySpec> KeyChoices=new List<KeySpec>();
  public ConfigState State;
  public string Version="1.0";
  Dictionary<string,byte[]> original=new Dictionary<string,byte[]>();
  public ConfigStore(string root){Root=Path.GetFullPath(root);ReadSchema();Load();}
  public static bool IsPackage(string root){
   return Directory.Exists(root)&&new[]{"MouseAim-Settings.ini","Keybindings.ini","Keybindings.schema.json","MouseAim-Settings.ps1"}.All(x=>File.Exists(Path.Combine(root,x)));
  }
  void ReadSchema(){
   var json=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(Root,"Keybindings.schema.json"),Encoding.UTF8)) as Dictionary<string,object>;
   foreach(var a in (object[])json["actions"]){
    var d=(Dictionary<string,object>)a;
    Actions.Add(new ActionSpec{Name=(string)d["name"],Label=(string)d["label"],Default=(string)d["default"]});
   }
   foreach(var pair in (Dictionary<string,object>)json["keys"]){
    var d=(Dictionary<string,object>)pair.Value;
    string label=(string)d["label"];
    if(pair.Key=="MouseRight")label="鼠标右键";if(pair.Key=="MouseLeft")label="鼠标左键";
    if(pair.Key=="None")label="未绑定";
    KeyChoices.Add(new KeySpec{Token=pair.Key,Label=label,VK=Convert.ToInt32(d["vk"])});
   }
  }
  public ConfigState Defaults(){
   var s=new ConfigState();
   foreach(var r in Rules)s.Values[r.Key]=r.Default;
   foreach(var r in FovRules)s.Values[r.Key]=r.Default;
   foreach(var a in Actions)s.Keys[a.Name]=a.Default;
   return s;
  }
  public void Load(){
   if(!IsPackage(Root))throw new InvalidOperationException("请选择完整的 AC8 MOD 文件夹，需包含设置文件和键位定义。");
   original.Clear();
   foreach(var f in Files)original[f]=File.Exists(Path.Combine(Root,f))?File.ReadAllBytes(Path.Combine(Root,f)):null;
   State=Parse(original);
   try{var info=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(Root,"package-info.json"))) as Dictionary<string,object>;Version=(string)info["version"];}catch{}
  }
  static string Text(byte[] b){if(b==null)return "";return Encoding.UTF8.GetString(b).TrimStart('\uFEFF');}
  public ConfigState Parse(Dictionary<string,byte[]> data){
   var s=Defaults();var seen=new HashSet<string>();
   foreach(string raw in Text(data["MouseAim-Settings.ini"]).Split('\n')){
    var line=raw.Trim();if(line.Length==0||line.StartsWith(";")||line.StartsWith("#"))continue;
    var m=Regex.Match(line,@"^([a-z_]+)\s*=\s*([0-9]+(?:\.[0-9]+)?)$");
    if(!m.Success)throw new FormatException("鼠标配置格式无效："+line);
    string key=m.Groups[1].Value;
    if(!Rules.Any(x=>x.Key==key))throw new FormatException("工具尚不支持这个参数："+key);
    if(!seen.Add(key))throw new FormatException("配置里重复出现了参数："+key);
    s.Values[key]=double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture);
   }
   seen.Clear();bool section=false;
   foreach(string raw in Text(data["Keybindings.ini"]).Split('\n')){
    string line=raw.Trim();if(line.Length==0||line.StartsWith(";")||line.StartsWith("#"))continue;
    if(line.Equals("[bindings]",StringComparison.OrdinalIgnoreCase)){if(section)throw new FormatException("键位配置含重复分区。");section=true;continue;}
    var m=Regex.Match(line,@"^([a-z_]+)\s*=\s*([A-Za-z0-9]+)$");
    if(!section||!m.Success)throw new FormatException("键位配置格式无效："+line);
    string name=m.Groups[1].Value;
    if(!s.Keys.ContainsKey(name)||!seen.Add(name))throw new FormatException("键位功能未知或重复："+name);
    var key=KeyChoices.FirstOrDefault(x=>x.Token.Equals(m.Groups[2].Value,StringComparison.OrdinalIgnoreCase));
    if(key==null)throw new FormatException("不支持的按键："+m.Groups[2].Value);
    s.Keys[name]=key.Token;
   }
   var colors=Regex.Matches(Text(data["Hud-Style.ini"]),@"(?mi)^\s*color\s*=\s*(#[0-9a-f]{6})\s*$");
   if(data["Hud-Style.ini"]!=null){
    if(colors.Count!=1)throw new FormatException("准星颜色配置应包含一个 color=#RRGGBB。");
    s.Color=colors[0].Groups[1].Value.ToUpperInvariant();
   }
   if(data["FOV.ini"]!=null){
    var fovSeen=new HashSet<string>();
    foreach(string raw in Text(data["FOV.ini"]).Split('\n')){
     string line=raw.Trim();if(line.Length==0||line.StartsWith("#")||line.StartsWith(";"))continue;
     var m=Regex.Match(line,@"^([A-Za-z]+)\s*=\s*([0-9]+(?:\.[0-9]+)?)$");
     int index=m.Success?Array.IndexOf(FovTokens,m.Groups[1].Value):-1;
     if(index<0||!fovSeen.Add(FovTokens[index]))throw new FormatException("FOV 配置未知或重复："+line);
     s.Values[FovRules[index].Key]=double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture);
    }
    if(fovSeen.Count!=3)throw new FormatException("FOV 配置需要 Cockpit、HUD、ThirdPerson 三项。");
   }
   // Normalize supported legacy control profiles in the same way as the launcher.
   if(s.Values["control_mode"]==1)s.Values["control_mode"]=0;
   if(new[]{2.0,4.0,5.0}.Contains(s.Values["control_mode"]))s.Values["control_mode"]=3;
   Validate(s);return s;
  }
  public void Validate(ConfigState s){
   foreach(var r in Rules){
    double v;
    if(!s.Values.TryGetValue(r.Key,out v)||double.IsNaN(v)||double.IsInfinity(v)||v<r.Min||v>r.Max||(r.Integer&&v!=Math.Floor(v)))
     throw new InvalidOperationException(r.Label+"需要在 "+r.Min.ToString(CultureInfo.InvariantCulture)+"–"+r.Max.ToString(CultureInfo.InvariantCulture)+" 范围内"+(r.Integer?"取整数。":"。"));
   }
   foreach(var r in FovRules){
    double v;if(!s.Values.TryGetValue(r.Key,out v)||double.IsNaN(v)||double.IsInfinity(v)||(v!=0&&(v<40||v>130)))
     throw new InvalidOperationException(r.Label+"应为 0（原生）或 40–130 度。");
   }
   if(s.Values["control_mode"]!=0&&s.Values["control_mode"]!=3)throw new InvalidOperationException("默认飞控模式只能选择 PEACE 或 WAR。");
   if(!Regex.IsMatch(s.Color,@"^#[0-9A-Fa-f]{6}$"))throw new InvalidOperationException("准星颜色请填写 #RRGGBB，例如 #70FF90。");
   var assigned=new Dictionary<int,string>();
   foreach(var a in Actions){
    string token;if(!s.Keys.TryGetValue(a.Name,out token))throw new InvalidOperationException("缺少键位："+a.Label);
    var k=KeyChoices.FirstOrDefault(x=>x.Token==token);
    if(k==null)throw new InvalidOperationException("不支持的按键："+token);
    if(k.VK!=0&&assigned.ContainsKey(k.VK))throw new InvalidOperationException("按键冲突："+k.Label+" 同时用于“"+assigned[k.VK]+"”和“"+a.Label+"”。");
    if(k.VK!=0)assigned[k.VK]=a.Label;
   }
  }
  static byte[] Encode(string text){return new UTF8Encoding(false).GetBytes(text);}
  static string ReplaceValue(string text,string key,string value){
   string pattern=@"(?m)^(\s*"+Regex.Escape(key)+@"\s*=\s*)[^\r\n]*";
   if(Regex.IsMatch(text,pattern))return Regex.Replace(text,pattern,m=>m.Groups[1].Value+value);
   return text.TrimEnd()+"\r\n"+key+"="+value+"\r\n";
  }
  Dictionary<string,byte[]> Serialize(ConfigState s){
   var data=new Dictionary<string,byte[]>();
   string text=Text(original["MouseAim-Settings.ini"]);
   foreach(var r in Rules)text=ReplaceValue(text,r.Key,s.Values[r.Key].ToString("0.########",CultureInfo.InvariantCulture));
   data[Files[0]]=Encode(text);
   text=Text(original["Keybindings.ini"]);
   if(!text.Contains("[bindings]"))text="; Applied on the next MOD launch.\r\n[bindings]\r\n";
   foreach(var a in Actions)text=ReplaceValue(text,a.Name,s.Keys[a.Name]);
   data[Files[1]]=Encode(text);
   text=original[Files[2]]==null?"; Reticle color; applied on next launch. Native HUD only.\r\n":Text(original[Files[2]]);
   data[Files[2]]=Encode(ReplaceValue(text,"color",s.Color.ToUpperInvariant()));
   text=original["FOV.ini"]==null?"# Horizontal FOV; 0 keeps the original game FOV.\r\n":Text(original["FOV.ini"]);
   for(int i=0;i<3;i++)text=ReplaceValue(text,FovTokens[i],s.Values[FovRules[i].Key].ToString("0.###",CultureInfo.InvariantCulture));
   data["FOV.ini"]=Encode(text);
   return data;
  }
  static bool Same(byte[] a,byte[] b){return a==null?b==null:b!=null&&a.SequenceEqual(b);}
  void AssertUnchanged(){
   foreach(string f in Files){
    string p=Path.Combine(Root,f);byte[] now=File.Exists(p)?File.ReadAllBytes(p):null;
    if(!Same(original[f],now))throw new InvalidOperationException("配置已被其他程序修改。请先重新读取配置，再保存，避免覆盖新设置。");
   }
  }
  public static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","");}
  string MakeBackup(){
   string folder=Path.Combine(Root,"settings-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
   Directory.CreateDirectory(folder);
   var manifest=new Dictionary<string,string>();
   foreach(string f in Files){
    byte[] b=original[f];manifest[f]=b==null?"MISSING":Hash(b);
    if(b!=null)File.WriteAllBytes(Path.Combine(folder,f),b);
   }
   File.WriteAllText(Path.Combine(folder,"backup.json"),new JavaScriptSerializer().Serialize(manifest),Encoding.UTF8);
   return folder;
  }
  static void AtomicWrite(string path,byte[] data){
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    File.WriteAllBytes(temp,data);
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public string MutexName(){return "Local\\AC8Config-"+Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(Root).TrimEnd('\\','/').ToLowerInvariant()));}
  void Commit(Dictionary<string,byte[]> data){
   using(var mutex=new System.Threading.Mutex(false,MutexName())){
    bool held=false;
    try{
     try{held=mutex.WaitOne(5000);}catch(System.Threading.AbandonedMutexException){held=true;}
     if(!held)throw new InvalidOperationException("设置正在同步，请稍后重试。");
     CommitLocked(data);
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }
  void CommitLocked(Dictionary<string,byte[]> data){
   AssertUnchanged();MakeBackup();var applied=new List<string>();
   try{
    foreach(string f in Files){
     string path=Path.Combine(Root,f);applied.Add(f);
     if(data[f]==null){if(File.Exists(path))File.Delete(path);}else AtomicWrite(path,data[f]);
    }
   }catch{
    foreach(string f in applied.AsEnumerable().Reverse()){
     string path=Path.Combine(Root,f);
     if(original[f]==null){if(File.Exists(path))File.Delete(path);}else AtomicWrite(path,original[f]);
    }
    throw;
   }
   Load();
  }
  bool RuntimeTagChanged(string key,Dictionary<string,byte[]> now){
   string pattern=@"(?m)^; AC8 runtime "+Regex.Escape(key)+@" [^\r\n]*";
   return Regex.Match(Text(original[Files[0]]),pattern).Value!=Regex.Match(Text(now[Files[0]]),pattern).Value;
  }
  public bool Refresh(ConfigState pending){
   var now=Files.ToDictionary(f=>f,f=>File.Exists(Path.Combine(Root,f))?File.ReadAllBytes(Path.Combine(Root,f)):null);
   if(Files.All(f=>Same(original[f],now[f])))return false;
   var latest=Parse(now);
   // External changes win conflicts. Keep unrelated edits in the menu.
   foreach(var r in Rules.Concat(FovRules))if(latest.Values[r.Key]!=State.Values[r.Key]||RuntimeTagChanged(r.Key,now))pending.Values[r.Key]=latest.Values[r.Key];
   foreach(var a in Actions)if(latest.Keys[a.Name]!=State.Keys[a.Name])pending.Keys[a.Name]=latest.Keys[a.Name];
   if(!string.Equals(latest.Color,State.Color,StringComparison.OrdinalIgnoreCase))pending.Color=latest.Color;
   original=now;State=latest;return true;
  }
  public void Save(ConfigState s){Validate(s);Commit(Serialize(s));}
  public string LatestBackup(){
   string folder=Path.Combine(Root,"settings-backups");
   if(!Directory.Exists(folder))return null;
   return Directory.GetDirectories(folder).Where(x=>File.Exists(Path.Combine(x,"backup.json"))).OrderByDescending(x=>x,StringComparer.Ordinal).FirstOrDefault();
  }
  public void Restore(){
   string folder=LatestBackup();if(folder==null)throw new InvalidOperationException("还没有备份。首次保存设置时会自动建立备份。");
   var manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"backup.json")));
   var data=new Dictionary<string,byte[]>();
   foreach(string f in Files){
    string h;if(!manifest.TryGetValue(f,out h)){
     if(f=="FOV.ini"){data[f]=original[f];continue;}
     throw new InvalidOperationException("备份不完整："+f);
    }
    data[f]=h=="MISSING"?null:File.ReadAllBytes(Path.Combine(folder,f));
    if(data[f]!=null&&Hash(data[f])!=h)throw new InvalidOperationException("备份校验失败："+f);
   }
   Parse(data);Commit(data);
  }
  public static Rule Find(string key){return Rules.Concat(FovRules).First(x=>x.Key==key);}
 }
}
