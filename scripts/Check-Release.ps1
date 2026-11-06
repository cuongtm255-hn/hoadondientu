<#
.SYNOPSIS
  P6-04 / ADR 0005: kiểm tra bản Release không chứa mã giả (development adapter, mock) hay bí mật.

.DESCRIPTION
  Chỉ đọc, không chạy ứng dụng và không gọi mạng. Đầu vào là thư mục đầu ra của bản Release
  (mặc định src\HoaDonDienTu.WinForms\bin\Release). Kiểm:
    1. Các assembly của ứng dụng không có kiểu nào thuộc namespace Development hoặc tên Mock/Fake/Dummy.
    2. Các chuỗi trong assembly không chứa nhãn dữ liệu giả ("[TEST]", "DỮ LIỆU GIẢ", "MOCK", "mô phỏng", "localhost").
    3. Không có mẫu bí mật (JWT, Bearer token, khóa riêng PEM, "password=") trong assembly và file cấu hình.
    4. Thư mục Release không chứa file không được phép (*.raw.*, *.db, companies.json, license.dat, dữ liệu mẫu).
  Thoát với mã 1 nếu có vi phạm. Build trước:  MSBuild src\HoaDonDienTu.WinForms\HoaDonDienTu.WinForms.csproj -p:Configuration=Release
#>
param(
    [string]$ReleaseDir = (Join-Path $PSScriptRoot '..\src\HoaDonDienTu.WinForms\bin\Release')
)

$ErrorActionPreference = 'Stop'
$ReleaseDir = (Resolve-Path $ReleaseDir).Path
$violations = New-Object System.Collections.Generic.List[string]

function Add-Violation([string]$text) { $script:violations.Add($text) }

$appFiles = @('HoaDonDienTu.exe', 'HoaDonDienTu.Application.dll', 'HoaDonDienTu.Domain.dll', 'HoaDonDienTu.Infrastructure.dll') |
    ForEach-Object { Join-Path $ReleaseDir $_ }
foreach ($f in $appFiles) { if (-not (Test-Path $f)) { throw "Không thấy $f. Hãy build cấu hình Release trước." } }

# ---- 1. Kiểu giả ----
$typePattern = '(\.Development(\.|$))|Mock|Fake|Dummy'
foreach ($f in $appFiles) {
    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($f)
    try { $types = $assembly.GetTypes() }
    catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }
    foreach ($t in $types) {
        if ($t.FullName -match $typePattern) { Add-Violation ("Kiểu giả trong Release: {0} ({1})" -f $t.FullName, (Split-Path $f -Leaf)) }
    }
}

# ---- 2 + 3. Chuỗi cấm và mẫu bí mật trong assembly ----
function Get-Strings([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    # Chuỗi người dùng của .NET là UTF-16; chuỗi tài nguyên thường là UTF-8. Đọc cả hai.
    [System.Text.Encoding]::Unicode.GetString($bytes)
    [System.Text.Encoding]::UTF8.GetString($bytes)
}

$forbiddenText = @('[TEST]', 'DỮ LIỆU GIẢ', 'MOCK', 'mô phỏng', 'localhost', 'MockPortal')
$secretPatterns = @(
    @{ Name = 'JWT'; Regex = 'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{5,}' },
    @{ Name = 'Bearer token'; Regex = 'Bearer\s+[A-Za-z0-9._\-]{20,}' },
    @{ Name = 'Khóa riêng PEM'; Regex = '-----BEGIN (RSA |EC )?PRIVATE KEY-----' },
    @{ Name = 'password= có giá trị'; Regex = '(?i)password\s*=\s*[^\s;"'']{3,}' }
)

foreach ($f in $appFiles) {
    $name = Split-Path $f -Leaf
    $texts = @(Get-Strings $f)
    foreach ($word in $forbiddenText) {
        foreach ($text in $texts) {
            if ($text.IndexOf($word, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                Add-Violation ("Chuỗi cấm '{0}' trong {1}" -f $word, $name)
                break
            }
        }
    }

    foreach ($p in $secretPatterns) {
        foreach ($text in $texts) {
            if ([regex]::IsMatch($text, $p.Regex)) {
                Add-Violation ("Có mẫu bí mật ({0}) trong {1}" -f $p.Name, $name)
                break
            }
        }
    }
}

# File cấu hình và dữ liệu đi kèm cũng quét mẫu bí mật.
$configFiles = Get-ChildItem $ReleaseDir -Recurse -File -Include *.config, *.json, *.xml, *.txt -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(locales|swiftshader|DawnCache|GPUCache)\\' -and $_.Length -lt 5MB -and $_.Name -notlike 'CefSharp*.xml' }
foreach ($file in $configFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($p in $secretPatterns) {
        if ([regex]::IsMatch($text, $p.Regex)) { Add-Violation ("Có mẫu bí mật ({0}) trong {1}" -f $p.Name, $file.FullName.Substring($ReleaseDir.Length + 1)) }
    }
}

# ---- 4. File không được phép ----
$forbiddenFiles = @('*.raw.*', '*.db', '*.db-wal', 'companies.json', 'license.dat', 'company.identity.json', 'sample-api-*', '*mock*', '*Mock*')
foreach ($pattern in $forbiddenFiles) {
    Get-ChildItem $ReleaseDir -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(locales|swiftshader|DawnCache|GPUCache)\\' } |
        ForEach-Object { Add-Violation ("File không được phép trong Release: " + $_.FullName.Substring($ReleaseDir.Length + 1)) }
}

if ($violations.Count -gt 0) {
    Write-Host ("KHÔNG ĐẠT: {0} vi phạm" -f $violations.Count) -ForegroundColor Red
    $violations | Sort-Object -Unique | ForEach-Object { Write-Host (" - " + $_) }
    exit 1
}

Write-Host ("ĐẠT: {0} assembly ứng dụng không có mã giả, nhãn dữ liệu giả hay mẫu bí mật; thư mục Release không có file bị cấm." -f $appFiles.Count) -ForegroundColor Green
exit 0
