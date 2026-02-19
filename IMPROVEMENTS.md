# ðŸ› PROBLEMAS CONHECIDOS

Mescla dos problemas conhecidos com o roadmap para manter tudo em um lugar.

## TODO CONSOLIDADO (FEATURES + CORRECOES)

Lista unificada de coisas a fazer, juntando:
- bugs/lacunas tecnicas atuais (P0/P1)
- backlog de produto enviado em 2026-02-19

### P0 - CORRIGIR AGORA (qualidade e confiabilidade)

- [ ] Corrigir progresso de quest `WalkTime`: hoje usa cast para `int` em segundos fracionarios e quase sempre soma 0 (`QuestService.OnWalkTime`).
- [ ] Fazer `DoNotDisturb` bloquear spawn e combate, nao apenas notificacoes.
- [ ] Corrigir troca de perfil nas Settings para salvar no perfil selecionado (evitar gravar no perfil antigo).
- [ ] Integrar `ModLoader` ao runtime de sprites (hoje carrega mods, mas `SpriteLoader` nao usa caminho/mod override).
- [ ] Aplicar `Enemy.AllowMonitorTravel` do config (hoje o controle esta hardcoded na window).
- [ ] Resetar/regerar spawn pool dinamico quando o player trocar de Pokemon no PC Box.
- [ ] Persistir `ActiveProfileId` e `TotalPets` corretamente no save.
- [ ] Fechar lacunas de configuracao sem efeito real (`MinimalMode`, `RespectHighContrast`, `VsyncEnabled`, parte dos parametros de captura).

### P1 - PROGRESSAO QUE DA VONTADE DE DEIXAR ABERTO

- [ ] XP + nivel por andar, lutar, capturar e quests.
- [x] Evolucao (troca de especie por nivel/amizade/condicao).
- [ ] "Shiny procedural" sem novo asset (tint/hue shift raro no sprite).
- [ ] Perfis de gameplay (`Trabalho`, `Jogo`, `Relax`) alterando somente configs.

### P1 - COMBATE E CAPTURA COM MAIS VARIEDADE (SEM NOVAS ANIMACOES)

- [ ] Moves com cooldown usando a mesma animacao, mudando regra de dano/crit/efeito.
- [ ] Status effects: sono, veneno, paralisia.
- [ ] Overlay textual de status (`Zzz`, `⚡`, `☠`).
- [ ] Efeitos visuais simples: hit flash, tremida, slow, tint de cor.
- [ ] Captura mais justa: chance por HP, status, raridade e tipo de bola (visual igual, logica melhor).

### P2 - QUESTS / ACHIEVEMENTS / POKEDEX (METAGAME)

- [ ] Daily quests com streak de dias.
- [ ] Pokedex de `visto/capturado`.
- [ ] Stats expandidas: lutas vencidas, capturas, distancia percorrida.
- [ ] Historico/album das ultimas capturas com cards simples no PC Box.

### P2 - INTERACAO COM O USUARIO (SEM NOVOS ASSETS)

- [ ] Click/drag do pet para reposicionar.
- [ ] Carinho/treino (cliques aumentando amizade e alterando comportamento).
- [ ] Balao de fala com texto/emoji (alerta de inimigo, humor, etc).

### P1 - INTERACAO COM ICONES DO DESKTOP (ESTILO DESKTOP GOOSE)

- [ ] Adicionar config com 3 modos.
- [ ] Modo `0` desligado: comportamento normal, sem interacao com icones.
- [ ] Modo `A` arrasto fake (padrao recomendado): pet "pega" copia visual do icone em overlay e solta sem mover posicao real.
- [ ] Modo `B` arrasto real: pet move posicao real do icone no desktop.
- [ ] Seguranca para modo real: salvar layout anterior e botao `Restaurar layout`.
- [ ] Seguranca para modo real: respeitar Focus/DND (nao mexer em reuniao/jogo).
- [ ] Seguranca para modo real: detectar "Auto-organizar icones" e avisar/cair para fake.
- [ ] Extras de UX: chance configuravel (`raramente`, `as vezes`, `sempre`).
- [ ] Extras de UX: brincadeira curta (2-5s).
- [ ] Extras de UX: blocklist de icones protegidos (ex.: Lixeira e atalhos criticos).

