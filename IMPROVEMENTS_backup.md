# 🐛 PROBLEMAS CONHECIDOS

Mescla dos problemas conhecidos com o roadmap para manter tudo em um lugar.

## ✅ Resolvidos

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

### **EnumerateSpriteFolders ignorava sprites na pasta raiz quando havia subpastas**

**Status:** ✅ Resolvido em commit `[hash]`

**Descrição:**
`SpriteDirectoryHelper.EnumerateSpriteFolders` retornava apenas subpastas de formas quando elas existiam, ignorando sprites na pasta raiz do Dex.

**Solução:**
Adicionada verificação `hasRootSprites` para incluir pasta raiz como forma "0000" antes de processar subpastas.

**Commit:** `[hash do próximo commit]`

---

### **Tratamento de erro silencioso em GameplayConfigLoader**

**Status:** ✅ Resolvido

**Descrição:**
O `catch` em `Pokebar.Core/Serialization/GameplayConfigLoader.cs` não logava falhas, dificultando diagnóstico em produção.

**Solução:**
Adicionado log via `Trace.TraceError` no `catch` ao carregar configurações.

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

### **Mojibake em docs e comentários**

**Status:** ABERTO

**Descrição:**
Há sinais de mojibake apesar do padrão de encoding declarado (ex.: `ENCODING_STANDARD.md`, `.editorconfig`, `Pokebar.Core/Serialization/FinalOffsets.cs`).

**Impacto:**
- Dificulta leitura e manutenção
- Indica inconsistência de encoding na pipeline de edição

---

### **Falta de testes automatizados**

**Status:** ABERTO

**Descrição:**
Não há projetos de testes no repositório (nenhum `*Test*` encontrado).

**Impacto:**
- Maior risco de regressões
- Dificulta evolução segura das fases do roadmap

---

### **Classes WPF muito carregadas**

**Status:** ABERTO

**Descrição:**
Classes como `Pokebar.DesktopPet/MainWindow.xaml.cs` concentram muita lógica.

**Impacto:**
- Aumenta acoplamento
- Manutenção e testes ficam mais difíceis

---

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

# 🚀 ROADMAP DE IMPLEMENTAÇÃO - ORDEM DE EXECUÇÃO

Melhorias reorganizadas em **fases sequenciais**, do alicerce até o lançamento. Cada fase prepara a próxima.

---

## **FASE 0: Fundação Técnica** ⚙️
*Pré-requisito para tudo. Sem isso, o resto vira retrabalho.*

- [x] **Encoding UTF-8 (evitar mojibake)** — Corrigir agora evita refazer configs/saves/UI depois.
- [x] **Padronização de nomes de sprites** — Define o "contrato" entre pipeline/editor/runtime.
- [x] **Suporte a formas/variantes em subpastas** — Estrutura de pastas que suporta os 1025 + variações sem gambiarra.
- [x] **Tudo data-driven (JSON)** — Move balanceamento/configurações pro JSON antes de crescer muito.
- [x] **Configurações hardcoded → arquivo de config** — Separa lógica de parâmetros (velocidade, spawn, escala, etc).

---

## **FASE 1: Infraestrutura Crítica** 🏗️
*Ferramentas que você usa todos os dias. Prioridade absoluta.*

- [x] **Tratamento de erros + logging estruturado** — Serilog com arquivos rolling. Você vai precisar disso JÁ.
- [x] **Logs rolling (por dia/tamanho)** — Parte do anterior. Evita logs gigantes.
- [x] **Captura global de exceções** — App não pode "sumir" sem trace. Catch no AppDomain + UI thread.
- [ ] **Overlay debug (ativável)** — Hitbox, FPS, estado, monitor. Debug 10x mais rápido.
- [ ] **Diagnóstico em 1 clique** — Gera zip com logs + config (sanitizado). Facilita suporte.

---

## **FASE 2: Runtime Core** 🎮
*Motor do jogo funcionando sólido.*

- [ ] **Limpeza de entidades inativas** — `EntityManager.RemoveInactive()` no loop. Evita leak de memória.
- [ ] **Consumo real de Pokéballs** — Inventário funcional + bloqueio de captura sem bola.
- [ ] **Loop com delta time (movimento consistente)** — Troca `DispatcherTimer` por delta time com `Stopwatch`.
- [ ] **Física/captura estável** — Simulação independente de FPS. Captura usa delta time.
- [ ] **Frames/sprites "congelados" (`Freeze`)** — `BitmapSource.Freeze()` em todos os frames carregados.

---

## **FASE 3: Performance** ⚡
*App leve e responsivo.*

