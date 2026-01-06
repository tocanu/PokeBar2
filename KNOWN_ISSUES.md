# 🐛 PROBLEMAS CONHECIDOS

## ✅ RESOLVIDOS

### **Conflito de indexação: Pipeline suporta variantes, Editor/Runtime indexam só por Dex**

**Status:** ✅ RESOLVIDO (commits 4b962a1, 045a68e)

**Descrição Original:**
O pipeline gerava variantes (0025_0006) mas Editor/Runtime usavam apenas Dex (int) como chave, causando colisões.

**Solução Implementada:**
- `OffsetAdjustment.DexNumber` (int) → `UniqueId` (string)
- `Dictionary<int, OffsetAdjustment>` → `Dictionary<string, OffsetAdjustment>`
- Todos os consumidores (Pipeline, Editor, App, DesktopPet) agora usam UniqueId
- Forma base sem sufixo: `0025` (mais limpo)
- Formas alternativas: `0025_0006`, `0025_0007`
- 1685 variantes únicas processadas sem colisões

**Arquivos Atualizados:**
- ✅ `Pokebar.Core/Serialization/FinalOffsets.cs`
- ✅ `Pokebar.Pipeline/Program.cs`
- ✅ `Pokebar.Editor/MainWindow.xaml.cs`
- ✅ `Pokebar.App/MainWindow.xaml.cs`
- ✅ `Pokebar.DesktopPet/Animation/SpriteLoader.cs`
- ✅ `Pokebar.DesktopPet/Entities/BaseEntity.cs`
- ✅ `Pokebar.DesktopPet/Entities/PokemonPet.cs`

**Resultado:**
- ✅ pokemon_offsets_final.json e pokemon_offsets_runtime.json agora usam UniqueId (string)
- ✅ Editor pode ajustar offsets por forma individualmente
- ✅ Runtime carrega sprites e offsets corretos por variante
- ✅ 0 colisões (exceto 1 duplicata conhecida: Pikachu 0025)

---

## ⚠️ Média Prioridade

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

## ⚠️ Média Prioridade

### **Pastas Aninhadas de Variantes Não Suportadas**

**Status:** LIMITAÇÃO CONHECIDA

**Descrição:**
`EnumerateSpriteFolders` suporta apenas 1 nível de profundidade de pastas. Estruturas com múltiplos níveis (ex: 0025/0000/0000/0002) não são processadas.

**Exemplo:** Pikachu (0025)
```
SpriteCollab/sprite/0025/
├── 0000/           → ✅ Processado como "0025"
│   ├── 0000/      → ❌ Não processado (2º nível)
│   │   └── 0002/  → ❌ Não processado (3º nível)
│   └── 0001/      → ❌ Não processado (2º nível)
├── 0006/           → ✅ Processado como "0025_0006"
└── 0007/           → ✅ Processado como "0025_0007"
```

**Impacto:**
- Formas ultra-específicas (sub-variantes) não são detectadas
- Gera 1 duplicata no pokemon_offsets_final.json (Pikachu 0025)
- FinalOffsets.Load() mantém última ocorrência (comportamento esperado)

**Solução Futura:**
Implementar recursão profunda no `EnumerateSpriteFolders` com formato:
- 1 nível: `0025` (base)
- 2 níveis: `0025_0006` (Cosplay)
- 3 níveis: `0025_0006_0001` (Cosplay variant A)
- 4 níveis: `0025_0006_0001_0002` (Cosplay variant A subtype)

**Prioridade:** Baixa (afeta apenas Pokémon com estruturas complexas, ~1-2% do total)

---

## 📝 Notas de Implementação

## 📝 Notas de Implementação

### Design Decisions

**UniqueId sem sufixo "_0000" para forma base:**
- Decisão: Forma base usa apenas `0025` em vez de `0025_0000`
- Razão: Mais limpo, menos verboso para o caso comum (90%+ dos Pokémon)
- Implementação: `PokemonVariant.UniqueId` property (linha 13)
- Impacto: JSONs raw ficam misturados (`pokemon_0025_raw.json` + `pokemon_0025_0006_raw.json`)

**Formato de arquivo:**
- Forma base: `pokemon_0025_raw.json` → UniqueId: `"0025"`
- Formas alternativas: `pokemon_0025_0006_raw.json` → UniqueId: `"0025_0006"`

**Loader behavior (FinalOffsets.Load):**
- Mantém última ocorrência em caso de duplicatas (`GroupBy(i => i.UniqueId).ToDictionary(g => g.Key, g => g.Last())`)
- Mesmo comportamento do formato antigo (DexNumber)
- Permite sobrescrever offsets carregando arquivo com ajustes manuais

**Arquivos gerados pelo pipeline (ignorados pelo git):**
- `Assets/Raw/pokemon_*_raw.json` - Metadata bruta por variante
- `Assets/Final/pokemon_offsets_runtime.json` - Offsets merged para runtime (1685 registros)
- `Assets/Final/pokemon_offsets_final.json` - Offsets do editor (1027 registros, pode ter ajustes manuais)
