; ============================================================
;  PokeBar — Inno Setup Script
;  Gerado para: .NET 8.0 WPF, instalação per-user (sem UAC)
;
;  Como usar:
;    1. Publique o app: veja build-release.ps1
;    2. Compile este script: ISCC.exe pokebar-setup.iss
;    3. O instalador sai em: dist\PokeBar-Setup-{Version}.exe
;
;  Para novo release:
;    - Atualize MyAppVersion abaixo (ou passe /DMyAppVersion=x.y.z ao ISCC)
;    - Atualize as mesmas versões no Pokebar.DesktopPet.csproj
; ============================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName      "PokeBar"
#define MyAppPublisher "PokeBar"
#define MyAppURL       "https://github.com/tocanu/PokeBar2"
#define MyAppExeName   "Pokebar.DesktopPet.exe"
#define MyAppMutex     "Pokebar_SingleInstance"

; Pasta de publicação (gerada por build-release.ps1)
#define PublishDir     "..\dist\publish"

[Setup]
; Identidade
AppId={{A7C2F3B1-9D4E-4F81-BB2A-3E8C6D0F1A25}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Instalação per-user: não precisa de UAC
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Aparência
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Compressão (lzma2 = melhor ratio para arquivos .NET)
Compression=lzma2
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Output
OutputDir=..\dist
OutputBaseFilename=PokeBar-Setup-{#MyAppVersion}

; Fechar instâncias em execução antes de instalar
CloseApplications=yes
CloseApplicationsFilter=*{#MyAppExeName}*
RestartApplications=yes

; Informações na janela
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Installer

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Criar atalho na Área de Trabalho"; \
    GroupDescription: "Atalhos adicionais:"; Flags: unchecked
Name: "startup";      Description: "Iniciar com o Windows (bandeja do sistema)"; \
    GroupDescription: "Inicialização:"; Flags: unchecked

[Files]
; ── Todos os arquivos publicados ──────────────────────────────────────────────
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Menu Iniciar
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

; Área de Trabalho (opcional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
    Tasks: desktopicon

; Inicialização com Windows (opcional — chave Run no registro por usuário)
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
    Tasks: startup

[Registry]
; Remove entrada de Startup se a tarefa não foi selecionada
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: none; ValueName: "{#MyAppName}"; \
    Flags: deletevalue; Tasks: not startup

[Run]
; Abrir o app ao final da instalação
Filename: "{app}\{#MyAppExeName}"; \
    Description: "Iniciar {#MyAppName}"; \
    Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Limpar arquivos de log gerados em runtime (na pasta de install)
; Dados do usuário em %AppData%\Pokebar\ são preservados intencionalmente.
Type: filesandordirs; Name: "{app}\logs"
