# Runs the QA installer tests with Pester 5 and writes NUnit XML for Azure DevOps.
# Must run elevated on a disposable Windows machine: it installs and removes software and a service.
param(
    [Parameter(Mandatory)] [string] $V1,
    [Parameter(Mandatory)] [string] $V2,
    [string] $LogDir = (Join-Path ([IO.Path]::GetTempPath()) 'installkit-qa-logs'),
    [string] $ResultFile = (Join-Path ([IO.Path]::GetTempPath()) 'installkit-qa-results.xml')
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The QA tests install software: run them elevated.'
}

if (-not (Get-Module -ListAvailable Pester | Where-Object Version -ge '5.5.0')) {
    Install-Module Pester -MinimumVersion 5.5.0 -Force -Scope CurrentUser -SkipPublisherCheck
}
Import-Module Pester -MinimumVersion 5.5.0

$container = New-PesterContainer -Path (Join-Path $PSScriptRoot 'Installer.Tests.ps1') -Data @{
    V1     = (Resolve-Path $V1).Path
    V2     = (Resolve-Path $V2).Path
    LogDir = $LogDir
}

$config = New-PesterConfiguration
$config.Run.Container = $container
$config.Run.Exit = $true
$config.Output.Verbosity = 'Detailed'
$config.TestResult.Enabled = $true
$config.TestResult.OutputFormat = 'NUnitXml'
$config.TestResult.OutputPath = $ResultFile

Invoke-Pester -Configuration $config
