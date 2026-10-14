using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace AC8Settings {
 public sealed partial class SettingsWindow {
  string gamePath="",steamPath="",missileMode="guidance",launchOption="";
  bool busy;
  string[] gameCandidates=new string[0],steamCandidates=new string[0];
  TextBox gameInput,steamInput,optionInput,logInput;
  ComboBox featureInput;
  readonly List<string> operationLog=new List<string>();
  void LoadInstallation(){
   gameInput=null;steamInput=null;optionInput=null;featureInput=null;
   gamePath=ReadPackage("game-path.txt");steamPath=ReadPackage("steam-path.txt");launchOption=ReadPackage("Steam-Launch-Option.txt");
   savedSteamLaunch=ReadPackage("Launcher-Options.ini")!="steam_launch=0";
   ((CheckBox)Window.FindName("SteamLaunch")).IsChecked=savedSteamLaunch;
   string feature=ReadPackage("features.ini");
   missileMode=feature.Contains("missile_mode=none")?"none":feature.Contains("missile_mode=full")||feature.Contains("missile_enhancement=1")?"full":"guidance";
  }
  string ReadPackage(string name){string p=Path.Combine(store.Root,name);return File.Exists(p)?File.ReadAllText(p).Trim().Trim('\uFEFF'):"";}
  void CommitInstallFields(){
   if(gameInput!=null)gamePath=gameInput.Text;
   if(steamInput!=null)steamPath=steamInput.Text;
   if(featureInput!=null&&featureInput.SelectedIndex>=0)missileMode=new[]{"guidance","full","none"}[featureInput.SelectedIndex];
  }
  Button ActionButton(string label,Action click,bool primary=false){
   var b=new Button{Content=label,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,9,9),FontSize=12};
   if(primary)b.Tag="primary";b.Click+=(s,e)=>click();return b;
  }
  void Actions(params Button[] buttons){var wrap=new WrapPanel{Margin=new Thickness(0,12,0,3)};foreach(var b in buttons)wrap.Children.Add(b);body.Children.Add(wrap);}
  void InstallationPage(){
   gameInput=null;steamInput=null;featureInput=null;optionInput=null;
   PathRow("游戏位置","可以选择游戏根目录、Win64 目录或 AceCombat8.exe。",gamePath,false);
   PathRow("Steam 程序","选择 Steam 安装目录中的 steam.exe。",steamPath,true);
   Actions(ActionButton("自动定位",()=>RunOperation("Discover")),ActionButton("检查环境",()=>RunOperation("Check")),ActionButton("校验安装包",()=>RunOperation("CheckPackage")));
   Section("选择安装内容","三种方案都包含鼠标飞控；导弹模块按你的选择启用。");
   var row=Row("安装方案","仅比例引导保留原版导弹性能；完整强化包含性能、近炸与外观。");
   featureInput=new ComboBox{VerticalAlignment=VerticalAlignment.Center};
   foreach(string s in new[]{"飞控 + 仅比例引导","飞控 + 完整导弹强化","仅鼠标飞控"})featureInput.Items.Add(s);
   featureInput.SelectedIndex=Array.IndexOf(new[]{"guidance","full","none"},missileMode);
   featureInput.SelectionChanged+=(s,e)=>{if(featureInput.SelectedIndex>=0)missileMode=new[]{"guidance","full","none"}[featureInput.SelectedIndex];};
   Grid.SetColumn(featureInput,1);row.Children.Add(featureInput);

   Section("Steam 启动选项","首次安装或移动 MOD 文件夹后：保存方案，再将下面这一行粘贴到 Steam → 游戏属性 → 启动选项。");
   optionInput=new TextBox{Text=launchOption,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontFamily=numbers,FontSize=12,MinHeight=62};
   body.Children.Add(optionInput);
   Actions(ActionButton("复制启动选项",()=>{if(string.IsNullOrWhiteSpace(launchOption)){SetStatus("先保存安装方案，工具才会生成正确的启动选项。",true);return;}try{Clipboard.SetText(launchOption);SetStatus("启动选项已复制 · 请粘贴到 Steam 的游戏属性中");}catch(Exception ex){SetStatus(ex.Message,true);}}));
   Note("安装方案保存后，由 Steam 完成云同步，再自动部署 MOD。游戏退出后保留原有清理流程；本工具不会直接改写 Steam 账户配置。");
   Logs();
  }
  void PathRow(string label,string hint,string value,bool steam){
   var row=Row(label,hint);
   row.ColumnDefinitions[1].Width=new GridLength(Math.Max(240,Math.Min(360,(Window.Width-274)*.52)));
   var controls=new Grid();controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var input=new TextBox{Text=value,FontSize=12,MinHeight=40,VerticalAlignment=VerticalAlignment.Center};
   var browse=new Button{Content="浏览",Padding=new Thickness(11,7,11,7),Margin=new Thickness(8,0,0,0),FontSize=12};
   if(steam)steamInput=input;else gameInput=input;
   input.TextChanged+=(s,e)=>{if(steam)steamPath=input.Text;else gamePath=input.Text;};
   browse.Click+=(s,e)=>{
    if(steam){var dialog=new Microsoft.Win32.OpenFileDialog{Title="选择 steam.exe",Filter="Steam 程序|steam.exe",CheckFileExists=true};if(dialog.ShowDialog(Window)==true)input.Text=dialog.FileName;}
    else{using(var dialog=new Forms.FolderBrowserDialog{Description="选择游戏根目录或 Win64 文件夹",ShowNewFolderButton=false})if(dialog.ShowDialog()==Forms.DialogResult.OK)input.Text=dialog.SelectedPath;}
   };
   controls.Children.Add(input);Grid.SetColumn(browse,1);controls.Children.Add(browse);Grid.SetColumn(controls,1);row.Children.Add(controls);
   var candidates=steam?steamCandidates:gameCandidates;
   if(candidates.Length>1){
    var choice=new ComboBox{Margin=new Thickness(0,9,0,4),FontSize=12};foreach(string c in candidates)choice.Items.Add(c);
    choice.SelectionChanged+=(s,e)=>{if(choice.SelectedItem!=null)input.Text=choice.SelectedItem.ToString();};body.Children.Add(choice);
   }
  }
  void MaintenancePage(){
   bool disabled=File.Exists(Path.Combine(store.Root,"mod-disabled.flag"))||(File.Exists(Path.Combine(Path.GetDirectoryName(store.Root),"AC8-Managed-Packages.json"))&&File.Exists(Path.Combine(Path.GetDirectoryName(store.Root),"mod-disabled.flag")));
   Section(disabled?"模组当前已停用":"模组当前已启用","只切换本整合包的启动模式，保留游戏原文件。");
   Actions(ActionButton("启用模组",()=>RunOperation("Enable")),ActionButton("停用模组",()=>RunOperation("Disable")),ActionButton("检查原版启动条件",()=>RunOperation("CheckOriginal")));
   Note("启停前需退出游戏。原有检查会确认加载器归属与会话状态，不会删除未知 MOD 文件。");
   Section("清理与恢复","正常退出会自动清理；异常退出后，可在这里恢复。");
   Actions(ActionButton("清理当前会话",()=>RunOperation("Cleanup")),ActionButton("备份并清理历史残留",()=>{
    CommitInstallFields();
    if(MessageBox.Show(Window,"将先备份、校验，再清理可确认属于本整合包的残留。\n\n请确认这些文件来自本 MOD；其他或未知 MOD 会被保留。\n\n游戏位置："+gamePath,"备份并清理历史残留",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)RunOperation("Recover",true);
   }));
   Actions(ActionButton("打开清理备份",()=>OpenFolder("cleanup-backups")),ActionButton("打开设置备份",()=>OpenFolder("settings-backups")));
   Logs();
  }
  void DiagnosticsPage(){
   Section("运行记录","查看会话、错误诊断与日志，不需要重新启动游戏。");
   Actions(ActionButton("打开运行记录",()=>OpenFolder("sessions")),ActionButton("打开诊断目录",()=>OpenFolder("diagnostics")),ActionButton("打开 MOD 文件夹",()=>OpenFolder("")));
   Section("分析最近一次运行","已内置分析所需的 Python 与 NumPy；分析结果写入对应会话目录。");
   Actions(ActionButton("分析最近一次运行",()=>RunOperation("Analyze"),true),ActionButton("校验安装包",()=>RunOperation("CheckPackage")));
   Logs();
  }
  void HelpPage(){
   Section("安装 MOD","将完整文件夹解压到游戏目录之外，双击 AC8Workbench.exe。");
   Note("1. 在“安装与启动”中选择游戏位置、Steam 程序和安装方案。\n2. 点击左下角“保存设置”。\n3. 首次使用或移动文件夹后，复制启动选项，粘贴到 Steam → 游戏属性 → 启动选项。\n4. 点击右下角“开始游戏”。游戏退出后会自动清理本次部署。");
   Actions(ActionButton("打开安装说明",()=>OpenDocument("docs/INSTALL.md")));
  }
  void Logs(){
   Section("操作日志","后台处理时这里会持续显示输出。");
   logInput=new TextBox{Text=string.Join(Environment.NewLine,operationLog),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=115,MaxHeight=240,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=numbers,FontSize=12};
   body.Children.Add(logInput);
  }
  void OpenFolder(string relative){
   string folder=Path.Combine(store.Root,relative);
   if(!Directory.Exists(folder)){SetStatus("这个目录尚未生成："+relative);return;}
   try{Process.Start(new ProcessStartInfo("explorer.exe",Operations.Quote(folder)){UseShellExecute=true});}catch(Exception ex){SetStatus(ex.Message,true);}
  }
  void OpenDocument(string relative){
   string file=Path.Combine(store.Root,relative.Replace('/',Path.DirectorySeparatorChar));
   if(!File.Exists(file)){SetStatus("说明文件不存在："+relative,true);return;}
   try{Process.Start(new ProcessStartInfo("notepad.exe",Operations.Quote(file)){UseShellExecute=true});}catch(Exception ex){SetStatus(ex.Message,true);}
  }
  void SetBusy(bool value){
   busy=value;body.IsEnabled=nav.IsEnabled=!value;
   ((Button)Window.FindName("ChoosePackage")).IsEnabled=!value;((Button)Window.FindName("Reload")).IsEnabled=!value;
   ((Button)Window.FindName("StartGame")).IsEnabled=!value;
   ((CheckBox)Window.FindName("SteamLaunch")).IsEnabled=!value;
   save.IsEnabled=!value&&(page==5||Dirty());defaults.IsEnabled=!value;restore.IsEnabled=!value&&store.LatestBackup()!=null;
   ((ProgressBar)Window.FindName("Busy")).Visibility=value?Visibility.Visible:Visibility.Collapsed;
  }
  async void RunOperation(string action,bool confirm=false){
   if(busy||store==null)return;
   CommitInstallFields();if(!CommitEditors())return;
   RefreshConfiguration();
   if((action=="Start"||action=="Save")&&Dirty()&&!Save())return;
   bool steam=((CheckBox)Window.FindName("SteamLaunch")).IsChecked==true;
   if(action=="Start"&&!steam)action="StartDirect";
   var request=new OperationRequest{Action=action,Root=store.Root,GamePath=gamePath,SteamPath=steamPath,MissileMode=missileMode,ConfirmRecovery=confirm};
   var labels=new Dictionary<string,string>{{"Discover","自动定位"},{"Check","环境检查"},{"Save","保存安装方案"},{"Start","开始游戏"},{"StartDirect","开始游戏"},{"Recover","历史残留清理"},{"Cleanup","会话清理"},{"Enable","启用模组"},{"Disable","停用模组"},{"CheckOriginal","原版启动检查"},{"Analyze","运行分析"},{"CheckPackage","安装包校验"}};
   SetBusy(true);SetStatus("正在处理："+labels[action]+" · 完成前请勿关闭工具");
   try{
    if(action=="Start"||action=="StartDirect"){
     bool installationChanged=ReadPackage("game-path.txt")!=gamePath||ReadPackage("steam-path.txt")!=steamPath||string.IsNullOrWhiteSpace(launchOption);
     string storedFeature=ReadPackage("features.ini");
     string storedMode=storedFeature.Contains("missile_mode=none")?"none":storedFeature.Contains("missile_mode=full")||storedFeature.Contains("missile_enhancement=1")?"full":"guidance";
     if(installationChanged||storedMode!=missileMode){
      var installation=await Task.Run(()=>Operations.Run(new OperationRequest{Action="Save",Root=store.Root,GamePath=gamePath,SteamPath=steamPath,MissileMode=missileMode},null));
      if(!installation.Success){SetBusy(false);SetStatus(installation.Title+" · "+installation.Message,true);return;}
      if(!string.IsNullOrEmpty(installation.GameRoot)){gamePath=installation.GameRoot;request.GamePath=gamePath;}
      if(!string.IsNullOrEmpty(installation.Option))launchOption=installation.Option;
     }
    }
    var result=await Task.Run(()=>Operations.Run(request,line=>Window.Dispatcher.BeginInvoke(new Action(()=>{
     operationLog.Add(line);if(operationLog.Count>600)operationLog.RemoveAt(0);
     if(logInput!=null){logInput.Text=string.Join(Environment.NewLine,operationLog);logInput.ScrollToEnd();}
    }))));
    if(!string.IsNullOrEmpty(result.GameRoot))gamePath=result.GameRoot;
    if(!string.IsNullOrEmpty(result.SteamPath))steamPath=result.SteamPath;
    if(result.GameCandidates!=null)gameCandidates=result.GameCandidates;
    if(result.SteamCandidates!=null)steamCandidates=result.SteamCandidates;
    if(!string.IsNullOrEmpty(result.Option))launchOption=result.Option;
    SetBusy(false);ShowPage(page);SetStatus(result.Title+" · "+result.Message,!result.Success);
   }catch(Exception ex){SetBusy(false);SetStatus(ex.Message,true);}
  }
 }
}
