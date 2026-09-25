[Setup]
AppName=汐月 XiYue
AppVersion=1.2.0
AppPublisher=SchoolBusytools
DefaultDirName={autopf}\XiYue
DefaultGroupName=汐月
UninstallDisplayIcon={app}\SBtools.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=.\publish\installer
OutputBaseFilename=XiYue_Setup_v1.2.0
SetupIconFile=Assets\app.ico
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\汐月"; Filename: "{app}\SBtools.exe"
Name: "{group}\卸载汐月"; Filename: "{uninstallexe}"
Name: "{autodesktop}\汐月"; Filename: "{app}\SBtools.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SBtools.exe"; Description: "立即启动汐月"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"