# Biên dịch CefSpike cạnh bản Release của ứng dụng (cần đã build Release để có Chromium trong bin\Release).
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$bin = Join-Path $repo 'src\HoaDonDienTu.WinForms\bin\Release'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path (Join-Path $bin 'libcef.dll'))) { throw "Chưa có Chromium trong $bin; build Release trước." }
$out = Join-Path $bin 'CefSpike.exe'
& $csc /nologo /platform:x86 /target:exe /out:$out `
    /r:"$bin\CefSharp.dll" /r:"$bin\CefSharp.Core.dll" /r:"$bin\CefSharp.WinForms.dll" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $PSScriptRoot 'Program.cs')
Push-Location $bin
try { & $out; "exit code $LASTEXITCODE" } finally { Pop-Location }
