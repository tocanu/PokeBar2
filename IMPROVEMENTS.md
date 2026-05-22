# ðŸ› PROBLEMAS CONHECIDOS

Mescla dos problemas conhecidos com o roadmap para manter tudo em um lugar.

## TODO CONSOLIDADO (FEATURES + CORRECOES)

Lista unificada de coisas a fazer, juntando:
- bugs/lacunas tecnicas atuais (P0/P1)
- backlog de produto enviado em 2026-02-19

## PLANO DE REFATORACAO (Q1 2026)

Objetivo: manter o que ja funciona, corrigir riscos de regressao e reduzir latencia percebida na UI (principalmente PC Box).

### Sprint 1 - Corretude e regressao (P0)

- [ ] Separar daily em dois marcos: `LastDailyGeneratedDate` e `LastDailyCompletedDate`.
- [ ] Corrigir expiracao de `Sleep` para limpar status ao fim do efeito.
- [ ] Corrigir `UiSfxService` para aplicar `Volume` dinamicamente e evitar truncamento de playback.
- [ ] Completar chaves de localizacao faltantes (`en-US` e `pt-BR`).
- [ ] Rodar reset diario durante runtime (app atravessando meia-noite).

### Sprint 2 - Performance perceptivel (PC Box primeiro)

- [ ] Instrumentar metrica de abertura da PC Box (tempo ate primeira pintura e tempo total de sprites).
- [ ] Abrir PC Box com placeholder imediato e carregar sprites de forma assincrona (`GetAnimationsAsync`).
- [ ] Preload da primeira pagina da party em background (apos boot, captura e troca de pokemon).
- [ ] Evitar rebuild completo da grade ao selecionar card (atualizar apenas item alterado).
- [ ] Meta de UX: PC Box < 120ms warm e < 300ms cold para primeira tela interativa.

### Sprint 3 - Arquitetura e manutencao

- [ ] Extrair responsabilidades de `MainWindow` para orchestrators dedicados (`SaveCoordinator`, `QuestRuntime`, `WindowServices`).
- [ ] Debounce/coalescing de `PerformSave()` para reduzir I/O sincronizado sem perder consistencia.
- [ ] Centralizar side-effects de estado para reduzir espalhamento de `_saveData = _saveData with`.
- [ ] Remover codigo morto e alinhar config/documentacao (frequencias, flags, defaults).

### Sprint 4 - UX/Config e operacao

- [ ] Controle de som completo em Settings (mute + volume), com padrao inicial desligado.
- [ ] Aplicar mudancas de tray/notificacao sem exigir restart quando tecnicamente possivel.
- [ ] Revisar textos hardcoded e padronizar localizacao (inclusive labels de tempo e historico).
- [ ] Fechar lacunas de testes para combate/captura/quests/PC Box.

---

## MULTI-MONITOR — BUGS & STATUS (atualizado 2026-05-22)

### Corrigidos nesta sessao

- [x] **[CRITICO] DPI errado em monitores secundarios** — `UpdateWindowPosition` e `PetWindow.UpdatePosition` usavam `physicalPx / monitorDpiScale` para calcular `Window.Left/Top`, o que e incorreto para PerMonitorV2. Fix: usa `PresentationSource.CompositionTarget.TransformFromDevice` que converte fisico → logico corretamente para o contexto DPI da propria janela.
- [x] **[ALTO] Monitores nao re-escaneados apos mudanca** — `InitializeTaskbars()` so era chamado em `OnLoaded`. Conectar/desconectar monitor ou mudar DPI deixava `_taskbars` obsoleto. Fix: hook `WM_DISPLAYCHANGE` (0x007E) no `HitTestHook` dispara `InitializeTaskbars()` + reposicionamento do pet via `Dispatcher.InvokeAsync`.
- [x] **[ALTO] `Process.GetProcessById` em cada janela por varredura de fullscreen** — abria handle de processo para cada janela visivel a cada `FullscreenCheckMs` ms. Fix: cache `pid → processName` com TTL de 10 s e limpeza periodica de 60 s em `FullscreenService`.

### Pendentes (backlog)

