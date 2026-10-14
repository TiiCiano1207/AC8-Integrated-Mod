from pathlib import Path
import argparse,hashlib,json,re,shutil
source=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,required=True);parser.add_argument('--destination',type=Path,required=True);args=parser.parse_args()
root=args.package.resolve();stage=args.destination.resolve();stage.mkdir(parents=True,exist_ok=True)
assert stage!=root and not stage.is_relative_to(root)
sha=lambda b:hashlib.sha256(b).hexdigest()
for line in (root/'FILES.sha256').read_text('utf-8-sig').splitlines():
 m=re.match(r'[0-9a-fA-F]{64}\s+(.+)',line)
 if not m:continue
 name=m[1].replace('\\','/')
 if name.startswith(('sessions/','diagnostics/','cleanup-backups/','settings-backups/','resource-backups/')) or name in ['AC8Settings.exe','AC8Workbench.exe']:continue
 original=(root/name).resolve();assert original.is_relative_to(root)
 if original.is_file():
  target=stage/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,target)
for name in ['App.cs','Settings.cs','SelfTest.cs','MainWindow.xaml','AssemblyInfo.cs','app.manifest','app.ico','build.ps1','Bundle.cs','Bootstrap.xaml','Operations.cs','WorkbenchUI.cs','WorkbenchTest.cs','Workbench-Worker.ps1','stage_external.py','Update-Resources.ps1','Update-Resources.cmd','Runtime-Settings.ps1']:
 target=stage/'source/SettingsTool'/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source/name,target)
for name in ['Workbench-Worker.ps1','Update-Resources.ps1','Update-Resources.cmd','Runtime-Settings.ps1']:shutil.copy2(source/name,stage/name)
for p in stage.rglob('*.ps1'):p.write_text(p.read_text('utf-8-sig'),encoding='utf-8-sig',newline='\r\n')
for name in ['game-path.txt','steam-path.txt','Steam-Launch-Option.txt']:(stage/name).write_text('',encoding='utf-8')
(stage/'Start-GUI.cmd').write_text('@echo off\r\nstart "" "%~dp0AC8Workbench.exe" --package "%~dp0."\r\n',encoding='ascii',newline='')
notes='''AC8 外置资源启动工具 1.0
解压/复制完整文件夹，双击其中的 AC8Workbench.exe，保持旁边的资源、配置、脚本和 runtime 文件夹完整。
EXE 只包含启动/设置界面，不包含 MOD 资源。无需运行时解包，不会复用旧版解包目录。
首次使用：安装与启动 → 选择游戏与 Steam → 保存方案 → 把启动选项粘贴到 Steam → 启动游戏。
原有三种导弹方案、飞控设置、准星颜色、键位、FOV、模组管理及内置运行分析继续保留。

驾驶舱 C：舱内头部偏移随飞机机动，左右/上下使用飞机局部坐标；只平滑头部，松开后平滑返回圆圈。
修改 INI 参数不需要重编 EXE；保存后下次启动生效。
修改 payload 内 Lua/贴图/资源后，运行 Update-Resources.cmd 更新资源校验；元数据会自动备份。
原生 C++ 改动需重编对应 DLL 并放回 payload，再更新校验；EXE 不需要重新编译。
更新资源校验不会更改游戏版本要求、参数范围、加载器归属或清理规则。
安装方案和启动顺序沿用原 MOD；线上菜单行为保持原样，使用范围仍为离线单人。
'''
(stage/'docs/WORKBENCH.txt').write_text(notes,encoding='utf-8-sig')
(stage/'外置版使用说明.txt').write_text(notes,encoding='utf-8-sig')
info=json.loads((stage/'package-info.json').read_text('utf-8-sig'));info['settings_tool_version']='1.0';info['distribution']='external-resources'
(stage/'package-info.json').write_text(json.dumps(info,indent=2)+'\n',encoding='utf-8')
snapshot=json.loads((stage/'docs/SOURCE-SNAPSHOT.json').read_text('utf-8-sig'))
snapshot['files']=[e for e in snapshot['files'] if not e['path'].startswith('source/SettingsTool/')]
for p in sorted((stage/'source/SettingsTool').iterdir()):
 if p.is_file():snapshot['files'].append({'path':p.relative_to(stage).as_posix(),'sha256':sha(p.read_bytes())})
snapshot['note']+=' External-resource launcher: no embedded MOD bundle; resource checksum updater included.'
(stage/'docs/SOURCE-SNAPSHOT.json').write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
files=[p for p in sorted(stage.rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p.name!='FILES.sha256']
(stage/'FILES.sha256').write_text(''.join(sha(p.read_bytes())+'  '+p.relative_to(stage).as_posix()+'\n' for p in files),encoding='utf-8')
assert (stage/'runtime/python/python.exe').is_file()
assert not (stage/'bundle.zip').exists() and not (stage/'payload/Game/Binaries/Win64/ue4ss/Mods/AC8OfflineGuard').exists()
print('Prepared external resource folder:',len(files),'files. No resource ZIP embedded or generated.')
