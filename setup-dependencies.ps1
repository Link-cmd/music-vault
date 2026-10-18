$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$dependencyRoot = Join-Path $PSScriptRoot '.deps'
$downloads = Join-Path $dependencyRoot 'downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null

function Test-ArchiveHash($Path, $Hash) {
    return (Test-Path -LiteralPath $Path) -and ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Hash)
}

$naudioArchive = Join-Path $downloads 'naudio-1.10.0.zip'
$naudioHash = 'E4A80EED41DAD794695F9C114432DE4667D20BDFA80825AA94D159708CF29217'
if (-not (Test-ArchiveHash $naudioArchive $naudioHash)) {
    Invoke-WebRequest -UseBasicParsing 'https://api.nuget.org/v3-flatcontainer/naudio/1.10.0/naudio.1.10.0.nupkg' -OutFile $naudioArchive
}
if (-not (Test-ArchiveHash $naudioArchive $naudioHash)) { throw 'NAudio archive checksum mismatch.' }
Expand-Archive -LiteralPath $naudioArchive -DestinationPath (Join-Path $dependencyRoot 'naudio') -Force

$decoderArchive = Join-Path $downloads 'imageio-ffmpeg-0.6.0-win64.zip'
$decoderHash = '02FA47C83703C37DF6BFE4896AAB339013F62BF02C5EBF2DCE6DA56AF04FFC0A'
if (-not (Test-ArchiveHash $decoderArchive $decoderHash)) {
    $metadata = Invoke-RestMethod 'https://pypi.org/pypi/imageio-ffmpeg/0.6.0/json'
    $wheel = @($metadata.urls | Where-Object filename -eq 'imageio_ffmpeg-0.6.0-py3-none-win_amd64.whl')
    if ($wheel.Count -ne 1 -or $wheel[0].digests.sha256 -ne $decoderHash.ToLowerInvariant()) { throw 'Unexpected decoder wheel metadata.' }
    Invoke-WebRequest -UseBasicParsing $wheel[0].url -OutFile $decoderArchive
}
if (-not (Test-ArchiveHash $decoderArchive $decoderHash)) { throw 'FFmpeg package checksum mismatch.' }
Expand-Archive -LiteralPath $decoderArchive -DestinationPath (Join-Path $dependencyRoot 'decoder-package') -Force

$naudioDll = Join-Path $dependencyRoot 'naudio\lib\net35\NAudio.dll'
$decoderExe = Join-Path $dependencyRoot 'decoder-package\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
if (-not (Test-ArchiveHash $naudioDll 'BC4BACC3B8B28D898F1671B79F216CCA439F95EB60CD32D3E3ECAFBECAC42780')) { throw 'NAudio DLL checksum mismatch.' }
if (-not (Test-ArchiveHash $decoderExe '2CE797A0F88D7F067180338FB227F7B1928EA727BD9A4D7A1D022F7C52AF71A3')) { throw 'FFmpeg executable checksum mismatch.' }
Write-Host 'Verified NAudio 1.10.0 and FFmpeg 7.1 dependencies.'
