$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$vendor = Join-Path $root 'vendor'
New-Item -ItemType Directory -Force $vendor | Out-Null
$zip = Join-Path $vendor 'ubol.zip'
$version = '2026.1122.1948'
Invoke-WebRequest "https://github.com/uBlockOrigin/uBOL-home/releases/download/$version/uBOLite_$version.edge.zip" -OutFile $zip
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne '7617e0b164552b280043a84c24dc85178eed274f7941da91b1325d4f2aca535c') { throw 'uBOL integrity check failed' }
$runtime = Join-Path $vendor 'MicrosoftEdgeWebview2Setup.exe'
Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $runtime
$signature = Get-AuthenticodeSignature $runtime
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') { throw 'WebView2 bootstrapper signature is not valid Microsoft code' }
Write-Host 'Verified uBlock Origin Lite archive and Microsoft runtime bootstrapper.'