---
## âœ… Resolvidos

### **Conflito de indexaÃ§Ã£o: Pipeline suporta variantes, Editor/Runtime indexam sÃ³ por Dex**

**Status:** âœ… RESOLVIDO (commits 4b962a1, 045a68e)

**DescriÃ§Ã£o Original:**
O pipeline gerava variantes (0025_0006) mas Editor/Runtime usavam apenas Dex (int) como chave, causando colisÃµes.

**SoluÃ§Ã£o Implementada:**
- `OffsetAdjustment.DexNumber` (int) â†’ `UniqueId` (string)
- `Dictionary<int, OffsetAdjustment>` â†’ `Dictionary<string, OffsetAdjustment>`
- Todos os consumidores (Pipeline, Editor, App, DesktopPet) agora usam UniqueId
- Forma base sem sufixo: `0025` (mais limpo)
- Formas alternativas: `0025_0006`, `0025_0007`
- 1685 variantes Ãºnicas processadas sem colisÃµes

**Arquivos Atualizados:**
- âœ… `Pokebar.Core/Serialization/FinalOffsets.cs`
- âœ… `Pokebar.Pipeline/Program.cs`
- âœ… `Pokebar.Editor/MainWindow.xaml.cs`
- âœ… `Pokebar.App/MainWindow.xaml.cs`
- âœ… `Pokebar.DesktopPet/Animation/SpriteLoader.cs`
- âœ… `Pokebar.DesktopPet/Entities/BaseEntity.cs`
- âœ… `Pokebar.DesktopPet/Entities/PokemonPet.cs`

**Resultado:**
- âœ… pokemon_offsets_final.json e pokemon_offsets_runtime.json agora usam UniqueId (string)
- âœ… Editor pode ajustar offsets por forma individualmente
- âœ… Runtime carrega sprites e offsets corretos por variante
- âœ… 0 colisÃµes (exceto 1 duplicata conhecida: Pikachu 0025)

---

### **EnumerateSpriteFolders ignorava sprites na pasta raiz quando havia subpastas**

**Status:** âœ… Resolvido em commit `[hash]`

**DescriÃ§Ã£o:**
`SpriteDirectoryHelper.EnumerateSpriteFolders` retornava apenas subpastas de formas quando elas existiam, ignorando sprites na pasta raiz do Dex.

**SoluÃ§Ã£o:**
Adicionada verificaÃ§Ã£o `hasRootSprites` para incluir pasta raiz como forma "0000" antes de processar subpastas.

**Commit:** `[hash do prÃ³ximo commit]`

---

### **Tratamento de erro silencioso em GameplayConfigLoader**

**Status:** âœ… Resolvido

**DescriÃ§Ã£o:**
O `catch` em `Pokebar.Core/Serialization/GameplayConfigLoader.cs` nÃ£o logava falhas, dificultando diagnÃ³stico em produÃ§Ã£o.

**SoluÃ§Ã£o:**
Adicionado log via `Trace.TraceError` no `catch` ao carregar configuraÃ§Ãµes.

---

## âš ï¸ MÃ©dia Prioridade

### **Pastas Aninhadas de Variantes NÃ£o Suportadas**

**Status:** LIMITAÃ‡ÃƒO CONHECIDA

**DescriÃ§Ã£o:**
`EnumerateSpriteFolders` suporta apenas 1 nÃ­vel de profundidade de pastas. Estruturas com mÃºltiplos nÃ­veis (ex: 0025/0000/0000/0002) nÃ£o sÃ£o processadas.