- [ ] **[MEDIO] Auto-hide global, nao por taskbar** — `IsAutoHideEnabled()` usa `ABM_GETSTATE` sem `hWnd`, retornando estado do taskbar primario. Taskbars secundarios podem ter auto-hide independente. Solucao: checar o estado via `hWnd` especifico de cada `Shell_SecondaryTrayWnd`. Arquivo: `TaskbarService.cs`.
- [ ] **[MEDIO] `GetNeighbor` assume layout horizontal** — taskbars sao ordenados por `BoundsPx.Left`; monitores empilhados verticalmente (mesmo `Left`) ficam em ordem nao deterministica e `toRight` nao tem sentido. Solucao: determinar eixo de travessia pelo vetor de velocidade + diferenca de bounds. Arquivo: `MainWindow.cs`.
- [ ] **[MEDIO] 1 frame de glitch visual ao trocar monitor** — `SetCurrentTaskbar()` atualiza `_pokemon.Y` mas nao chama `UpdateWindowPosition()` imediatamente, gerando 1 frame com DPI errado. Solucao: chamar `UpdateWindowPosition()` ao final de `SetCurrentTaskbar()`. Arquivo: `MainWindow.cs`.
- [ ] **[BAIXO] `TaskbarInfo.Bounds` e `GroundY` sao codigo morto** — `Bounds` (em DIPs) e computado em `TryBuildPrimaryTaskbar/TryBuildSecondaryTaskbar` mas nunca acessado no MainWindow. So `BoundsPx` e `GroundYPx` sao usados. Remover ou documentar intencao futura. Arquivo: `TaskbarService.cs`.
- [ ] **[BAIXO] Inimigo pode ficar fora de todos os bounds apos desconexao de monitor** — se o monitor do inimigo sumir, `GetEnemyTaskbar` cai no fallback para `_currentTaskbar` e `ShouldAllowOutside` pode devolver `false` com comportamento inesperado. Adicionar deteccao de "orfao" em `UpdateEnemyMovement` e teleportar para o taskbar do player. Arquivo: `MainWindow.cs`.

---

## VARREDURA DE BUGS — CHECKLIST CONTINUO

> Rodar esta lista apos cada sprint antes do commit. Adicionar novos itens conforme bugs sao encontrados.

### Corretude de estado
- [ ] Verificar se todos os `_saveData = _saveData with { ... }` sao seguidos de `PerformSave()` ou se ha janelas onde estado muda sem persistir.
- [ ] Auditar `RestoreFromSave()` de todos os servicos: garantir que campos novos adicionados ao `SaveData` tem valor default sano quando save e antigo (sem o campo).
- [ ] Checar que `CleanupDeadEnemies()` remove inimigos de `_enemyTaskbars` alem de `_enemyWindows` (ja faz — confirmar permanece correto em refatoracoes).

### Servicos e inicializacao
- [ ] Verificar que `_questService`, `_pokedexService`, `_levelService` e `_evolutionService` nunca sao acessados antes de `InitializeWindowsIntegration()` completar (race condition em `OnLoaded`).
- [ ] Confirmar que `UiSfxService.Dispose()` e chamado no `OnClosed` (hoje `_sfxService` nao esta no bloco de dispose de `OnClosed`).
- [ ] Auditar todos os eventos assinados com `+=` para verificar se tem `−=` correspondente no shutdown (memory leak em handlers de servicos).

### Multi-monitor (continuo)
- [ ] Testar `WM_DISPLAYCHANGE` com: (a) adicionar monitor, (b) remover monitor enquanto inimigo esta nele, (c) mudar DPI em runtime, (d) mudar resolucao.
- [ ] Validar `TransformFromDevice` quando o app e iniciado em monitor secundario (nao no primario).
- [ ] Checar comportamento quando `PresentationSource.FromVisual` retorna null (janela minimizada ou fora de arvore visual).

### Combate e captura
- [ ] Verificar que `Sleep` expira corretamente: `sleepLeft` chega a 0, `IsSkippedByStatus` retorna `false`, mas `enemy.ActiveStatus` ainda pode estar em `Sleep` apos o combate (nunca e limpo no `ResolveCombat` se o inimigo perdeu dormindo).
- [ ] Confirmar que `CombatResult.EnemyStatus` e aplicado corretamente a `enemy.ActiveStatus` para bonus de captura — e que status `Sleep` pos-combate aumenta chance mas nao bloqueia a animacao de capture ball.
- [ ] Auditar `CaptureManager.TryStartCapture`: `scale` passado vem de `taskbar?.DpiScale ?? 1.0` — mesma raiz do Bug 1; verificar se posicionamento da capture ball esta correto em DPI misto.

