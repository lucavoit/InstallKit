# Builds a "consumer" installer the way a user without a monorepo would:
#   - outside this repository (so none of its Directory.Build.props, solution or project references apply)
#   - from the packed NuGet packages only (SDK, engines, generators, template), via a local feed
#   - with the payload taken from a folder that another pipeline produced, passed as a path
#
#   ./qa/consumer/Build-Consumer.ps1 -PackagesDir artifacts/packages -WorkDir $env:RUNNER_TEMP/consumer -Engine WixSharp
#
# Output: <WorkDir>/out/ConsumerDemo.msi and ConsumerDemoSetup.exe, version 1.2.3.
param(
    [Parameter(Mandatory)] [string] $PackagesDir,   # folder with the InstallKit .nupkg files
    [Parameter(Mandatory)] [string] $WorkDir,       # must be outside the repository
    [ValidateSet('WixSharp', 'WixSharpCore')] [string] $Engine = 'WixSharp',
    [string] $WixVersion = '6.0.2'
)

$ErrorActionPreference = 'Stop'
function Run([string] $What, [scriptblock] $Block) {
    Write-Host "::group::$What"
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE" }
    Write-Host '::endgroup::'
}

$packages = (Resolve-Path $PackagesDir).Path
if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$work = (Resolve-Path $WorkDir).Path

# The SDK package name carries the version the packages were built with.
$sdkPackage = Get-ChildItem $packages -Filter 'InstallKit.Sdk.*.nupkg' | Select-Object -First 1
if (-not $sdkPackage) { throw "No InstallKit.Sdk package in $packages" }
$sdkVersion = $sdkPackage.BaseName.Substring('InstallKit.Sdk.'.Length)
$templatePackage = Get-ChildItem $packages -Filter 'InstallKit.Templates.*.nupkg' | Select-Object -First 1
Write-Host "Packed InstallKit version: $sdkVersion"

# 1. The payload: what another pipeline would have published. Not a project, just files.
$payload = Join-Path $work 'external-publish'
New-Item -ItemType Directory -Force (Join-Path $payload 'data') | Out-Null
Copy-Item (Join-Path $env:windir 'System32\whoami.exe') (Join-Path $payload 'app.exe')
Set-Content (Join-Path $payload 'readme.txt') 'Produced by another pipeline.'
Set-Content (Join-Path $payload 'data\info.json') '{ "origin": "built-elsewhere" }'

# 2. A solution folder with its own NuGet config (local feed first) and a pinned wix tool.
$solution = Join-Path $work 'solution'
New-Item -ItemType Directory -Force $solution | Out-Null
Set-Content (Join-Path $solution 'nuget.config') @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="installkit-local" value="$packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
Push-Location $solution
try {
    Run 'Pin the wix tool' { dotnet new tool-manifest | Out-Null; dotnet tool install wix --version $WixVersion }

    # 3. The project comes from the packed template, as in the README.
    Run 'Install the template package' { dotnet new install $templatePackage.FullName }
    Run 'Generate the installer project' {
        dotnet new installkit -n Consumer.Installer --product 'Consumer Demo' --manufacturer Consumer `
            --payload '..\Dummy\Dummy.csproj' --engine $Engine --sdkVersion $sdkVersion --bundle
    }

    # The template points at a payload project; a consumer with prebuilt files drops that line
    # and describes the files with WithDirectory instead.
    $project = Join-Path $solution 'Consumer.Installer\Consumer.Installer.csproj'
    (Get-Content $project -Raw) -replace '(?s)\s*<ItemGroup>\s*<InstallerPayload[^>]*/>\s*</ItemGroup>', '' | Set-Content $project

    Set-Content (Join-Path $solution 'Consumer.Installer\Program.cs') @'
using InstallKit;

// The pipeline that built the application tells the installer where the files are.
var payloadDir = System.Environment.GetEnvironmentVariable("CONSUMER_PAYLOAD_DIR")
    ?? throw new System.InvalidOperationException("Set CONSUMER_PAYLOAD_DIR to the folder with the published application.");

return InstallerHost.Run(args, plan =>
{
    var app = plan.Msi(MsiBuilder.Create("Consumer Demo")
        .WithManufacturer("Consumer")
        .WithUpgradeCode("4b8e1f20-6d37-4c95-a1e4-9c2d70f35b18")
        .WithOutputFileName("ConsumerDemo")
        .WithDirectory(payloadDir)
        .WithMainExecutable("app.exe"));

    plan.Bundle(BundleBuilder.Create("Consumer Demo")
        .WithManufacturer("Consumer")
        .WithUpgradeCode("d3a79c15-82be-4f06-b5d1-7e0a4c96f2b3")
        .WithOutputFileName("ConsumerDemoSetup")
        .WithMsi(app));
});
'@

    # 4. Build from the packages only.
    $env:CONSUMER_PAYLOAD_DIR = $payload
    $out = Join-Path $work 'out'
    Run 'Build the consumer installer' {
        dotnet build $project -c Release -p:InstallKitVersion=1.2.3 "-p:InstallKitOutputDir=$out\"
    }
}
finally {
    Pop-Location
}