**Exemplo:** Pikachu (0025)
```
SpriteCollab/sprite/0025/
â”œâ”€â”€ 0000/           â†’ âœ… Processado como "0025"
â”‚   â”œâ”€â”€ 0000/      â†’ âŒ NÃ£o processado (2Âº nÃ­vel)
â”‚   â”‚   â””â”€â”€ 0002/  â†’ âŒ NÃ£o processado (3Âº nÃ­vel)
â”‚   â””â”€â”€ 0001/      â†’ âŒ NÃ£o processado (2Âº nÃ­vel)
â”œâ”€â”€ 0006/           â†’ âœ… Processado como "0025_0006"
â””â”€â”€ 0007/           â†’ âœ… Processado como "0025_0007"
```

**Impacto:**
- Formas ultra-especÃ­ficas (sub-variantes) nÃ£o sÃ£o detectadas
- Gera 1 duplicata no pokemon_offsets_final.json (Pikachu 0025)
- FinalOffsets.Load() mantÃ©m Ãºltima ocorrÃªncia (comportamento esperado)

**SoluÃ§Ã£o Futura:**
Implementar recursÃ£o profunda no `EnumerateSpriteFolders` com formato:
- 1 nÃ­vel: `0025` (base)
- 2 nÃ­veis: `0025_0006` (Cosplay)
- 3 nÃ­veis: `0025_0006_0001` (Cosplay variant A)
- 4 nÃ­veis: `0025_0006_0001_0002` (Cosplay variant A subtype)

**Prioridade:** Baixa (afeta apenas PokÃ©mon com estruturas complexas, ~1-2% do total)

---

### **Mojibake em docs e comentÃ¡rios**

**Status:** ABERTO

**DescriÃ§Ã£o:**
HÃ¡ sinais de mojibake apesar do padrÃ£o de encoding declarado (ex.: `ENCODING_STANDARD.md`, `.editorconfig`, `Pokebar.Core/Serialization/FinalOffsets.cs`).

**Impacto:**
- Dificulta leitura e manutenÃ§Ã£o
- Indica inconsistÃªncia de encoding na pipeline de ediÃ§Ã£o

---

### **Falta de testes automatizados**

**Status:** EM PROGRESSO

**Descri??o:**
Foi criado o projeto `tests/Pokebar.Tests` com xUnit e testes cr?ticos iniciais (FinalOffsets, PokemonVariant, SpriteDirectoryHelper, SpriteSheetAnalyzer).

**Impacto:**
- Reduz risco de regress?es nos componentes centrais
- Ainda faltam testes para loaders/config e runtime

---

### **Classes WPF muito carregadas**

**Status:** ABERTO

**DescriÃ§Ã£o:**
Classes como `Pokebar.DesktopPet/MainWindow.xaml.cs` concentram muita lÃ³gica.

**Impacto:**
- Aumenta acoplamento
- ManutenÃ§Ã£o e testes ficam mais difÃ­ceis

---

## ðŸ“ Notas de ImplementaÃ§Ã£o

### Design Decisions

**UniqueId sem sufixo "_0000" para forma base:**
- DecisÃ£o: Forma base usa apenas `0025` em vez de `0025_0000`
- RazÃ£o: Mais limpo, menos verboso para o caso comum (90%+ dos PokÃ©mon)
- ImplementaÃ§Ã£o: `PokemonVariant.UniqueId` property (linha 13)
- Impacto: JSONs raw ficam misturados (`pokemon_0025_raw.json` + `pokemon_0025_0006_raw.json`)

**Formato de arquivo:**
- Forma base: `pokemon_0025_raw.json` â†’ UniqueId: `"0025"`
- Formas alternativas: `pokemon_0025_0006_raw.json` â†’ UniqueId: `"0025_0006"`

**Loader behavior (FinalOffsets.Load):**
- MantÃ©m Ãºltima ocorrÃªncia em caso de duplicatas (`GroupBy(i => i.UniqueId).ToDictionary(g => g.Key, g => g.Last())`)
- Mesmo comportamento do formato antigo (DexNumber)
- Permite sobrescrever offsets carregando arquivo com ajustes manuais