### Performance
- [ ] Medir tempo de `GetAllTaskbars()` no `WM_DISPLAYCHANGE` handler — se `EnumWindows` for lento em sistemas com muitas janelas, considerar debounce de 500 ms.
- [ ] Verificar que o `_pidCache` em `FullscreenService` nao cresce indefinidamente se processos de curta duracao criarem muitas entradas antes da limpeza.

### Testes automatizados faltando (prioridade alta)
- [ ] `CombatManager`: testar `SimulateRounds` com player muito mais forte (deve sempre vencer), empate (coin flip), e status Sleep expirando no turno certo.
- [ ] `QuestService`: testar `OnWalkTime` acumulando fracionario, `CheckDailyReset` na virada de dia, e `ClaimReward` em quest nao concluida (deve retornar null).
- [ ] `PokedexService`: testar `RestoreFromSave` com party implicando capturados, e `OnSeen`/`OnCaptured` com dex invalido (<= 0).
- [ ] `SaveManager`: testar fallback para backup quando save corrompido, e atomicidade (arquivo .tmp nao deve permanecer apos save bem-sucedido).
- [ ] `TaskbarService`: mockar as chamadas Win32 e testar `InferPosition` para taskbar em cada borda, e `GetAllTaskbars` com zero monitores.

---

### P0 - CORRIGIR AGORA (qualidade e confiabilidade)

- [x] Corrigir progresso de quest `WalkTime`: hoje usa cast para `int` em segundos fracionarios e quase sempre soma 0 (`QuestService.OnWalkTime`).
- [x] Corrigir troca de perfil nas Settings para salvar no perfil selecionado (evitar gravar no perfil antigo).
- [x] Integrar `ModLoader` ao runtime de sprites (hoje carrega mods, mas `SpriteLoader` nao usa caminho/mod override).
- [x] Aplicar `Enemy.AllowMonitorTravel` do config (hoje o controle esta hardcoded na window).
- [x] Resetar/regerar spawn pool dinamico quando o player trocar de Pokemon no PC Box.
- [x] Persistir `ActiveProfileId` e `TotalPets` corretamente no save.
- [x] Fechar lacunas de configuracao sem efeito real (`MinimalMode`, `RespectHighContrast`, `VsyncEnabled`, parte dos parametros de captura).
- [ ] [Alta] Separar controle de daily por `LastDailyGeneratedDate` e `LastDailyCompletedDate` para evitar reset/regeracao no mesmo dia sem conclusao (`QuestService`).
- [ ] [Alta] Limpar status `Sleep` ao expirar no combate para nao manter bonus indevido de captura (`CombatManager`/`CaptureManager`).
- [ ] [Media] Corrigir aplicacao de volume de SFX: alterar `Volume` deve regerar/atualizar cache de audio (`UiSfxService`).
- [ ] [Media] Corrigir risco de som truncado por descarte imediato de `MemoryStream`/`SoundPlayer` no `Play()` (`UiSfxService`).
- [ ] [Media] Adicionar chaves de localizacao faltantes em `locale/en-US.json` e `locale/pt-BR.json` (Pokedex, stats novas, history).
- [ ] [Media] Rodar `CheckDailyReset()` continuamente durante runtime (virada de dia com app aberto), nao so na inicializacao.
- [ ] [Baixa] Corrigir caractere invalido no icone de estatistica de derrotas em `SettingsWindow`.

### P1 - PROGRESSAO QUE DA VONTADE DE DEIXAR ABERTO

- [x] XP + nivel por lutar, capturar.
- [x] Evolucao (troca de especie por nivel/amizade/condicao).
- [x] Perfis de gameplay (`Trabalho`, `Jogo`, `Relax`) alterando somente configs.

### P1 - COMBATE E CAPTURA COM MAIS VARIEDADE (SEM NOVAS ANIMACOES)

- [x] Moves com cooldown usando a mesma animacao, mudando regra de dano/crit/efeito.
- [x] Status effects: sono, veneno, paralisia.
- [x] Overlay textual de status (`Zzz`, `⚡`, `☠`).
- [x] Efeitos visuais simples: hit flash, tremida, slow, tint de cor.
- [x] Captura mais justa: chance por HP, status, raridade e tipo de bola (visual igual, logica melhor).

