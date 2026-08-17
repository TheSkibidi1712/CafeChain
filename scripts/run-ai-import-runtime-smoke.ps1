#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$TesseractPath,
    [string]$TessdataPath,
    [switch]$SkipNativeOcr,
    [switch]$SkipOllama,
    [switch]$SkipSqlServer,
    [switch]$SkipFullSuite
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$testProject = Join-Path $repositoryRoot 'CafeChain.Tests\CafeChain.Tests.csproj'
$manifestPath = Join-Path $repositoryRoot 'CafeChain.Tests\Fixtures\AIImport\runtime-smoke-manifest.json'
$markdownPath = Join-Path $repositoryRoot 'CafeChain\Doc\AI_SMART_IMPORT_RUNTIME_SMOKE_REPORT.md'
$jsonPath = Join-Path $repositoryRoot 'CafeChain\Doc\AI_SMART_IMPORT_RUNTIME_SMOKE_REPORT.json'
$startedAt = [DateTimeOffset]::UtcNow
$runId = 'aiimport-' + $startedAt.ToString('yyyyMMddHHmmss')
$stages = [Collections.Generic.List[object]]::new()

function Resolve-TesseractExecutable {
    if ($TesseractPath) { return $TesseractPath }
    if ($env:CAFECHAIN_TESSERACT_PATH) { return $env:CAFECHAIN_TESSERACT_PATH }
    $command = Get-Command tesseract -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $windowsDefault = 'C:\Program Files\Tesseract-OCR\tesseract.exe'
    if (Test-Path -LiteralPath $windowsDefault) { return $windowsDefault }
    return $null
}

function Resolve-TessdataDirectory {
    if ($TessdataPath) { return $TessdataPath }
    if ($env:CAFECHAIN_TESSDATA_PATH) { return $env:CAFECHAIN_TESSDATA_PATH }
    $local = Join-Path $env:LOCALAPPDATA 'CafeChain\OCR\tessdata'
    if (Test-Path -LiteralPath $local) { return $local }
    return (Join-Path $repositoryRoot 'CafeChain\Resources\OCR\tessdata')
}

function Invoke-SmokeStage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$SkipReason
    )
    if ($SkipReason) {
        $stage = [pscustomobject]@{ Name = $Name; Status = 'SKIPPED'; DurationMs = 0; Detail = $SkipReason }
        $stages.Add($stage)
        Write-Host "SKIPPED $Name - $SkipReason"
        return $stage
    }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
    $timer.Stop()
    $status = if ($exitCode -eq 0) { 'PASSED' } else { 'FAILED' }
    $safeFailure = if ($exitCode -eq 0) { '' }
    elseif (($output -join "`n") -match 'Failed to generate SSPI context') { 'SQL_SERVER_SSPI_CONTEXT' }
    else {
        ($output | Where-Object {
            $_ -match '^\s*(Failed CafeChain\.Tests|Failed!|Error Message:|Passed!)'
        } | Select-Object -Last 12) -join ' | '
    }
    $stage = [pscustomobject]@{
        Name = $Name
        Status = $status
        DurationMs = $timer.ElapsedMilliseconds
        Detail = $safeFailure
    }
    $stages.Add($stage)
    Write-Host "$status $Name ($($timer.ElapsedMilliseconds) ms)"
    return $stage
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Runtime smoke manifest was not found.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$tesseractExecutable = Resolve-TesseractExecutable
$tessdataDirectory = Resolve-TessdataDirectory
$modelsReady = (Test-Path -LiteralPath (Join-Path $tessdataDirectory 'vie.traineddata')) -and
    (Test-Path -LiteralPath (Join-Path $tessdataDirectory 'eng.traineddata'))
$tesseractVersion = $null
if ($tesseractExecutable -and (Test-Path -LiteralPath $tesseractExecutable)) {
    $versionOutput = & $tesseractExecutable --version 2>&1
    if ($LASTEXITCODE -eq 0) { $tesseractVersion = [string]($versionOutput | Select-Object -First 1) }
}

