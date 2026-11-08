#requires -Version 5.1
<#
.SYNOPSIS
    Ký số (Authenticode) các file .exe/.dll/.msi: dùng cho bản Release và cho trình cài đặt tự làm.
.DESCRIPTION
    Dùng signtool.exe của Windows SDK, ký SHA-256 và đóng dấu thời gian (để chữ ký còn hiệu lực sau khi chứng chỉ hết hạn).
    Chứng chỉ lấy từ MỘT trong hai nơi:
      -Thumbprint   chứng chỉ đã cài trong kho Windows (Cert:\CurrentUser\My hoặc LocalMachine\My); token USB/HSM cũng ở đây.
      -PfxPath      file .pfx; mật khẩu lấy từ biến môi trường HDDT_SIGN_PFX_PASSWORD, nếu không có thì hỏi trên cửa sổ (không hiện ký tự).
    Mật khẩu và file .pfx không bao giờ được ghi vào repo, vào log hay vào tham số dòng lệnh của script. Sau khi ký, script chạy
    `signtool verify /pa` trên từng file (trừ khi -SkipVerify, dùng cho chứng chỉ tự ký khi thử luồng).
.EXAMPLE
    .\scripts\Sign-Files.ps1 -Path .\dist\HoaDonDienTu-Setup.exe -Thumbprint 0123ABCD...
.EXAMPLE
    .\scripts\Sign-Files.ps1 -Path src\HoaDonDienTu.WinForms\bin\Release -PfxPath D:\keys\cdsoft.pfx
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Path,
    [string]$Thumbprint,
    [string]$PfxPath,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$Description = 'Hóa đơn điện tử',
    [string[]]$Include = @('HoaDonDienTu*.exe', 'HoaDonDienTu*.dll', '*.msi', '*Setup*.exe'),
    [string]$SignToolPath,
    [switch]$NoTimestamp,
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'

function Find-SignTool {
    if ($SignToolPath) {
        if (Test-Path $SignToolPath) { return $SignToolPath }
        throw "Không thấy signtool tại $SignToolPath."
    }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $found = $null
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending | Select-Object -First 1
    }
    if (-not $found) { throw 'Không tìm thấy signtool.exe. Cài Windows SDK (thành phần "Signing Tools") hoặc truyền -SignToolPath.' }
    return $found.FullName
}

if ((-not $Thumbprint) -and (-not $PfxPath)) { throw 'Cần -Thumbprint hoặc -PfxPath.' }
if ($Thumbprint -and $PfxPath) { throw 'Chỉ dùng một trong -Thumbprint và -PfxPath.' }
if ($PfxPath -and -not (Test-Path $PfxPath)) { throw "Không thấy file chứng chỉ: $PfxPath" }

# Gom file cần ký: file được chỉ thẳng tên thì ký luôn; thư mục thì lấy theo -Include (đệ quy).
$files = New-Object System.Collections.Generic.List[string]
foreach ($p in $Path) {
    if (-not (Test-Path $p)) { throw "Không thấy: $p" }
    $item = Get-Item $p
    if ($item.PSIsContainer) {
        foreach ($pattern in $Include) {
            Get-ChildItem $item.FullName -Recurse -File -Filter $pattern | ForEach-Object { if (-not $files.Contains($_.FullName)) { $files.Add($_.FullName) } }
        }
    }
    elseif (-not $files.Contains($item.FullName)) { $files.Add($item.FullName) }
}
if ($files.Count -eq 0) { throw 'Không có file nào để ký.' }

$signtool = Find-SignTool

$certArgs = @()
if ($Thumbprint) {
    $certArgs += @('/sha1', ($Thumbprint -replace '\s', ''))
}
else {
    $password = $env:HDDT_SIGN_PFX_PASSWORD
    if (-not $password) {
        $secure = Read-Host -AsSecureString "Mật khẩu của $PfxPath"
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try { $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }
    $certArgs += @('/f', (Resolve-Path $PfxPath).Path, '/p', $password)
}

$tsArgs = @()
if (-not $NoTimestamp) { $tsArgs += @('/tr', $TimestampUrl, '/td', 'sha256') }

$failed = @()
foreach ($file in $files) {
    $rel = $file
    Write-Host "Ký: $rel"
    $all = @('sign', '/fd', 'sha256') + $certArgs + $tsArgs + @('/d', $Description, $file)
    # Không in $all vì có thể chứa mật khẩu.
    & $signtool @all | Out-Null
    if ($LASTEXITCODE -ne 0) { $failed += $rel; continue }

    if (-not $SkipVerify) {
        & $signtool verify /pa /q $file | Out-Null
        if ($LASTEXITCODE -ne 0) { $failed += "$rel (xác minh thất bại)" }
    }
}

if ($failed.Count -gt 0) { throw ("Ký/xác minh lỗi: " + ($failed -join '; ')) }
Write-Host ("XONG: đã ký {0} file." -f $files.Count) -ForegroundColor Green
