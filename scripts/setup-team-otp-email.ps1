#Requires -Version 5.1
<#
.SYNOPSIS
  One-step local setup: store shared Gmail App Password in User Secrets for OTP SMTP.

.DESCRIPTION
  - Does NOT hard-code or print the App Password.
  - Reads shared sender address from tracked CafeChain/appsettings.json when possible.
  - Writes User Secrets on the Web project (UserSecretsId).
  - Never writes secrets into appsettings.Local.json or source files.

.EXAMPLE
  .\scripts\setup-team-otp-email.ps1
#>

$ErrorActionPreference = 'Stop'

function Find-RepoRoot {
    $dir = Get-Location
    for ($i = 0; $i -lt 8 -and $null -ne $dir; $i++) {
        $web = Join-Path $dir.Path 'CafeChain\CafeChain.csproj'
        $alt = Join-Path $dir.Path 'CafeChain.csproj'
        if (Test-Path $web) { return $dir.Path }
        if (Test-Path $alt) { return (Split-Path $dir.Path -Parent) }
        $dir = $dir.Parent
    }
    throw "Could not find CafeChain web project from current directory. Run from repo root."
}

function Get-SharedSenderAddress {
    param([string]$RepoRoot)
    $path = Join-Path $RepoRoot 'CafeChain\appsettings.json'
    if (-not (Test-Path $path)) { return 'cafechain8386@gmail.com' }
    try {
        $json = Get-Content $path -Raw | ConvertFrom-Json
        $addr = $json.Email.Address
        if ([string]::IsNullOrWhiteSpace($addr)) { return 'cafechain8386@gmail.com' }
        return $addr.Trim()
    }
    catch {
        return 'cafechain8386@gmail.com'
    }
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString]$Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

$repoRoot = Find-RepoRoot
$projectPath = Join-Path $repoRoot 'CafeChain\CafeChain.csproj'
if (-not (Test-Path $projectPath)) {
    throw "Web project not found: $projectPath"
}

$sender = Get-SharedSenderAddress -RepoRoot $repoRoot

Write-Host ""
Write-Host "CafeChain — team OTP email setup" -ForegroundColor Cyan
Write-Host "  Project : $projectPath"
Write-Host "  Sender  : $sender  (from tracked config)"
Write-Host "  Host    : smtp.gmail.com:587"
Write-Host "  Mode    : Smtp"
Write-Host ""
Write-Host "Get the shared Gmail App Password from the team private channel." -ForegroundColor Yellow
Write-Host "Do NOT paste it into git, issues, or screenshots." -ForegroundColor Yellow
Write-Host ""

$secure = Read-Host -Prompt "Paste Gmail App Password (input hidden)" -AsSecureString
if ($null -eq $secure -or $secure.Length -eq 0) {
    throw "App Password was empty. Aborted."
}

$plain = ConvertFrom-SecureStringPlain -Secure $secure
$plain = ($plain -replace '\s', '').Trim()
if ([string]::IsNullOrWhiteSpace($plain) -or $plain.Length -lt 8) {
    throw "App Password looks invalid (too short after removing spaces). Aborted."
}

try {
    Write-Host "Writing User Secrets (password not echoed)..." -ForegroundColor DarkGray
    & dotnet user-secrets set "Email:DeliveryMode" "Smtp" --project $projectPath | Out-Null
    & dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com" --project $projectPath | Out-Null
    & dotnet user-secrets set "Email:SmtpPort" "587" --project $projectPath | Out-Null
    & dotnet user-secrets set "Email:Address" $sender --project $projectPath | Out-Null
    & dotnet user-secrets set "Email:Password" $plain --project $projectPath | Out-Null
}
finally {
    $plain = $null
    [GC]::Collect()
}

Write-Host ""
Write-Host "User Secrets status:" -ForegroundColor Green
$list = & dotnet user-secrets list --project $projectPath 2>&1
$passwordConfigured = $false
foreach ($line in $list) {
    if ($line -match 'Email:Password') {
        Write-Host "  Email:Password = configured (value hidden)"
        $passwordConfigured = $true
    }
    elseif ($line -match 'Email:') {
        Write-Host "  $line"
    }
}

if (-not $passwordConfigured) {
    Write-Host "  WARNING: Email:Password not found in user-secrets list." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "OK — local SMTP secret stored in User Secrets only." -ForegroundColor Green
Write-Host "Restart backend before testing Send OTP." -ForegroundColor Yellow
Write-Host ""
Write-Host "  cd CafeChain"
Write-Host "  dotnet run --launch-profile http"
Write-Host ""
Write-Host "Docs: CafeChain/docs/testing/email-otp-local-setup.md"
Write-Host ""
