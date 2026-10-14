using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using Forms=System.Windows.Forms;

namespace AC8Settings {
 public sealed class Editor {
  public string Key;public TextBox Input;public Func<bool> Commit;
 }
 public sealed partial class SettingsWindow {
  public Window Window;
  ConfigStore store;ConfigState draft;string savedSignature="";DispatcherTimer configTimer;
  bool savedSteamLaunch=true;bool loading,previewMode;int page=5;bool light;bool autoStarted;
  TextBlock title,description,status,packageName;
  StackPanel body,nav;Button save,defaults,restore;
  List<Button> navButtons=new List<Button>();List<Editor> editors=new List<Editor>();
  List<Shape> reticle=new List<Shape>();Canvas previewCanvas;
  FontFamily numbers=new FontFamily("Consolas");
  readonly string[] titles={"鼠标与飞控","相机与视角","准星与显示","键位设置","高级飞控","安装与启动","模组管理","诊断与分析","安装说明"};
  readonly string[] descriptions={
   "调整操纵手感。这里显示的是当前配置，保存后下次启动生效。",
   "调整 FOV、驾驶舱平滑跟随，以及机外镜头的位置与响应。",
   "把准星调到容易看清的颜色与亮度，预览会随设置更新。",
   "设置自由观察与手动操纵键；手动轴键应与游戏内绑定一致。",
   "用于细调飞控。保留默认值通常即可，调整前请阅读说明。",
   "定位游戏、选择安装内容，再开始游戏。",
   "切换模组启停，检查原版启动条件，恢复异常退出后的会话。",
   "运行记录、完整性校验与内置运行数据分析。",
   "安装 MOD 并设置游戏启动方式。"};
  readonly string[] names={"安装与启动","常用设置","相机与视角","准星与显示","键位设置","高级飞控","模组管理","诊断与分析","安装说明"};
  readonly int[] navPages={5,0,1,2,3,4,6,7,8};
  readonly string[] icons={
   "M2,4 L18,4 M2,10 L18,10 M2,16 L18,16 M7,1 L7,7 M14,7 L14,13 M9,13 L9,19",
   "M2,6 L13,6 L13,15 L2,15 Z M13,9 L19,6 L19,15 L13,12",
   "M10,1 L10,5 M10,15 L10,19 M1,10 L5,10 M15,10 L19,10 M10,5 A5,5 0 1 1 9.99,5",
   "M1,4 L19,4 L19,17 L1,17 Z M4,7 L5,7 M8,7 L9,7 M12,7 L13,7 M16,7 L17,7 M4,11 L5,11 M8,11 L9,11 M12,11 L13,11 M4,14 L15,14",
   "M2,16 L6,16 L6,10 L10,10 L10,4 L15,4 M2,4 L5,4 M14,10 L18,10 M13,16 L18,16"};
  static readonly Dictionary<string,string> LightColors=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"#101A22","#F3F1EB"},{"#15232D","#E6EBED"},{"#F1EEE6","#192A35"},{"#A8B8C2","#4E6471"},
   {"#1B2B36","#F8FAFA"},{"#0C151C","#FFFFFF"},{"#344550","#BDCAD1"},{"#647C8C","#708592"},
   {"#304451","#D6E1E6"},{"#172731","#FFFFFF"},{"#314957","#E1EAF0"},{"#3C3927","#FCE8BA"},
   {"#526B7A","#708592"},{"#8097A5","#607585"},{"#324955","#B9C7CF"},{"#466372","#BBD2E0"},
   {"#BDCED6","#46616F"},{"#FFAA97","#A33E2C"},{"#A08540","#A08540"},{"#607D8E","#718B9A"}};
  public SettingsWindow(string root,bool preview,bool lightTheme=false){
   previewMode=preview;light=lightTheme;
   string xaml;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))using(var reader=new StreamReader(stream))xaml=reader.ReadToEnd();
   xaml=xaml.Replace("Property=\"Foreground\" Value=\"#101A22\"","Property=\"Foreground\" Value=\"#111416\"").Replace("Property=\"Fill\" Value=\"#101A22\"","Property=\"Fill\" Value=\"#111416\"");
   if(light)foreach(var pair in LightColors)xaml=xaml.Replace(pair.Key,pair.Value);
   Window=(Window)XamlReader.Parse(xaml);
   if(!preview){var area=SystemParameters.WorkArea;Window.MinWidth=Math.Min(Window.MinWidth,Math.Max(760,area.Width-24));Window.MinHeight=Math.Min(Window.MinHeight,Math.Max(560,area.Height-24));Window.Width=Math.Min(Window.Width,Math.Max(Window.MinWidth,area.Width-24));Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,area.Height-24));}
   title=(TextBlock)Window.FindName("PageTitle");description=(TextBlock)Window.FindName("PageDescription");
   status=(TextBlock)Window.FindName("Status");packageName=(TextBlock)Window.FindName("PackageName");
   body=(StackPanel)Window.FindName("PageBody");nav=(StackPanel)Window.FindName("Navigation");
   save=(Button)Window.FindName("Save");defaults=(Button)Window.FindName("PageDefaults");restore=(Button)Window.FindName("RestoreBackup");
   for(int i=0;i<names.Length;i++){
    int index=navPages[i];var b=new Button{Style=(Style)Window.FindResource("Nav"),HorizontalContentAlignment=HorizontalAlignment.Left};
    var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(26)});grid.ColumnDefinitions.Add(new ColumnDefinition());
    string[] extra={"M2,4 L18,4 L18,16 L2,16 Z M7,8 L14,10 L7,13 Z","M10,2 L18,6 L18,12 L10,18 L2,12 L2,6 Z M7,10 L9,12 L14,7","M3,17 L3,11 M8,17 L8,4 M13,17 L13,8 M18,17 L18,2","M3,3 L17,3 L17,17 L3,17 Z M8,7 C8,3 15,4 13,8 L10,11 M10,14 L10,15"};
    var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(index==5?extra[0]:index==6?extra[1]:index==7?extra[2]:index==8?extra[3]:icons[index]),Stroke=Brush("#A8B8C2"),StrokeThickness=1.4,Width=18,Height=18,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center};
    var label=new TextBlock{Text=names[i],Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    Grid.SetColumn(label,1);grid.Children.Add(path);grid.Children.Add(label);b.Content=grid;
    b.Click+=(s,e)=>{if(CommitEditors())ShowPage(index);};nav.Children.Add(b);navButtons.Add(b);
   }
   save.Click+=(s,e)=>{if(page==5)RunOperation("Save");else Save();};
   ((Button)Window.FindName("StartGame")).Click+=(s,e)=>RunOperation("Start");
   ((CheckBox)Window.FindName("SteamLaunch")).Checked+=(s,e)=>Changed();
   ((CheckBox)Window.FindName("SteamLaunch")).Unchecked+=(s,e)=>Changed();
   Window.SourceInitialized+=(s,e)=>{try{int dark=light?0:1;DwmSetWindowAttribute(new WindowInteropHelper(Window).Handle,20,ref dark,4);}catch{}};
   defaults.Click+=(s,e)=>ResetPage();
   restore.Click+=(s,e)=>Restore();
   ((Button)Window.FindName("ChoosePackage")).Click+=(s,e)=>ChooseFolder();
   ((Button)Window.FindName("Reload")).Click+=(s,e)=>{
    if(store==null){ChooseFolder();return;}
    if(Dirty()&&MessageBox.Show(Window,"重新读取会放弃尚未保存的调整。继续？","重新读取配置",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    Open(store.Root);
   };
   if(!preview){
    configTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
    configTimer.Tick+=(s,e)=>RefreshConfiguration();configTimer.Start();
   }
   Window.Closed+=(s,e)=>{if(configTimer!=null)configTimer.Stop();};
   Window.ContentRendered+=(s,e)=>{if(!previewMode&&!autoStarted&&store!=null&&page==5){autoStarted=true;RunOperation("Discover");}};
   Window.Closing+=(s,e)=>{if(busy){e.Cancel=true;SetStatus("后台操作尚未结束，请等待完成后再关闭。");return;}if(!previewMode&&Dirty()){
    var choice=MessageBox.Show(Window,"有尚未保存的调整。是否保存后关闭？","未保存的设置",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
    if(choice==MessageBoxResult.Cancel||(choice==MessageBoxResult.Yes&&!Save()))e.Cancel=true;
   }};
   if(!string.IsNullOrEmpty(root))Open(root);
   else{save.IsEnabled=defaults.IsEnabled=restore.IsEnabled=false;ErrorPage("尚未选择 MOD 文件夹","点击左下方“切换文件夹”，选择包含 MouseAim-Settings.ini 的完整 MOD 包。");}
  }
  SolidColorBrush Brush(string color){string mapped;if(light&&LightColors.TryGetValue(color,out mapped))color=mapped;return RawBrush(color);}
  static SolidColorBrush RawBrush(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  TextBlock Text(string text,double size=14,string color="#F1EEE6"){
   return new TextBlock{Text=text,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap};
  }
  void SetStatus(string text,bool error=false){status.Text=text;status.Foreground=Brush(error?"#FFAA97":"#A8B8C2");}
  bool Dirty(){return store!=null&&draft!=null&&draft.Signature()!=savedSignature||store!=null&&savedSteamLaunch!=(((CheckBox)Window.FindName("SteamLaunch")).IsChecked==true);}
  void Changed(){
   if(loading)return;
   save.IsEnabled=store!=null&&(page==5||Dirty());restore.IsEnabled=store!=null&&store.LatestBackup()!=null;
   SetStatus(Dirty()?"有未保存的调整 · 保存后，下次启动游戏生效":"设置已保存 · 下次启动游戏时会应用");
   UpdatePreview();
  }
  void Open(string root){
   try{
    var candidate=new ConfigStore(root);
    if(!previewMode&&!RootOwnership.Acquire(root))throw new InvalidOperationException("这个 MOD 文件夹的工具已经打开，请切换到已有窗口。");
    store=candidate;draft=store.State.Copy();savedSignature=draft.Signature();
    packageName.Text=new DirectoryInfo(root).Name;packageName.ToolTip=root;
    ((TextBlock)Window.FindName("VersionText")).Text="MOD "+store.Version;
    LoadInstallation();
    string fonts=System.IO.Path.Combine(root,"ui","creeperux","fonts")+System.IO.Path.DirectorySeparatorChar;
    if(Directory.Exists(fonts)){
     ((TextBlock)Window.FindName("Brand")).FontFamily=new FontFamily(new Uri(fonts,UriKind.Absolute),"./#Chakra Petch");
     numbers=new FontFamily(new Uri(fonts,UriKind.Absolute),"./#Share Tech Mono");
    }
    defaults.IsEnabled=true;ShowPage(page);Changed();SetStatus(page==5?"MOD 资源已就绪 · 定位游戏并选择安装方案":"已读取当前配置 · 调整后点击保存，下次启动生效");
   }catch(Exception ex){store=null;draft=null;editors.Clear();ErrorPage("配置暂时无法读取",ex.Message);SetStatus(ex.Message,true);save.IsEnabled=defaults.IsEnabled=restore.IsEnabled=false;}
  }
  void ErrorPage(string heading,string reason){title.Text=heading;description.Text="请选择正确的 MOD 文件夹，或先恢复有效配置。";body.Children.Clear();var t=Text(reason,14,"#FFAA97");t.Margin=new Thickness(0,18,0,0);body.Children.Add(t);}
  void ChooseFolder(){
   if(!CommitEditors())return;
   if(Dirty()&&MessageBox.Show(Window,"切换文件夹会放弃尚未保存的调整。继续切换？","切换 MOD 文件夹",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   using(var dialog=new Forms.FolderBrowserDialog()){
    dialog.Description="选择包含 MouseAim-Settings.ini 的 AC8 MOD 文件夹";
    if(store!=null)dialog.SelectedPath=store.Root;
    if(dialog.ShowDialog()==Forms.DialogResult.OK)Open(dialog.SelectedPath);
   }
  }
  public void ShowPage(int index){
   if(store==null)return;
   loading=true;page=index;editors.Clear();reticle.Clear();previewCanvas=null;
   body.Children.Clear();title.Text=titles[index];description.Text=descriptions[index];
   ((ScrollViewer)Window.FindName("ContentScroll")).ScrollToTop();
   for(int i=0;i<navButtons.Count;i++){
    navButtons[i].Background=Brush(navPages[i]==page?"#3C3927":"#15232D");
    navButtons[i].BorderBrush=Brush(navPages[i]==page?"#A08540":"#15232D");
   }
   int selected=Array.IndexOf(navPages,page);
   Window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>navButtons[selected].BringIntoView()));
   if(store==null){loading=false;return;}
   switch(index){
    case 0:
     Number("sensitivity","鼠标灵敏度","数值越高，移动相同距离时方向变化越大。",true);
     Number("free_look_zoom","自由观察放大","与自由观察键一起按住缩放键时的额外倍率；驾驶舱模式使用游戏原生缩放。",false,"倍");
     Choice("control_mode","默认飞控模式","PEACE 适合日常飞行；WAR 使用更精确的跟踪控制。",new[]{0,3},new[]{"PEACE · 日常飞行","WAR · 精确跟踪"});
     Toggle("model_assist","模型辅助","按飞机响应提供辅助制动。具体强度可在高级飞控中调整。");
     break;
    case 1:
     Choice("camera_mode","机外相机模式","进入驾驶舱或第一人称时自动保留游戏机位，C 键可自由观察。此处决定机外跟随方式；飞行中 F3 临时切换。",new[]{2,1,0},new[]{"游戏原生机位","机外拉远跟随","机外原生相对机位"});
     Note("用游戏的 V 键切换驾驶舱、第一人称与机外视角。驾驶舱保留原生位置；舱内平滑跟随可在下方设置。");
     Section("独立视野角度","水平 FOV。0 保持游戏原生动态视野；自定义范围为 40–130°。");
     FovPresets();
     Number("fov_cockpit","驾驶舱 FOV","仅应用于显示驾驶舱的视角。",false,"°");
     Number("fov_hud","第一人称 FOV","应用于没有驾驶舱模型的第一人称视角。",false,"°");
     Number("fov_third_person","机外 FOV","应用于游戏的第三人称视角，包括 MOD 机外跟随。",false,"°");
     Note("固定 FOV 会替代对应视角的原生动态缩放。更大的视野也会改变视觉操纵感；此项不修改飞控强度或鼠标灵敏度。");
     Section("驾驶舱跟随","应用于带驾驶舱的第一人称视角。在舱内平滑转头跟随控制圆圈，保留原生位置与 FOV。");
     Toggle("first_person_follow","驾驶舱跟随圆圈","视角平滑跟随鼠标控制方向。按住 C 在舱内转头，视角随飞机机动；松开后平滑回到飞控方向。");
     Number("first_person_response","驾驶舱响应速度","越低转动越柔和、延迟越大；越高跟随越快。默认 6，范围 2–20。");
     Section("机外跟随","下列距离与高度只影响“机外拉远跟随”模式。");
     Number("camera_distance_m","跟随距离","相机位于飞机后方的距离。",false,"米");
     Number("camera_height_m","跟随高度","相机位于飞机上方的高度。",false,"米");
     Section("机外相机响应","驾驶舱自由观察不使用下列跟随参数。");
     Number("follow_rate","方向响应","越高跟随越快，越低转动越柔和。");
     Number("level_rate","水平回正","越低回正越温和；设为 0 关闭相机水平回正。");
     break;
    case 2:
     ColorSection();
     Number("hud_opacity","准星不透明度","越高越清晰；颜色、背景和游戏光晕会共同影响观感。",false,"%",100);
     Toggle("hud_boresight","机头参考十字","显示机头与机炮的指向；它不表示射击提前量。");
     Toggle("hud_connector","方向短刻度","显示鼠标圆环朝向机头的方向刻度。");
     Toggle("hud_link_always","持续显示方向刻度","保持方向刻度显示，关闭时按距离渐隐。");
     Number("hud_fps","准星更新目标","配置的更新频率，不代表实际画面帧率。",false,"Hz");
     Choice("hud_renderer","准星渲染方式","推荐原生准星与按需面板。颜色选择仅应用于原生准星模式。",new[]{3,2,1,0},new[]{"原生准星 + 按需面板（推荐）","仅原生准星","旧版 GPU 绘制","旧版 GDI 绘制"});
     break;
    case 3:
     Note("这些设置不会修改游戏本身的键位。手动轴键请与游戏内绑定保持一致；F1–F10 为 MOD 功能保留。");
     foreach(var action in store.Actions)KeyRow(action);
     break;
    case 4:
     Number("model_assist_strength","模型辅助强度","模型辅助开启时生效，0 表示不施加辅助强度。");
     Number("mouse_reference_fov","鼠标参考视野","用于缩放时保持鼠标位移感；通常保留 62。",false,"°");
     Number("arrival_braking","备用制动参数","仅用于旧版启动或回退控制；不会设置主飞控模型的阻尼。");
     Note("保存仅更新外部配置，不会改动飞控 DLL。每次保存前自动建立备份，可恢复最近一次保存前的设置。");
     break;
    case 5:InstallationPage();break;
    case 6:MaintenancePage();break;
    case 7:DiagnosticsPage();break;
    case 8:HelpPage();break;
   }
   save.Visibility=Visibility.Visible;
   ((StackPanel)Window.FindName("SettingTools")).Visibility=page<=4?Visibility.Visible:Visibility.Collapsed;
   ((Button)Window.FindName("StartGame")).IsEnabled=!busy;
   ((CheckBox)Window.FindName("SteamLaunch")).IsEnabled=!busy;
   ((TextBlock)Window.FindName("FooterHint")).Text=page<=4?"下次启动生效":"资源与原有校验流程已保留";
   loading=false;UpdatePreview();save.IsEnabled=page==5||Dirty();restore.IsEnabled=store.LatestBackup()!=null;
  }
  void Section(string heading,string hint){
   var s=new StackPanel{Margin=new Thickness(0,23,0,4)};
   var h=Text(heading,17);h.FontWeight=FontWeights.SemiBold;s.Children.Add(h);
   if(!string.IsNullOrEmpty(hint)){var t=Text(hint,12,"#A8B8C2");t.Margin=new Thickness(0,7,0,0);s.Children.Add(t);}
   body.Children.Add(s);
  }
  void Note(string content){
   var t=Text(content,12,"#BDCED6");t.Margin=new Thickness(0,13,0,17);t.LineHeight=21;body.Children.Add(t);
  }
  Grid Row(string label,string hint){
   var border=new Border{BorderBrush=Brush("#344550"),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,17,0,17)};
   var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(270)});
   var s=new StackPanel{Margin=new Thickness(0,0,24,0),VerticalAlignment=VerticalAlignment.Center};
   var l=Text(label);l.FontWeight=FontWeights.Medium;s.Children.Add(l);
   var h=Text(hint,12,"#A8B8C2");h.Margin=new Thickness(0,7,0,0);h.LineHeight=19;s.Children.Add(h);
   g.Children.Add(s);border.Child=g;body.Children.Add(border);return g;
  }
  static string Format(double v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
  void Number(string key,string label,string hint,bool logarithmic=false,string unit="",double scale=1){
   Rule rule=ConfigStore.Find(key);var row=Row(label+(unit.Length>0?"（"+unit+"）":""),hint);
   var controls=new Grid{VerticalAlignment=VerticalAlignment.Center};
   controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(82)});
   var slider=new Slider{Minimum=0,Maximum=100,SmallChange=1,LargeChange=5,IsMoveToPointEnabled=true,Margin=new Thickness(0,0,15,0)};
   var input=new TextBox{Text=Format(draft.Values[key]*scale),FontFamily=numbers,FontSize=16,HorizontalContentAlignment=HorizontalAlignment.Center};
   input.ToolTip="范围："+Format(rule.Min*scale)+"–"+Format(rule.Max*scale)+(unit.Length>0?" "+unit:"");
   AutomationProperties.SetName(input,label+"数值");AutomationProperties.SetName(slider,label+"滑块");
   Grid.SetColumn(input,1);controls.Children.Add(slider);controls.Children.Add(input);Grid.SetColumn(controls,1);row.Children.Add(controls);
   if(unit.Length>0){input.Padding=new Thickness(5,9,5,9);}
   bool syncing=false;
   Func<double,double> toSlider=v=>key.StartsWith("fov_")?(v==0?0:1+99*(v-40)/90):logarithmic?100*Math.Log(v/rule.Min)/Math.Log(rule.Max/rule.Min):100*(v-rule.Min)/(rule.Max-rule.Min);
   Func<double,double> fromSlider=v=>key.StartsWith("fov_")?(v<1?0:40+90*(v-1)/99):logarithmic?rule.Min*Math.Pow(rule.Max/rule.Min,v/100):rule.Min+(rule.Max-rule.Min)*v/100;
   slider.Value=toSlider(draft.Values[key]);
   Func<bool> commit=()=>{
    double v;
    bool ok=double.TryParse(input.Text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out v);
    v/=scale;
    ok=ok&&!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=rule.Min&&v<=rule.Max&&(!rule.Integer||v==Math.Floor(v))&&(!key.StartsWith("fov_")||v==0||v>=40);
    input.BorderBrush=Brush(ok?"#647C8C":"#FFAA97");
    if(!ok){SetStatus(key.StartsWith("fov_")?label+"：请输入 0（原生）或 40–130 度。":label+"：请输入 "+Format(rule.Min*scale)+"–"+Format(rule.Max*scale)+(rule.Integer?" 的整数。":" 范围内的数值。"),true);return false;}
    draft.Values[key]=v;syncing=true;slider.Value=toSlider(v);syncing=false;Changed();return true;
   };
   editors.Add(new Editor{Key=key,Input=input,Commit=commit});
   input.TextChanged+=(s,e)=>{if(!syncing&&!loading)commit();};
   input.LostKeyboardFocus+=(s,e)=>commit();
   input.KeyDown+=(s,e)=>{if(e.Key==Key.Enter)commit();};
   slider.ValueChanged+=(s,e)=>{if(syncing||loading)return;double v=fromSlider(slider.Value);v=rule.Integer?Math.Round(v):Math.Round(v,3);
    if(key.StartsWith("fov_")){if(v>0&&v<40)v=40;v=Math.Round(v,1);}
    syncing=true;input.Text=Format(v*scale);input.BorderBrush=Brush("#647C8C");draft.Values[key]=v;syncing=false;Changed();
   };
  }
  void Choice(string key,string label,string hint,int[] values,string[] labels){
   var row=Row(label,hint);var combo=new ComboBox{VerticalAlignment=VerticalAlignment.Center};
   foreach(string l in labels)combo.Items.Add(l);combo.SelectedIndex=Array.IndexOf(values,(int)draft.Values[key]);
   AutomationProperties.SetName(combo,label);combo.SelectionChanged+=(s,e)=>{if(loading||combo.SelectedIndex<0)return;draft.Values[key]=values[combo.SelectedIndex];Changed();};
   Grid.SetColumn(combo,1);row.Children.Add(combo);
  }
  void Toggle(string key,string label,string hint){
   var row=Row(label,hint);var check=new CheckBox{IsChecked=draft.Values[key]!=0,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Content=draft.Values[key]!=0?"已开启":"已关闭"};
   AutomationProperties.SetName(check,label);
   RoutedEventHandler action=(s,e)=>{if(loading)return;draft.Values[key]=check.IsChecked==true?1:0;check.Content=check.IsChecked==true?"已开启":"已关闭";Changed();};
   check.Checked+=action;check.Unchecked+=action;Grid.SetColumn(check,1);row.Children.Add(check);
  }
  void KeyRow(ActionSpec action){
   string label=action.Label.Replace("：手动接管检测","");
   string hint=action.Name=="free_look"?"按住转头，松开返回。":action.Name=="free_look_zoom"?"与自由观察键一起按住。":"用于识别手动操纵，应与游戏内相应按键一致。";
   var row=Row(label,hint);var combo=new ComboBox{VerticalAlignment=VerticalAlignment.Center};
   foreach(var k in store.KeyChoices)combo.Items.Add(k);
   combo.SelectedItem=store.KeyChoices.First(x=>x.Token==draft.Keys[action.Name]);
   AutomationProperties.SetName(combo,label);
   combo.SelectionChanged+=(s,e)=>{if(loading||combo.SelectedItem==null)return;draft.Keys[action.Name]=((KeySpec)combo.SelectedItem).Token;Changed();};
   Grid.SetColumn(combo,1);row.Children.Add(combo);
  }
  void ColorSection(){
   var layout=new Grid{Margin=new Thickness(0,4,0,8)};
   layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(270)});
   var left=new StackPanel{Margin=new Thickness(0,4,26,0)};
   var l=Text("准星颜色",16);l.FontWeight=FontWeights.SemiBold;left.Children.Add(l);
   var hint=Text("圆环、头瞄括号、参考十字与方向刻度统一使用这个颜色。",12,"#A8B8C2");hint.Margin=new Thickness(0,8,0,13);hint.LineHeight=20;left.Children.Add(hint);
   var colorRow=new Grid();colorRow.ColumnDefinitions.Add(new ColumnDefinition());colorRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var input=new TextBox{Text=draft.Color,FontFamily=numbers,FontSize=17,MaxLength=7};AutomationProperties.SetName(input,"准星颜色十六进制值");
   var pick=new Button{Content="选色",Padding=new Thickness(13,7,13,7),Margin=new Thickness(8,0,0,0)};Grid.SetColumn(pick,1);
   colorRow.Children.Add(input);colorRow.Children.Add(pick);left.Children.Add(colorRow);
   Func<bool> commit=()=>{bool ok=Regex.IsMatch(input.Text,@"^#[0-9A-Fa-f]{6}$");input.BorderBrush=Brush(ok?"#647C8C":"#FFAA97");
    if(!ok){SetStatus("颜色请填写 #RRGGBB，例如 #70FF90。",true);return false;}draft.Color=input.Text.ToUpperInvariant();Changed();return true;};
   editors.Add(new Editor{Key="color",Input=input,Commit=commit});input.TextChanged+=(s,e)=>{if(!loading)commit();};
   pick.Click+=(s,e)=>{using(var d=new Forms.ColorDialog()){
    d.FullOpen=true;d.Color=System.Drawing.ColorTranslator.FromHtml(draft.Color);
    if(d.ShowDialog()==Forms.DialogResult.OK)input.Text="#"+d.Color.R.ToString("X2")+d.Color.G.ToString("X2")+d.Color.B.ToString("X2");
   }};
   var presets=new WrapPanel{Margin=new Thickness(0,12,0,0)};
   foreach(var pair in new[]{new[]{"HUD 绿","#70FF90"},new[]{"冰青","#58CBE1"},new[]{"琥珀","#FFBD4A"},new[]{"白","#FFFFFF"}}){
    string hex=pair[1];var b=new Button{Content=pair[0],FontSize=11,Padding=new Thickness(8,5,8,5),MinHeight=29,Margin=new Thickness(0,0,6,6)};
    b.BorderBrush=RawBrush(hex);b.Click+=(s,e)=>input.Text=hex;presets.Children.Add(b);
   }left.Children.Add(presets);
   layout.Children.Add(left);
   var right=new StackPanel();
   var edge=new Border{CornerRadius=new CornerRadius(8),ClipToBounds=true,Background=Brush("#386B83"),Height=174};
   previewCanvas=new Canvas{Width=270,Height=174};edge.Child=previewCanvas;
   previewCanvas.Children.Add(new Line{X1=0,Y1=119,X2=270,Y2=119,Stroke=Brush("#91ABBB"),StrokeThickness=1,Opacity=.35});
   var ellipse=new Ellipse{Width=64,Height=64,StrokeThickness=2.8};Canvas.SetLeft(ellipse,103);Canvas.SetTop(ellipse,55);reticle.Add(ellipse);previewCanvas.Children.Add(ellipse);
   foreach(double[] points in new[]{new double[]{115,87,128,87},new double[]{142,87,155,87},new double[]{135,67,135,80},new double[]{135,94,135,107}}){
    var line=new Line{X1=points[0],Y1=points[1],X2=points[2],Y2=points[3],StrokeThickness=2.8};line.Tag="cross";reticle.Add(line);previewCanvas.Children.Add(line);
   }
   foreach(var p in new[]{new double[]{104,60,96,68},new double[]{166,60,174,68},new double[]{104,114,96,106},new double[]{166,114,174,106}}){
    var line=new Line{X1=p[0],Y1=p[1],X2=p[2],Y2=p[3],StrokeThickness=2};reticle.Add(line);previewCanvas.Children.Add(line);
   }
   right.Children.Add(edge);var t=Text("颜色与透明度示意 · 实机光晕会有所不同",11,"#A8B8C2");t.Margin=new Thickness(0,8,0,0);right.Children.Add(t);
   Grid.SetColumn(right,1);layout.Children.Add(right);body.Children.Add(layout);
  }
  void UpdatePreview(){
   if(draft==null||reticle.Count==0||!Regex.IsMatch(draft.Color,@"^#[0-9A-Fa-f]{6}$"))return;
   foreach(var s in reticle){s.Stroke=RawBrush(draft.Color);s.Opacity=draft.Values["hud_opacity"];if((string)s.Tag=="cross")s.Visibility=draft.Values["hud_boresight"]==0?Visibility.Hidden:Visibility.Visible;}
  }
  bool CommitEditors(){
   foreach(var editor in editors)if(!editor.Commit()){editor.Input.Focus();return false;}
   return true;
  }
  bool Save(){
   if(store==null||!CommitEditors())return false;
   try{RefreshConfiguration();store.Save(draft);draft=store.State.Copy();savedSignature=draft.Signature();ShowPage(page);Changed();savedSteamLaunch=((CheckBox)Window.FindName("SteamLaunch")).IsChecked==true;File.WriteAllText(System.IO.Path.Combine(store.Root,"Launcher-Options.ini"),"steam_launch="+(savedSteamLaunch?"1":"0")+Environment.NewLine,new UTF8Encoding(false));Changed();SetStatus("设置已保存，原配置已自动备份 · 下次启动游戏生效");return true;}
   catch(Exception ex){SetStatus(ex.Message,true);return false;}
  }
  void RefreshConfiguration(){
   if(store==null||busy)return;
   try{
    if(!store.Refresh(draft))return;
    savedSignature=store.State.Signature();
    // Preserve the reading position when an in-game adjustment arrives.
    var scroll=(ScrollViewer)Window.FindName("ContentScroll");double offset=scroll.VerticalOffset;
    ShowPage(page);scroll.ScrollToVerticalOffset(offset);Changed();
    SetStatus("已同步最新配置 · 游戏内调整优先，其他未保存的调整已保留");
   }catch(Exception ex){SetStatus("配置同步暂未完成："+ex.Message,true);}
  }
  void Restore(){
   if(store==null)return;
   if(Dirty()&&MessageBox.Show(Window,"恢复备份会替换尚未保存的调整。继续恢复？","恢复最近备份",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   try{store.Restore();draft=store.State.Copy();savedSignature=draft.Signature();ShowPage(page);Changed();SetStatus("已恢复最近备份 · 恢复前的当前设置也已备份");}
   catch(Exception ex){SetStatus(ex.Message,true);}
  }
  void ResetPage(){
   if(store==null||page>4)return;var d=store.Defaults();
   string[][] sets={
    new[]{"sensitivity","free_look_zoom","control_mode","model_assist"},
    new[]{"camera_mode","camera_distance_m","camera_height_m","follow_rate","level_rate","fov_cockpit","fov_hud","fov_third_person","first_person_follow","first_person_response"},
    new[]{"hud_opacity","hud_boresight","hud_connector","hud_link_always","hud_fps","hud_renderer"},
    new string[0],new[]{"model_assist_strength","mouse_reference_fov","arrival_braking"}};
   foreach(string key in sets[page])draft.Values[key]=d.Values[key];
   if(page==2)draft.Color=d.Color;if(page==3)draft.Keys=new Dictionary<string,string>(d.Keys);
   ShowPage(page);Changed();SetStatus("本页已恢复默认 · 机外跟随为默认，驾驶舱保留原生机位，驾驶舱平滑跟随；准星默认为绿色");
  }
  public void Snapshot(string path,double width,double height){
   Window.MinWidth=Math.Min(Window.MinWidth,width);Window.MinHeight=Math.Min(Window.MinHeight,height);Window.Width=width;Window.Height=height;Window.WindowStartupLocation=WindowStartupLocation.Manual;Window.Left=-12000;Window.Top=-12000;Window.ShowInTaskbar=false;Window.ShowActivated=false;
   Window.Show();
   Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
    try{
     var element=(FrameworkElement)Window.Content;element.UpdateLayout();
     double scale=1.5;var bmp=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bmp.Render(element);
     var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));
     Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));using(var stream=File.Create(path))enc.Save(stream);
    }finally{Window.Close();}
   }));
  }
  void FovPresets(){
   var wrap=new WrapPanel{Margin=new Thickness(0,12,0,2)};
   foreach(double val in new[]{0.0,90.0,120.0}){
    double value=val;var b=new Button{Content=val==0?"全部原生":val==90?"全部 90°":"全部 120°",FontSize=12,Padding=new Thickness(13,7,13,7),Margin=new Thickness(0,0,8,6)};
    b.Click+=(s,e)=>{if(!CommitEditors())return;foreach(var r in ConfigStore.FovRules)draft.Values[r.Key]=value;ShowPage(page);Changed();};wrap.Children.Add(b);
   }
   body.Children.Add(wrap);
  }
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{
    if(args.Contains("--self-test"))return SelfTest.Run(args);
    if(args.Contains("--ui-test"))return SelfTest.UI(args);
    if(args.Contains("--bundle-test"))return WorkbenchTest.Run(args);
    if(args.Contains("--extract")){
     if(!Bundle.Present)throw new InvalidOperationException("这个版本使用外置资源，无需解包。请保留 EXE 旁的完整 MOD 文件夹。");
     Bundle.Extract(Arg(args,"--extract",null),!args.Contains("--no-copy-exe"),null);
     string report=Arg(args,"--report",null);if(report!=null)File.WriteAllText(report,"PASS extraction");return 0;
    }
    string root=Arg(args,"--package",null);
    if(root==null){
     foreach(string candidate in new[]{AppDomain.CurrentDomain.BaseDirectory})if(Bundle.Present?Bundle.Compatible(candidate):ConfigStore.IsPackage(candidate)){root=candidate;break;}
    }
    if(root==null&&Bundle.Present){root=Bundle.Remembered()??Bundle.ChooseAndExtract();if(root==null)return 0;}
    if(root==null)throw new DirectoryNotFoundException("请从完整 MOD 文件夹内运行 AC8Workbench.exe。EXE 旁需保留 payload、配置和启动脚本；不能只移动 EXE。");
    var app=new Application{ShutdownMode=ShutdownMode.OnLastWindowClose};bool snapshot=args.Contains("--snapshot");
    if(!snapshot&&root!=null&&!RootOwnership.Acquire(root)){MessageBox.Show("这个 MOD 文件夹的工具已经打开。","AC8");return 0;}
    var ui=new SettingsWindow(root,snapshot,args.Contains("--light"));string page=Arg(args,"--page","install");
    ui.ShowPage(page=="hud"?2:page=="camera"?1:page=="keys"?3:page=="advanced"?4:page=="common"?0:page=="manage"?6:page=="diagnostics"?7:page=="help"?8:5);
    if(snapshot)ui.Snapshot(Arg(args,"--snapshot",null),double.Parse(Arg(args,"--width","1080"),CultureInfo.InvariantCulture),double.Parse(Arg(args,"--height","760"),CultureInfo.InvariantCulture));
    else ui.Window.Show();
    try{app.Run();}finally{RootOwnership.Release();}return 0;
   }catch(Exception ex){
    string report=Arg(args,"--report",null);if(report!=null)File.WriteAllText(report,ex.ToString());
    else MessageBox.Show("设置工具无法启动："+ex.Message,"AC8 飞控设置",MessageBoxButton.OK,MessageBoxImage.Error);
    return 1;
   }
  }
  public static string Arg(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
 }
}
