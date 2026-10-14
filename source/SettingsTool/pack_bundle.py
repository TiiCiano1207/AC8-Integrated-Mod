from pathlib import Path
import hashlib,json,re,shutil,zipfile,argparse

src=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,default=src.parents[1]);parser.add_argument('--python-runtime',type=Path)
args=parser.parse_args()
root=args.package
stage=src/'package'
stage.mkdir(exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
paths=[]
for line in (root/'FILES.sha256').read_text('utf-8-sig').splitlines():
    m=re.match(r'[0-9a-fA-F]{64}\s+(.+)',line)
    if m:
        name=m[1].replace('\\','/')
        if name.startswith(('sessions/','diagnostics/','cleanup-backups/','settings-backups/','runtime/')) or name in ['AC8Settings.exe','AC8Workbench.exe']:
            continue
        original=(root/name).resolve()
        assert original.is_relative_to(root.resolve()),name
        if original.is_file():
            target=stage/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,target);paths.append(name)
manifest=json.loads((root/'payload-manifest.json').read_text('utf-8-sig'))
for e in manifest:
    p=root/'payload'/e['Path'].replace('\\','/')
    assert sha(p.read_bytes()).upper()==e['SHA256'].upper(),str(p)
for file in ['App.cs','Settings.cs','SelfTest.cs','MainWindow.xaml','AssemblyInfo.cs','app.manifest','app.ico','build.ps1','Bundle.cs','Bootstrap.xaml','Operations.cs','WorkbenchUI.cs','WorkbenchTest.cs','pack_bundle.py','Workbench-Worker.ps1']:
    target=stage/'source/SettingsTool'/file;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src/file,target)
(stage/'Workbench-Worker.ps1').write_text((src/'Workbench-Worker.ps1').read_text('utf-8-sig'),encoding='utf-8-sig')
# Match Windows PowerShell 5.1's parser encoding for all Chinese scripts.
for p in stage.rglob('*.ps1'):p.write_text(p.read_text('utf-8-sig'),encoding='utf-8-sig',newline='\r\n')
# Preserve automatic post-session analysis, preferring the embedded runtime.
common=stage/'Install-Common.ps1'
text=common.read_text('utf-8-sig')
needle=" $python=Get-Command python.exe -ErrorAction SilentlyContinue"
if needle in text:
    text=text.replace(needle," $bundledPython=Join-Path $PackageRoot 'runtime/python/python.exe'\n $python=if(Test-Path -LiteralPath $bundledPython -PathType Leaf){[pscustomobject]@{Source=[IO.Path]::GetFullPath($bundledPython)}}else{Get-Command python.exe -ErrorAction SilentlyContinue}")
else:
    assert '$bundledPython=' in text
text=text.replace("& $python.Source -c 'import numpy'","& $python.Source -E -B -c 'import numpy'")
text=text.replace("& $python.Source -E -c 'import numpy'","& $python.Source -E -B -c 'import numpy'")
text=text.replace("& $python.Source -X utf8","& $python.Source -E -B -X utf8")
text=text.replace("& $python.Source -E -X utf8","& $python.Source -E -B -X utf8")
common.write_text(text,encoding='utf-8-sig',newline='\r\n')
# Clean portable installation templates, retaining current flight/HUD preferences.
for name in ['game-path.txt','steam-path.txt','Steam-Launch-Option.txt']:(stage/name).write_text('',encoding='utf-8')
(stage/'Start-GUI.cmd').write_text('@echo off\r\nstart "" "%~dp0AC8Workbench.exe" --package "%~dp0."\r\n',encoding='ascii',newline='')

python=args.python_runtime or root/'runtime/python'
assert (python/'python.exe').exists(),'Provide --python-runtime for the analysis runtime.'
runtime=stage/'runtime/python';runtime.mkdir(parents=True,exist_ok=True)
for p in python.iterdir():
    if p.is_file() and p.name.lower() in ['python.exe','pythonw.exe','python3.dll','python312.dll','vcruntime140.dll','vcruntime140_1.dll','license.txt']:
        shutil.copy2(p,runtime/p.name)
for name in ['DLLs','Lib']:
    for p in (python/name).rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(python)
        if '__pycache__' in rel.parts or p.suffix=='.pyc':continue
        if name=='Lib' and 'site-packages' in rel.parts:
            i=rel.parts.index('site-packages')
            if len(rel.parts)<=i+1 or not (rel.parts[i+1]=='numpy' or rel.parts[i+1]=='numpy.libs' or rel.parts[i+1].startswith('numpy-')):continue
        target=runtime/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
