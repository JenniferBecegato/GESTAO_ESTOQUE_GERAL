; Compile este arquivo no Inno Setup depois de gerar a build Release.
; Usa somente os arquivos existentes: nao executa dotnet build/publish.
; Requisitos no destino: .NET Desktop Runtime 10 (x64) e WebView2 Runtime.
#define AppName "Campos - Controle de Estoque"
#define AppExeName "GESTAO_ESTOQUE_GERAL.exe"
#define ReleaseDir SourcePath + "bin\Release\net10.0-windows"
#define AppIcon SourcePath + "Resources\program_ico.ico"

#if !FileExists(ReleaseDir + "\" + AppExeName)
  #error Gere a build Release antes de compilar o instalador.
#endif
#define AppVersion GetVersionNumbersString(ReleaseDir + "\" + AppExeName)

[Setup]
AppId={{736FCE35-A5D1-4C48-8C08-B8B4F03434E1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Campos
DefaultDirName={autopf}\Campos\Controle de Estoque
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#SourcePath}output
OutputBaseFilename=Instalador-Campos-Controle-Estoque
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\program_ico.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na area de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppIcon}"; DestDir: "{app}"; DestName: "program_ico.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\program_ico.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\program_ico.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent
