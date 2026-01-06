# 🎨 Padrão de Nomenclatura de Sprites

## ✅ Estado Atual

Todos os nomes de arquivos de sprites foram **centralizados** na classe `SpriteFileNames` no `Pokebar.Core`.

## 📋 Nomes Padronizados

### Animações Principais
- **Walk:** `Walk-Anim.png` — Animação de caminhada
- **Idle:** `Idle-Anim.png` — Animação parada/respirando
- **Sleep:** `Sleep.png` — Pokémon dormindo

### Animações de Ataque (ordem de prioridade)
1. `Attack-Anim.png` — Ataque genérico
2. `Strike-Anim.png` — Golpe físico
3. `QuickStrike-Anim.png` — Golpe rápido
4. `MultiStrike-Anim.png` — Múltiplos golpes
5. `MultiScratch-Anim.png` — Múltiplos arranhões
6. `Scratch-Anim.png` — Arranhão simples

### Emotes e Animações Especiais
- `Hurt.png` — Recebendo dano
- `Charge.png` — Carregando ataque
- `Shoot.png` — Ataque à distância
- `Roar.png` — Rugindo
- `Swing.png` — Balançando
- `Double.png` — Ataque duplo
- `Bite.png` — Mordida
- `Pound.png` — Pancada
- `Hop.png` — Pulando
- `Appeal.png` — Performance
- `Dance.png` — Dançando
- `EventSleep.png` — Sleep especial de evento

### Variantes de Sleep
O sistema aceita múltiplas variantes de sleep (em ordem de prioridade):
1. `Sleep.png`
2. `Sleep-Anim.png`
3. `EventSleep.png`

## 🔧 Uso da Classe `SpriteFileNames`

### Constantes Básicas
```csharp
using Pokebar.Core.Sprites;

// Usar constantes em vez de strings hardcoded
var walkFile = FindFile(dir, SpriteFileNames.Walk);
var idleFile = FindFile(dir, SpriteFileNames.Idle);
var sleepFile = FindFile(dir, SpriteFileNames.Sleep);
```

### Arrays de Animações
```csharp
// Todas as animações de ataque (ordem de prioridade)
var attacks = SpriteFileNames.AttackAnimations;

// Todos os emotes
var emotes = SpriteFileNames.EmoteAnimations;

// Todas as variantes de sleep
var sleepVariants = SpriteFileNames.SleepVariants;
```

### Métodos Helper
```csharp
// Verificar tipo de animação
bool isMain = SpriteFileNames.IsMainAnimation("Walk-Anim.png"); // true
bool isAttack = SpriteFileNames.IsAttackAnimation("Strike-Anim.png"); // true
bool isEmote = SpriteFileNames.IsEmoteAnimation("Hurt.png"); // true

// Procurar melhor variante de sleep disponível
var files = Directory.GetFiles(pokemonDir);
var sleepFile = SpriteFileNames.FindSleepVariant(files);

// Procurar melhor animação de ataque disponível
var attackFile = SpriteFileNames.FindBestAttackAnimation(files);

// Listar todas as animações conhecidas
var allAnims = SpriteFileNames.AllAnimationFiles;
```

## 📦 Componentes Atualizados

### ✅ Pokebar.Core
- **Novo:** `Sprites/SpriteFileNames.cs` — Classe centralizada com constantes

### ✅ Pokebar.Pipeline
- **Atualizado:** `Program.cs` — Usa `SpriteFileNames` em vez de strings hardcoded
- FindFile(), FindFightFile(), mensagens de anomalias

### ✅ Pokebar.DesktopPet
- **Atualizado:** `Animation/SpriteLoader.cs` — Array `FightCandidates` usa `SpriteFileNames.AttackAnimations`

### ✅ Pokebar.Editor
- **Atualizado:** `MainWindow.xaml.cs` — Checks de nomes e ordem de sprites usam constantes

## 🎯 Benefícios

### Antes (problemas):
- ❌ Strings hardcoded espalhadas em 4 projetos
- ❌ Risco de typos ("Walk-Anim.png" vs "WalkAnim.png")
- ❌ Difícil adicionar novos tipos de animação
- ❌ Inconsistências entre pipeline/editor/runtime

### Depois (melhorias):
- ✅ **Single source of truth** — Um único lugar define os nomes
- ✅ **Type-safe** — Erros de compilação em vez de runtime
- ✅ **Fácil manutenção** — Adicionar novo tipo? Só editar `SpriteFileNames`
- ✅ **Consistência** — Pipeline, editor e runtime usam os mesmos nomes
- ✅ **Intellisense** — IDE sugere automaticamente os nomes corretos

## 🔍 Validação

```bash
# Compilar para verificar
dotnet build

# Testar pipeline
dotnet run --project Pokebar.Pipeline

# Testar editor
dotnet run --project Pokebar.Editor

# Testar aplicação
dotnet run --project Pokebar.DesktopPet
```

## 📝 Adicionar Nova Animação

Para adicionar um novo tipo de animação:

1. Editar `Pokebar.Core/Sprites/SpriteFileNames.cs`
2. Adicionar constante ou ao array apropriado
3. Recompilar — todas as partes usarão automaticamente

**Exemplo:**
```csharp
// Em SpriteFileNames.cs
public const string Jump = "Jump-Anim.png";

// Agora disponível em todo o projeto:
var jumpFile = FindFile(dir, SpriteFileNames.Jump);
```

## ⚠️ Regras

1. **NUNCA use strings hardcoded** para nomes de sprites
2. **SEMPRE use** `SpriteFileNames.X` ou os arrays
3. **Comparações** devem usar `StringComparison.OrdinalIgnoreCase`
4. **Novos tipos** devem ser adicionados primeiro no Core

---

**Status:** ✅ Completo  
**Data:** Janeiro 2026  
**Próxima ação:** Suporte a formas/variantes em subpastas
