#define MyAppName      "Messenger"
#define MyAppVersion   "2.0.2"
#define MyAppExeName   "Messenger.exe"
#define MyAppPublisher "Tomas Lundqvist"
#define SourceDir      "D:\vsproj\c#\Messenger.WPF\publish"

[Setup]
AppId={{4A64E45A-3CAB-46BF-802C-1857EF958D78}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=Messenger_Setup_{#MyAppVersion}
OutputDir=D:\vsproj\c#\Messenger.WPF\Installer
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SolidCompression=yes
WizardStyle=modern
Compression=lzma2/ultra64


[Languages]
Name: "swedish"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Huvudapplikation
Source: "{#SourceDir}\{#MyAppExeName}";  DestDir: "{app}"; Flags: ignoreversion
; DLL-filer och konfiguration
Source: "{#SourceDir}\*.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.json";           DestDir: "{app}"; Flags: ignoreversion
; Assets (ikoner)
Source: "{#SourceDir}\Assets\*";         DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
; WebView2 och .NET runtime
Source: "{#SourceDir}\runtimes\*";       DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
