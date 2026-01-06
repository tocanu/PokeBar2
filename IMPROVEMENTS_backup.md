# ðŸš€ ROADMAP DE IMPLEMENTAÃ‡ÃƒO - ORDEM DE EXECUÃ‡ÃƒO

Melhorias reorganizadas em **fases sequenciais**, do alicerce atÃ© o lanÃ§amento. Cada fase prepara a prÃ³xima.

---

## **FASE 0: FundaÃ§Ã£o TÃ©cnica** âš™ï¸
*PrÃ©-requisito para tudo. Sem isso, o resto vira retrabalho.*

- [ ] **Encoding UTF-8 (evitar mojibake)** â€” Corrigir agora evita refazer configs/saves/UI depois.
- [ ] **PadronizaÃ§Ã£o de nomes de sprites** â€” Define o "contrato" entre pipeline/editor/runtime.
- [ ] **Suporte a formas/variantes em subpastas** â€” Estrutura de pastas que suporta os 1025 + variaÃ§Ãµes sem gambiarra.
- [ ] **Tudo data-driven (JSON)** â€” Move balanceamento/configuraÃ§Ãµes pro JSON antes de crescer muito.
- [ ] **ConfiguraÃ§Ãµes hardcoded â†’ arquivo de config** â€” Separa lÃ³gica de parÃ¢metros (velocidade, spawn, escala, etc).

---

## **FASE 1: Infraestrutura CrÃ­tica** ðŸ—ï¸
*Ferramentas que vocÃª usa todos os dias. Prioridade absoluta.*

- [ ] **Tratamento de erros + logging estruturado** â€” Serilog com arquivos rolling. VocÃª vai precisar disso JÃ.
- [ ] **Logs rolling (por dia/tamanho)** â€” Parte do anterior. Evita logs gigantes.
- [ ] **Captura global de exceÃ§Ãµes** â€” App nÃ£o pode "sumir" sem trace. Catch no AppDomain + UI thread.
- [ ] **Overlay debug (ativÃ¡vel)** â€” Hitbox, FPS, estado, monitor. Debug 10x mais rÃ¡pido.
- [ ] **DiagnÃ³stico em 1 clique** â€” Gera zip com logs + config (sanitizado). Facilita suporte.

---

## **FASE 2: Runtime Core** ðŸŽ®
*Motor do jogo funcionando sÃ³lido.*

- [ ] **Limpeza de entidades inativas** â€” `EntityManager.RemoveInactive()` no loop. Evita leak de memÃ³ria.
- [ ] **Consumo real de PokÃ©balls** â€” InventÃ¡rio funcional + bloqueio de captura sem bola.
- [ ] **Loop com delta time (movimento consistente)** â€” Troca `DispatcherTimer` por delta time com `Stopwatch`.
- [ ] **FÃ­sica/captura estÃ¡vel** â€” SimulaÃ§Ã£o independente de FPS. Captura usa delta time.
- [ ] **Frames/sprites "congelados" (`Freeze`)** â€” `BitmapSource.Freeze()` em todos os frames carregados.

---

## **FASE 3: Performance** âš¡
*App leve e responsivo.*

- [ ] **Cache de `BitmapSource` (evitar recorte repetido)** â€” Dictionary<(dex, frame), BitmapSource> em memÃ³ria.
- [ ] **Cache de animaÃ§Ãµes para performance** â€” Guardar AnimationClip jÃ¡ processado por (dex, tipo).
- [ ] **Cache LRU (nÃ£o carregar tudo)** â€” Limita cache a Ãºltimos N PokÃ©mon usados.
- [ ] **Lazy loading de sprites nÃ£o usados** â€” Carrega sprite sÃ³ quando aparece/selecionado.
- [ ] **Carregamento assÃ­ncrono de assets** â€” `Task.Run` pra carregar sprites sem travar UI.
- [ ] **Throttling quando idle** â€” Reduz FPS se o pet estÃ¡ parado/sleepando.
- [ ] **Trocar `DispatcherTimer` por loop mais suave** â€” Usa `CompositionTarget.Rendering` pra animaÃ§Ã£o mais fluida.

---

## **FASE 4: PersistÃªncia & Estado** ðŸ’¾
*Salvar progresso e configuraÃ§Ãµes.*

- [ ] **PersistÃªncia (save/load)** â€” Sistema completo: party, inventÃ¡rio, progresso, settings.
- [ ] **LocalizaÃ§Ã£o (pt/en)** â€” Infraestrutura de i18n com `.resx` ou JSON. ComeÃ§a com pt-BR.
- [ ] **Perfis (Trabalho/Jogo/Stream)** â€” Diferentes configs por contexto. Salva no settings.json.