$oldRuntime = $env:CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE
$oldTesseract = $env:CAFECHAIN_TESSERACT_PATH
$oldTessdata = $env:CAFECHAIN_TESSDATA_PATH
try {
    Set-Location -LiteralPath $repositoryRoot
    Invoke-SmokeStage 'Build' @('build', $testProject, '--no-restore', '--nologo', '--verbosity', 'quiet') | Out-Null
    Invoke-SmokeStage '126 fixtures - deterministic/offline' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--filter', 'FullyQualifiedName~AIImportRuntimeFixtureTests.Pipeline_fixture_matches_business_manifest|FullyQualifiedName~AIImportRuntimeFixtureTests.Manifest_covers_every_committed_fixture_exactly_once',
        '--logger', 'console;verbosity=minimal') | Out-Null
    Invoke-SmokeStage 'AI Import non-SQL regression' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--filter', 'FullyQualifiedName~AIImport&FullyQualifiedName!~AIImportSqlServerTests',
        '--logger', 'console;verbosity=minimal') | Out-Null

    $nativeSkip = $null
    if ($SkipNativeOcr) { $nativeSkip = 'Skipped by parameter.' }
    elseif (-not $tesseractExecutable -or -not (Test-Path -LiteralPath $tesseractExecutable)) { $nativeSkip = 'Tesseract executable was not found.' }
    elseif (-not $modelsReady) { $nativeSkip = 'The vie/eng models are missing.' }
    else {
        $env:CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE = '1'
        $env:CAFECHAIN_TESSERACT_PATH = $tesseractExecutable
        $env:CAFECHAIN_TESSDATA_PATH = $tessdataDirectory
    }
    Invoke-SmokeStage '20 PDF scan - native Tesseract' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--filter', 'FullyQualifiedName~Native_tesseract_processes_every_scan_fixture_using_the_manifest',
        '--logger', 'console;verbosity=minimal') $nativeSkip | Out-Null

    $ollamaSkip = if ($SkipOllama) { 'Skipped by parameter.' } else { $null }
    if (-not $ollamaSkip) { $env:CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE = '1' }
    Invoke-SmokeStage 'Narrative fallback - Ollama qwen3:4b' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--filter', 'FullyQualifiedName~Runtime_ollama_processes_the_narrative_fallback_fixture',
        '--logger', 'console;verbosity=minimal') $ollamaSkip | Out-Null

    $sqlSkip = if ($SkipSqlServer) { 'Skipped by parameter.' }
    elseif (-not $env:CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING) { 'CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING is missing.' }
    else { $null }
    Invoke-SmokeStage 'SQL Server migration/session/confirm' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--filter', 'FullyQualifiedName~AIImportSqlServerTests',
        '--logger', 'console;verbosity=minimal') $sqlSkip | Out-Null

    $fullSkip = if ($SkipFullSuite) { 'Skipped by parameter.' } else { $null }
    Invoke-SmokeStage 'Full regression suite' @(
        'test', $testProject, '--no-build', '--no-restore',
        '--logger', 'console;verbosity=minimal') $fullSkip | Out-Null
}
finally {
    $env:CAFECHAIN_RUN_AIIMPORT_RUNTIME_SMOKE = $oldRuntime
    $env:CAFECHAIN_TESSERACT_PATH = $oldTesseract
    $env:CAFECHAIN_TESSDATA_PATH = $oldTessdata
    Set-Location -LiteralPath $repositoryRoot
}

