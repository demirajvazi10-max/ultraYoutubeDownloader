; Ultra YouTube Downloader - Inno Setup skripta
; Kompajlira se automatski kroz .github/workflows/release.yml (Inno-Setup-Action)
; kada gurnes tag "v*". Moze i rucno, ako imas Inno Setup instaliran lokalno:
; ISCC.exe installer\UltraYoutubeDownloader.iss  (pokretati iz root foldera repo-a)

#define MyAppName "Ultra YouTube Downloader"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Demir Ajvazi"
#define MyAppExeName "UltraYoutubeDownloader.exe"
#define MyAppURL "https://github.com/demirajvazi10-max/UltraYoutubeDownloader"

[Setup]
; Jedinstveni GUID za ovu aplikaciju - ne diraj posle prvog release-a,
; Windows ga koristi da prepozna update vs. novu instalaciju.
AppId={{9E2A1F3C-7B4D-4E8A-9C6F-2D5B8A1E4C90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; GitHub-ov standardni GPL-3.0 template se zove "LICENSE" (bez ekstenzije) -
; ako ga nazoves drugacije (npr. LICENSE.txt), izmeni liniju ispod da se poklapa.
LicenseFile=..\LICENSE
OutputDir=..\Output
OutputBaseFilename=UltraYoutubeDownloader-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; x64 arhitektura - eksplicitno postavljeno da instalacija ide u pravi
; (64-bit) Program Files, ista greska je nadjena i ispravljena kod Video Editora.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; dotnet publish (self-contained, single-file) izbacuje sve u /publish -
; pokupi ceo sadrzaj tog foldera, iskljucujuci .pdb fajlove za debug simbole.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
