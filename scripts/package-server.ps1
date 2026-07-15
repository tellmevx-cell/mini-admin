[CmdletBinding()]
param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory ".."))

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git is required to create the deployment package."
}
if (-not (Get-Command tar -ErrorAction SilentlyContinue)) {
    throw "Tar is required to verify the deployment package."
}

$commit = (& git -C $repositoryRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    throw "The MiniAdmin repository commit could not be determined."
}

$workingTreeChanges = & git -C $repositoryRoot status --short
if ($workingTreeChanges) {
    Write-Warning "The working tree has uncommitted changes. The package contains committed HEAD only."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\deploy"
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

$archivePath = Join-Path $resolvedOutputDirectory "mini-admin-server-$commit.tar.gz"
if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

& git -C $repositoryRoot archive --format=tar.gz --prefix=mini-admin/ --output=$archivePath HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Git archive failed."
}

$archiveEntries = & tar -tzf $archivePath
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $archivePath -Force
    throw "The generated archive could not be verified."
}

$sensitiveEntries = $archiveEntries | Where-Object {
    $_ -eq "mini-admin/.env" -or
    $_ -match "/appsettings\.(Development|Local)\.json$" -or
    $_ -match "\.(pfx|p12|pem|key)$" -or
    $_ -match "/id_(rsa|dsa|ecdsa|ed25519)$"
}
if ($sensitiveEntries) {
    Remove-Item -LiteralPath $archivePath -Force
    throw "Deployment package blocked because sensitive files were found: $($sensitiveEntries -join ', ')"
}

$checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
$sizeMb = [math]::Round((Get-Item -LiteralPath $archivePath).Length / 1MB, 2)

Write-Host "Server package created: $archivePath"
Write-Host "Size: $sizeMb MB"
Write-Host "SHA256: $checksum"
Write-Host "Upload it with 1Panel, extract it, then run: cd mini-admin && bash deploy.sh"
