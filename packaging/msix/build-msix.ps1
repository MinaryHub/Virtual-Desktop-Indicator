# Builds the MSIX package locally (for testing before Store submission).
# Usage from repo root:   pwsh packaging/msix/build-msix.ps1 -Version 1.3.5
#
# The Store re-signs the package on submission, so signing is NOT required to submit. To
# SIDELOAD-test the .msix on this machine you must sign it with a certificate whose subject
# matches Identity/@Publisher and trust that cert — see the notes printed at the end.
param(
    [string]$Version = "1.3.5"
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root
try {
    $stage = Join-Path $root 'msix-stage'
    $pub = Join-Path $root 'publish-msix'
    Remove-Item $stage, $pub -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "Publishing $Version ..." -ForegroundColor Cyan
    # Plain folder layout (NOT single-file): the MSIX is already the container, and an
    # unbundled layout starts faster and gives the Store's certification tooling real files
    # to inspect instead of one opaque bundle.
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:SkipVersionStamp=true `
        -p:Version=$Version -p:AssemblyVersion=$Version `
        -p:FileVersion=$Version -p:InformationalVersion=$Version `
        -o $pub
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    Remove-Item (Join-Path $pub '*.pdb') -ErrorAction SilentlyContinue

    Write-Host "Staging package layout ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item (Join-Path $pub '*') $stage -Recurse -Force
    Copy-Item (Join-Path $PSScriptRoot 'Assets') (Join-Path $stage 'Assets') -Recurse -Force
    $manifest = Join-Path $stage 'AppxManifest.xml'
    Copy-Item (Join-Path $PSScriptRoot 'Package.appxmanifest') $manifest -Force
    # Anchored to the <Identity> element on purpose: a bare Version="[0-9.]+" also matches the
    # tail of MinVersion="10.0.19041.0" in TargetDeviceFamily and would rewrite the minimum OS
    # version to the app version. -creplace is case-SENSITIVE so the lowercase version="1.0" in
    # the <?xml ...?> declaration is left alone. UTF-8 without a BOM: MakeAppx rejects both a
    # UTF-16 body (PowerShell's default) and, on some SDK versions, a UTF-8 BOM.
    $stamped = (Get-Content $manifest -Raw) -creplace '(<Identity[^>]*?Version=")[0-9.]+(")', "`${1}$Version.0`$2"
    [System.IO.File]::WriteAllText($manifest, $stamped, (New-Object System.Text.UTF8Encoding($false)))

    if (Select-String -Path $manifest -Pattern 'PLACEHOLDER' -Quiet) {
        Write-Warning "AppxManifest still has PLACEHOLDER identity values — fill in the Partner Center values before submitting to the Store."
    }

    $makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows 10/11 SDK." }

    $out = Join-Path $root "DeskCue-$Version.msix"
    Write-Host "Packing $out ..." -ForegroundColor Cyan
    & $makeappx.FullName pack /d $stage /p $out /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

    Write-Host "`nBuilt $out" -ForegroundColor Green
    Write-Host @"

Next steps:
  • Store submission: upload this .msix in Partner Center (it re-signs it for you).
  • Local sideload test: sign it first, e.g.
      `$cert = New-SelfSignedCertificate -Type Custom -Subject "<same as Identity/@Publisher>" ``
                 -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My ``
                 -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
      signtool sign /fd SHA256 /a /f <exported.pfx> /p <pw> "$out"
    then trust the cert in LocalMachine\TrustedPeople and: Add-AppxPackage "$out"
"@ -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
