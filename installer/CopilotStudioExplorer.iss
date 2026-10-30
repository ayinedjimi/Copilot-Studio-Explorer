#ifndef SourceDir
  #define SourceDir "..\outputs\Application"
#endif
#ifndef OutputDir
  #define OutputDir "output"
#endif

#define AppName "Copilot Studio Explorer"
#define AppVersion "3.1.0"
#define AppPublisher "Ayi NEDJIMI Consultants"
#define AppWebsite "https://ayinedjimi-consultants.fr"

[Setup]
AppId={{8024E29B-4D39-44F4-8EF9-DAB67108A749}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppWebsite}
AppSupportURL={#AppWebsite}
DefaultDirName={autopf}\Copilot Studio Explorer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Copilot-Studio-Explorer-Setup-3.1.0-x64
SetupIconFile=..\outputs\StudioDataverse\Assets\StudioDataverse.ico
UninstallDisplayIcon={app}\StudioDataverse.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\StudioDataverse.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\StudioDataverse.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\StudioDataverse.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')} }"; Flags: nowait postinstall skipifsilent unchecked
