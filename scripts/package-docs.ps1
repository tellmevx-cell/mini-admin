[CmdletBinding()]
param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory ".."))
$docsOutput = Join-Path $repositoryRoot "docs-site\.vitepress\dist"
$deployScriptSource = Join-Path $repositoryRoot "deploy-mini-admin-docs.sh"

foreach ($command in @("git", "pnpm", "tar")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required to build the documentation package."
    }
}

Push-Location $repositoryRoot
try {
    & pnpm docs:build
    if ($LASTEXITCODE -ne 0) {
        throw "The VitePress documentation build failed."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $docsOutput "index.html"))) {
        throw "The documentation build did not produce index.html."
    }

    if (-not (Test-Path -LiteralPath $deployScriptSource)) {
        throw "The server deployment script is missing: $deployScriptSource"
    }

    $commit = (& git rev-parse --short=12 HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw "The current Git commit could not be determined."
    }

    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path $repositoryRoot "artifacts\docs"
    }

    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

    $archivePath = Join-Path $resolvedOutputDirectory "mini-admin-docs-$commit.tar.gz"
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    & tar -C $docsOutput -czf $archivePath .
    if ($LASTEXITCODE -ne 0) {
        throw "The documentation archive could not be created."
    }

    $archiveEntries = & tar -tzf $archivePath
    if ($LASTEXITCODE -ne 0 -or -not ($archiveEntries -contains "./index.html")) {
        Remove-Item -LiteralPath $archivePath -Force
        throw "The documentation archive verification failed."
    }

    $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    $sizeMb = [math]::Round((Get-Item -LiteralPath $archivePath).Length / 1MB, 2)
    $deployScriptPath = Join-Path $resolvedOutputDirectory "deploy-mini-admin-docs.sh"
    Copy-Item -LiteralPath $deployScriptSource -Destination $deployScriptPath -Force
    $deployScriptChecksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $deployScriptPath).Hash.ToLowerInvariant()

    Write-Host "Documentation package created: $archivePath"
    Write-Host "Size: $sizeMb MB"
    Write-Host "SHA256: $checksum"
    Write-Host "Deployment script copied: $deployScriptPath"
    Write-Host "Script SHA256: $deployScriptChecksum"
    Write-Host "Upload both files to the server, then run:"
    Write-Host "  bash deploy-mini-admin-docs.sh --domain docs.example.com"
    Write-Host "Automatic 1Panel HTTPS:"
    Write-Host "  bash deploy-mini-admin-docs.sh --domain docs.example.com --auto-ssl --acme-email ops@example.com --cloudflare-email ops@example.com"
}
finally {
    Pop-Location
}