**Arquivos gerados pelo pipeline (ignorados pelo git):**
- `Assets/Raw/pokemon_*_raw.json` - Metadata bruta por variante
- `Assets/Final/pokemon_offsets_runtime.json` - Offsets merged para runtime (1685 registros)
- `Assets/Final/pokemon_offsets_final.json` - Offsets do editor (1027 registros, pode ter ajustes manuais)

# ðŸš€ ROADMAP DE IMPLEMENTAÃ‡ÃƒO - ORDEM DE EXECUÃ‡ÃƒO

Melhorias reorganizadas em **fases sequenciais**, do alicerce atÃ© o lanÃ§amento. Cada fase prepara a prÃ³xima.

---

## **FASE 0: FundaÃ§Ã£o TÃ©cnica** âš™ï¸
*PrÃ©-requisito para tudo. Sem isso, o resto vira retrabalho.*

- [x] **Encoding UTF-8 (evitar mojibake)** â€” Corrigir agora evita refazer configs/saves/UI depois.
- [x] **PadronizaÃ§Ã£o de nomes de sprites** â€” Define o "contrato" entre pipeline/editor/runtime.
- [x] **Suporte a formas/variantes em subpastas** â€” Estrutura de pastas que suporta os 1025 + variaÃ§Ãµes sem gambiarra.
- [x] **Tudo data-driven (JSON)** â€” Move balanceamento/configuraÃ§Ãµes pro JSON antes de crescer muito.
- [x] **ConfiguraÃ§Ãµes hardcoded â†’ arquivo de config** â€” Separa lÃ³gica de parÃ¢metros (velocidade, spawn, escala, etc).

---

## **FASE 1: Infraestrutura CrÃ­tica** ðŸ—ï¸
*Ferramentas que vocÃª usa todos os dias. Prioridade absoluta.*

- [x] **Tratamento de erros + logging estruturado** â€” Serilog com arquivos rolling. VocÃª vai precisar disso JÃ.
- [x] **Logs rolling (por dia/tamanho)** â€” Parte do anterior. Evita logs gigantes.
- [x] **Captura global de exceÃ§Ãµes** â€” App nÃ£o pode "sumir" sem trace. Catch no AppDomain + UI thread.
- [x] **Overlay debug (ativÃ¡vel)** â€” Hitbox, FPS, estado, monitor. Debug 10x mais rÃ¡pido.
- [x] **DiagnÃ³stico em 1 clique** â€” `DiagnosticService.GenerateDiagnosticZip()` gera zip no Desktop com system_info.txt + logs + gameplay.json + save.json. Flush Serilog antes de copiar.

---

## **FASE 2: Runtime Core** ðŸŽ®
*Motor do jogo funcionando sÃ³lido.*

- [x] **Limpeza de entidades inativas** â€” `CleanupDeadEnemies()` chama `RemoveInactive()` no loop + fecha windows de dead enemies.
- [x] **Consumo real de PokÃ©balls** â€” `TryConsumePokeball()` chamado antes da captura. Sem bola = sem captura. `StarterPokeballs` do config.
- [x] **Loop com delta time (movimento consistente)** â€” `Stopwatch` com resoluÃ§Ã£o ~1Î¼s + clamp a 100ms max. Substituiu `DateTime.Now`.
- [x] **FÃ­sica/captura estÃ¡vel** â€” `BaseSuccessRate` (50%) aplicado. Captura pode falhar, inimigo reaparece. Despawn timer 15s para fainted.
- [x] **Frames/sprites "congelados" (`Freeze`)** â€” `BitmapSource.Freeze()` jÃ¡ implementado em sheets e frames cropped.

---

## **FASE 3: Performance** âš¡
*App leve e responsivo.*

- [x] **Cache de `BitmapSource` (evitar decodificaÃ§Ã£o repetida)** â€” `SpriteLoader._bitmapCache` por path. Mesmo PNG nÃ£o Ã© decodificado duas vezes.
- [x] **Cache de animaÃ§Ãµes para performance** â€” `SpriteCache` com `PokemonAnimationSet` compartilhado entre entidades do mesmo dex.
- [x] **Cache LRU (nÃ£o carregar tudo)** â€” `SpriteCache` com eviction LRU e suporte a pin (player nunca Ã© evicted).
- [x] **Lazy loading de sprites nÃ£o usados** â€” Sprites carregados sob demanda no spawn. Cache reutiliza em respawns.
- [x] **Carregamento assÃ­ncrono de assets** â€” `SpriteCache.GetAnimationsAsync()` carrega sprites via `Task.Run` em background thread. Frames `Freeze()`d sÃ£o cross-thread safe. CoalesÃ§Ã£o de loads duplicados. Inimigos usam async no spawn.
- [x] **Throttling quando idle** â€” Reduz tick rate de 16ms (60fps) para 50ms (20fps) quando sem atividade.
- [x] **Trocar `DispatcherTimer` por loop mais suave** â€” `CompositionTarget.Rendering` com throttle inline via `GetCurrentTickInterval()`. Sincroniza com vsync do monitor. Removido `DispatcherTimer`.

---

## **FASE 4: PersistÃªncia & Estado** ðŸ’¾
*Salvar progresso e configuraÃ§Ãµes.*

- [x] **PersistÃªncia (save/load)** â€” `SaveData` (record) + `SaveManager` com JSON atÃ´mico. Save em `%AppData%/Pokebar/save.json` com backup automÃ¡tico. `PlayerPet.RestoreFromSave()` restaura estado no boot. Auto-save a cada 60s + save em captura/shutdown.
- [x] **EstatÃ­sticas acumuladas** â€” `PlayerStats` record rastreia TotalCaptured, TotalCaptureFailed, TotalBattles, TotalBattlesWon, TotalPokeballsUsed, TotalPlayTimeSeconds. Wired via `CombatManager.BattleEnded` e `CaptureManager.CaptureFailed/CaptureCompleted`.
- [x] **LocalizaÃ§Ã£o (pt/en)** â€” `Localizer` singleton JSON-based em `Pokebar.Core.Localization`. Detecta cultura do sistema ou `settings.json`. Fallback chain: cultura ativa â†’ en-US â†’ builtin â†’ chave literal. Arquivos `locale/pt-BR.json` e `locale/en-US.json`. `Localizer.Get("key", args)` com format. Integrado em `App.xaml.cs`.
- [x] **Perfis (Trabalho/Jogo/Stream)** â€” `AppSettings` record + `ProfileManager` com CRUD de perfis. Cada perfil Ã© um `gameplay_{id}.json` separado. "default" retrocompatÃ­vel com `gameplay.json`. `SwitchProfile()` retorna novo config. Perfis prÃ©-configurados: Default/Work/Stream. MainWindow carrega config via `ProfileManager.LoadActiveProfile()`.

---

## **FASE 5: IntegraÃ§Ã£o Windows** ðŸªŸ âœ… Complete
*Comportamento nativo e polish.*

