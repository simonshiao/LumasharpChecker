param([string]$OutputRoot)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Use Unicode code points so Windows PowerShell 5.1 handles the folder name.
$saveFolder = [string][char]0x5B58 + [string][char]0x5716
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Join-Path $PSScriptRoot '_output') $saveFolder
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not [System.IO.Directory]::Exists([System.IO.Path]::GetPathRoot($OutputRoot))) {
    throw "Target drive is unavailable: $OutputRoot"
}

# Batch, date, combined date/batch, and day-only directories.
$lots = @('LOT-A1001', '20261025', '2026-10-28_LOT-B2002', '31')
$created = 0
$preserved = 0
foreach ($lot in $lots) {
    foreach ($kind in @('OK', 'NG')) {
        for ($station = 1; $station -le 8; $station++) {
            $folder = Join-Path (Join-Path (Join-Path (Join-Path $OutputRoot '2026') '10') $lot) $kind
            $folder = Join-Path $folder ([string]$station)
            [System.IO.Directory]::CreateDirectory($folder) | Out-Null
            $name = '{0}_20261030120000_St{1:D2}_{2}_001.png' -f $lot, $station, $kind
            $file = Join-Path $folder $name
            if (Test-Path -LiteralPath $file) {
                $preserved++
                continue
            }

            $bitmap = New-Object System.Drawing.Bitmap(320, 240)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $font = New-Object System.Drawing.Font('Arial', 12)
            $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
            try {
                if ($kind -eq 'OK') {
                    $graphics.Clear([System.Drawing.Color]::FromArgb(72, 135, 90))
                } else {
                    $graphics.Clear([System.Drawing.Color]::FromArgb(146, 64, 60))
                }
                $graphics.DrawString(('TEST ONLY / ' + $kind), $font, $brush, 10, 18)
                $graphics.DrawString(('LOT: ' + $lot), $font, $brush, 10, 65)
                $graphics.DrawString(('STATION: ' + $station), $font, $brush, 10, 111)
                $graphics.DrawString('2026/10 - SYNTHETIC', $font, $brush, 10, 157)
                $bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
                $created++
            } finally {
                $brush.Dispose()
                $font.Dispose()
                $graphics.Dispose()
                $bitmap.Dispose()
            }
        }
    }
}

$monthFolder = Join-Path (Join-Path $OutputRoot '2026') '10'
$files = @(Get-ChildItem -LiteralPath $monthFolder -Recurse -File -Filter '*.png')
Write-Output "FIXTURE_ROOT=$OutputRoot"
Write-Output "BATCH_OR_DATE_FOLDERS=$($lots.Count)"
Write-Output "NEW_IMAGES=$created"
Write-Output "PRESERVED_EXISTING=$preserved"
Write-Output "TOTAL_PNG=$($files.Count)"
