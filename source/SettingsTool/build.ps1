param([string]$OutputPath=(Join-Path $PSScriptRoot '..\..\AC8Workbench.exe'),[switch]$EmbedBundle,[string]$BundlePath=(Join-Path $PSScriptRoot 'bundle.zip'),[string]$HashesPath=(Join-Path $PSScriptRoot 'bundle-hashes.json'))
$ErrorActionPreference='Stop'
$sourceDir=$PSScriptRoot
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
$outputDir=[IO.Path]::GetDirectoryName($OutputPath)
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
$arguments=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',
 ('/out:'+ $OutputPath),
 ('/win32manifest:'+ (Join-Path $sourceDir 'app.manifest')),
 ('/win32icon:'+ (Join-Path $sourceDir 'app.ico')),
 ('/resource:'+ (Join-Path $sourceDir 'MainWindow.xaml')+',MainWindow.xaml'),
 ('/resource:'+ (Join-Path $sourceDir 'Bootstrap.xaml')+',Bootstrap.xaml'),
 ('/r:'+ (Join-Path $framework 'WPF\WindowsBase.dll')),
 ('/r:'+ (Join-Path $framework 'WPF\PresentationCore.dll')),
 ('/r:'+ (Join-Path $framework 'WPF\PresentationFramework.dll')),
 ('/r:'+ (Join-Path $framework 'System.Xaml.dll')),
 '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll',
 '/r:System.IO.Compression.dll',
 (Join-Path $sourceDir 'Settings.cs'),(Join-Path $sourceDir 'App.cs'),(Join-Path $sourceDir 'SelfTest.cs'),(Join-Path $sourceDir 'AssemblyInfo.cs'),
 (Join-Path $sourceDir 'Bundle.cs'),(Join-Path $sourceDir 'Operations.cs'),(Join-Path $sourceDir 'WorkbenchUI.cs'),(Join-Path $sourceDir 'WorkbenchTest.cs'))
if($EmbedBundle){$arguments+=@('/resource:'+$BundlePath+',AC8.Bundle.zip','/resource:'+$HashesPath+',AC8.Bundle.hashes.json')}
& $compiler $arguments
if($LASTEXITCODE -ne 0){throw 'Workbench EXE compilation failed.'}
Write-Output 'AC8Workbench.exe compiled.'
