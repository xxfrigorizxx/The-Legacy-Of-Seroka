; Inno Setup script - SEROKA Launcher Alpha
; Compile avec ISCC.exe

#define AppName "SEROKA Frozen Legacy"
#define AppVersion "0.1.0-alpha.1"
#define Publisher "SEROKA Studio"
#define LauncherExeName "SEROKALauncher.exe"
#define GameExeName "SEROKAFrozenLegacy.exe"
#define SourceRoot "..\\.."
#define LauncherBuild "..\\..\\Launcher\\SEROKALauncher\\bin\\Release\\net8.0"
#define DistRoot ".."

[Setup]
AppId={{9ED7F2A1-0EE4-4C16-B67D-5738E2E7A1AE}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={localappdata}\\SEROKAFrozenLegacy
DefaultGroupName=SEROKA Frozen Legacy
DisableProgramGroupPage=yes
OutputDir=..\Installer\Output
OutputBaseFilename=SEROKAFrozenLegacy_Setup_Alpha
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; Launcher
Source: "{#LauncherBuild}\SEROKALauncher.exe"; DestDir: "{app}\launcher"; Flags: ignoreversion
Source: "{#LauncherBuild}\SEROKALauncher.dll"; DestDir: "{app}\launcher"; Flags: ignoreversion
Source: "{#LauncherBuild}\SEROKALauncher.runtimeconfig.json"; DestDir: "{app}\launcher"; Flags: ignoreversion
Source: "{#LauncherBuild}\SEROKALauncher.deps.json"; DestDir: "{app}\launcher"; Flags: ignoreversion
Source: "{#SourceRoot}\Launcher\SEROKALauncher\examples\launcher-config.remote.example.json"; DestDir: "{app}\launcher"; DestName: "launcher-config.json"; Flags: ignoreversion

; Manifest local initial
Source: "{#DistRoot}\manifest.alpha.json"; DestDir: "{app}\manifests"; DestName: "local-manifest.json"; Flags: ignoreversion

; Game payload initial (alpha)
Source: "{#SourceRoot}\SEROKAFrozenLegacy.exe"; DestDir: "{app}\game"; DestName: "{#GameExeName}"; Flags: ignoreversion
Source: "{#SourceRoot}\SEROKAFrozenLegacy.pck"; DestDir: "{app}\game"; DestName: "SEROKAFrozenLegacy.pck"; Flags: ignoreversion
Source: "{#SourceRoot}\data_Zero-K - Frozen Legacy_windows_x86_64\*"; DestDir: "{app}\game\data_Zero-K - Frozen Legacy_windows_x86_64"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\SEROKA Frozen Legacy"; Filename: "{app}\launcher\{#LauncherExeName}"
Name: "{autoprograms}\SEROKA Frozen Legacy"; Filename: "{app}\launcher\{#LauncherExeName}"

[Run]
Filename: "{app}\launcher\{#LauncherExeName}"; Description: "Lancer SEROKA Frozen Legacy"; Flags: nowait postinstall skipifsilent
