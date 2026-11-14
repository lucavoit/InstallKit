# Consumer QA: an installer project that lives outside this repository, built only from the packed
# NuGet packages (SDK, engine, template), with the payload taken from a folder another pipeline made.
# This is what a user without a monorepo does. It installs and removes software, so only run it on a
# disposable Windows machine, elevated.

param(
    [Parameter(Mandatory)] [string] $OutDir,      # folder the installer project built into
    [Parameter(Mandatory)] [string] $LogDir
)

BeforeAll {
    $ErrorActionPreference = 'Stop'

    $script:ProductName = 'Consumer Demo'
    $script:InstallDir  = Join-Path $env:ProgramFiles 'Consumer\Consumer Demo'
    $script:StartMenu   = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Consumer\Consumer Demo'
    $script:UninstallKeys = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'

    New-Item -ItemType Directory -Force $LogDir | Out-Null

    function Invoke-Msi([string] $Arguments, [string] $LogName) {
        $log = Join-Path $LogDir "$LogName.log"
        $p = Start-Process msiexec.exe -ArgumentList "$Arguments /qn /norestart /l*v `"$log`"" -Wait -PassThru
        if ($p.ExitCode -notin 0, 3010) {
            Get-Content $log -Tail 80 | Write-Host
            throw "msiexec $Arguments failed with exit code $($p.ExitCode)"
        }
    }

    function Get-Entries { @(Get-ChildItem $UninstallKeys -ErrorAction SilentlyContinue | Get-ItemProperty | Where-Object { $_.DisplayName -eq $ProductName -and $_.WindowsInstaller -eq 1 }) }

    foreach ($entry in Get-Entries) { Invoke-Msi "/x $($entry.PSChildName)" 'consumer-cleanup' }
}

Describe 'Installer built from packed NuGet packages and an external payload folder' {
    It 'produced an MSI and a setup bundle' {
        Join-Path $OutDir 'ConsumerDemo.msi' | Should -Exist
        Join-Path $OutDir 'ConsumerDemoSetup.exe' | Should -Exist
    }

    Context 'Installed' {
        BeforeAll { Invoke-Msi "/i `"$(Join-Path $OutDir 'ConsumerDemo.msi')`"" 'consumer-install' }

        It 'registers the product at the requested version' {
            $entries = Get-Entries
            $entries | Should -HaveCount 1
            $entries[0].DisplayVersion | Should -Be '1.2.3.0'
        }

        It 'installs every file of the external folder, keeping its structure' {
            Join-Path $InstallDir 'app.exe' | Should -Exist
            Join-Path $InstallDir 'readme.txt' | Should -Exist
            Join-Path $InstallDir 'data\info.json' | Should -Exist
            Get-Content (Join-Path $InstallDir 'data\info.json') -Raw | Should -Match 'built-elsewhere'
        }

        It 'creates the Start menu shortcut for the main executable' {
            Join-Path $StartMenu 'Consumer Demo.lnk' | Should -Exist
        }
    }

    Context 'Uninstalled' {
        BeforeAll { Invoke-Msi "/x $((Get-Entries)[0].PSChildName)" 'consumer-uninstall' }

        It 'removes the files, shortcut and Add/Remove entry' {
            Get-Entries | Should -HaveCount 0
            Join-Path $InstallDir 'app.exe' | Should -Not -Exist
            $StartMenu | Should -Not -Exist
        }
    }
}
