$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The 64-bit .NET Framework compiler is required on Windows.' }
$naudio = Join-Path $PSScriptRoot '.deps\naudio\lib\net35\NAudio.dll'
$decoder = Join-Path $PSScriptRoot '.deps\decoder-package\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
if (-not (Test-Path -LiteralPath $naudio) -or -not (Test-Path -LiteralPath $decoder)) { throw 'Run setup-dependencies.ps1 first.' }
$references = @('System.dll','System.Core.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$resources = @(
    "/resource:$PSScriptRoot\LICENSE.txt,MusicVault-LICENSE.txt",
    "/resource:$naudio,NAudio.dll",
    "/resource:$PSScriptRoot\vault.ico,vault.ico",
    "/resource:$decoder,decoder.exe",
    "/resource:$PSScriptRoot\NAudio-LICENSE.txt,NAudio-LICENSE.txt",
    "/resource:$PSScriptRoot\FFmpeg-LICENSE.txt,FFmpeg-LICENSE.txt",
    "/resource:$PSScriptRoot\Retro.xaml,Retro.xaml",
    "/resource:$PSScriptRoot\Mini.xaml,Mini.xaml"
)
$sources = @('AutoLevel.cs','Player.cs','Retro.cs','Modes.cs','Delete.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$PSScriptRoot\MusicVault-base.exe" "/win32icon:$PSScriptRoot\vault.ico" "/reference:$naudio" @resources @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host 'Built MusicVault-base.exe: an empty player, ready for Add songs.'
