using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
namespace AC8Settings {
 public static class SelfTest {
  static IEnumerable<DependencyObject> Children(DependencyObject root){
   yield return root;
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;
  }
  public static int UI(string[] args){
   string root=Program.Arg(args,"--package",null),report=Program.Arg(args,"--report",null);
   try{
    var app=new Application();var ui=new SettingsWindow(root,true);
    ui.Window.Left=-12000;ui.Window.Top=-12000;ui.Window.WindowStartupLocation=WindowStartupLocation.Manual;ui.Window.ShowInTaskbar=false;ui.Window.ShowActivated=false;ui.Window.Show();
    ui.ShowPage(0);ui.Window.UpdateLayout();
    Check(!Children(ui.Window).OfType<TextBlock>().Any(x=>x.Text=="灵敏度快捷值"),"Sensitivity presets remain.");
    var saveButton=(Button)ui.Window.FindName("Save");var startButton=(Button)ui.Window.FindName("StartGame");
    var steamChoice=(CheckBox)ui.Window.FindName("SteamLaunch");
    Check((string)startButton.Tag=="primary"&&saveButton.Tag==null,"Action hierarchy is wrong.");
    Check(saveButton.TranslatePoint(new Point(),ui.Window).X<steamChoice.TranslatePoint(new Point(),ui.Window).X&&steamChoice.TranslatePoint(new Point(),ui.Window).X<startButton.TranslatePoint(new Point(),ui.Window).X,"Footer action order is wrong.");
    Check(ui.Window.FindName("Theme")==null,"Theme toggle remains.");
    ui.ShowPage(8);ui.Window.UpdateLayout();
    Check(Children((DependencyObject)ui.Window.FindName("PageBody")).OfType<Button>().Count()==1,"Help includes non-install actions.");
    ui.ShowPage(0);ui.Window.UpdateLayout();
    var input=Children(ui.Window).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="鼠标灵敏度数值");
    string before=File.ReadAllText(Path.Combine(root,"MouseAim-Settings.ini"));
    input.Text="10";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(File.ReadAllText(Path.Combine(root,"MouseAim-Settings.ini"))==before,"Invalid UI input wrote configuration.");
    input.Text="0.123";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["sensitivity"]==.123,"UI numeric input did not save.");
    ui.ShowPage(2);ui.Window.UpdateLayout();
    input=Children(ui.Window).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="准星颜色十六进制值");
    input.Text="#FFBD4A";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Color=="#FFBD4A","UI color did not save.");
    ui.ShowPage(1);ui.Window.UpdateLayout();
    var modes=Children(ui.Window).OfType<ComboBox>().Single(x=>AutomationProperties.GetName(x)=="机外相机模式");
    modes.SelectedIndex=1;((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["camera_mode"]==1,"Camera selection not saved.");
    ui.Window.UpdateLayout();
    var fov=Children(ui.Window).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="驾驶舱 FOV数值");
    string fovBefore=File.ReadAllText(Path.Combine(root,"FOV.ini"));
    fov.Text="20";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(File.ReadAllText(Path.Combine(root,"FOV.ini"))==fovBefore,"Invalid FOV UI input wrote settings.");
    fov.Text="120";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["fov_cockpit"]==120,"FOV UI input not saved.");
    ui.Window.UpdateLayout();
    var fpResponse=Children(ui.Window).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="驾驶舱响应速度数值");
    string fpBefore=File.ReadAllText(Path.Combine(root,"MouseAim-Settings.ini"));
    fpResponse.Text="25";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(File.ReadAllText(Path.Combine(root,"MouseAim-Settings.ini"))==fpBefore,"Invalid first-person response saved.");
    fpResponse.Text="4.5";((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["first_person_response"]==4.5,"First-person response not saved.");
    var fpToggle=Children(ui.Window).OfType<CheckBox>().Single(x=>AutomationProperties.GetName(x)=="驾驶舱跟随圆圈");
    fpToggle.IsChecked=false;((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["first_person_follow"]==0,"First-person follow toggle not saved.");
    ((Button)ui.Window.FindName("PageDefaults")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    ((Button)ui.Window.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(new ConfigStore(root).State.Values["camera_mode"]==1,"Page defaults did not restore original external follow.");
    Check(new ConfigStore(root).State.Values["fov_cockpit"]==0,"FOV page defaults did not restore original FOV.");
    Check(new ConfigStore(root).State.Values["first_person_follow"]==1&&new ConfigStore(root).State.Values["first_person_response"]==6,"First-person defaults not restored.");
    ui.Window.Close();
    File.WriteAllText(report,"PASS\r\nUI numeric validation, numeric/color/FOV save, camera selection, first-person toggle/response and page defaults.",Encoding.UTF8);
    return 0;
   }catch(Exception ex){File.WriteAllText(report,"FAIL\r\n"+ex,Encoding.UTF8);return 1;}
  }
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,message);}
  public static int Run(string[] args){
   string source=Program.Arg(args,"--package",null),fixture=Program.Arg(args,"--fixture",null),report=Program.Arg(args,"--report",null);
   if(source==null||fixture==null||report==null)throw new ArgumentException("Self-test needs --package, --fixture, --report.");
   var checks=new List<string>();
   try{
    Directory.CreateDirectory(fixture);
    foreach(string f in new[]{"MouseAim-Settings.ini","Keybindings.ini","Keybindings.schema.json","MouseAim-Settings.ps1","package-info.json","Hud-Style.ini","FOV.ini"})
     if(File.Exists(Path.Combine(source,f)))File.Copy(Path.Combine(source,f),Path.Combine(fixture,f),true);
    var store=new ConfigStore(fixture);
    double initialCamera=store.State.Values["camera_mode"];checks.Add("Current settings and actual keyboard schema load correctly.");
    var state=store.State.Copy();state.Values["sensitivity"]=double.NaN;Reject(()=>store.Validate(state),"NaN accepted.");
    state=store.State.Copy();state.Values["hud_fps"]=120.5;Reject(()=>store.Validate(state),"Fractional frequency accepted.");
    state=store.State.Copy();state.Values["camera_mode"]=3;Reject(()=>store.Validate(state),"Unknown camera mode accepted.");
    state=store.State.Copy();state.Keys["free_look"]=state.Keys["pitch_up"];Reject(()=>store.Validate(state),"Duplicate key accepted.");
    state=store.State.Copy();state.Color="#12345";Reject(()=>store.Validate(state),"Malformed color accepted.");
    state=store.State.Copy();state.Values["fov_cockpit"]=20;Reject(()=>store.Validate(state),"FOV below 40 accepted.");
    state=store.State.Copy();state.Values["fov_hud"]=131;Reject(()=>store.Validate(state),"FOV above 130 accepted.");
    checks.Add("Ranges, integer settings, unsupported camera modes, key conflicts and malformed colors rejected.");
    var originals=ConfigStore.Files.ToDictionary(f=>f,f=>File.Exists(Path.Combine(fixture,f))?File.ReadAllBytes(Path.Combine(fixture,f)):null);
    state=store.State.Copy();state.Values["sensitivity"]=.123;state.Color="#8CFFB0";
    state.Values["fov_cockpit"]=120;state.Values["fov_hud"]=90;state.Values["fov_third_person"]=0;
    store.Save(state);Check(store.State.Values["sensitivity"]==.123&&store.State.Color=="#8CFFB0","Save/read round trip failed.");
    Check(store.State.Values["camera_mode"]==initialCamera,"Save changed selected camera mode.");
    Check(store.State.Values["fov_cockpit"]==120&&store.State.Values["fov_hud"]==90&&store.State.Values["fov_third_person"]==0,"FOV save/read round trip failed.");
    Check(File.ReadAllText(Path.Combine(fixture,"MouseAim-Settings.ini")).Contains("; Reference gun cross"),"INI comments lost.");
    Check(store.LatestBackup()!=null,"No automatic backup.");
    checks.Add("Three-file save round trip preserves comments and cockpit mode; automatic backup created.");
    store.Restore();
    foreach(string f in ConfigStore.Files){
     string p=Path.Combine(fixture,f);
     Check(originals[f]==null?!File.Exists(p):originals[f].SequenceEqual(File.ReadAllBytes(p)),"Restore bytes differ: "+f);
    }
    checks.Add("Backup restore reproduces exact previous bytes (including missing optional style file).");
    store=new ConfigStore(fixture);
    File.AppendAllText(Path.Combine(fixture,"MouseAim-Settings.ini"),"\r\n; external edit\r\n");
    state=store.State.Copy();state.Values["sensitivity"]=.2;
    Reject(()=>store.Save(state),"Concurrent external modification was overwritten.");
    Check(File.ReadAllText(Path.Combine(fixture,"MouseAim-Settings.ini")).Contains("; external edit"),"External edit lost.");
    checks.Add("External modifications are detected before writing, preventing stale overwrites.");
    store=new ConfigStore(fixture);state=store.State.Copy();
    state.Values["sensitivity"]=.25;state.Values["free_look_zoom"]=2.5;
    string settingPath=Path.Combine(fixture,"MouseAim-Settings.ini");
    string external=System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(settingPath),@"(?m)^sensitivity=[^\r\n]*","sensitivity=0.33");
    File.WriteAllText(settingPath,external);
    Check(store.Refresh(state),"External settings did not refresh.");
    Check(state.Values["sensitivity"]==.33&&state.Values["free_look_zoom"]==2.5,"Game priority or unrelated pending edit preservation failed.");
    store.Save(state);Check(store.State.Values["sensitivity"]==.33,"Refreshed game sensitivity overwritten.");
    state=store.State.Copy();state.Values["sensitivity"]=.5;
    File.AppendAllText(settingPath,"\r\n; AC8 runtime sensitivity test-session 2\r\n");
    store.Refresh(state);Check(state.Values["sensitivity"]==.33,"Same-value game event did not override a pending menu edit.");
    checks.Add("Game settings win conflicts; unrelated pending menu edits survive; same-value runtime events also win.");
    File.WriteAllText(report,"PASS\r\n"+string.Join("\r\n",checks),Encoding.UTF8);return 0;
   }catch(Exception ex){File.WriteAllText(report,"FAIL\r\n"+ex.ToString()+"\r\n"+string.Join("\r\n",checks),Encoding.UTF8);return 1;}
  }
 }
}