### P2 - QUESTS / ACHIEVEMENTS / POKEDEX (METAGAME)

- [x] Daily quests com streak de dias.
- [x] Pokedex de `visto/capturado`.
- [x] Stats expandidas: lutas vencidas, capturas, distancia percorrida.
- [x] Historico/album das ultimas capturas com cards simples no PC Box.

### P2 - INTERACAO COM O USUARIO (SEM NOVOS ASSETS)

- [x] Click/drag do pet para reposicionar.
- [x] Carinho/treino (cliques aumentando amizade e alterando comportamento).
- [x] Balao de fala com texto/emoji (alerta de inimigo, humor, etc).
- [ ] Controle de som (mute/volume de SFX) nas Settings, com som desligado por padrao na primeira execucao.

### P1 - MELHORIAS DE CODIGO (REVIEW 2026-02-20)

- [ ] Reduzir delay de abertura da PC Box: abrir com placeholder + lazy/async load de sprites da pagina (`PcBoxWindow.ShowPage`/`ShowHistory`) e pre-warm da party.
- [ ] Respeitar `DesktopIconConfig.SaveLayoutBeforeRealMode` no `DesktopIconService` (hoje a flag existe mas nao e usada).
- [ ] Alinhar documentacao e implementacao da chance de interacao com icones (`DesktopIconConfig.Frequency` vs `DesktopIconService.RollFrequencyCheck`).
- [ ] Localizar strings hardcoded de UI/UX (ex.: frases de icones e `FormatTimeAgo`) para evitar mistura PT/EN fora do `Localizer`.
- [ ] Remover codigo morto/nao usado (`PlayerPet.BeginCombat`, `CombatHp`, `QuestService.DailyStreakUpdated`, `GetStreakBonus`, `timeAgo` em `PcBoxWindow`).
- [ ] Atualizar `Assets/gameplay_default.json` com secoes novas (`sfx`, `desktopIcons`, `quests.daily*`) para facilitar discoverability de config.
- [ ] Limpar configuracao stale de projeto (`Sounds/*.wav` no `.csproj` sem pasta/arquivos reais).

### P1 - INTERACAO COM ICONES DO DESKTOP (ESTILO DESKTOP GOOSE)

- [x] Adicionar config com 3 modos.
- [x] Modo `0` desligado: comportamento normal, sem interacao com icones.
- [x] Modo `A` arrasto fake (padrao recomendado): pet "pega" copia visual do icone em overlay e solta sem mover posicao real.
- [x] Modo `B` arrasto real: pet move posicao real do icone no desktop.
- [x] Seguranca para modo real: salvar layout anterior e botao `Restaurar layout`.
- [x] Seguranca para modo real: respeitar Focus/DND (nao mexer em reuniao/jogo).
- [x] Seguranca para modo real: detectar "Auto-organizar icones" e avisar/cair para fake.
- [x] Extras de UX: chance configuravel (`raramente`, `as vezes`, `sempre`).
- [x] Extras de UX: brincadeira curta (2-5s).
- [x] Extras de UX: blocklist de icones protegidos (ex.: Lixeira e atalhos criticos).

---

## VARREDURA COMPLETA DE BUGS — 2026-05-22

> Revisao de cada arquivo do projeto. Bugs com [x] ja foram corrigidos nesta sessao.

### CRITICO — P0

- [x] **`IdleBehaviorService.GetIdleThreshold` re-rola random a cada frame**
  - Arquivo: `Services/IdleBehaviorService.cs`
  - Problema: `GetIdleThreshold` era chamado a cada tick do Update, gerando um novo threshold aleatorio a cada frame. O pet nunca conseguia satisfazer `_idleTimer >= threshold` de forma confiavel — o threshold mudava antes de ser atingido, causando idle behaviors nunca dispararem.
  - Fix: Renomeado para `SampleIdleThreshold`, chamado uma vez ao resetar o timer e armazenado em campo `_idleThreshold`. O Update compara contra o campo fixo.

### ALTO — P1

