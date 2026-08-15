#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$modelDirectory = Join-Path $repositoryRoot 'CafeChain\Resources\OCR\tessdata'
$tesseract = Get-Command 'tesseract' -ErrorAction SilentlyContinue

if (-not $tesseract) {
    Write-Error @'
Không tìm thấy Tesseract trên PATH.
Cài Tesseract cho Windows (không chạy tự động, có thể cần quyền quản trị):
  winget install --id UB-Mannheim.TesseractOCR --exact
Sau đó mở terminal mới và chạy lại script này.
'@
}

$models = @(
    @{
        Name = 'vie'
        Uri = 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/vie.traineddata'
        Sha256 = '79DF64CAF7BCFB2A27DF5042ECB6121E196EADA34DA774956995747636D5BFA1'
    },
    @{
        Name = 'eng'
        Uri = 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/eng.traineddata'
        Sha256 = '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'
    }
)

New-Item -ItemType Directory -Path $modelDirectory -Force | Out-Null
foreach ($model in $models) {
    $target = Join-Path $modelDirectory ($model.Name + '.traineddata')
    $download = $target + '.download'
    $isValid = (Test-Path -LiteralPath $target) -and
        ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $model.Sha256)
    if ($isValid -and -not $Force) {
        Write-Host "Model $($model.Name) đã tồn tại và checksum hợp lệ."
        continue
    }

    try {
        Invoke-WebRequest -Uri $model.Uri -OutFile $download -UseBasicParsing
        $actual = (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash
        if ($actual -ne $model.Sha256) {
            throw "Checksum không khớp cho model $($model.Name)."
        }
        Move-Item -LiteralPath $download -Destination $target -Force
        Write-Host "Đã cài model $($model.Name) từ tessdata_fast 4.1.0."
    }
    finally {
        if (Test-Path -LiteralPath $download) {
            Remove-Item -LiteralPath $download -Force
        }
    }
}

$versionOutput = & $tesseract.Source --version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Tesseract executable không chạy được. Hãy kiểm tra Visual C++ Runtime và PATH.'
}
$languages = & $tesseract.Source --tessdata-dir $modelDirectory --list-langs 2>&1
if ($LASTEXITCODE -ne 0 -or $languages -notcontains 'vie' -or $languages -notcontains 'eng') {
    throw 'Smoke check thất bại: Tesseract chưa tải được đủ model vie và eng.'
}

Write-Host ($versionOutput | Select-Object -First 1)
Write-Host 'READY: Tesseract local và model vie+eng đã sẵn sàng.'