- [x] **DPI/Scaling por monitor (nitidez real)** â€” `PerMonitorV2` awareness via `ApplicationHighDpiMode` no csproj + `app.manifest` com compatibility. ConfiguraÃ§Ã£o automÃ¡tica de DPI por monitor em runtime.
- [x] **Respeitar preferÃªncias do sistema** â€” `SystemPreferencesService` detecta alto contraste (`SystemParameters.HighContrast`) e reduced motion (`SPI_GETCLIENTAREAANIMATION`). Escuta `SystemEvents.UserPreferenceChanged` para mudanÃ§as em tempo real. Tick rate dobra quando reduced motion estÃ¡ ativo.
- [x] **Fullscreen melhor (com whitelist/blacklist)** â€” `FullscreenConfig` com modos: "hide" (padrÃ£o), "show" (sempre visÃ­vel), "whitelist" (pet aparece sÃ³ nesses apps), "blacklist" (pet some nesses apps). Detecta processo via `GetWindowThreadProcessId`. ConfigurÃ¡vel em `gameplay.json`.
- [x] **Menu no tray (Ã­cone perto do relÃ³gio)** â€” `TrayIconService` usa `System.Windows.Forms.NotifyIcon`. Menu: tÃ­tulo, pokeball count, pausar/retomar, diagnÃ³stico, sair. Double-click pausa/retoma. Suporte a Ã­cone customizado (`pokebar.ico`).
- [x] **NotificaÃ§Ãµes toast** â€” `NotificationService` usa balloon tips do tray. Eventos: captura sucesso/falha, batalha ganhou/perdeu, pause/resume. Responde a `ToastNotificationsEnabled` config.
- [x] **Atalhos globais** â€” `HotkeyService` com `RegisterHotKey`/`UnregisterHotKey` P/Invoke. WndProc via `HwndSource.AddHook`. Parser de strings ("Ctrl+Shift+P"). PadrÃ£o: Ctrl+Shift+P (pause), Ctrl+Shift+D (diagnÃ³stico). ConfigurÃ¡vel em `gameplay.json`.

---

## **FASE 6: UX & Produto** ðŸŽ¨ âœ…
*Features que fazem o app parecer "pronto".*

- [x] **Onboarding (primeiro uso)** â€” Tutorial simples no primeiro boot (OnboardingWindow com 3 pÃ¡ginas: Boas-vindas, Controles, Pronto).
- [x] **UI de seleÃ§Ã£o de PokÃ©mon (PC/Box)** â€” Window com grid de PokÃ©mon capturados, troca o pet ativo com clique. Hotkey Ctrl+Shift+B + menu tray.
- [x] **ConfiguraÃ§Ãµes com preview ao vivo** â€” Window de settings com preview do pet, tabs General/Behavior/Stats/Achievements.
- [x] **Modo minimalista** â€” Checkbox em Settings que reduz movimento e efeitos visuais.
- [x] **Modo "NÃ£o perturbe"** â€” Toggle no tray menu, bloqueia spawns, combates e notificaÃ§Ãµes.
- [x] **Screenshot/GIF rÃ¡pido** â€” Hotkey Ctrl+Shift+S captura frame e copia clipboard + salva PNG em %AppData%/Pokebar/Screenshots/.
- [x] **Conquistas/badges e perfil** â€” 10 achievements com condiÃ§Ãµes automÃ¡ticas, toast de notificaÃ§Ã£o, exibiÃ§Ã£o em Settings.

---

## **FASE 7: ConteÃºdo & Gameplay** ðŸŽ¯ âœ…
*Faz o app interessante a longo prazo.*

