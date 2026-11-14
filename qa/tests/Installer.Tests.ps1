# End-to-end QA for the InstallKit QA demo installer (qa/QaDemo.Installer).
# Installs, upgrades, repairs and uninstalls on the real machine, so only run it on a disposable agent.
#
#   ./qa/tests/Invoke-Qa.ps1 -V1 <dir with 1.0.0 build> -V2 <dir with 1.1.0 build>
#
# The Describe blocks run in order and build on each other: install 1.0.0, upgrade to 1.1.0,
# repair, uninstall, then the same product through the setup.exe bundle.

param(
    [Parameter(Mandatory)] [string] $V1,      # folder with QaDemo.msi built as 1.0.0
    [Parameter(Mandatory)] [string] $V2,      # folder with QaDemo.msi and QaDemoSetup.exe built as 1.1.0
    [Parameter(Mandatory)] [string] $LogDir
)

BeforeAll {
    $ErrorActionPreference = 'Stop'

    $script:ProductName = 'InstallKit QA Demo'
    $script:ServiceName = 'InstallKitQaDemo'
    $script:InstallDir  = Join-Path $env:ProgramFiles 'InstallKit\InstallKit QA Demo'
    $script:StartMenu   = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'InstallKit\InstallKit QA Demo'
    $script:DesktopLink = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'InstallKit QA Demo.lnk'
    $script:Evidence    = Join-Path $env:ProgramData 'InstallKitQa'
    # The bundle's engine is 32-bit, so its entry lives in the WOW6432Node view; look in both.
    $script:UninstallKeys = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'

    New-Item -ItemType Directory -Force $LogDir | Out-Null

    function Invoke-Msi([string] $Arguments, [string] $LogName) {
        $log = Join-Path $LogDir "$LogName.log"
        $p = Start-Process msiexec.exe -ArgumentList "$Arguments /qn /norestart /l*v `"$log`"" -Wait -PassThru
        if ($p.ExitCode -notin 0, 3010) {
            Get-Content $log -Tail 80 | Write-Host
            throw "msiexec $Arguments failed with exit code $($p.ExitCode) (log: $log)"
        }
    }

    function Invoke-Bundle([string] $Exe, [string] $Arguments, [string] $LogName) {
        $log = Join-Path $LogDir "$LogName.log"
        $p = Start-Process $Exe -ArgumentList "$Arguments /quiet /norestart /log `"$log`"" -Wait -PassThru
        if ($p.ExitCode -notin 0, 3010) {
            Get-ChildItem $LogDir -Filter "$LogName*" | ForEach-Object { Get-Content $_.FullName -Tail 80 | Write-Host }
            throw "$Exe $Arguments failed with exit code $($p.ExitCode)"
        }
    }

    # Add/Remove Programs entries for the MSI itself (WindowsInstaller = 1) and for the bundle.
    function Get-Entries { @(Get-ChildItem $UninstallKeys -ErrorAction SilentlyContinue | Get-ItemProperty | Where-Object { $_.DisplayName -eq $ProductName }) }
    function Get-MsiEntries    { @(Get-Entries | Where-Object { $_.WindowsInstaller -eq 1 }) }
    function Get-BundleEntries { @(Get-Entries | Where-Object { $_.WindowsInstaller -ne 1 }) }

    function Wait-Heartbeat {
        $file = Join-Path $InstallDir 'heartbeat.txt'
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline) {
            if ((Test-Path $file) -and (Get-Item $file).LastWriteTime -gt (Get-Date).AddSeconds(-15)) { return $true }
            Start-Sleep -Seconds 1
        }
        return $false
    }

    function Assert-Removed {
        Get-Service $ServiceName -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        Get-MsiEntries | Should -HaveCount 0
        Join-Path $InstallDir 'QaDemo.exe' | Should -Not -Exist
        Join-Path $InstallDir 'config' | Should -Not -Exist
        $StartMenu | Should -Not -Exist
        $DesktopLink | Should -Not -Exist
    }

    # Start from a clean machine state (a previous failed attempt on the same agent, for example).
    foreach ($entry in Get-MsiEntries) { Invoke-Msi "/x $($entry.PSChildName)" 'cleanup' }
    Remove-Item $Evidence -Recurse -Force -ErrorAction SilentlyContinue
}

