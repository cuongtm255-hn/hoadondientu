<#
.SYNOPSIS
  P6-05: dựng bản Release sạch, kiểm, rồi đóng gói thành một file ZIP có thể cài bằng cách giải nén (bản "portable") kèm thông tin phiên bản và SHA-256.

.DESCRIPTION
  Các bước, dừng ngay khi một bước lỗi:
    1. Xóa src\HoaDonDienTu.WinForms\bin\Release (thư mục này không tự dọn; từng có file spike sót lại) rồi build Release (x86).
    2. Chạy scripts\Check-Release.ps1 (không có mã giả, nhãn dữ liệu giả, bí mật hay file cấm).
    3. Chép các file cần thiết vào thư mục tạm, bỏ file .pdb, tài liệu NuGet và thư mục đệm của Chromium; kiểm đủ file bắt buộc
       (ứng dụng, SQLite x86 và x64, Chromium).
    4. Ghi release-info.json (phiên bản ứng dụng, schema cơ sở dữ liệu, CefSharp, SQLite, thời điểm build, commit) và SHA256SUMS.txt.
    5. Nén thành dist\HoaDonDienTu-<phiên bản>-win-x86.zip, giải nén thử ra thư mục tạm, chạy lại Check-Release và đối chiếu SHA-256.
  Không chạy ứng dụng và không gọi mạng (ngoài việc khôi phục NuGet của MSBuild).

  Lưu ý: gói này chưa có trình cài đặt (không có phím tắt hay mục gỡ cài); người dùng giải nén vào một thư mục rồi chạy HoaDonDienTu.exe.
  Dữ liệu người dùng nằm ở %LOCALAPPDATA%\HoaDonDienTu nên cập nhật bằng cách giải nén đè bản mới không mất dữ liệu.
#>
param(
    [string]$MSBuildPath,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\dist'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$releaseDir = Join-Path $repo 'src\HoaDonDienTu.WinForms\bin\Release'
$csproj = Join-Path $repo 'src\HoaDonDienTu.WinForms\HoaDonDienTu.WinForms.csproj'
$check = Join-Path $PSScriptRoot 'Check-Release.ps1'

function Step([string]$text) { Write-Host ("== " + $text) -ForegroundColor Cyan }

function Find-MSBuild {
    if ($MSBuildPath -and (Test-Path $MSBuildPath)) { return $MSBuildPath }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    $known = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path $known) { return $known }
    throw 'Không tìm thấy MSBuild. Truyền -MSBuildPath <đường dẫn MSBuild.exe>.'
}

function Run-Check([string]$dir) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $check -ReleaseDir $dir
    if ($LASTEXITCODE -ne 0) { throw "Check-Release báo vi phạm trên $dir" }
}

# ---- 1. Build sạch ----
if (-not $SkipBuild) {
    Step 'Dọn bin\Release và build Release'
    if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
    $msbuild = Find-MSBuild
    & $msbuild $csproj -restore -p:Configuration=Release -nologo -v:m
    if ($LASTEXITCODE -ne 0) { throw 'Build Release thất bại.' }
}
if (-not (Test-Path (Join-Path $releaseDir 'HoaDonDienTu.exe'))) { throw 'Chưa có bản Release; bỏ -SkipBuild.' }

# ---- 2. Kiểm Release ----
Step 'Kiểm bản Release (mã giả, bí mật, file cấm)'
Run-Check $releaseDir

