# Checks the installkit command line tool against the MSIs the QA job just built. Reading an MSI needs Windows.
#
#   ./qa/tests/Invoke-ToolChecks.ps1 -V1 <dir with 1.0.0 build> -V2 <dir with 1.1.0 build>
param(
    [Parameter(Mandatory)] [string] $V1,
    [Parameter(Mandatory)] [string] $V2
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\..\src\InstallKit.Tool'

dotnet build $project -c Release | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Building the installkit tool failed.' }

function Invoke-Tool {
    $output = & dotnet run --project $project -c Release --no-build -- @args 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "installkit $($args -join ' ') failed with exit code ${LASTEXITCODE}:`n$output" }
    return $output
}

function Assert-That([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
    Write-Host "ok: $Message"
}

$msi1 = Join-Path (Resolve-Path $V1).Path 'QaDemo.msi'
$msi2 = Join-Path (Resolve-Path $V2).Path 'QaDemo.msi'

$guid = (Invoke-Tool new-guid).Trim()
$parsed = [guid]::Empty
Assert-That ([guid]::TryParse($guid, [ref]$parsed) -and $parsed -ne [guid]::Empty) 'new-guid prints a GUID'

$summary = Invoke-Tool inspect $msi1 --json | ConvertFrom-Json
Assert-That ($summary.ProductName -eq 'InstallKit QA Demo') 'inspect reads the product name'
Assert-That ($summary.Version -eq '1.0.0.0') 'inspect reads the version'
Assert-That ($summary.UpgradeCode -eq '7c3f0d52-8a41-4e6b-b1d9-2f54a8c3e917' -or $summary.UpgradeCode -eq '{7C3F0D52-8A41-4E6B-B1D9-2F54A8C3E917}') 'inspect reads the upgrade code'
Assert-That ($summary.Manufacturer -eq 'InstallKit') 'inspect reads the manufacturer'
Assert-That (@($summary.Files | Where-Object { $_.Path -like '*QA Demo\QaDemo.exe' }).Count -eq 1) 'inspect lists the main executable'
Assert-That (@($summary.Files | Where-Object { $_.Path -like '*QA Demo\worker\QaDemo.exe' }).Count -eq 1) 'inspect lists the file of an optional feature in its folder'
Assert-That (@($summary.Files | Where-Object { $_.Path -like '*docs*readme.txt' }).Count -eq 1) 'inspect lists a file in a subfolder'
Assert-That (@($summary.Features).Count -ge 3) 'inspect lists the features'
Assert-That (@($summary.Services | Where-Object { $_ -like 'InstallKitQaDemo*' }).Count -eq 1) 'inspect lists the service'
Assert-That (@($summary.RegistryValues | Where-Object { $_ -like '*QA Demo\InstallPath = [[]INSTALLDIR]*' }).Count -eq 1) 'inspect lists registry values'

$text = Invoke-Tool inspect $msi1
Assert-That ($text -match 'InstallKit QA Demo 1\.0\.0\.0') 'inspect prints a readable summary'

$diff = Invoke-Tool diff $msi1 $msi2
Assert-That ($diff -match '~ version: 1\.0\.0\.0 -> 1\.1\.0\.0') 'diff reports the version change'
Assert-That ($diff -notmatch 'upgrade code') 'diff does not report the unchanged upgrade code'

$same = Invoke-Tool diff $msi1 $msi1
Assert-That ($same.Trim() -eq 'No differences.') 'diff of a file with itself reports no differences'

# The prerequisite bundle is only built, never installed; it must exist next to the MSI the QA job built.
Assert-That (Test-Path (Join-Path (Resolve-Path $V1).Path 'QaPrerequisitesSetup.exe')) 'the bundle with prerequisite helpers was built'