Describe 'Fresh install of 1.0.0 with MODE=server' {
    BeforeAll { Invoke-Msi "/i `"$(Join-Path $V1 'QaDemo.msi')`" MODE=server" '1-install-1.0.0' }

    It 'registers exactly one product in Add/Remove Programs' {
        $entries = Get-MsiEntries
        $entries | Should -HaveCount 1
        $entries[0].DisplayVersion | Should -Be '1.0.0.0'
        $entries[0].Publisher | Should -Be 'InstallKit'
    }

    It 'copies the payload into Program Files' {
        Join-Path $InstallDir 'QaDemo.exe' | Should -Exist
    }

    It 'copies the extra directory into config\' {
        Join-Path $InstallDir 'config\appsettings.qa.json' | Should -Exist
    }

    It 'creates the Start menu shortcuts' {
        Join-Path $StartMenu 'InstallKit QA Demo.lnk' | Should -Exist
        Join-Path $StartMenu 'Uninstall InstallKit QA Demo.lnk' | Should -Exist
    }

    It 'creates the desktop shortcut' {
        $DesktopLink | Should -Exist
    }

    It 'installs the service as automatic and starts it' {
        $service = Get-Service $ServiceName
        $service.StartType | Should -Be 'Automatic'
        $service.Status | Should -Be 'Running'
    }

    It 'runs the installed service (it writes a heartbeat)' {
        Wait-Heartbeat | Should -BeTrue
    }

    It 'runs the OnAfterInstall C# handler, which reads the MODE property' {
        Get-Content (Join-Path $InstallDir 'mode.txt') -Raw | Should -Be 'server'
    }

    It 'runs RunAfterInstall with the formatted MODE argument' {
        Get-Content (Join-Path $InstallDir 'init.txt') -Raw | Should -Be 'server'
    }
}

Describe 'Major upgrade to 1.1.0 without MODE' {
    BeforeAll { Invoke-Msi "/i `"$(Join-Path $V2 'QaDemo.msi')`"" '2-upgrade-1.1.0' }

    It 'replaces 1.0.0 instead of installing side by side' {
        $entries = Get-MsiEntries
        $entries | Should -HaveCount 1
        $entries[0].DisplayVersion | Should -Be '1.1.0.0'
    }

    It 'does not run RunBeforeUninstall during an upgrade' {
        Join-Path $Evidence 'unregistered.txt' | Should -Not -Exist
    }

    It 'uses the default MODE when none is given' {
        Get-Content (Join-Path $InstallDir 'mode.txt') -Raw | Should -Be 'client'
    }

    It 'keeps the service running' {
        (Get-Service $ServiceName).Status | Should -Be 'Running'
        Wait-Heartbeat | Should -BeTrue
    }
}

Describe 'Repair' {
    BeforeAll {
        Remove-Item (Join-Path $InstallDir 'config\appsettings.qa.json') -Force
        Invoke-Msi "/fa $((Get-MsiEntries)[0].PSChildName)" '3-repair'
    }

    It 'restores a deleted file' {
        Join-Path $InstallDir 'config\appsettings.qa.json' | Should -Exist
    }
}

Describe 'Uninstall' {
    BeforeAll { Invoke-Msi "/x $((Get-MsiEntries)[0].PSChildName)" '4-uninstall' }

    It 'runs RunBeforeUninstall on a real uninstall' {
        Join-Path $Evidence 'unregistered.txt' | Should -Exist
    }

    It 'removes the service, files, shortcuts and Add/Remove Programs entry' {
        Assert-Removed
    }
}

Describe 'Bundle (QaDemoSetup.exe) install and uninstall' {
    BeforeAll {
        Remove-Item $Evidence -Recurse -Force -ErrorAction SilentlyContinue
        $script:Setup = Join-Path $V2 'QaDemoSetup.exe'
        Invoke-Bundle $Setup '' '5-bundle-install'
    }

    It 'registers the bundle in Add/Remove Programs' {
        $bundles = Get-BundleEntries
        $bundles | Should -HaveCount 1
        $bundles[0].DisplayVersion | Should -Be '1.1.0.0'
    }

    It 'installs the chained MSI' {
        Get-MsiEntries | Should -HaveCount 1
        (Get-Service $ServiceName).Status | Should -Be 'Running'
    }

    It 'uninstalls everything through the bundle' {
        Invoke-Bundle $Setup '/uninstall' '6-bundle-uninstall'
        Get-BundleEntries | Should -HaveCount 0
        Assert-Removed
    }
}
