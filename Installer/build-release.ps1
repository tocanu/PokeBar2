<#
.SYNOPSIS
    Compila o PokeBar, publica os binários e gera o instalador Inno Setup.

.DESCRIPTION
    Passos executados:
      1. Lê a versão do Pokebar.DesktopPet.csproj
      2. Publica o projeto (self-contained, win-x64)
      3. Invoca o ISCC.exe (compilador do Inno Setup) para gerar o Setup.exe
      4. Saída: dist\PokeBar-Setup-{Version}.exe

.PARAMETER Version
    Sobrescreve a versão lida do csproj (ex: "1.2.3").
    Se omitido, usa a versão do csproj.

.PARAMETER SkipBuild
    Pula a publicação do .NET e vai direto para o Inno Setup.
    Útil para regenerar o installer sem recompilar.

.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -Version "1.2.0"
    .\build-release.ps1 -SkipBuild
#>

param(
    [string] $Version   = "",
    [switch] $SkipBuild = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Caminhos ─────────────────────────────────────────────────────────────────
$Root       = Split-Path $PSScriptRoot -Parent
$CsprojPath = Join-Path $Root "Pokebar.DesktopPet\Pokebar.DesktopPet.csproj"
$PublishDir = Join-Path $Root "dist\publish"
$DistDir    = Join-Path $Root "dist"
$IssFile    = Join-Path $PSScriptRoot "pokebar-setup.iss"

# ── Detectar versão ───────────────────────────────────────────────────────────
if (-not $Version) {
    [xml]$csproj = Get-Content $CsprojPath
    $Version = $csproj.Project.PropertyGroup |
                Where-Object { $_.Version } |
                Select-Object -First 1 -ExpandProperty Version
    if (-not $Version) {
        Write-Error "Versão não encontrada no csproj. Defina <Version> ou use -Version."
        exit 1
    }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  PokeBar Build Script  v$Version".PadRight(43) + "║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ── Passo 1: Publicar ─────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host "▶ Publicando projeto .NET (self-contained, win-x64)..." -ForegroundColor Yellow

    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    $publishArgs = @(
        "publish", $CsprojPath,
        "--configuration", "Release",
        "--runtime", "win-x64",
        "--self-contained", "true",
        "/p:PublishSingleFile=false",
        "/p:EnableCompressionInSingleFile=false",
        "/p:Version=$Version",
        "--output", $PublishDir,
        "--nologo"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish falhou (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }

    Write-Host "  ✔ Publicado em: $PublishDir" -ForegroundColor Green
} else {
    Write-Host "⚠ SkipBuild: usando publicação existente em $PublishDir" -ForegroundColor DarkYellow
    if (-not (Test-Path $PublishDir)) {
        Write-Error "Pasta de publicação não encontrada: $PublishDir. Execute sem -SkipBuild primeiro."
        exit 1
    }
}

# ── Passo 2: Compilar instalador Inno Setup ───────────────────────────────────
Write-Host ""
Write-Host "▶ Compilando instalador com Inno Setup..." -ForegroundColor Yellow

# Localizar ISCC.exe
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "${env:USERPROFILE}\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if (-not $isccCandidates) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║  ISCC.exe não encontrado. Instale o Inno Setup 6:               ║" -ForegroundColor Red
    Write-Host "║  https://jrsoftware.org/isdl.php                                ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Error "Inno Setup não instalado."
    exit 1
}

$iscc = $isccCandidates[0]
Write-Host "  Usando ISCC: $iscc"

# Garantir pasta dist
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

& $iscc $IssFile "/DMyAppVersion=$Version" "/Q"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup falhou (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

$setupExe = Join-Path $DistDir "PokeBar-Setup-$Version.exe"
if (Test-Path $setupExe) {
    $sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  ✔ Instalador gerado com sucesso!                   ║" -ForegroundColor Green
    Write-Host "║  Arquivo : PokeBar-Setup-$Version.exe".PadRight(54) + "║" -ForegroundColor Green
    Write-Host "║  Tamanho : $($sizeMb) MB".PadRight(54) + "║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "Próximos passos:" -ForegroundColor Cyan
    Write-Host "  1. Teste o instalador: .\dist\PokeBar-Setup-$Version.exe"
    Write-Host "  2. Crie uma tag Git:   git tag v$Version && git push --tags"
    Write-Host "  3. Publique no GitHub: gh release create v$Version dist\PokeBar-Setup-$Version.exe --title 'PokeBar v$Version'"
} else {
    Write-Error "Arquivo de saída não encontrado: $setupExe"
    exit 1
}
