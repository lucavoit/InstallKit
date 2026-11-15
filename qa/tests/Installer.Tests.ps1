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
    [string] $V1Patch = '',                   # optional: folder with a 1.0.1 build, for the side-by-side patch test
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
    $script:BinDir      = Join-Path $script:InstallDir 'bin'
    $script:Classes     = 'HKLM:\SOFTWARE\Classes'
    $script:RegKey      = 'HKLM:\SOFTWARE\InstallKit\QA Demo'
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

    function Get-SideEntries { @(Get-ChildItem $UninstallKeys -ErrorAction SilentlyContinue | Get-ItemProperty | Where-Object { $_.DisplayName -like 'InstallKit QA Side *' -and $_.WindowsInstaller -eq 1 }) }
    function Get-SideDir([string] $Line) { Join-Path $env:ProgramFiles "InstallKit\InstallKit QA Side $Line" }

    function Get-MachinePathEntries { @([Environment]::GetEnvironmentVariable('Path', 'Machine') -split ';' | ForEach-Object { $_.TrimEnd('\') }) }

    # The allow rules of a folder for an identity such as '*\Users'.
    function Get-FolderRules([string] $Path, [string] $Identity) {
        @((Get-Acl $Path).Access | Where-Object { $_.AccessControlType -eq 'Allow' -and $_.IdentityReference.Value -like $Identity })
    }

    function Test-Right($Rule, [string] $Right) {
        $flag = [Security.AccessControl.FileSystemRights]$Right
        ($Rule.FileSystemRights -band $flag) -eq $flag
    }

    function Assert-Removed {
        Get-Service $ServiceName -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        Get-MsiEntries | Should -HaveCount 0
        Join-Path $InstallDir 'QaDemo.exe' | Should -Not -Exist
        Join-Path $InstallDir 'config' | Should -Not -Exist
        $StartMenu | Should -Not -Exist
        $DesktopLink | Should -Not -Exist
        $RegKey | Should -Not -Exist
        Join-Path $InstallDir 'docs' | Should -Not -Exist
        Join-Path $InstallDir 'samples' | Should -Not -Exist
        Join-Path $InstallDir 'logs' | Should -Not -Exist
        Get-MachinePathEntries | Should -Not -Contain $BinDir
        Get-NetFirewallRule -DisplayName 'InstallKit QA Demo*' -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        "$Classes\.ikqa" | Should -Not -Exist
        "$Classes\QaDemo.ikqa" | Should -Not -Exist
        "$Classes\installkitqa" | Should -Not -Exist
        [Environment]::GetEnvironmentVariable('INSTALLKIT_QA_HOME', 'Machine') | Should -BeNullOrEmpty
    }

    foreach ($entry in Get-SideEntries) { Invoke-Msi "/x $($entry.PSChildName)" 'cleanup-side' }

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

    It 'writes the registry values, expanding [INSTALLDIR]' {
        $key = Get-Item $RegKey
        $key.GetValue('InstallPath') | Should -Be ($InstallDir + '\')
        $key.GetValue('') | Should -Be 'InstallKit QA Demo'
        $key.GetValue('Telemetry') | Should -Be 0
        $key.GetValueKind('Telemetry') | Should -Be 'DWord'
        $key.GetValueKind('LogDir') | Should -Be 'ExpandString'
        $key.GetValue('LogDir') | Should -Be (Join-Path $env:ProgramData 'InstallKitQa')
    }

    It 'writes the MODE property into config\qa.config and leaves other settings alone' {
        $config = [xml](Get-Content (Join-Path $InstallDir 'config\qa.config') -Raw)
        ($config.configuration.appSettings.add | Where-Object key -eq 'Server').value | Should -Be 'server'
        ($config.configuration.appSettings.add | Where-Object key -eq 'Other').value | Should -Be 'untouched'
    }

    It 'installs the default-on feature and leaves the default-off feature out' {
        Join-Path $InstallDir 'docs\readme.txt' | Should -Exist
        Join-Path $InstallDir 'samples' | Should -Not -Exist
    }

    It 'gives Authenticated Users Modify on the install folder and keeps the administrators' {
        $rules = Get-FolderRules $InstallDir '*Authenticated Users'
        $rules | Should -Not -BeNullOrEmpty
        Test-Right $rules[0] 'Modify' | Should -BeTrue
        $admins = Get-FolderRules $InstallDir '*\Administrators'
        $admins | Should -Not -BeNullOrEmpty
        Test-Right $admins[0] 'FullControl' | Should -BeTrue
    }

    It 'does not change the permissions of Program Files or the manufacturer folder' {
        Get-FolderRules $env:ProgramFiles '*Authenticated Users' | Should -BeNullOrEmpty
        Get-FolderRules (Split-Path $InstallDir) '*Authenticated Users' | Should -BeNullOrEmpty
    }

    It 'creates the logs folder with Modify for Users' {
        $logs = Join-Path $InstallDir 'logs'
        $logs | Should -Exist
        $rules = Get-FolderRules $logs '*\Users'
        $rules | Should -Not -BeNullOrEmpty
        Test-Right $rules[0] 'Modify' | Should -BeTrue
    }

    It 'gives LocalService Read on the payload folder config' {
        $rules = Get-FolderRules (Join-Path $InstallDir 'config') '*\LOCAL SERVICE'
        $rules | Should -Not -BeNullOrEmpty
        Test-Right $rules[0] 'ReadAndExecute' | Should -BeTrue
        Test-Right $rules[0] 'Write' | Should -BeFalse
    }

    It 'registers the .ikqa file association with an open command' {
        (Get-Item "$Classes\.ikqa").GetValue('') | Should -Be 'QaDemo.ikqa'
        (Get-Item "$Classes\QaDemo.ikqa").GetValue('') | Should -Be 'InstallKit QA file'
        $command = Get-Item "$Classes\QaDemo.ikqa\shell\open\command"
        $command.GetValue('') | Should -Be ('"' + (Join-Path $InstallDir 'QaDemo.exe') + '" "%1"')
        $command.GetValueKind('') | Should -Be 'String'
    }

    It 'registers the installkitqa:// URL protocol' {
        $key = Get-Item "$Classes\installkitqa"
        $key.GetValue('') | Should -Be 'URL:InstallKit QA link'
        $key.GetValueNames() | Should -Contain 'URL Protocol'
        (Get-Item "$Classes\installkitqa\shell\open\command").GetValue('') | Should -Be ('"' + (Join-Path $InstallDir 'QaDemo.exe') + '" "%1"')
    }

    It 'creates an enabled inbound firewall rule for the port' {
        $rule = Get-NetFirewallRule -DisplayName 'InstallKit QA Demo port'
        $rule | Should -HaveCount 1
        $rule.Enabled | Should -Be 'True'
        $rule.Direction | Should -Be 'Inbound'
        $rule.Action | Should -Be 'Allow'
        $filter = $rule | Get-NetFirewallPortFilter
        $filter.Protocol | Should -Be 'TCP'
        $filter.LocalPort | Should -Be '48765'
    }

    It 'creates a firewall rule for the installed program, limited to the local subnet and private profile' {
        $rule = Get-NetFirewallRule -DisplayName 'InstallKit QA Demo program'
        $rule.Enabled | Should -Be 'True'
        $rule.Profile | Should -Be 'Private'
        ($rule | Get-NetFirewallApplicationFilter).Program | Should -Be (Join-Path $InstallDir 'QaDemo.exe')
        ($rule | Get-NetFirewallAddressFilter).RemoteAddress | Should -Be 'LocalSubnet'
    }

    It 'adds the install folder\bin to the machine PATH and sets the machine variable' {
        Get-MachinePathEntries | Should -Contain $BinDir
        [Environment]::GetEnvironmentVariable('INSTALLKIT_QA_HOME', 'Machine') | Should -Be ($InstallDir + '\')
    }
}

Describe 'Major upgrade to 1.1.0 without MODE' {
    BeforeAll {
        # A running program the installer is told to close: a console program under the name the MSI looks for.
        $script:CloseTarget = Join-Path $env:TEMP 'InstallKitQaClose.exe'
        Copy-Item (Join-Path $env:SystemRoot 'System32\ping.exe') $CloseTarget -Force
        $script:CloseProcess = Start-Process $CloseTarget -ArgumentList '-t', '127.0.0.1' -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 1
        $script:CloseWasRunning = -not $CloseProcess.HasExited

        Invoke-Msi "/i `"$(Join-Path $V2 'QaDemo.msi')`"" '2-upgrade-1.1.0'
    }

    AfterAll {
        Get-Process -Name 'InstallKitQaClose' -ErrorAction SilentlyContinue | Stop-Process -Force
        Remove-Item $CloseTarget -Force -ErrorAction SilentlyContinue
    }

    It 'closes the program named in WithCloseApplication' {
        $CloseWasRunning | Should -BeTrue
        $CloseProcess.WaitForExit(15000) | Should -BeTrue
    }

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

Describe 'Changing features of the installed product' {
    It 'adds an optional feature with ADDLOCAL' {
        Invoke-Msi "/i $((Get-MsiEntries)[0].PSChildName) ADDLOCAL=Samples" '3b-add-feature'
        Join-Path $InstallDir 'samples\sample.txt' | Should -Exist
        Join-Path $InstallDir 'docs\readme.txt' | Should -Exist
    }

    It 'removes a feature with REMOVE and keeps the rest' {
        Invoke-Msi "/i $((Get-MsiEntries)[0].PSChildName) REMOVE=Docs" '3c-remove-feature'
        Join-Path $InstallDir 'docs' | Should -Not -Exist
        Join-Path $InstallDir 'samples\sample.txt' | Should -Exist
        Join-Path $InstallDir 'QaDemo.exe' | Should -Exist
    }
}

Describe 'Versions of a side-by-side product' {
    It 'installs 1.0.0 as the line 1.0 with its own folder and service' {
        Invoke-Msi "/i `"$(Join-Path $V1 'QaSide.msi')`"" '5a-side-1.0.0'

        $entries = Get-SideEntries
        $entries | Should -HaveCount 1
        $entries[0].DisplayName | Should -Be 'InstallKit QA Side 1.0'
        $entries[0].DisplayVersion | Should -Be '1.0.0.0'
        Join-Path (Get-SideDir '1.0') 'QaDemo.exe' | Should -Exist
        Join-Path (Get-SideDir '1.0') 'config\qa.config' | Should -Exist
        Get-Service 'InstallKitQaSide-1.0' | Should -Not -BeNullOrEmpty
    }

    It 'installs 1.1.0 next to it instead of replacing it' {
        Invoke-Msi "/i `"$(Join-Path $V2 'QaSide.msi')`"" '5b-side-1.1.0'

        $entries = Get-SideEntries | Sort-Object DisplayName
        $entries | Should -HaveCount 2
        $entries[0].DisplayName | Should -Be 'InstallKit QA Side 1.0'
        $entries[0].DisplayVersion | Should -Be '1.0.0.0'
        $entries[1].DisplayName | Should -Be 'InstallKit QA Side 1.1'
        $entries[1].DisplayVersion | Should -Be '1.1.0.0'
        Join-Path (Get-SideDir '1.0') 'QaDemo.exe' | Should -Exist
        Join-Path (Get-SideDir '1.1') 'QaDemo.exe' | Should -Exist
        Get-Service 'InstallKitQaSide-1.0' | Should -Not -BeNullOrEmpty
        Get-Service 'InstallKitQaSide-1.1' | Should -Not -BeNullOrEmpty
    }

    It 'lets a patch release replace only its own line' -Skip:(-not $V1Patch) {
        Invoke-Msi "/i `"$(Join-Path $V1Patch 'QaSide.msi')`"" '5c-side-1.0.1'

        $entries = Get-SideEntries | Sort-Object DisplayName
        $entries | Should -HaveCount 2
        $entries[0].DisplayVersion | Should -Be '1.0.1.0'
        $entries[1].DisplayVersion | Should -Be '1.1.0.0'
        Join-Path (Get-SideDir '1.0') 'QaDemo.exe' | Should -Exist
        Join-Path (Get-SideDir '1.1') 'QaDemo.exe' | Should -Exist
    }

    It 'removes one line without touching the other' {
        $line10 = Get-SideEntries | Where-Object DisplayName -eq 'InstallKit QA Side 1.0'
        Invoke-Msi "/x $($line10.PSChildName)" '5d-side-uninstall-1.0'

        Get-SideEntries | Should -HaveCount 1
        (Get-SideEntries)[0].DisplayName | Should -Be 'InstallKit QA Side 1.1'
        # The service's heartbeat.txt is not part of the MSI, so the folder itself may stay; its files must not.
        Join-Path (Get-SideDir '1.0') 'QaDemo.exe' | Should -Not -Exist
        Join-Path (Get-SideDir '1.0') 'config' | Should -Not -Exist
        Get-Service 'InstallKitQaSide-1.0' -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        Join-Path (Get-SideDir '1.1') 'QaDemo.exe' | Should -Exist
        Get-Service 'InstallKitQaSide-1.1' | Should -Not -BeNullOrEmpty
    }

    It 'removes the last line completely' {
        Invoke-Msi "/x $((Get-SideEntries)[0].PSChildName)" '5e-side-uninstall-1.1'

        Get-SideEntries | Should -HaveCount 0
        Join-Path (Get-SideDir '1.1') 'QaDemo.exe' | Should -Not -Exist
        Join-Path (Get-SideDir '1.1') 'config' | Should -Not -Exist
        Get-Service 'InstallKitQaSide-1.1' -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
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
