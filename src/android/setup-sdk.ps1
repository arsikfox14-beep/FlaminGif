# Скачивает минимальный Android SDK (platform 34 + build-tools 34) в .sdk.
# Нужен только интернет и PowerShell 5+. JDK 17+ должен быть установлен.

param([switch]$Force)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = $PSScriptRoot
$sdk = Join-Path $root '.sdk'
$downloads = Join-Path $sdk 'downloads'
$tmp = Join-Path $sdk 'tmp'

$platformZip = Join-Path $downloads 'platform-34.zip'
$buildToolsZip = Join-Path $downloads 'build-tools-34.zip'
$r8Jar = Join-Path $sdk 'tools\r8.jar'
$r8Version = '9.5.32'

$platformUrl = 'https://dl.google.com/android/repository/platform-34-ext7_r03.zip'
$buildToolsUrl = 'https://dl.google.com/android/repository/build-tools_r34-windows.zip'
$r8Url = "https://maven.google.com/com/android/tools/r8/$r8Version/r8-$r8Version.jar"

New-Item -ItemType Directory -Force -Path $downloads | Out-Null

if ($Force -or !(Test-Path $platformZip)) {
    Write-Host 'Скачиваем platform-34 (60 МБ)...'
    Invoke-WebRequest -Uri $platformUrl -OutFile $platformZip -UseBasicParsing -TimeoutSec 900
}
if ($Force -or !(Test-Path $buildToolsZip)) {
    Write-Host 'Скачиваем build-tools 34 (56 МБ)...'
    Invoke-WebRequest -Uri $buildToolsUrl -OutFile $buildToolsZip -UseBasicParsing -TimeoutSec 900
}
if ($Force -or !(Test-Path $r8Jar)) {
    Write-Host 'Скачиваем R8 (20 МБ)...'
    New-Item -ItemType Directory -Force -Path (Split-Path $r8Jar) | Out-Null
    Invoke-WebRequest -Uri $r8Url -OutFile $r8Jar -UseBasicParsing -TimeoutSec 900
}

if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

Write-Host 'Распаковываем platform...'
[System.IO.Compression.ZipFile]::ExtractToDirectory($platformZip, (Join-Path $tmp 'platform'))
Write-Host 'Распаковываем build-tools...'
[System.IO.Compression.ZipFile]::ExtractToDirectory($buildToolsZip, (Join-Path $tmp 'build-tools'))

$jar = Get-ChildItem (Join-Path $tmp 'platform') -Recurse -Filter android.jar | Select-Object -First 1
if (!$jar) { throw 'android.jar не найден в архиве platform' }

$aapt2 = Get-ChildItem (Join-Path $tmp 'build-tools') -Recurse -Filter aapt2.exe | Select-Object -First 1
if (!$aapt2) { throw 'aapt2.exe не найден в архиве build-tools' }

$platformDir = Join-Path $sdk 'platforms\android-34'
$buildToolsDir = Join-Path $sdk 'build-tools\34.0.0'

foreach ($dir in @($platformDir, $buildToolsDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path $dir) | Out-Null
}

Move-Item $jar.Directory.FullName $platformDir
Move-Item $aapt2.Directory.FullName $buildToolsDir

Remove-Item $tmp -Recurse -Force

Write-Host ''
Write-Host "android.jar: $(Join-Path $platformDir 'android.jar')"
Write-Host "aapt2:       $(Join-Path $buildToolsDir 'aapt2.exe')"
Write-Host 'SDK готов. Теперь: powershell -ExecutionPolicy Bypass -File build.ps1'