$offlinePassed = ($stages | Where-Object Name -eq '126 fixtures - deterministic/offline').Status -eq 'PASSED'
$nativeStatus = ($stages | Where-Object Name -eq '20 PDF scan - native Tesseract').Status
$fixtureResults = foreach ($case in $manifest.cases) {
    $isScan = $case.file.StartsWith('04_PDF_SCAN/', [StringComparison]::Ordinal)
    [pscustomobject]@{
        File = $case.file
        Format = ($case.file -split '/')[0]
        OfflineStatus = if ($offlinePassed) { 'PASSED' } else { 'FAILED' }
        NativeStatus = if (-not $isScan) { 'NOT_APPLICABLE' } else { $nativeStatus }
        Classification = if ($case.nativeClassification) { $case.nativeClassification } else { 'PRODUCT_EXPECTATION' }
        ExpectedOutcome = $case.expectedOutcome
        NativeExpectedOutcome = $case.nativeExpectedOutcome
        Note = $case.nativeNote
    }
}
$failedStages = @($stages | Where-Object Status -eq 'FAILED').Count
$skippedStages = @($stages | Where-Object Status -eq 'SKIPPED').Count
$scopedStages = @($stages | Where-Object Name -ne 'Full regression suite')
$scopedFailures = @($scopedStages | Where-Object Status -eq 'FAILED').Count
$scopedSkips = @($scopedStages | Where-Object Status -eq 'SKIPPED').Count
$scopedStatus = if ($scopedFailures -gt 0) { 'FAILED' } elseif ($scopedSkips -gt 0) { 'PASSED_WITH_SKIPS' } else { 'PASSED' }
$report = [ordered]@{
    SchemaVersion = 2
    RunId = $runId
    StartedAtUtc = $startedAt.ToString('O')
    FinishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Status = if ($failedStages -gt 0) { 'FAILED' } elseif ($skippedStages -gt 0) { 'PASSED_WITH_SKIPS' } else { 'PASSED' }
    AIImportStatus = $scopedStatus
    UIRuntime = 'EXTERNAL_BROWSER_REQUIRED'
    TesseractVersion = $tesseractVersion
    OcrLanguages = 'vie+eng'
    OllamaModel = 'qwen3:4b'
    MigrationBaseline = '20260815152712_InitialCreate'
    ForwardMigration = '20260816170000_AddPreparedItemTargetStockLevel'
    Stages = $stages
    Fixtures = @($fixtureResults)
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# AI Smart Import Runtime Smoke Report')
$lines.Add('')
$lines.Add(('- Run: `{0}`' -f $runId))
$lines.Add("- Status: **$($report.Status)**")
$lines.Add("- AI Import scoped status: **$($report.AIImportStatus)**")
$lines.Add("- Fixture: $(@($manifest.cases).Count) (Excel 43, DOC/DOCX 33, PDF text 30, PDF scan 20)")
$tesseractLabel = if ($tesseractVersion) { $tesseractVersion } else { 'not executed' }
$lines.Add(('- Tesseract: {0} | `vie+eng` | `--oem 1 --psm 3`' -f $tesseractLabel))
$lines.Add('- Ollama smoke model: `qwen3:4b` (test process only)')
$lines.Add('- Migration: `20260815152712_InitialCreate` -> `20260816170000_AddPreparedItemTargetStockLevel`')
$lines.Add('')
$lines.Add('## Stage results')
$lines.Add('')
$lines.Add('| Stage | Status | Duration (ms) |')
$lines.Add('|---|---:|---:|')
foreach ($stage in $stages) { $lines.Add("| $($stage.Name) | $($stage.Status) | $($stage.DurationMs) |") }
$lines.Add('')
$lines.Add('## Confirmed blocker/limitation')
$lines.Add('')
$lines.Add('- `S19_scan_unknown_extra_columns.pdf`: `tessdata_fast vie+eng` with PSM 3 at DPI 200 yields one word. The pipeline returns typed layout failure and never infers an unknown header without evidence.')
$lines.Add('- The SQL stage requires `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING`; it uses a unique GUID database and deletes it during teardown.')
$lines.Add('- Rendered UI journeys require an external Browser session. This PowerShell runner covers view/JavaScript contracts but does not claim click/screenshot evidence.')
$sqlStage = $stages | Where-Object Name -eq 'SQL Server migration/session/confirm'
if ($sqlStage.Status -eq 'FAILED') {
    $lines.Add(('- SQL stage blocker: `{0}`.' -f $sqlStage.Detail))
}
$fullStage = $stages | Where-Object Name -eq 'Full regression suite'
if ($fullStage.Status -eq 'FAILED') {
    $lines.Add(('- Full-suite failures outside the scoped AI Import matrix remain visible as regression debt: `{0}`.' -f $fullStage.Detail))
}
$lines.Add('')
$lines.Add('This report contains no document content, OCR text, secret, connection string, or temporary path.')
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8

Write-Host "Runtime smoke completed: $($report.Status)."
if ($failedStages -gt 0) { exit 1 }
