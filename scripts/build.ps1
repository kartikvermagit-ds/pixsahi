[CmdletBinding()]
param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot 'dist'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler was not found.' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$exe = Join-Path $dist 'PixSahi.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$exe" "/win32manifest:$repoRoot\src\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "$repoRoot\src\Imaging.cs" "$repoRoot\src\PixSahi.cs" "$repoRoot\src\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $exe"
$qa = $null
if ($Test) {
    $qa = Join-Path $repoRoot ('artifacts\qa-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $qa | Out-Null
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.Arguments = '--self-test "' + $qa + '"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Tests exceeded 60 seconds.' }
        if ($process.ExitCode -ne 0 -or (Test-Path (Join-Path $qa 'FAILED.txt')) -or -not (Test-Path (Join-Path $qa 'PASSED.txt'))) {
            if (Test-Path (Join-Path $qa 'FAILED.txt')) { Get-Content (Join-Path $qa 'FAILED.txt') }
            throw "Tests failed. Inspect $qa"
        }
        Get-Content (Join-Path $qa 'PASSED.txt')
    } finally { $process.Dispose() }
}
if ($Package) {
    $stage = Join-Path $repoRoot ('artifacts\package-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item -LiteralPath $exe -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\START-HERE.txt') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stage
    if ($qa) { Copy-Item -LiteralPath (Join-Path $qa 'PASSED.txt') -Destination (Join-Path $stage 'Validation.txt') }
    $zip = Join-Path $dist 'PixSahi-Windows.zip'
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $hashes = foreach ($asset in @($exe, $zip)) {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $asset)
    }
    $hashes | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
    Write-Output "Packaged $zip"
}