---

## **FASE 5: IntegraÃ§Ã£o Windows** ðŸªŸ
*Comportamento nativo e polish.*

- [ ] **DPI/Scaling por monitor (nitidez real)** â€” `PerMonitorV2` awareness + ajuste de render.
- [ ] **Respeitar preferÃªncias do sistema** â€” Detecta modo alto contraste, reduzir movimento, etc.
- [ ] **Fullscreen melhor (com whitelist/blacklist)** â€” Lista configurÃ¡vel de apps que permitem pet em fullscreen.
- [ ] **Menu no tray (Ã­cone perto do relÃ³gio)** â€” NotifyIcon com menu: trocar PokÃ©mon, pausar, configs, sair.
- [ ] **NotificaÃ§Ãµes toast** â€” Windows 10/11 notifications pra eventos importantes.
- [ ] **Atalhos globais** â€” Registra hotkeys com `RegisterHotKey` API.

---

## **FASE 6: UX & Produto** ðŸŽ¨
*Features que fazem o app parecer "pronto".*

- [ ] **Onboarding (primeiro uso)** â€” Tutorial simples no primeiro boot.
- [ ] **ConfiguraÃ§Ãµes com preview ao vivo** â€” Window de settings com preview do pet respondendo.
- [ ] **Modo minimalista** â€” Reduz movimento, efeitos, frequÃªncia de updates.
- [ ] **Modo "NÃ£o perturbe"** â€” Regras: nÃ£o aparecer em fullscreen, silenciar, etc.
- [ ] **Screenshot/GIF rÃ¡pido** â€” Captura momento e salva na Ã¡rea de trabalho.
- [ ] **Conquistas/badges e perfil** â€” Sistema leve de progresso com JSON local.

---

## **FASE 7: ConteÃºdo & Gameplay** ðŸŽ¯
*Faz o app interessante a longo prazo.*

- [ ] **Humor/amizade** â€” Valor simples que muda animaÃ§Ãµes/reaÃ§Ãµes.
- [ ] **InteraÃ§Ãµes leves (sem estressar)** â€” Bocejar, deitar, olhar mouse, reagir a clique.
- [ ] **Movimento mais inteligente** â€” Evita Ã¡reas ruins, pontos de descanso, contexto.
- [ ] **Eventos por horÃ¡rio** â€” Spawns diferentes manhÃ£/tarde/noite.
- [ ] **Shiny/raridade transparente** â€” Sistema de shiny com chance clara.
- [ ] **MissÃµes rÃ¡pidas** â€” Objetivos curtos com recompensas simples.
- [ ] **Packs/mods controlados** â€” Sistema de validaÃ§Ã£o e carregamento de mods.

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
| 0: FundaÃ§Ã£o | 5 | 1 semana | ðŸ”´ CrÃ­tico |
| 1: Infraestrutura | 5 | 1 semana | ðŸ”´ CrÃ­tico |
| 2: Runtime Core | 5 | 1-2 semanas | ðŸ”´ CrÃ­tico |
| 3: Performance | 7 | 2 semanas | ðŸŸ¡ Alta |
| 4: PersistÃªncia | 3 | 1 semana | ðŸŸ¡ Alta |
| 5: Windows | 6 | 2 semanas | ðŸŸ¡ Alta |
| 6: UX/Produto | 6 | 2-3 semanas | ðŸŸ¢ MÃ©dia |
| 7: ConteÃºdo | 7 | 2-3 semanas | ðŸŸ¢ MÃ©dia |
| 8: Visual Gen 3 | 10 | 2-3 semanas | ðŸ”µ Polish |
| 9: Qualidade | 6 | 2-3 semanas | ðŸ”µ Polish |
| 10: DistribuiÃ§Ã£o | 4 | 1 semana | ðŸ”µ Launch |

**Total: 64 itens | 17-23 semanas (4-6 meses)**

---

## **ðŸŽ¯ MILESTONES SUGERIDOS**

- **M1: Base SÃ³lida** (Fases 0-2) â†’ App estÃ¡vel e debugÃ¡vel
- **M2: PerformÃ¡tico** (Fase 3) â†’ Roda suave em qualquer PC
- **M3: Funcional** (Fases 4-5) â†’ Salva estado + integraÃ§Ã£o Windows
- **M4: Atraente** (Fases 6-7) â†’ UX completa + gameplay interessante
- **M5: Polished** (Fase 8) â†’ Visual Gen 3 autÃªntico
- **M6: Profissional** (Fases 9-10) â†’ Pronto pra lanÃ§amento pÃºblico