- [x] **Drag-to-reposition aplica DPI scale duas vezes**
  - Arquivo: `MainWindow.xaml.cs` (OnPokemonMouseMove)
  - Problema: `dx = PointToScreen(...) delta` ja esta em pixels fisicos. Multiplicar por `DpiScale` novamente faz o pet se mover 1.5x mais rapido que o mouse em monitores 150% DPI.
  - Fix: Removida multiplicacao por `scale`; `_pokemon.X = _dragStartPokemonX + dx` (sem scale).

- [x] **`AnimationPlayer.Update` loop infinito se `FrameTime <= 0`**
  - Arquivo: `Animation/AnimationPlayer.cs`
  - Problema: `while (_elapsedTime >= _currentClip.FrameTime)` nunca termina se `FrameTime` for 0 ou negativo (config malformada ou sprite com 0 frames). Trava o app silenciosamente.
  - Fix: Guard adicionado antes do loop: `if (_currentClip.FrameTime <= 0) return;`

### MEDIO — P2

- [x] **`CaptureManager` descarta tempo de overrun na transicao de fase**
  - Arquivo: `Capture/CaptureManager.cs`
  - Problema: Ao transitar Travel→Absorb e Absorb→Shake, o codigo fazia `_active.Elapsed = 0`. Se a fase terminou com `Elapsed = faseDuration + 0.05s`, os 0.05s extras eram descartados. Para fases curtas (shake = 0.25s), isso gerava ~20% de erro no timing.
  - Fix: Carry forward: `_active.Elapsed = Math.Max(0, _active.Elapsed - faseDuration)`.

- [ ] **`MoodService._petCooldownTimer` nao persistido no save**
  - Arquivo: `Services/MoodService.cs`
  - Problema: Timer de cooldown de carinho reseta para 0 em cada restart. Permite dar carinho infinito apos reiniciar o app sem esperar o cooldown.
  - Fix sugerido: Adicionar `PetCooldownRemaining` ao `SaveData` e restaurar em `MoodService`.

- [ ] **`ProfileManager.CreateProfile` salva arquivo antes de adicionar entry na lista**
  - Arquivo: `Services/ProfileManager.cs`
  - Problema: Se o processo travar entre `SaveProfile(id, config)` e `settings.Profiles.Add(id)`, fica arquivo de config orfao sem entrada no manifesto.
  - Fix sugerido: Construir a lista atualizada em memoria antes de salvar; so persistir apos ambos estarem prontos.

- [ ] **`SpawnPoolBuilder` tem Pokemons listados em dois tiers simultaneamente**
  - Arquivo: `Services/SpawnPoolBuilder.cs`
  - Problema: #3, #6, #9, #12, #15 aparecem em `IsMiddleStage` E `IsFinalStage`. Funciona (IsFinalStage checado primeiro), mas os dados sao inconsistentes.
  - Fix sugerido: Remover entradas duplicadas de `IsMiddleStage` que sao final-evolutions.

### BAIXO — P3

- [ ] **`PlayerPet.BuildStat` usa apenas 5 buckets de stat para 1025 Pokemon**
  - Arquivo: `Entities/PlayerPet.cs`
  - Problema: `baseValue + level + (dex % 5)` — apenas 5 variacoes de stat para todo o roster. Pikachu #25 e Mewtwo #150 tem stats identicas no mesmo nivel.
  - Fix sugerido: Lookup em base_stats.json por dex, ou hash mais distribuido.

- [ ] **`CombatManager.ResolveCombat`: `TakeDamage(MaxHp)` e redundante**
  - Arquivo: `Combat/CombatManager.cs`
  - Problema: Apos `SetHp(0)`, chamar `TakeDamage(MaxHp)` e semanticamente confuso (parece dano, e na verdade force-faint).
  - Fix sugerido: Substituir por `enemy.SetHp(0); enemy.Faint();`.

- [ ] **Zero testes para logica de gameplay**
  - O projeto tem apenas 2 arquivos de teste reais. Nao ha nenhum teste para:
    - `CombatManager.SimulateRounds` (status, crit, empate)
    - `CaptureManager` (fases, chance de captura)
    - `QuestService` (reset diario, streak, claim)
    - `LevelService` (multiplos level-ups, XP overflow)
    - `AchievementService` (desbloqueio por threshold)
    - `SaveManager` (save/load/backup/corrupto)

