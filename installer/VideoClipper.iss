; Built by the GitHub Action (see .github/workflows/release.yml).
; Expects: dist\app\VideoClipper.exe and dist\ffmpeg\* (ffmpeg.exe, DLLs, LICENSE.txt)
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6D1B6E5A-3C0A-4B8E-9B77-0C5D7A4F2E11}
AppName=Video Clipper
AppVersion={#AppVersion}
AppPublisher=Ciaran Gallagher
AppPublisherURL=https://github.com/ciaran036/VideoClipper
DefaultDirName={autopf}\Video Clipper
DefaultGroupName=Video Clipper
UninstallDisplayIcon={app}\VideoClipper.exe
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=VideoClipper-Setup-{#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\app\VideoClipper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\ffmpeg\*"; DestDir: "{app}\ffmpeg"; Flags: ignoreversion recursesubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Video Clipper"; Filename: "{app}\VideoClipper.exe"
Name: "{autodesktop}\Video Clipper"; Filename: "{app}\VideoClipper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoClipper.exe"; Description: "Launch Video Clipper"; Flags: nowait postinstall skipifsilent
