param([string]$PackageRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PackageRoot 'PowerShell-Compat.ps1')
$PackageRoot=[IO.Path]::GetFullPath($PackageRoot).TrimEnd('\','/')
foreach($name in @('package-info.json','payload-manifest.json','FILES.sha256','MouseAim-Settings.ini')){
 if(!(Test-Path -LiteralPath (Join-Path $PackageRoot $name) -PathType Leaf)){throw "Not a complete MOD package: $name"}
}
if((Test-Path -LiteralPath (Join-Path $PackageRoot 'AceCombat8.exe')) -or (Test-Path -LiteralPath (Join-Path $PackageRoot 'Game/Binaries/Win64/AceCombat8.exe'))){throw 'Run this in the external MOD folder, not the game installation.'}
$payload=[IO.Path]::GetFullPath((Join-Path $PackageRoot 'payload')).TrimEnd('\','/')
$entries=@();$known=@{}
foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName){
 if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Linked resource rejected: $($file.FullName)"}
 $absolute=[IO.Path]::GetFullPath($file.FullName)
 if(!$absolute.StartsWith($payload+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Resource escaped payload.'}
 $relative=$absolute.Substring($payload.Length+1)
 $entries+=,[pscustomobject]@{Path=$relative;SHA256=(Get-AC8FileHash -LiteralPath $absolute).Hash}
 $known[('payload/'+$relative.Replace('\','/'))]=$true
}
$utf8=New-Object Text.UTF8Encoding($false)
$updated=@{}
$updated['payload-manifest.json']=$utf8.GetBytes((ConvertTo-Json -InputObject $entries -Depth 6)+"`n")
$native=Join-Path $payload 'Game/Binaries/Win64/ue4ss/Mods/AC8MouseAim/Scripts/ac8_mouse_aim_010.dll'
$nativeSHA=(Get-AC8FileHash -LiteralPath $native).Hash
$info=Get-Content -LiteralPath (Join-Path $PackageRoot 'package-info.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$info.native_sha256=$nativeSHA.ToLowerInvariant()
$updated['package-info.json']=$utf8.GetBytes((ConvertTo-Json -InputObject $info -Depth 20)+"`n")
$snapshot=Get-Content -LiteralPath (Join-Path $PackageRoot 'docs/SOURCE-SNAPSHOT.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$snapshot.native_sha256=$nativeSHA
foreach($entry in $snapshot.files){
 $path=[IO.Path]::GetFullPath((Join-Path $PackageRoot $entry.path))
 if(!$path.StartsWith($PackageRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Source catalog path escaped package.'}
 if(Test-Path -LiteralPath $path -PathType Leaf){$entry.sha256=(Get-AC8FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
}
$updated['docs/SOURCE-SNAPSHOT.json']=$utf8.GetBytes((ConvertTo-Json -InputObject $snapshot -Depth 30)+"`n")
$lines=New-Object 'Collections.Generic.List[string]';$seen=@{}
function BytesSHA([byte[]]$Data){$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($Data)).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}}
foreach($line in Get-Content -LiteralPath (Join-Path $PackageRoot 'FILES.sha256') -Encoding UTF8){
 if($line -notmatch '^[0-9a-fA-F]{64}\s+(.+)$'){continue}
 $name=$Matches[1].Replace('\','/');$path=[IO.Path]::GetFullPath((Join-Path $PackageRoot $name))
 if(!$path.StartsWith($PackageRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'File catalog path escaped package.'}
 if(!(Test-Path -LiteralPath $path -PathType Leaf)){continue}
 $hash=if($updated.ContainsKey($name)){BytesSHA $updated[$name]}else{(Get-AC8FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
 $lines.Add($hash+'  '+$name);$seen[$name]=$true
}
foreach($name in $known.Keys | Sort-Object){if(!$seen.ContainsKey($name)){$lines.Add((Get-AC8FileHash -LiteralPath (Join-Path $PackageRoot $name)).Hash.ToLowerInvariant()+'  '+$name)}}
$updated['FILES.sha256']=$utf8.GetBytes(($lines -join "`n")+"`n")
$backup=Join-Path $PackageRoot ('resource-backups/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach($name in $updated.Keys){
 $saved=Join-Path $backup $name;New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $PackageRoot $name) -Destination $saved
}
try{
 foreach($name in $updated.Keys){[IO.File]::WriteAllBytes((Join-Path $PackageRoot $name),$updated[$name])}
 & (Join-Path $PackageRoot 'Check-Package.ps1') -PackageRoot $PackageRoot
}catch{
 foreach($name in $updated.Keys){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $PackageRoot $name) -Force}
 throw
}
Write-Host ('Resource catalog updated. Backup: '+$backup)
