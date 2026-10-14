# Persist explicit in-game adjustments without rewriting untouched menu settings.
function Get-AC8ConfigMutexName([string]$Root) {
 $canonical=[IO.Path]::GetFullPath($Root).TrimEnd('\','/').ToLowerInvariant()
 $hash=[Security.Cryptography.SHA256]::Create()
 try{return 'Local\AC8Config-'+[BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-','')}finally{$hash.Dispose()}
}
function Write-AC8SettingAtomic([string]$Path,[string]$Text) {
 $temporary=$Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try {
  [IO.File]::WriteAllText($temporary,$Text,[Text.UTF8Encoding]::new($false))
  if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporary,$Path,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($temporary,$Path)}
 }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
}
function Sync-AC8RuntimeSettings([string]$Root,[string]$Runtime,$State) {
 # The runtime may be the current deployment or its verified cleanup backup.
 $id=[guid]::Empty
 if(!$State -or ![guid]::TryParse([string]$State.Id,[ref]$id)){throw 'Runtime settings require a valid owned session.'}
 $marker=Join-Path $Runtime 'AC8SourceInit-owner.txt'
 if(!(Test-Path -LiteralPath $marker) -or ([IO.File]::ReadAllText($marker)).Trim() -ne [string]$State.Id){throw 'Runtime settings ownership mismatch.'}
 $results=[IO.Path]::GetFullPath([string]$State.Results)
 $sessions=[IO.Path]::GetFullPath((Join-Path $Root 'sessions')).TrimEnd('\','/')+'\'
 if(!$results.StartsWith($sessions,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime settings results path escaped package.'}
 $runtimeFile=Join-Path $Runtime 'Mods/AC8MouseAim/Scripts/runtime-settings.ini'
 if(!(Test-Path -LiteralPath $runtimeFile -PathType Leaf)){return}
 if((Get-Item -LiteralPath $runtimeFile).Length -gt 8192){throw 'Runtime settings file exceeded size limit.'}
 $values=@{}
 foreach($line in [IO.File]::ReadAllLines($runtimeFile)){
  if($line -notmatch '^([a-z_]+)=([0-9]+(?:\.[0-9]+)?)$' -or $values.ContainsKey($Matches[1])){throw 'Malformed runtime settings.'}
  $values[$Matches[1]]=$Matches[2]
 }
 $allowed=@('sensitivity','free_look_zoom','control_mode','camera_mode','hud_boresight')
 $all=@('version','revision')+$allowed+@($allowed | ForEach-Object {$_+'_revision'})
 if(@($values.Keys | Where-Object {$_ -notin $all}).Count -or $values.version -ne '1' -or $values.revision -notmatch '^[1-9][0-9]*$'){throw 'Unsupported runtime settings.'}
 foreach($key in $allowed){
  if($values.ContainsKey($key) -ne $values.ContainsKey($key+'_revision')){throw 'Runtime setting missing its revision.'}
  if($values.ContainsKey($key) -and ($values[$key+'_revision'] -notmatch '^[1-9][0-9]*$' -or [long]$values[$key+'_revision'] -gt [long]$values.revision)){throw 'Invalid runtime setting revision.'}
 }
 $mutex=[Threading.Mutex]::new($false,(Get-AC8ConfigMutexName $Root));$held=$false
 try {
  try{$held=$mutex.WaitOne(5000)}catch [Threading.AbandonedMutexException]{$held=$true}
  if(!$held){throw 'Settings are busy; runtime sync will retry.'}
  $ledgerPath=Join-Path $results 'settings-sync.json';$seen=@{}
  if(Test-Path -LiteralPath $ledgerPath){
   $ledger=Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
   foreach($p in $ledger.PSObject.Properties){$seen[$p.Name]=$p.Value}
  }
  $changed=@($allowed | Where-Object {$values.ContainsKey($_) -and (!$seen.ContainsKey($_) -or [long]$values[$_+'_revision'] -gt [long]$seen[$_])})
  if(!$changed.Count){return}
  $path=Join-Path $Root 'MouseAim-Settings.ini'
  $text=[IO.File]::ReadAllText($path)
  foreach($key in $changed){
   $pattern='(?m)^(\s*'+[regex]::Escape($key)+'\s*=\s*)[^\r\n]*'
   $value=$values[$key]
   if([regex]::IsMatch($text,$pattern)){$text=[regex]::Replace($text,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m) $m.Groups[1].Value+$value})}
   else{$text=$text.TrimEnd()+"`r`n$key=$value`r`n"}
   # A same-value hotkey action must also win over a pending menu edit.
   $tag='; AC8 runtime '+$key+' '+$State.Id+' '+$values[$key+'_revision']
   $tagPattern='(?m)^; AC8 runtime '+[regex]::Escape($key)+' [^\r\n]*'
   if([regex]::IsMatch($text,$tagPattern)){$text=[regex]::Replace($text,$tagPattern,$tag)}else{$text=$text.TrimEnd()+"`r`n$tag`r`n"}
  }
  # Validate the entire proposed configuration before replacing the current one.
  $validation=Join-Path $results 'settings-validation.ini'
  try{[IO.File]::WriteAllText($validation,$text);$null=Read-MouseSettings $validation}finally{if(Test-Path -LiteralPath $validation){Remove-Item -LiteralPath $validation -Force}}
  if(!$seen.ContainsKey('_backup')){
   $backup=Join-Path $Root ('settings-backups/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
   New-Item -ItemType Directory -Path $backup -Force | Out-Null
   $manifest=[ordered]@{}
   foreach($name in @('MouseAim-Settings.ini','Keybindings.ini','Hud-Style.ini','FOV.ini')){
    $source=Join-Path $Root $name
    if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination (Join-Path $backup $name);$manifest[$name]=(Get-AC8FileHash -LiteralPath $source).Hash}
    else{$manifest[$name]='MISSING'}
   }
   Write-AC8SettingAtomic (Join-Path $backup 'backup.json') ($manifest | ConvertTo-Json)
   $seen['_backup']=$backup
  }
  Write-AC8SettingAtomic $path $text
  foreach($key in $changed){$seen[$key]=[long]$values[$key+'_revision']}
  Write-AC8SettingAtomic $ledgerPath ($seen | ConvertTo-Json)
 }finally{if($held){$mutex.ReleaseMutex()};$mutex.Dispose()}
}