- [ ] **Cache de `BitmapSource` (evitar recorte repetido)** — Dictionary<(dex, frame), BitmapSource> em memória.
- [ ] **Cache de animações para performance** — Guardar AnimationClip já processado por (dex, tipo).
- [ ] **Cache LRU (não carregar tudo)** — Limita cache a últimos N Pokémon usados.
- [ ] **Lazy loading de sprites não usados** — Carrega sprite só quando aparece/selecionado.
- [ ] **Carregamento assíncrono de assets** — `Task.Run` pra carregar sprites sem travar UI.
- [ ] **Throttling quando idle** — Reduz FPS se o pet está parado/sleepando.
- [ ] **Trocar `DispatcherTimer` por loop mais suave** — Usa `CompositionTarget.Rendering` pra animação mais fluida.

---

## **FASE 4: Persistência & Estado** 💾
*Salvar progresso e configurações.*

- [ ] **Persistência (save/load)** — Sistema completo: party, inventário, progresso, settings.
- [ ] **Localização (pt/en)** — Infraestrutura de i18n com `.resx` ou JSON. Começa com pt-BR.
- [ ] **Perfis (Trabalho/Jogo/Stream)** — Diferentes configs por contexto. Salva no settings.json.

---

## **FASE 5: Integração Windows** 🪟
*Comportamento nativo e polish.*

- [ ] **DPI/Scaling por monitor (nitidez real)** — `PerMonitorV2` awareness + ajuste de render.
- [ ] **Respeitar preferências do sistema** — Detecta modo alto contraste, reduzir movimento, etc.
- [ ] **Fullscreen melhor (com whitelist/blacklist)** — Lista configurável de apps que permitem pet em fullscreen.
- [ ] **Menu no tray (ícone perto do relógio)** — NotifyIcon com menu: trocar Pokémon, pausar, configs, sair.
- [ ] **Notificações toast** — Windows 10/11 notifications pra eventos importantes.
- [ ] **Atalhos globais** — Registra hotkeys com `RegisterHotKey` API.

---

## **FASE 6: UX & Produto** 🎨
*Features que fazem o app parecer "pronto".*

- [ ] **Onboarding (primeiro uso)** — Tutorial simples no primeiro boot.
- [ ] **UI de seleção de Pokémon (PC/Box)** — Window com grid de Pokémon capturados, troca o pet ativo com clique. Estilo Gen 3 PC Storage.
- [ ] **Configurações com preview ao vivo** — Window de settings com preview do pet respondendo.
- [ ] **Modo minimalista** — Reduz movimento, efeitos, frequência de updates.
- [ ] **Modo "Não perturbe"** — Regras: não aparecer em fullscreen, silenciar, etc.
- [ ] **Screenshot/GIF rápido** — Captura momento e salva na área de transferência.
- [ ] **Conquistas/badges e perfil** — Sistema leve de progresso com JSON local.

---

## **FASE 7: Conteúdo & Gameplay** 🎯
*Faz o app interessante a longo prazo.*

- [ ] **Expandir spawn pool para todos os 1025+ Pokémon** — Gerar automaticamente spawnWeights para todos os Pokémon disponíveis no SpriteCollab. Usar raridades baseadas em tiers (comum/incomum/raro/lendário).
- [ ] **Humor/amizade** — Valor simples que muda animações/reações.
- [ ] **Interações leves (sem estressar)** — Bocejar, deitar, olhar mouse, reagir a clique.
- [ ] **Movimento mais inteligente** — Evita áreas ruins, pontos de descanso, contexto.
- [ ] **Eventos por horário** — Spawns diferentes manhã/tarde/noite.
- [ ] **Shiny/raridade transparente** — Sistema de shiny com chance clara.
- [ ] **Missões rápidas** — Objetivos curtos com recompensas simples.
- [ ] **Packs/mods controlados** — Sistema de validação e carregamento de mods.

---

## **FASE 8: Visual "Gen 3 GBA"** 🎮
*Tema Pokémon autêntico.*

- [ ] **Fonte pixel (original/open) e texto aliased** — Import da fonte + `TextOptions.TextFormattingMode="Display"`.
- [ ] **Pixel perfect (Nearest neighbor + snap)** — `RenderOptions.BitmapScalingMode="NearestNeighbor"` + `SnapsToDevicePixels`.
- [ ] **Tema com paleta limitada (tokens)** — Define 8-12 cores fixas em ResourceDictionary.
- [ ] **UI em resolução base + escala inteira** — Desenha 160x144 (ou similar) e escala 2x/3x/4x.
- [ ] **Caixas de diálogo estilo Gen 3** — Custom Control com moldura pixelada.
- [ ] **9-slice nas molduras** — `BorderThickness` + `Image.Stretch="Fill"` com nine-patch.
- [ ] **Botões viram "itens de menu"** — ListBox custom com highlight e cursor.
- [ ] **Cursor piscando + typewriter text** — Animação de texto por char + cursor blinking.
- [ ] **Janelas sem chrome do Windows** — `WindowStyle="None"` + custom titlebar.
- [ ] **SFX de UI (select/cancel) próprios** — Sons curtos (.wav) com `MediaPlayer`.

