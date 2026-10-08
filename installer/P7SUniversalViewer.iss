#ifndef Version
  #define Version "2.0.0"
#endif
#ifndef PublishDirectory
  #define PublishDirectory "..\artifacts\publish"
#endif
[Setup]
AppId={{E9F771EE-62DE-42BE-86C8-474D617354CF}
AppName=P7S Universal Viewer
AppVersion={#Version}
AppPublisher=ALMARFELD
AppPublisherURL=https://almarfeld.com
AppSupportURL=https://almarfeld.com/support/
AppUpdatesURL=https://github.com/AlinTibi/p7s-universal-viewer/releases
DefaultDirName={localappdata}\Programs\P7S Universal Viewer
DefaultGroupName=P7S Universal Viewer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=P7SViewer_Setup-v{#Version}
SetupIconFile=..\src\P7SUniversalViewer\Assets\p7s.ico
UninstallDisplayIcon={app}\P7SUniversalViewer.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\P7S Universal Viewer"; Filename: "{app}\P7SUniversalViewer.exe"
Name: "{autodesktop}\P7S Universal Viewer"; Filename: "{app}\P7SUniversalViewer.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\P7SUniversalViewer.exe"; Description: "Launch P7S Universal Viewer"; Flags: nowait postinstall skipifsilent