---

## SUGESTOES DE FEATURES E MELHORIAS

### Gameplay de alto impacto

- [ ] **Sistema de tipos Pokemon** — Adicionar `Type` em `MoveDefinition` e tabela de efetividade simplificada (weak/neutral/resist/immune). Impacto enorme na profundidade do combate sem mudar arquitetura.

- [ ] **Base stats reais por Pokemon** — Substituir `BuildStat(..., dex % 5)` por lookup num JSON de base stats (HP/ATK/DEF por dex). Diferencia muito o combate entre Pokemon fracos e fortes.

- [ ] **Animacao de evolucao com flash** — Hoje a evolucao e troca de sprite silenciosa. Adicionar: piscar branco (HitFlash existente), SFX de evolucao, balao "EVOLUIU!".

- [ ] **Ciclo dia/noite afetando spawns** — Usar `DateTime.Now.Hour` para modificar spawn pool: Ghost/Dark types a noite (22h-6h), Fire/Normal de dia. Simples com o SpawnPoolBuilder existente.

- [ ] **Clima aleatorio** — A cada hora sortear um clima (Sol, Chuva, Neve) que aparece no balao e afeta mood/velocidade. Ex: Chuva = mood Sad +20%.

### UX e Janelas

- [ ] **Barra de HP durante combate** — `ProgressBar` acima do sprite do inimigo que decrementa a cada `RoundMessage`. O evento ja existe, so falta a UI.

- [ ] **Janela de Quests** — Nao ha forma de ver quests ativas na UI. Adicionar painel de quests (aba em Settings ou item de tray) com progresso e recompensa.

- [ ] **XP bar no tooltip do tray** — `LevelService` ja tem `CurrentXp` e `NextLevelXp`. Adicionar linha `XP: 48/100 ████░░` no tooltip existente.

- [ ] **Filtros da Pokedex por geracao** — Adicionar chips de geracao (Gen 1-9) mapeados por ranges de dex. Simples sem dados extras.

- [ ] **Historico de combates** — `List<CombatRecord>` no SaveData (dex vencido, resultado, timestamp). Mostrar como aba no PC Box similar ao historico de capturas.

### Performance e Arquitetura

- [ ] **Extirpar `MainWindow` como God Object** — Com ~2800 linhas, MainWindow faz spawn, combate, captura, UI, saves e movimento. Extrair orchestrators: `CombatCoordinator`, `SpawnCoordinator`, `SaveCoordinator`, `WindowManager`. Sprint 3 ja lista isso.

- [ ] **Versionamento de SaveData** — Adicionar `int SchemaVersion = 1` e migrations. Hoje campos novos usam defaults JSON silenciosamente, o que pode corruper estado ao atualizar o app.

- [ ] **Debounce do `PerformSave`** — Varios eventos chamam `PerformSave` individualmente (pet click, achievement, battle end). Debounce de 2s coalesceria em um unico I/O por rajada de eventos.

- [ ] **Metrica de hit rate do SpriteCache** — Adicionar contador de hits/misses logado periodicamente. Util para tunar `maxEntries` sem regredir performance.

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

- [x] **Fonte pixel (original/open) e texto aliased** — Import da fonte + `TextOptions.TextFormattingMode="Display"`.
- [x] **Pixel perfect (Nearest neighbor + snap)** — `RenderOptions.BitmapScalingMode="NearestNeighbor"` + `SnapsToDevicePixels`.
- [x] **Tema com paleta limitada (tokens)** — Define 8-12 cores fixas em ResourceDictionary.
- [x] **UI em resolução base + escala inteira** — Desenha 160x144 (ou similar) e escala 2x/3x/4x.
- [x] **Caixas de diálogo estilo Gen 3** — Custom Control com moldura pixelada.
- [x] **9-slice nas molduras** — `BorderThickness` + `Image.Stretch="Fill"` com nine-patch.
- [x] **Botões viram "itens de menu"** — ListBox custom com highlight e cursor.
- [x] **Cursor piscando + typewriter text** — Animação de texto por char + cursor blinking.
- [x] **Janelas sem chrome do Windows** — `WindowStyle="None"` + custom titlebar.
- [x] **SFX de UI (select/cancel) próprios** — Sons curtos (.wav) com `MediaPlayer`.

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
