; FlipsiInk Installer (Inno Setup)
; Copyright (C) 2026 Fabian Kirchweger
; GPL v3

#define AppName "FlipsiInk"
#define AppVersion "0.1.0"
#define AppPublisher "TechFlipsi"
#define AppPublisherURL "https://github.com/TechFlipsi/FlipsiInk"
#define AppExeName "FlipsiInk.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=FlipsiInk_Setup_{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Dark/Light Mode Support
WizardResizable=yes
WizardSize=800x500

; Uninstall
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.WelcomeLabel2=Dies installiert {#AppName} auf Ihrem Computer.%n%nEs wird ein eigener Ordner "{#AppName}" im gewählten Pfad erstellt.%n%nKlicken Sie auf Weiter, um fortzufahren.
english.WelcomeLabel2=This will install {#AppName} on your computer.%n%nA dedicated "{#AppName}" folder will be created in the selected path.%n%nClick Next to continue.

german.SelectDirLabel3=Der Installationspfad wird immer einen eigenen Ordner "{#AppName}" erstellen.
english.SelectDirLabel3=The installation path will always create a dedicated "{#AppName}" folder.

german.DirExistsWarning=Der Ordner "%s" existiert bereits. Wollen Sie dort installieren?
english.DirExistsWarning=The folder "%s" already exists. Do you want to install there?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Haupt-Executable
Source: "..\publish\FlipsiInk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Sprachdateien
Source: "..\publish\Lang\*"; DestDir: "{app}\Lang"; Flags: ignoreversion recursesubdirs createallsubdirs
; Models-Ordner (leer, für später)
[Dirs]
Name: "{app}\Models"; Flags: uninsalwaysdelete
Name: "{localappdata}\FlipsiInk"; Flags: uninsneveruninstall
Name: "{localappdata}\FlipsiInk\Notes"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DarkModeCheckBox: TCheckBox;
  IsDarkMode: Boolean;

procedure InitializeWizard;
begin
  // Detect Windows Dark Mode
  IsDarkMode := RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize', 'AppsUseLightTheme') and
                (RegReadIntegerValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize', 'AppsUseLightTheme') = 0);

  // Set wizard colors based on system theme
  if IsDarkMode then
  begin
    WizardForm.Color := $1E1E1E;
    WizardForm.Font.Color := $FFFFFF;
    WizardForm.Bevel.Color := $2D2D2D;
    WizardForm.WizardBitmapImage.BackColor := $1E1E1E;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Path: string;
begin
  Result := True;

  // Ensure FlipsiInk folder is always in the path
  if CurPageID = wpSelectDir then
  begin
    Path := WizardDirValue;

    // If user didn't include "FlipsiInk" in the path, add it
    if not PathContainsFlipsiInk(Path) then
    begin
      Path := AddBackslash(Path) + '{#AppName}';
      WizardForm.DirEdit.Text := Path;
    end;
  end;
end;

function PathContainsFlipsiInk(Path: string): Boolean;
var
  LowerPath: string;
begin
  LowerPath := LowerCase(ExtractFileName(ExcludeTrailingBackslash(Path)));
  Result := (LowerPath = LowerCase('{#AppName}'));
end;

function UpdateReadyMemos(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  ResultStr: String;
begin
  ResultStr := '';
  if MemoDirInfo <> '' then
    ResultStr := ResultStr + MemoDirInfo + NewLine + NewLine;

  // Show data storage path
  ResultStr := ResultStr + 'Notizen speichern in:' + NewLine +
               ExpandConstant('{localappdata}\FlipsiInk\Notes') + NewLine + NewLine;

  if MemoGroupInfo <> '' then
    ResultStr := ResultStr + MemoGroupInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then
    ResultStr := ResultStr + MemoTasksInfo;

  Result := ResultStr;
end;

procedure CurStepChanged(CurStep: TSetupStepType);
begin
  if CurStep = ssPostInstall then
  begin
    // Create default config with system theme
    SaveStringToFile(
      ExpandConstant('{localappdata}\FlipsiInk\config.json'),
      '{' + #13#10 +
      '  "Language": "de",' + #13#10 +
      '  "Theme": "' + IfThen(IsDarkMode, 'dark', 'system') + '",' + #13#10 +
      '  "ModelPath": "",' + #13#10 +
      '  "AutoUpdate": true,' + #13#10 +
      '  "UpdateChannel": "stable"' + #13#10 +
      '}',
      False
    );
  end;
end;