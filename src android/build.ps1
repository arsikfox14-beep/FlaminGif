# Собирает FlaminGif.apk без Gradle: aapt2 + javac + d8 + zipalign + apksigner.
# Требуется JDK 17+ и .sdk (см. setup-sdk.ps1).

param(
    [switch]$Clean,
    [switch]$Desktop
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$root = $PSScriptRoot
$sdk = Join-Path $root '.sdk'
$platformJar = Join-Path $sdk 'platforms\android-34\android.jar'
$buildTools = Join-Path $sdk 'build-tools\34.0.0'
$r8Jar = Join-Path $sdk 'tools\r8.jar'
$build = Join-Path $root 'build'
$outApk = Join-Path $root 'FlaminGif.apk'
$r8Version = '9.5.32'

if (!(Test-Path $platformJar) -or !(Test-Path (Join-Path $buildTools 'aapt2.exe'))) {
    Write-Host 'SDK не найден, запускаем setup-sdk.ps1...'
    & (Join-Path $root 'setup-sdk.ps1')
}
if (!(Test-Path $r8Jar)) {
    Write-Host "Скачиваем R8 $r8Version (20 МБ)..."
    New-Item -ItemType Directory -Force -Path (Split-Path $r8Jar) | Out-Null
    Invoke-WebRequest -Uri "https://maven.google.com/com/android/tools/r8/$r8Version/r8-$r8Version.jar" `
        -OutFile $r8Jar -UseBasicParsing -TimeoutSec 900
}

if ($Clean -and (Test-Path $build)) {
    Remove-Item $build -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $build | Out-Null
Remove-Item (Join-Path $build '*') -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path `
    (Join-Path $build 'gen'), (Join-Path $build 'classes'), (Join-Path $build 'dex') | Out-Null

$aapt2 = Join-Path $buildTools 'aapt2.exe'
$resZip = Join-Path $build 'res.zip'
$unsigned = Join-Path $build 'app-unsigned.apk'

Write-Host '1/6 aapt2 compile...'
& $aapt2 compile --dir (Join-Path $root 'res') -o $resZip
if ($LASTEXITCODE -ne 0) { throw 'aapt2 compile failed' }

Write-Host '2/6 aapt2 link...'
& $aapt2 link -o $unsigned `
    -I $platformJar `
    --manifest (Join-Path $root 'AndroidManifest.xml') `
    -A (Join-Path $root 'assets') `
    --java (Join-Path $build 'gen') `
    --min-sdk-version 24 `
    --target-sdk-version 34 `
    --version-code 5302 `
    --version-name '5.3' `
    --replace-version `
    $resZip
if ($LASTEXITCODE -ne 0) { throw 'aapt2 link failed' }

Write-Host '3/6 javac...'
$sources = @()
$sources += (Get-ChildItem (Join-Path $build 'gen') -Recurse -Filter *.java).FullName
$sources += (Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.java).FullName
& javac -encoding UTF-8 --release 11 -classpath $platformJar -d (Join-Path $build 'classes') @sources
if ($LASTEXITCODE -ne 0) { throw 'javac failed' }

Write-Host '4/6 d8...'
$classes = (Get-ChildItem (Join-Path $build 'classes') -Recurse -Filter *.class).FullName
& java -cp $r8Jar com.android.tools.r8.D8 --release --min-api 24 --lib $platformJar `
    --output (Join-Path $build 'dex') @classes
if ($LASTEXITCODE -ne 0) { throw 'd8 failed' }

Write-Host '5/6 упаковка classes.dex...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($unsigned, 'Update')
try {
    $old = $zip.GetEntry('classes.dex')
    if ($old) { $old.Delete() }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        (Join-Path $build 'dex\classes.dex'),
        'classes.dex',
        [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
} finally {
    $zip.Dispose()
}

& (Join-Path $buildTools 'zipalign.exe') -p -f 4 $unsigned (Join-Path $build 'app-aligned.apk')
if ($LASTEXITCODE -ne 0) { throw 'zipalign failed' }

Write-Host '6/6 подпись...'
$ksDir = Join-Path $root 'keystore'
$ks = Join-Path $ksDir 'video2gif.jks'
if (!(Test-Path $ks)) {
    New-Item -ItemType Directory -Force -Path $ksDir | Out-Null
    & keytool -genkeypair -v -keystore $ks -alias video2gif -keyalg RSA -keysize 2048 `
        -validity 10000 -storepass video2gif -keypass video2gif `
        -dname 'CN=video2gif, OU=Mobile, O=video2gif, C=RU'
    if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }
}

if (Test-Path $outApk) { Remove-Item $outApk -Force }
& (Join-Path $buildTools 'apksigner.bat') sign `
    --ks $ks --ks-key-alias video2gif `
    --ks-pass pass:video2gif --key-pass pass:video2gif `
    --out $outApk (Join-Path $build 'app-aligned.apk')
if ($LASTEXITCODE -ne 0) { throw 'apksigner failed' }

& (Join-Path $buildTools 'apksigner.bat') verify --verbose $outApk | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'apksigner verify failed' }

$info = & $aapt2 dump badging $outApk | Select-String -Pattern '^package:|^application-label:|^sdkVersion:|^targetSdkVersion:'
Write-Host ''
$info | ForEach-Object { Write-Host $_.Line }
Write-Host ''
Write-Host ("APK: " + $outApk)
Write-Host ("Размер: " + [math]::Round((Get-Item $outApk).Length / 1MB, 1) + " МБ")

if ($Desktop) {
    $desk = [Environment]::GetFolderPath('Desktop')
    Copy-Item $outApk (Join-Path $desk 'FlaminGif.apk') -Force
    Write-Host ("Скопировано на рабочий стол: " + (Join-Path $desk 'FlaminGif.apk'))
}