# ---- 3. Chép file cần thiết ----
$exe = Join-Path $releaseDir 'HoaDonDienTu.exe'
$version = [System.Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3)
$name = "HoaDonDienTu-$version-win-x86"
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("hddt-package-" + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $staging $name
New-Item -ItemType Directory -Force $stage | Out-Null

Step "Chép file vào $name"
$excludeDirs = @('DawnCache', 'GPUCache')
$excludeFiles = @('*.pdb', 'CefSharp*.xml', 'System.Data.SQLite.xml', 'Krypton*.xml', 'PdfSharp*.xml', 'QRCoder.xml', 'Newtonsoft.Json.xml')
Get-ChildItem $releaseDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($releaseDir.Length + 1)
    $first = $relative.Split('\')[0]
    if ($excludeDirs -contains $first) { return }
    foreach ($pattern in $excludeFiles) { if ($_.Name -like $pattern) { return } }
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item $_.FullName $target
}

$required = @(
    'HoaDonDienTu.exe', 'HoaDonDienTu.Application.dll', 'HoaDonDienTu.Domain.dll', 'HoaDonDienTu.Infrastructure.dll',
    'System.Data.SQLite.dll', 'x86\SQLite.Interop.dll', 'x64\SQLite.Interop.dll',
    'libcef.dll', 'CefSharp.dll', 'CefSharp.Core.dll', 'CefSharp.WinForms.dll', 'CefSharp.BrowserSubprocess.exe',
    'icudtl.dat', 'resources.pak', 'v8_context_snapshot.bin'
)
$missing = $required | Where-Object { -not (Test-Path (Join-Path $stage $_)) }
if ($missing) { throw ('Thiếu file bắt buộc trong gói: ' + ($missing -join ', ')) }

# ---- 4. Thông tin phiên bản và SHA-256 ----
Step 'Ghi release-info.json và SHA256SUMS.txt'
$ps32 = Join-Path $env:WinDir 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
$schema = & $ps32 -NoProfile -Command "[void][Reflection.Assembly]::LoadFrom('$($stage -replace "'", "''")\HoaDonDienTu.Infrastructure.dll'); [HoaDonDienTu.Infrastructure.Persistence.Sqlite.CompanyDatabaseSchema]::Migrations.Count"
$cef = (Get-Item (Join-Path $stage 'CefSharp.dll')).VersionInfo.FileVersion
$sqlite = (Get-Item (Join-Path $stage 'System.Data.SQLite.dll')).VersionInfo.ProductVersion
$commit = (& git -C $repo rev-parse HEAD 2>$null)
$dirty = [bool](& git -C $repo status --porcelain 2>$null)
$info = [ordered]@{
    name = 'Hóa đơn điện tử'
    appVersion = $version
    databaseSchemaVersion = [int]$schema
    cefSharp = $cef
    chromium = '109 (CefSharp 109.1.110, bản cuối hỗ trợ Windows 7/8; xem docs/security/P6-03-security-review.md mục F-2)'
    sqlite = $sqlite
    platform = 'x86 (.NET Framework 4.5.2)'
    builtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    gitCommit = $commit
    workingTreeClean = (-not $dirty)
}
$info | ConvertTo-Json | Set-Content (Join-Path $stage 'release-info.json') -Encoding UTF8

$sums = New-Object System.Collections.Generic.List[string]
Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $sums.Add($hash + '  ' + $_.FullName.Substring($stage.Length + 1).Replace('\', '/'))
}
$sums | Set-Content (Join-Path $stage 'SHA256SUMS.txt') -Encoding UTF8

# ---- 5. Nén, giải nén thử, đối chiếu ----
Step 'Nén và kiểm gói'
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$zip = Join-Path $OutputDir ($name + '.zip')
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)

$verify = Join-Path $staging 'verify'
[System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $verify)
$extracted = Join-Path $verify $name
Run-Check $extracted
$bad = @()
foreach ($line in (Get-Content (Join-Path $extracted 'SHA256SUMS.txt'))) {
    $parts = $line -split '  ', 2
    $file = Join-Path $extracted ($parts[1].Replace('/', '\'))
    if (-not (Test-Path $file) -or (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $parts[0]) { $bad += $parts[1] }
}
if ($bad.Count -gt 0) { throw ('Giải nén thử không khớp SHA-256: ' + ($bad -join ', ')) }

$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content ($zip + '.sha256') ($zipHash + '  ' + (Split-Path $zip -Leaf)) -Encoding ASCII
Remove-Item $staging -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host ("XONG: {0}" -f $zip) -ForegroundColor Green
Write-Host ("  Phiên bản {0}, schema {1}, CefSharp {2}, {3} MB, SHA-256 {4}" -f $version, $schema, $cef, $size, $zipHash)
if ($dirty) { Write-Host '  CẢNH BÁO: thư mục làm việc git có thay đổi chưa commit; không nên phát hành bản này.' -ForegroundColor Yellow }
exit 0