- [x] **Expandir spawn pool para todos os 1025+ PokÃ©mon** â€” `SpawnPoolBuilder` gera automaticamente pool dinÃ¢mico a partir dos offsets. Raridades por tier (common/uncommon/rare/legendary) com pesos configurÃ¡veis.
- [x] **Humor/amizade** â€” `MoodService` + `PlayerPet.Friendship` (0â€“255). Mood (Happy/Neutral/Sad/Sleepy) influencia animaÃ§Ãµes, velocidade e idle behaviors. Amizade sobe com captura/batalha/acariciar, desce ao perder.
- [x] **InteraÃ§Ãµes leves (sem estressar)** â€” `IdleBehaviorService` com animaÃ§Ãµes de Sit, Lay, Sleep, LookUp, Hop, Pose, Nod, Shock. Mood influencia escolha do comportamento. "Acariciar" via tray menu com cooldown.
- [x] **Movimento mais inteligente** â€” `SmartMovementService` com pausas periÃ³dicas (5â€“18s), desaceleraÃ§Ã£o nas bordas (100px), modificadores de velocidade por mood (Happy=1.15x, Sad=0.75x, Sleepy=0.6x).
- [ ] **Eventos por horÃ¡rio** â€” Spawns diferentes manhÃ£/tarde/noite. *(Descartado por escolha do usuÃ¡rio.)*
- [x] **Shiny/raridade transparente** â€” Chance 1/512 (configurÃ¡vel em `ShinyConfig`). Shiny flag no `EnemyPet`, notificaÃ§Ã£o toast ao aparecer, lista de shinies capturados em `SaveData`.
- [x] **MissÃµes rÃ¡pidas** â€” `QuestService` com 10 templates (captura, batalha, acariciar, pokÃ©balls, shiny). Auto-gera atÃ© 3 ativas, auto-claim com recompensas (pokÃ©balls, rare spawn, friendship boost).
- [x] **Packs/mods controlados** â€” `ModLoader` carrega mods de `%AppData%/Pokebar/mods/` com `manifest.json`. ValidaÃ§Ã£o de schema, limite de mods, sprint path overrides por dex.

---

## **FASE 8: Visual "Gen 3 GBA"** ðŸŽ®
*Tema PokÃ©mon autÃªntico.*

- [ ] **Fonte pixel (original/open) e texto aliased** â€” Import da fonte + `TextOptions.TextFormattingMode="Display"`.
- [ ] **Pixel perfect (Nearest neighbor + snap)** â€” `RenderOptions.BitmapScalingMode="NearestNeighbor"` + `SnapsToDevicePixels`.
- [ ] **Tema com paleta limitada (tokens)** â€” Define 8-12 cores fixas em ResourceDictionary.
- [ ] **UI em resoluÃ§Ã£o base + escala inteira** â€” Desenha 160x144 (ou similar) e escala 2x/3x/4x.
- [ ] **Caixas de diÃ¡logo estilo Gen 3** â€” Custom Control com moldura pixelada.
- [ ] **9-slice nas molduras** â€” `BorderThickness` + `Image.Stretch="Fill"` com nine-patch.
- [ ] **BotÃµes viram "itens de menu"** â€” ListBox custom com highlight e cursor.
- [ ] **Cursor piscando + typewriter text** â€” AnimaÃ§Ã£o de texto por char + cursor blinking.
- [ ] **Janelas sem chrome do Windows** â€” `WindowStyle="None"` + custom titlebar.
- [ ] **SFX de UI (select/cancel) prÃ³prios** â€” Sons curtos (.wav) com `MediaPlayer`.

---

## **FASE 9: Qualidade & Robustez** ðŸ›¡ï¸
*Engenharia de software profissional.*

- [ ] **Testes unitÃ¡rios cirÃºrgicos** â€” xUnit nos componentes crÃ­ticos: loader, combat, capture.
- [ ] **Golden tests do pipeline** â€” Compara output esperado vs real do pipeline.
- [ ] **Analyzers/nullable/style** â€” Ativa warnings como errors, nullable contexts.
- [ ] **CI/CD (build automÃ¡tico)** â€” GitHub Actions ou Azure DevOps: build + test + publish.
- [ ] **Crash reporting opcional (opt-in)** â€” Sentry ou similar, com consentimento.
- [ ] **Telemetria mÃ­nima e Ã©tica (opt-in)** â€” Apenas crash/perf, transparente.

---

## **FASE 10: DistribuiÃ§Ã£o** ðŸ“¦
*LanÃ§amento pÃºblico.*

- [ ] **MSIX** â€” Empacotamento moderno do Windows.
- [ ] **Assinatura de release** â€” Code signing certificate pra evitar SmartScreen.
- [ ] **Auto-update (fora da Store)** â€” Squirrel.Windows ou similar.
- [ ] **Beta/Stable** â€” Canais separados pra testar antes de lanÃ§ar.

---

## **ðŸ“Š RESUMO POR FASE**