---

## **FASE 9: Qualidade & Robustez** 🛡️
*Engenharia de software profissional.*

- [ ] **Testes unitários cirúrgicos** — xUnit nos componentes críticos: loader, combat, capture.
- [ ] **Golden tests do pipeline** — Compara output esperado vs real do pipeline.
- [ ] **Analyzers/nullable/style** — Ativa warnings como errors, nullable contexts.
- [ ] **CI/CD (build automático)** — GitHub Actions ou Azure DevOps: build + test + publish.
- [ ] **Crash reporting opcional (opt-in)** — Sentry ou similar, com consentimento.
- [ ] **Telemetria mínima e ética (opt-in)** — Apenas crash/perf, transparente.

---

## **FASE 10: Distribuição** 📦
*Lançamento público.*

- [ ] **MSIX** — Empacotamento moderno do Windows.
- [ ] **Assinatura de release** — Code signing certificate pra evitar SmartScreen.
- [ ] **Auto-update (fora da Store)** — Squirrel.Windows ou similar.
- [ ] **Beta/Stable** — Canais separados pra testar antes de lançar.

---

## **📊 RESUMO POR FASE**

| Fase | Itens | Tempo Estimado | Impacto |
|------|-------|----------------|---------|
| 0: Fundação | 5 | ✅ Completo | 🔴 Crítico |
| 1: Infraestrutura | 5 | ⚡ 3/5 completos | 🔴 Crítico |
| 2: Runtime Core | 5 | 1-2 semanas | 🔴 Crítico |
| 3: Performance | 7 | 2 semanas | 🟡 Alta |
| 4: Persistência | 3 | 1 semana | 🟡 Alta |
| 5: Windows | 6 | 2 semanas | 🟡 Alta |
| 6: UX/Produto | 7 | 2-3 semanas | 🟢 Média |
| 7: Conteúdo | 8 | 2-3 semanas | 🟢 Média |
| 8: Visual Gen 3 | 10 | 2-3 semanas | 🔵 Polish |
| 9: Qualidade | 6 | 2-3 semanas | 🔵 Polish |
| 10: Distribuição | 4 | 1 semana | 🔵 Launch |

**Total: 66 itens | 17-23 semanas (4-6 meses)**

---

## **🎯 MILESTONES SUGERIDOS**

- **M1: Base Sólida** (Fases 0-2) → App estável e debugável ⚡ Em progresso
- **M2: Performático** (Fase 3) → Roda suave em qualquer PC
- **M3: Funcional** (Fases 4-5) → Salva estado + integração Windows
- **M4: Atraente** (Fases 6-7) → UX completa + gameplay interessante
- **M5: Polished** (Fase 8) → Visual Gen 3 autêntico
- **M6: Profissional** (Fases 9-10) → Pronto pra lançamento público

---

## **💡 NOTAS DE IMPLEMENTAÇÃO**

### **UI de Seleção de Pokémon (PC/Box)**
- **FASE 6** - Após persistência estar implementada
- Window estilo Gen 3 PC Storage System
- Grid com sprites dos Pokémon capturados
- Filtros: por tipo, geração, favoritos
- Preview do sprite com animação
- Info: nome, nível, estatísticas básicas
- Botão para trocar o Pokémon ativo
- Integra com o sistema de save/load

### **Expandir Spawn Pool para 1025+ Pokémon**
- **FASE 7** - Após lazy loading/cache implementados
- Script automático para gerar spawnWeights de todos os Pokémon no SpriteCollab
- Sistema de raridade: Comum (60%), Incomum (25%), Raro (12%), Lendário (3%)
- Configurável por bioma/horário (futuro)
- Arquivo separado `pokemon_spawn_data.json` com metadados de cada Pokémon
- Permite override manual de pesos específicos no gameplay_config.json

### **Offsets automáticos (hitbox/ground)**
- **FASE 3-4** - Pipeline/Editor antes de Gameplay expandir
- Calcular bbox por frame (pixels opacos) e derivar hitbox encolhendo via fator configurável (ex: 0.9) + clamp mínimo.
- Ground offset = menor linha Y com pixel opaco (ou média dos últimos N) por animação; fallback manual continua valendo se existir.
- Salvar no JSON final/runtime por `UniqueId` + animação; Editor mostra preview e permite override/lock.
- Tratar pastas aninhadas (ex.: Pikachu fêmea) e ignorar frames vazios para não gerar NaN.

