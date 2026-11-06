#requires -Version 5.1
<#
.SYNOPSIS
    Sinh cặp khóa RSA-2048 dùng để ký mã kích hoạt (chỉ chạy MỘT lần cho mỗi sản phẩm).
.DESCRIPTION
    Ghi khóa BÍ MẬT vào .local-secrets\HDDT-License.private.xml (đã bị git bỏ qua, TUYỆT ĐỐI không commit) và khóa CÔNG KHAI
    vào .local-secrets\HDDT-License.public.xml. Dán nội dung khóa công khai vào LicenseConstants.PublicKeyXml
    (src\HoaDonDienTu.Infrastructure\Licensing\MachineLicenseInfrastructure.cs) rồi build lại.
    Đổi khóa thì mọi mã kích hoạt đã cấp trước đó không còn hợp lệ, nên script từ chối ghi đè nếu không có -Force.
#>
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) '.local-secrets'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$private = Join-Path $OutDir 'HDDT-License.private.xml'
$public = Join-Path $OutDir 'HDDT-License.public.xml'

if ((Test-Path $private) -and -not $Force) {
    throw "Đã có khóa tại $private. Đổi khóa làm hỏng mọi mã kích hoạt đã cấp; chỉ chạy lại với -Force khi chắc chắn."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(2048)
try {
    $rsa.PersistKeyInCsp = $false
    [System.IO.File]::WriteAllText($private, $rsa.ToXmlString($true), (New-Object System.Text.UTF8Encoding($false)))
    [System.IO.File]::WriteAllText($public, $rsa.ToXmlString($false), (New-Object System.Text.UTF8Encoding($false)))
}
finally {
    $rsa.Dispose()
}

Write-Host "Đã ghi khóa bí mật: $private (KHÔNG commit, sao lưu ở nơi an toàn)"
Write-Host "Đã ghi khóa công khai: $public (dán vào LicenseConstants.PublicKeyXml)"