(stage/'licenses/Workbench-Python-NumPy.txt').write_text('The built-in analysis runtime contains CPython and NumPy.\nCPython license: runtime/python/LICENSE.txt\nNumPy and bundled BLAS licenses: runtime/python/Lib/site-packages/numpy-2.3.5.dist-info/LICENSE.txt\nOriginal MOD notices remain in THIRD_PARTY_NOTICES.md.\n',encoding='utf-8')
notice=stage/'THIRD_PARTY_NOTICES.md'
text=notice.read_text('utf-8-sig')
if 'Workbench analysis runtime' not in text:
    text+='\n\n## Workbench analysis runtime\n\nThis local integrated tool includes CPython and NumPy for the original analysis scripts. Full licenses are preserved at runtime/python/LICENSE.txt and runtime/python/Lib/site-packages/numpy-2.3.5.dist-info/LICENSE.txt. See also licenses/Workbench-Python-NumPy.txt.\n'
notice.write_text(text,encoding='utf-8')
notes='''AC8 一体化 MOD 工具 2.1.4

单文件发行：只需 AC8Workbench.exe。运行后选择游戏目录之外的位置，解包并逐个校验资源。
解包目录保留配置、会话、诊断与清理备份。之后可双击该目录中的 AC8Workbench.exe 或 Start-GUI.cmd。
首次安装：自动定位或浏览游戏与 Steam → 检查环境 → 选择导弹方案 → 保存安装方案 → 复制启动选项到 Steam → 从 Steam 启动。
安装方案保存后，在 Steam 云同步完成后临时部署 MOD；游戏退出后沿用原清理流程。

统一功能
安装与启动：游戏/Steam 自动发现、版本/包校验、三种导弹方案、安装配置、启动选项生成/复制、Steam 启动。
设置：鼠标、PEACE/WAR、相机、驾驶舱、准星颜色/透明度、键位、高级飞控；自动备份/恢复与参数校验。
模组管理：启用/停用、原版启动条件检查、当前会话清理、带备份的历史残留恢复。
诊断与分析：打开运行记录和诊断、分析最近一次会话；已内置 Python 与 NumPy，不需另外安装。
帮助：原版安装、快捷键、限制、驾驶舱修正、许可文档；深色/浅色界面切换。

不会覆盖非空文件夹进行解包。请使用新文件夹，不要覆盖旧版会话或备份。
Steam 启动选项仍需手动粘贴，工具不会修改账户配置。云同步和部署/退出清理顺序与原版相同。
导弹与鼠标飞控保留本次定制版本，包括自动驾驶舱识别、驾驶舱平滑跟随与 C 自由观察、原版无裁剪目标输入与离屏方向提示和白色绘制源可调绿色准星。
游戏构建与加载器归属检查继续生效；不会删除未知来源的加载器。
Windows PowerShell 和 .NET Framework 使用系统组件。
'''
(stage/'docs/WORKBENCH.txt').write_text(notes,encoding='utf-8-sig')
info=json.loads((stage/'package-info.json').read_text('utf-8-sig'));info['settings_tool_version']='2.1.4';info['local_customization']+='; single-file workbench, original lifecycle operations, bundled analysis runtime'
(stage/'package-info.json').write_text(json.dumps(info,indent=2)+'\n',encoding='utf-8')
snapshot=json.loads((stage/'docs/SOURCE-SNAPSHOT.json').read_text('utf-8-sig'))
snapshot['files']=[e for e in snapshot['files'] if not e['path'].startswith('source/SettingsTool/')]
for p in sorted((stage/'source/SettingsTool').iterdir()):
    if p.is_file():snapshot['files'].append({'path':p.relative_to(stage).as_posix(),'sha256':sha(p.read_bytes())})
snapshot['note']+=' Workbench 2.0 adds single-file extraction, all original operation routing and bundled analysis runtime; native flight DLL unchanged.'
(stage/'docs/SOURCE-SNAPSHOT.json').write_text(json.dumps(snapshot,indent=2)+'\n',encoding='utf-8')
# Regenerate the distribution file list, including mutable templates and runtime licenses.
files=[p for p in sorted(stage.rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p.relative_to(stage).as_posix()!='FILES.sha256']
(stage/'FILES.sha256').write_text(''.join(sha(p.read_bytes())+'  '+p.relative_to(stage).as_posix()+'\n' for p in files),encoding='utf-8')
hashes={}
with zipfile.ZipFile(src/'bundle.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if not p.is_file() or '__pycache__' in p.parts or p.suffix=='.pyc':continue
        rel=p.relative_to(stage).as_posix();data=p.read_bytes();hashes[rel]=sha(data);z.writestr(rel,data)
(src/'bundle-hashes.json').write_text(json.dumps(hashes,separators=(',',':')),encoding='utf-8')
(src/'使用说明.txt').write_text(notes,encoding='utf-8-sig')
print('Bundle:',len(hashes),'files;',round((src/'bundle.zip').stat().st_size/1024/1024,2),'MiB compressed; historical logs/backups excluded.')
