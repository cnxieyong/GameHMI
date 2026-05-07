[Setup]
AppName=智造工厂
AppVersion=1.0
AppPublisher=智造工厂
DefaultDirName={autopf}\GameHMI
DefaultGroupName=智造工厂
OutputDir=.\publish
OutputBaseFilename=智造工厂_Setup
SetupIconFile=.\publish\GameHMI\GameHMI.exe
Compression=lzma2
SolidCompression=yes
UninstallDisplayName=智造工厂
PrivilegesRequired=lowest

[Files]
Source: "publish\GameHMI\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\智造工厂"; Filename: "{app}\GameHMI.exe"
Name: "{group}\卸载智造工厂"; Filename: "{uninstallexe}"
Name: "{autodesktop}\智造工厂"; Filename: "{app}\GameHMI.exe"

[Run]
Filename: "{app}\GameHMI.exe"; Description: "启动智造工厂"; Flags: nowait postinstall skipifsilent
