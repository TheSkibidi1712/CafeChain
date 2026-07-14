#Requires -Version 5.1
<#
.SYNOPSIS
  Remove Email:Password from User Secrets for the CafeChain web project.
#>

$ErrorActionPreference = 'Stop'

function Find-RepoRoot {
    $dir = Get-Location
    for ($i = 0; $i -lt 8 -and $null -ne $dir; $i++) {
        $web = Join-Path $dir.Path 'CafeChain\CafeChain.csproj'
        if (Test-Path $web) { return $dir.Path }
        $dir = $dir.Parent
    }
    throw "Could not find CafeChain web project. Run from repo root."
}

$repoRoot = Find-RepoRoot
$projectPath = Join-Path $repoRoot 'CafeChain\CafeChain.csproj'
if (-not (Test-Path $projectPath)) {
    throw "Web project not found: $projectPath"
}

Write-Host "Removing Email:Password from User Secrets..." -ForegroundColor Cyan
Write-Host "  Project: $projectPath"

try {
    & dotnet user-secrets remove "Email:Password" --project $projectPath 2>&1 | Out-Null
    Write-Host "OK — Email:Password removed (if it existed)." -ForegroundColor Green
}
catch {
    Write-Host "Note: remove finished with message: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Also clear optional related secrets set by setup script (non-secret SMTP metadata).
foreach ($key in @('Email:DeliveryMode', 'Email:SmtpHost', 'Email:SmtpPort', 'Email:Address')) {
    try { & dotnet user-secrets remove $key --project $projectPath 2>$null | Out-Null } catch { }
}

Write-Host "Restart backend if it is running." -ForegroundColor Yellow
