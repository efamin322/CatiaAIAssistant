; Inno Setup script — CATIA AI Engineering Assistant
; Built by .github/workflows/release.yml:  iscc /DAppVersion=x.y.z /DSourceDir=<publish dir> installer\CatiaAIAssistant.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

[Setup]
AppId={{B8E1D5C2-6F3A-4B7E-9C21-3A9D7E5F1C40}
AppName=CATIA AI Engineering Assistant
AppVersion={#AppVersion}
AppPublisher=efamin322
AppPublisherURL=https://github.com/efamin322/CatiaAIAssistant
DefaultDirName={autopf}\CatiaAIAssistant
DefaultGroupName=CATIA AI Assistant
OutputDir=..\artifacts
OutputBaseFilename=CatiaAIAssistant-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
UninstallDisplayIcon={app}\CatiaAIAssistant.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CATIA AI Assistant"; Filename: "{app}\CatiaAIAssistant.exe"
Name: "{group}\Uninstall CATIA AI Assistant"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CATIA AI Assistant"; Filename: "{app}\CatiaAIAssistant.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CatiaAIAssistant.exe"; Description: "{cm:LaunchProgram,CATIA AI Assistant}"; Flags: nowait postinstall skipifsilent
