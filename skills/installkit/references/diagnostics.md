# InstallKit diagnostics

Every message has the form `InstallKit : error IKxxx: ...` (or `warning`), so it shows in the Visual Studio error list and in Azure DevOps annotations. The table is generated from the source; the message of the build is the authority.

## Build and SDK

| Code | Kind | What it says |
|---|---|---|
| `IK001` | error | The version cannot be used as an MSI version (empty, not a version, or major/minor above 255 or build above 65535) |
| `IK002` | warning | No tool manifest with a pinned wix version was found |
| `IK003` | error | Building MSIs needs Windows (WiX and the WixSharp engine run on .NET Framework) |
| `IK004` | error | WiX $(InstallKitWixVersion) requires accepting its Open Source Maintenance Fee EULA (https://wixtoolset.org/osmf/) |
| `IK005` | error | Unknown InstallKitEngine '$(InstallKitEngine)' |

## Model, files and install events

| Code | Kind | What it says |
|---|---|---|
| `IK100` | error | The installer plan is empty |
| `IK101` | error | … has no UpgradeCode |
| `IK102` | error | …: UpgradeCode '…' is not a valid GUID |
| `IK103` | error | … uses the same UpgradeCode as … |
| `IK110` | error | … has no files |
| `IK111` | error | … uses payload '…', but the project has no <InstallerPayload> with that name (known: …) |
| `IK112` | error | …: payload directory '…' does not exist |
| `IK120` | error | … asks for a desktop shortcut but has no MainExecutable |
| `IK121` | error | …: the … '…' is not in any payload |
| `IK122` | warning | MSI '…' is a … package (…) |
| `IK130` | error | … has an empty chain |
| `IK131` | error | … chains MSI '…', which is not part of this plan |
| `IK132` | error | … chains '…', which does not exist |
| `IK133` | error | … chains '…', which does not exist |
| `IK134` | error | …: '…' has no DetectCondition, so it would run on every install |
| `IK140` | error | …: logo '…' does not exist |
| `IK141` | error | …: logo '…' must be a .png, .ico, .bmp or .jpg file |
| `IK150` | error | …: the … handler '…' cannot run at install time: … |
| `IK151` | error | …: a … command has no path |
| `IK152` | error | …: accent colour '…' must look like #0067C0 |
| `IK153` | error | …: background colour '…' must look like #0F1115 |
| `IK154` | error | …: text colour '…' must look like #F3F4F6 |
| `IK160` | error | Install event handler must be a method |
| `IK161` | error | Install event handler must be static |
| `IK162` | error | Install event handler must be internal or public |
| `IK163` | error | MSI '…': no generated custom action for the … handler |
| `IK170` | error | …: a registry value under … has the key '…' |
| `IK171` | error | …: … is a DWord but its value '…' is not a whole number |
| `IK172` | error | …: … is under HKLM, which a per-user install cannot write |
| `IK180` | error | …: '…' is not a valid environment variable name (it must not be empty or contain '=') |
| `IK181` | error | …: the environment variable '…' has no value |
| `IK182` | error | …: setting PATH would replace the whole variable |
| `IK183` | error | …: the … entry '…' contains ';' |
| `IK190` | error | …: '…' is not a valid file extension |
| `IK191` | error | …: '…' is not a valid URL scheme |
| `IK192` | error | …: the … has no program |
| `IK193` | error | … associates '…' more than once |
| `IK194` | error | …: the ProgId '…' must start with a letter and contain only letters, digits, '.' and '_' |
| `IK195` | error | … registers the URL scheme '…' more than once |

## Features, settings and UI

| Code | Kind | What it says |
|---|---|---|
| `IK200` | warning | the … engine has no WPF dialogs; MSI '…' uses the native modern dialogs instead of the flat ones |
| `IK201` | error | MSI '…' has install event handlers, which the … engine does not support |
| `IK202` | warning | MSI '…' adds WPF dialogs, which only show with Ui = ModernWpf |
| `IK204` | warning | MSI '…': Theme Auto follows the Windows setting only in the flat UI (Appearance.Flat) |
| `IK210` | error | …: a firewall rule has no name |
| `IK211` | error | …: the … names neither a port nor a program |
| `IK212` | error | …: the … has the port '…' |
| `IK213` | error | …: the … has a port, so its protocol must be Tcp or Udp |
| `IK214` | error | …: the … cannot be created by a per-user install, which does not run elevated |
| `IK220` | error | …: an XML value has no file |
| `IK221` | error | …: the XPath '…' for '…' is not valid, for example /configuration/appSettings/add[@key='Server']/@value |
| `IK222` | error | …: …: XmlValue edits XML files only |
| `IK223` | error | …: the … has no value |
| `IK230` | error | …: '…' is not a process file name |
| `IK231` | error | … closes '…' more than once |
| `IK232` | error | …: the timeout for closing '…' is … seconds |
| `IK240` | error | …: '…' is not a usable feature name |
| `IK241` | error | … declares the feature '…' more than once (names that differ only in spaces or punctuation are the same feature) |
| `IK242` | error | …: files are assigned to the feature '…', which is not declared |
| `IK243` | error | …: the feature '…' has no files |
| `IK244` | error | …: the … '…' is in the optional feature '…', but it is set up whether or not that feature is installed |
| `IK246` | warning | MSI '…' has optional features, which users can only choose in the FeatureTree and Modern UIs or with ADDLOCAL and REMOVE on the command line |
| `IK250` | error | …: a folder permission for … has no account |
| `IK251` | error | …: '…' is not a folder below the install directory |
| `IK252` | error | …: … is inside a payload folder, which is not supported yet |
| `IK253` | error | … gives '…' rights on … more than once |
| `IK260` | error | …: the download address '…' must be a full https URL |
| `IK261` | error | …: the SHA-512 hash of '…' must be 128 hex digits |
| `IK262` | error | …: the size of '…' must be given in bytes (1 to 2147483647), for example from (Get-Item <file>).Length |
| `IK263` | error | …: '…' is not a usable installer file name |
| `IK264` | error | … looks up the variable '…' twice |
| `IK265` | error | …: .NET … is not a valid major version |
| `IK270` | error | … installs side by side but its install directory '…' does not contain the version line '…', so versions would share one folder |
| `IK271` | error | … chains MSI '…', which installs side by side, but the bundle does not |
| `IK273` | warning | '…' installs side by side, but {string.Join( |
| `IK280` | error | …: '…' is not a usable property name |
| `IK281` | error | …: the property '…' is defined more than once (as a prompt or with Settings(s => s.Property(...))) |
| `IK282` | error | …: the prompt for '…' has no label |
| `IK283` | error | … asks for … values, but the setup page has room for … |
| `IK284` | error | …: '…' is a property that Windows Installer or InstallKit uses itself |
| `IK285` | warning | MSI '…' has prompted properties, which only the modern UIs ask for |

## Analyzers, versions and requirements

| Code | Kind | What it says |
|---|---|---|
| `IK300` | warning | The upgrade code must be a constant GUID |
| `IK301` | error | The upgrade code is not a valid GUID |
| `IK302` | error | The upgrade code is used more than once |
| `IK303` | warning | Create builders with Create(...) |
| `IK310` | warning | the version '…' has the suffix '…', which MSI cannot store |
| `IK311` | error | The version '$(InstallKitVersion)' does not fit an MSI version: major and minor can be at most 255 and the build number at most 65535 (here $(_IkMajor).$(_IkMinor).$(_IkB… |
| `IK320` | error | …: a launch condition has no condition |
| `IK321` | error | …: the launch condition '…' has no message |
| `IK330` | error | …: a data folder below … is not supported |
| `IK331` | error | …: '…' is not a folder path below %ProgramData%. Use a relative path such as Contoso\My App, without '..', a drive, wildcards or MSI properties |
| `IK332` | error | …: the data folder '…' gives nobody access |
| `IK333` | error | …: an entry of the data folder '…' has no account |
| `IK334` | error | … gives '…' rights on the data folder '…' more than once |
| `IK335` | error | … declares the data folder '…' more than once |

## Internal

| Code | Kind | What it says |
|---|---|---|
| `IK900` | error | … |
