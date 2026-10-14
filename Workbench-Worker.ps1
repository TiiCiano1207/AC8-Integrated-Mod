param([Parameter(Mandatory=$true)][string]$Request,[Parameter(Mandatory=$true)][string]$Response)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$r=Get-Content -LiteralPath $Request -Raw -Encoding UTF8 | ConvertFrom-Json
$root=[IO.Path]::GetFullPath([string]$r.Root)
$result=$null
try{
 if([string]$r.Action -notin @('Discover','Check','Save','Start','StartDirect','Recover','Cleanup','Disable','Enable','CheckOriginal','Analyze','CheckPackage')){throw 'Unsupported workbench action.'}
 . (Join-Path $root 'Gui-Core.ps1')
 switch([string]$r.Action){
  {$_ -in @('Discover','Check','Save','Start','StartDirect','Recover')}{
   $result=Invoke-AC8GuiAction -Action $r.Action -Root $root -GamePath ([string]$r.GamePath) -SteamPath ([string]$r.SteamPath) -MissileMode ([string]$r.MissileMode) -ConfirmRecovery:([bool]$r.ConfirmRecovery)
  }
  'CheckPackage'{
   & (Join-Path $root 'Check-Package.ps1') -PackageRoot $root
   $result=[pscustomobject]@{Success=$true;Title='安装包校验通过';Message='资源、配置和部署校验通过；未启动游戏。'}
  }
  'Cleanup'{
   & (Join-Path $root 'Launch-Offline.ps1') -CleanupOnly -GameRoot ([string]$r.GamePath)
   $result=[pscustomobject]@{Success=$true;Title='会话清理完成';Message='已按原有归属与备份规则清理本包会话；未知文件不会被直接删除。'}
  }
  {$_ -in @('Disable','Enable','CheckOriginal')}{
   $modeArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $root 'Mod-Mode.ps1'),'-Action',([string]$r.Action),'-Root',$root)
   if(![string]::IsNullOrWhiteSpace([string]$r.GamePath)){$modeArguments+=@('-GameRoot',([string]$r.GamePath))}
   & (Join-Path $PSHOME 'powershell.exe') @modeArguments
   if($LASTEXITCODE -ne 0){throw '模组模式切换或原版检查未通过。详情见下方操作日志。'}
   $title=switch($r.Action){'Disable'{'模组已停用'} 'Enable'{'模组已启用'} default{'原版启动条件检查通过'}}
   $result=[pscustomobject]@{Success=$true;Title=$title;Message='已执行原 MOD 的模式与加载器检查。下次从 Steam 启动时使用对应模式。'}
  }
  'Analyze'{
   $python=Join-Path $root 'runtime/python/python.exe'
   if(!(Test-Path -LiteralPath $python)){throw '内置分析运行时缺失，请重新解包完整发行文件。'}
   $env:PATH=(Split-Path $python)+';'+$env:PATH
   $env:PYTHONHOME=Split-Path $python
   $env:PYTHONPATH=''
   & (Join-Path $root 'Analyze-Latest.ps1')
   $result=[pscustomobject]@{Success=$true;Title='分析操作已结束';Message='使用内置 Python 与 NumPy。具体分析结果及是否缺少记录，请查看操作日志与 sessions 文件夹。'}
  }
 }
 if(!$result){throw '后台操作没有返回结果。'}
}catch{
 $failure=$_
 if(Get-Command Show-AC8Problem -ErrorAction SilentlyContinue){Show-AC8Problem $failure $root ([string]$r.Action)}
 $hint=[string]$failure.Exception.Data['AC8Hint']
 if(!$hint){$hint='查看操作日志和诊断文件，修正路径、权限或文件归属后再试。'}
 $result=[pscustomobject]@{Success=$false;Title='操作未完成';Message=$failure.Exception.Message+[Environment]::NewLine+$hint}
}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Response),($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
if(!$result.Success){exit 1}