| Fase | Itens | Tempo Estimado | Impacto |
|------|-------|----------------|---------|
| 0: FundaÃ§Ã£o | 5 | âœ… Completo | ðŸ”´ CrÃ­tico |
| 1: Infraestrutura | 5 | âœ… Completo | ðŸ”´ CrÃ­tico |
| 2: Runtime Core | 5 | âœ… Completo | ðŸ”´ CrÃ­tico |
| 3: Performance | 7 | âœ… Completo | ðŸŸ¡ Alta |
| 4: PersistÃªncia | 4 | âœ… Completo | ðŸ”´ CrÃ­tico |
| 7: ConteÃºdo | 8 | âœ… Completo (7/8) | ðŸŸ¢ MÃ©dia |
| 8: Visual Gen 3 | 10 | 2-3 semanas | ðŸ”µ Polish |
| 9: Qualidade | 6 | 2-3 semanas | ðŸ”µ Polish |
| 10: DistribuiÃ§Ã£o | 4 | 1 semana | ðŸ”µ Launch |

**Total: 66 itens | 17-23 semanas (4-6 meses)**

---

## **ðŸŽ¯ MILESTONES SUGERIDOS**

- **M1: Base SÃ³lida** (Fases 0-2) â†’ App estÃ¡vel e debugÃ¡vel âœ… Completo
- **M2: PerformÃ¡tico** (Fase 3) â†’ Roda suave em qualquer PC âœ… Completo
- **M3: Funcional** (Fases 4-5) â†’ Salva estado + integraÃ§Ã£o Windows âš¡ FASE 4 completa
- **M4: Atraente** (Fases 6-7) â†’ UX completa + gameplay interessante
- **M5: Polished** (Fase 8) â†’ Visual Gen 3 autÃªntico
- **M6: Profissional** (Fases 9-10) â†’ Pronto pra lanÃ§amento pÃºblico

---

## **ðŸ’¡ NOTAS DE IMPLEMENTAÃ‡ÃƒO**

### **UI de SeleÃ§Ã£o de PokÃ©mon (PC/Box)**
- **FASE 6** âœ… - Completa
- Window estilo Gen 3 PC Storage System
- Grid com sprites dos PokÃ©mon capturados
- Filtros: por tipo, geraÃ§Ã£o, favoritos
- Preview do sprite com animaÃ§Ã£o
- Info: nome, nÃ­vel, estatÃ­sticas bÃ¡sicas
- BotÃ£o para trocar o PokÃ©mon ativo
- Integra com o sistema de save/load

### **Expandir Spawn Pool para 1025+ PokÃ©mon**
- **FASE 7** - ApÃ³s lazy loading/cache implementados
- Script automÃ¡tico para gerar spawnWeights de todos os PokÃ©mon no SpriteCollab
- Sistema de raridade: Comum (60%), Incomum (25%), Raro (12%), LendÃ¡rio (3%)
- ConfigurÃ¡vel por bioma/horÃ¡rio (futuro)
- Arquivo separado `pokemon_spawn_data.json` com metadados de cada PokÃ©mon
- Permite override manual de pesos especÃ­ficos no gameplay_config.json

### **Offsets automÃ¡ticos (hitbox/ground)**
- **FASE 3-4** - Pipeline/Editor antes de Gameplay expandir
- Calcular bbox por frame (pixels opacos) e derivar hitbox encolhendo via fator configurÃ¡vel (ex: 0.9) + clamp mÃ­nimo.
- Ground offset = menor linha Y com pixel opaco (ou mÃ©dia dos Ãºltimos N) por animaÃ§Ã£o; fallback manual continua valendo se existir.
- Salvar no JSON final/runtime por `UniqueId` + animaÃ§Ã£o; Editor mostra preview e permite override/lock.
- Tratar pastas aninhadas (ex.: Pikachu fÃªmea) e ignorar frames vazios para nÃ£o gerar NaN.


