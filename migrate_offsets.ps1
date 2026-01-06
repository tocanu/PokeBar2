# Script para migrar offsets de DexNumber (int) para UniqueId (string)
param(
    [string]$InputPath = "Assets\Final\pokemon_offsets_final.json",
    [string]$OutputPath = "Assets\Final\pokemon_offsets_final_migrated.json"
)

Write-Host "Migrando offsets de DexNumber para UniqueId..." -ForegroundColor Cyan

if (-not (Test-Path $InputPath)) {
    Write-Host "Erro: Arquivo nao encontrado: $InputPath" -ForegroundColor Red
    exit 1
}

# Ler JSON antigo
$json = Get-Content $InputPath -Raw | ConvertFrom-Json

Write-Host "Lidos $($json.Count) registros do arquivo antigo"

# Converter para novo formato
$migrated = @()
foreach ($entry in $json) {
    # Converter DexNumber para UniqueId no formato "0000_0000"
    $dex = [int]$entry.DexNumber
    $uniqueId = "{0:D4}_0000" -f $dex
    
    # Criar novo objeto com UniqueId
    $newEntry = [PSCustomObject]@{
        UniqueId = $uniqueId
        GroundOffsetY = $entry.GroundOffsetY
        CenterOffsetX = $entry.CenterOffsetX
        Reviewed = $entry.Reviewed
        HitboxX = $entry.HitboxX
        HitboxY = $entry.HitboxY
        HitboxWidth = $entry.HitboxWidth
        HitboxHeight = $entry.HitboxHeight
        FrameWidth = $entry.FrameWidth
        FrameHeight = $entry.FrameHeight
        GridColumns = $entry.GridColumns
        GridRows = $entry.GridRows
        PrimarySpriteFile = $entry.PrimarySpriteFile
        WalkSpriteFile = $entry.WalkSpriteFile
        IdleSpriteFile = $entry.IdleSpriteFile
        FightSpriteFile = $entry.FightSpriteFile
        HasAttackAnimation = if ($null -eq $entry.HasAttackAnimation) { $false } else { $entry.HasAttackAnimation }
    }
    
    $migrated += $newEntry
}

Write-Host "Convertidos $($migrated.Count) registros para novo formato"

# Salvar JSON migrado
$migrated | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8

Write-Host "Migracao concluida!" -ForegroundColor Green
Write-Host "  Arquivo original: $InputPath"
Write-Host "  Arquivo migrado:  $OutputPath"
Write-Host ""
Write-Host "Para aplicar a migracao:" -ForegroundColor Yellow
Write-Host "  1. Verifique o arquivo migrado"
Write-Host "  2. Substitua: Move-Item '$OutputPath' '$InputPath' -Force"
