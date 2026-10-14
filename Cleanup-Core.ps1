. ([IO.Path]::Combine($PSScriptRoot,'PowerShell-Compat.ps1'))
# Shared by the session launcher and the standalone historical recovery tool.
$AC8LoaderHash='CF440B9EB8643BB7C434ACFDA696AEE57FD981D185DCA5E57FB8DBB18F8FC1CD'
function Write-AC8Json($Value,[string]$Path) {
 $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath ($Path+'.tmp') -Encoding UTF8
 Move-Item -LiteralPath ($Path+'.tmp') -Destination $Path -Force
}
function Assert-AC8NoLinks([string]$Path,[switch]$Tree) {
 $item=Get-Item -LiteralPath $Path -Force
 while($item){
  if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Filesystem link refused: $($item.FullName)"}
  if($item -is [IO.FileInfo]){$item=$item.Directory}else{$item=$item.Parent}
 }
 if($Tree -and (Test-Path -LiteralPath $Path -PathType Container)){
  # Enumerate one level at a time: never recurse through a filesystem link.
  foreach($child in Get-ChildItem -LiteralPath $Path -Force){Assert-AC8NoLinks $child.FullName -Tree}
 }
}
function Get-AC8Snapshot([string]$Path) {
 Assert-AC8NoLinks $Path -Tree
 if(Test-Path -LiteralPath $Path -PathType Leaf){
  return @([pscustomobject]@{Path='';SHA256=(Get-AC8FileHash -LiteralPath $Path).Hash})
 }
 $files=@(Get-ChildItem -LiteralPath $Path -File -Recurse -Force | Sort-Object FullName)
 return @($files | ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($Path.Length+1);SHA256=(Get-AC8FileHash -LiteralPath $_.FullName).Hash}})
}
function Assert-AC8Snapshot([string]$Path,$Expected) {
 $actual=@(Get-AC8Snapshot $Path)
 if(($actual | ConvertTo-Json -Depth 4 -Compress) -ne (@($Expected) | ConvertTo-Json -Depth 4 -Compress)){throw "Contents changed or backup verification failed: $Path"}
}
function Assert-AC8IntegratedFOV([string]$ModPath) {
 Assert-AC8NoLinks $ModPath -Tree
 $reference=Join-Path $PSScriptRoot 'payload/Game/Binaries/Win64/ue4ss/Mods/AC8CockpitFOV/Scripts/main.lua'
 $script=Join-Path $ModPath 'Scripts/main.lua'
 if(!(Test-Path -LiteralPath $reference -PathType Leaf) -or !(Test-Path -LiteralPath $script -PathType Leaf) -or
    (Get-AC8FileHash -LiteralPath $reference).Hash -ne (Get-AC8FileHash -LiteralPath $script).Hash){
  throw 'Unknown AC8CockpitFOV script; retained for manual review.'
 }
 foreach($file in Get-ChildItem -LiteralPath $ModPath -File -Recurse -Force){
  $relative=$file.FullName.Substring($ModPath.Length+1).Replace('\','/')
  if($relative -notin @('FOV.ini','Scripts/main.lua')){throw ('Unknown AC8CockpitFOV file; retained: '+$relative)}
 }
 # FOV.ini is intentionally editable; accept valid personal values instead of
 # requiring its hash to match the packaged defaults.
 $config=Join-Path $ModPath 'FOV.ini'
 if(!(Test-Path -LiteralPath $config -PathType Leaf)){throw 'AC8CockpitFOV configuration missing; retained for review.'}
 $seen=@{}
 foreach($line in Get-Content -LiteralPath $config -Encoding UTF8){
  $clean=$line.Trim();if(!$clean -or $clean.StartsWith('#') -or $clean.StartsWith(';')){continue}
  if($clean -notmatch '^(Cockpit|HUD|ThirdPerson)\s*=\s*([0-9]+(?:\.[0-9]+)?)$'){throw 'Invalid AC8CockpitFOV configuration; retained for review.'}
  $name=$Matches[1];$value=[double]::Parse($Matches[2],[Globalization.CultureInfo]::InvariantCulture)
  if($seen.ContainsKey($name) -or ($value -ne 0 -and ($value -lt 40 -or $value -gt 130))){throw 'Invalid AC8CockpitFOV value; retained for review.'}
  $seen[$name]=$value
 }
 if($seen.Count -ne 3){throw 'Incomplete AC8CockpitFOV configuration; retained for review.'}
}
function Invoke-AC8Cleanup {
 param([string]$GameRoot,[string]$BackupRoot,$State,[switch]$RecoverHistorical,[switch]$CheckOnly)
 if(Get-Process -Name AceCombat8 -ErrorAction SilentlyContinue){throw 'Close AC8 normally before cleanup.'}
 $game=[IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
 $operation=$null
 if(!$CheckOnly){$operation=Enter-AC8Operation $game}
 try {
 $w64=[IO.Path]::GetFullPath((Join-Path $game 'Game/Binaries/Win64'))
 if(!(Test-Path -LiteralPath (Join-Path $w64 'AceCombat8.exe') -PathType Leaf)){throw 'Select the game root containing Game/Binaries/Win64/AceCombat8.exe.'}
 Assert-AC8NoLinks $w64
 $backup=[IO.Path]::GetFullPath($BackupRoot).TrimEnd('\','/')
 if($backup.Equals($game,[StringComparison]::OrdinalIgnoreCase) -or $backup.StartsWith($game+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup backup must be outside the game directory.'}
 if($State -and [IO.Path]::GetFullPath([string]$State.Win64) -ne $w64){throw 'Session game path changed; refusing cleanup.'}
 $names=@('dwmapi.dll','ue4ss','steam_appid.txt')
 $present=@($names | Where-Object {Test-Path -LiteralPath (Join-Path $w64 $_)})
 if(!$present.Count){Write-Host 'CLEANUP COMPLETE: all three deployed items are absent.';return $null}
 if(!$State -and !$RecoverHistorical){throw ('No session record, but residual items exist: '+($present -join ', ')+'. Use Recover-Cleanup.cmd after reviewing ownership.')}
 $ownedRoot=Join-Path $w64 'ue4ss'
 if(Test-Path -LiteralPath $ownedRoot){
  if(!(Test-Path -LiteralPath $ownedRoot -PathType Container)){throw 'ue4ss is not a directory; manual review required.'}
  $marker=Join-Path $ownedRoot 'AC8SourceInit-owner.txt'
  if(!(Test-Path -LiteralPath $marker -PathType Leaf)){throw 'Ownership marker missing. Unknown UE4SS directory retained for manual review.'}
  $id=(Get-Content -LiteralPath $marker -Raw -Encoding UTF8).Trim()
  if($State){
   if($id -ne $State.Id){throw 'Ownership marker mismatched. Files retained.'}
  }else{
   $parsed=[guid]::Empty
   if(![guid]::TryParse($id,[ref]$parsed)){throw 'Unrecognized ownership marker. Files retained.'}
  }
  $mods=Join-Path $ownedRoot 'Mods'
  if(Test-Path -LiteralPath $mods){
   $other=@(Get-ChildItem -LiteralPath $mods -Directory -Force | Where-Object {$_.Name -notin @('AC8MouseAim','AC8SourceInit','AC8CockpitFOV')})
   if($other.Count){throw ('Other UE4SS mods found; manual review required: '+(($other | ForEach-Object {$_.Name}) -join ', '))}
   $otherFiles=@(Get-ChildItem -LiteralPath $mods -File -Force | Where-Object Name -ne 'mods.txt')
   if($otherFiles.Count){throw ('Other UE4SS mod files found; retained for review: '+(($otherFiles | ForEach-Object Name) -join ', '))}
   $fov=Join-Path $mods 'AC8CockpitFOV'
   if(Test-Path -LiteralPath $fov){Assert-AC8IntegratedFOV $fov}
  }
 }
 # Validate every item before any mutation. Unknown/changed loaders are never removed.
 $plans=@()
 foreach($name in $present){
  $path=Join-Path $w64 $name
  $snapshot=@(Get-AC8Snapshot $path)
  if($name -ne 'ue4ss'){
   if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Expected a file: $name"}
   $record=@($State.Files | Where-Object {$_.Name -eq $name})
   if($record.Count -gt 1){throw "Duplicate file record: $name"}
   if($record.Count){
    if($snapshot[0].SHA256 -ne $record[0].SHA256){throw "Staged file changed; retained for review: $name"}
   }elseif($name -eq 'dwmapi.dll'){
    if($snapshot[0].SHA256 -ne $AC8LoaderHash){throw 'Unknown dwmapi.dll hash; retained for manual review.'}
   }elseif((Get-Content -LiteralPath $path -Raw).Trim() -ne '2288340'){
    throw 'Unknown steam_appid.txt contents; retained for manual review.'
   }
  }
  $plans+= [pscustomobject]@{Name=$name;Files=$snapshot;Removed=$false}
 }
 if($CheckOnly){Write-Host ('CLEANUP CHECK: recognized items: '+($present -join ', ')+'. No files changed.');return $null}
 New-Item -ItemType Directory -Path $backup -Force | Out-Null
 Assert-AC8NoLinks $backup
 $archive=Join-Path $backup ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $archive | Out-Null
 $report=[pscustomobject]@{Version=1;Win64=$w64;Status='backing-up';Items=$plans}
 $reportPath=Join-Path $archive 'cleanup-report.json'
 Write-AC8Json $report $reportPath
 # Backup and verify every item before removing any deployed item.
 foreach($plan in $plans){
  $src=Join-Path $w64 $plan.Name
  $dst=Join-Path $archive $plan.Name
  Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
  Assert-AC8Snapshot $dst $plan.Files
 }
 $report.Status='backup-verified';Write-AC8Json $report $reportPath
 foreach($plan in $plans){
  if(Get-Process -Name AceCombat8 -ErrorAction SilentlyContinue){throw "Game started during cleanup; backup retained at $archive"}
  $target=[IO.Path]::GetFullPath((Join-Path $w64 $plan.Name))
  if($target -notin @($names | ForEach-Object {Join-Path $w64 $_})){throw 'Cleanup target escaped allowlist.'}
  Assert-AC8Snapshot $target $plan.Files
  Remove-Item -LiteralPath $target -Recurse -Force
  $plan.Removed=$true;Write-AC8Json $report $reportPath
 }
 $remaining=@($names | Where-Object {Test-Path -LiteralPath (Join-Path $w64 $_)})
 if($remaining.Count){throw ('Cleanup incomplete: '+($remaining -join ', '))}
 $report.Status='complete';Write-AC8Json $report $reportPath
 Write-Host "CLEANUP COMPLETE: all three deployed items are absent. Verified backup: $archive"
 return $archive
 }finally{Exit-AC8Operation $operation}
}
