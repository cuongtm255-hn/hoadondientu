#requires -Version 5.1
<#
.SYNOPSIS
    Cấp mã kích hoạt cho một máy từ mã máy khách gửi tới.
.EXAMPLE
    .\scripts\New-ActivationKey.ps1 -MachineCode 0123ABCD-4567EF01-89ABCDEF-02468ACE
.DESCRIPTION
    Chỉ dùng khi không cấp mã qua máy chủ (xem docs/runbooks/license-keys.md mục 0). Mã có dạng EZACC1|<payload base64url>|<chữ ký base64url>; payload là product=CDSOFT_EZACC;machine=...;license=PerMachine;issued=...
    ký RSA-SHA256 bằng khóa bí mật trong .local-secrets (đã bị git bỏ qua). Mã chỉ đúng với đúng máy đó, không hết hạn.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$MachineCode,
    [string]$PrivateKeyPath = (Join-Path (Split-Path -Parent $PSScriptRoot) '.local-secrets\HDDT-License.private.xml'),
    [string]$Product = 'CDSOFT_EZACC',
    [string]$KeyPrefix = 'EZACC1'
)

$ErrorActionPreference = 'Stop'

$normalized = ($MachineCode -replace '[-\s]', '').ToUpperInvariant()
if ($normalized -notmatch '^[0-9A-F]{32}$') {
    throw 'Mã máy phải gồm 32 ký tự hex, dạng XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX.'
}

if (-not (Test-Path $PrivateKeyPath)) {
    throw "Không thấy khóa bí mật tại $PrivateKeyPath. Chạy scripts\New-LicenseKeyPair.ps1 trước (hoặc lấy khóa đã sao lưu)."
}

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$formatted = ($normalized -replace '(.{8})(?!$)', '$1-')
$issued = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$payload = "product=$Product;machine=$formatted;license=PerMachine;issued=$issued"
$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
try {
    $rsa.PersistKeyInCsp = $false
    $rsa.FromXmlString([System.IO.File]::ReadAllText($PrivateKeyPath))
    $sha = New-Object System.Security.Cryptography.SHA256CryptoServiceProvider
    $signature = $rsa.SignData($payloadBytes, $sha)
}
finally {
    $rsa.Dispose()
}

$key = "$KeyPrefix|$(ConvertTo-Base64Url $payloadBytes)|$(ConvertTo-Base64Url $signature)"
Write-Host "Máy: $formatted"
Write-Host 'Mã kích hoạt (gửi nguyên một dòng cho khách):'
Write-Output $key
