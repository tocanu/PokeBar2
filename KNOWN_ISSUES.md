# 🐛 PROBLEMAS CONHECIDOS

## ⚠️ Média Prioridade

### **Conflito de indexação: Pipeline suporta variantes, Editor/Runtime indexam só por Dex**

**Status:** Não resolvido - Aguardando refatoração do sistema de offsets

**Descrição:**
O pipeline (`Pokebar.Pipeline/Program.cs`) agora varre e processa variantes de Pokémon usando o formato `{dex}_{formId}` (ex: `0025_0006` para Pikachu forma 6). Porém:

- **Editor** (`Pokebar.Editor/MainWindow.xaml.cs` line 20): Carrega offsets usando apenas o Dex como chave
- **Runtime** (`Pokebar.DesktopPet/Animation/SpriteLoader.cs`): Carrega sprites usando apenas o Dex
- **Offsets finais** (`FinalOffsets.cs` line 6): Dicionário indexado apenas por `int Dex`

**Impacto:**
- Se o pipeline gerar offsets para múltiplas formas do mesmo Pokémon, **as formas sobrescrevem umas às outras** no dicionário
- Apenas a última forma processada fica disponível
- Formas alternativas (ex: Alola, Galar, Mega, Gigantamax) **não funcionam** no runtime

**Exemplo de colisão:**
```
Pipeline processa:
- 0025/0000 (Pikachu normal) → offsets calculados
- 0025/0006 (Pikachu forma 6) → sobrescreve offsets do 0025 normal

Runtime carrega:
- SpriteLoader.LoadAnimation(dex: 25) → recebe offsets da forma 6
- Animações ficam desalinhadas
```

**Solução planejada (FASE 3 - Performance):**
1. Mudar chave dos offsets de `int` para `string` (formato `{dex}_{formId}`)
2. Atualizar `FinalOffsets.cs`: `Dictionary<string, PokemonOffsets>`
3. Atualizar `SpriteLoader` para aceitar parâmetro opcional `formId`
4. Manter compatibilidade: `formId = "0000"` por padrão
5. UI do PC/Box (FASE 6) permitirá selecionar formas específicas

**Workaround atual:**
- Usar apenas sprites sem variantes (pasta raiz do Dex)
- Evitar processar formas alternativas no pipeline até refatoração

**Arquivos afetados:**
- `Pokebar.Core/Serialization/FinalOffsets.cs`
- `Pokebar.Core/Models/PokemonSpriteMetadata.cs`
- `Pokebar.Pipeline/Program.cs`
- `Pokebar.Editor/MainWindow.xaml.cs`
- `Pokebar.DesktopPet/Animation/SpriteLoader.cs`

**Issue relacionada:** #TBD (criar issue no GitHub quando priorizar)

---

## ✅ Resolvidos

### **EnumerateSpriteFolders ignorava sprites na pasta raiz quando havia subpastas**

**Status:** ✅ Resolvido em commit `[hash]`

**Descrição:**
`SpriteDirectoryHelper.EnumerateSpriteFolders` retornava apenas subpastas de formas quando elas existiam, ignorando sprites na pasta raiz do Dex.

**Solução:**
Adicionada verificação `hasRootSprites` para incluir pasta raiz como forma "0000" antes de processar subpastas.

**Commit:** `[hash do próximo commit]`

---

## 📝 Notas de implementação

### Quando resolver o conflito de variantes:

**Mudanças necessárias:**

1. **FinalOffsets.cs:**
```csharp
// Antes
public Dictionary<int, PokemonOffsets> Offsets { get; set; }

// Depois
public Dictionary<string, PokemonOffsets> Offsets { get; set; }
// Chave no formato: "0025_0000" ou "0025_0006"
```

2. **SpriteLoader.cs:**
```csharp
// Adicionar sobrecarga
public AnimationClip? LoadAnimation(int dex, string formId = "0000", AnimationType type = AnimationType.Idle)
{
    var key = $"{dex:D4}_{formId}";
    // Buscar offsets com key ao invés de dex
}
```

3. **Pipeline/Program.cs:**
```csharp
// Já está usando variant.UniqueId (formato correto)
// Apenas garantir que offsets sejam salvos com chave string
```

4. **Compatibilidade retroativa:**
```csharp
// Manter fallback para formato antigo
if (!offsets.TryGetValue(uniqueId, out var offset))
{
    // Tenta formato legado apenas com Dex
    offsets.TryGetValue($"{dex:D4}_0000", out offset);
}
```

**Testes necessários:**
- [ ] Pipeline processa múltiplas formas sem colisão
- [ ] Editor carrega offsets de formas específicas
- [ ] Runtime mostra animações corretas por forma
- [ ] Fallback funciona para offsets legados
- [ ] UI do PC permite trocar entre formas

**Tempo estimado:** 3-4 horas (parte da FASE 3)
