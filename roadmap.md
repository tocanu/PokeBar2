# ðŸ§­ **ROADMAP COMPLETO DO PROJETO â€“ DESKTOP POKÃ‰MON PET**

### **C# .NET 8 â€“ WPF â€“ SpriteCollab Integration**
---

# 1. ðŸŽ¯ **VISÃƒO DO PROJETO**

O objetivo Ã© desenvolver um **Desktop Pet PokÃ©mon animado**, que vive na **barra de tarefas do Windows**, se movimenta, interage com Ã­cones, possui humor e personalidade, enfrenta PokÃ©mon selvagens, permite captura e possui interfaces de PokÃ©Mart, PokÃ©Center e PC PokÃ©mon.

O sistema utilizarÃ¡ os **sprites oficiais e completos do SpriteCollab**, cobrindo **todos os 1025 PokÃ©mon**, e garantirÃ¡ que todos funcionem corretamente mesmo com spritesheet incompletos, irregulares ou desalinhados.

O aplicativo deve ser **extremamente leve**, funcionando enquanto o usuÃ¡rio joga ou utiliza o PC, sem impacto perceptÃ­vel de performance.

---

# 2. ðŸ•¹ï¸ **FUNCIONALIDADES DO PRODUTO**

### 2.1 Mascote Animado

* Caminha na barra de tarefas (eixo X).
* Usa animaÃ§Ãµes de andar/idle/sleep/emote conforme disponÃ­vel.
* Interage com Ã­cones (empurra, senta, abre).
* Coleta moedas.
* Reage ao mouse (puxar, soltar, jogar).

### 2.2 Comportamento e Personalidade

* Estados de humor: feliz, cansado, com fome, irritado, entediado.
* Personalidades: tÃ­mido, travesso, preguiÃ§oso (afetando comportamentos).

### 2.3 Multi-Monitor

* Suporte total a mÃºltiplos monitores.
* Caminho contÃ­nuo entre telas.
* OpÃ§Ã£o para ordem invertida (ex.: monitor fÃ­sico 2 â†’ 1).

### 2.4 Encontros e Batalhas

* Spawns aleatÃ³rios de PokÃ©mon selvagens.
* Batalhas automÃ¡ticas usando tipos/efetividade.
* Jogador arrasta pokÃ©bolas para capturar.

### 2.5 Interfaces de Jogo

* PokÃ©Center (cura).
* PokÃ©Mart (loja).
* PC PokÃ©mon (organizaÃ§Ã£o e seleÃ§Ã£o).

---

# 3. ðŸ§© **DESAFIO TÃ‰CNICO CENTRAL**

Sprites do SpriteCollab:

* TÃªm **tamanhos diferentes** entre PokÃ©mon.
* Mudam de tamanho a cada frame.
* Mudam verticalmente entre animaÃ§Ãµes (saltos em Y).
* Alguns tÃªm **walk completo**, outros **somente idle**.
* Alguns nÃ£o tÃªm nem walk nem idle (single frame).
* As folhas seguem padrÃµes diferentes por espÃ©cie.

ðŸ‘‰ **Usar detecÃ§Ã£o automÃ¡tica pura = quebra animaÃ§Ã£o.**
ðŸ‘‰ **Configurar 1025 PokÃ©mon na mÃ£o = impossÃ­vel.**

---

# 4. ðŸ› ï¸ **SOLUÃ‡ÃƒO TÃ‰CNICA DEFINITIVA â€“ PIPELINE + EDITOR**

A soluÃ§Ã£o Ã© formada por **3 camadas**, todas integradas:

---

# 4.1 **CAMADA A â€“ Pipeline Console (automÃ¡tico)**

### Objetivo:

Processar **todos os sprites do SpriteCollab** automaticamente e gerar dados brutos Ãºteis.

### O pipeline farÃ¡:

1. **Ler cada PokÃ©mon** no diretÃ³rio do SpriteCollab.

2. Fazer parsing de:

   * `Walk-Anim.png`
   * `Idle-Anim.png`
   * `Sleep.png`
   * Emotes quando existirem

3. **Detectar tamanho dos frames (frameWidth, frameHeight)**

4. **Detectar grid da spritesheet (cols, rows)**

5. **Calcular Ground Offset (groundOffsetY):**

   * Para cada frame, ler de baixo pra cima atÃ© achar pixel com alpha > 0.
   * Pegar o maior ou mÃ©dia dos valores.

6. **Calcular centerOffsetX**

   * Centro horizontal baseado nos pixels visÃ­veis.

7. **Detectar bodyType sugerido:**

   * Baseado na altura Ãºtil:

     * `Small`, `Medium`, `Tall`, `Long`, `Flying`.

8. **Detectar lacunas:**

   * Tem walk? â†’ true/false
   * Tem idle? â†’ true/false
   * Nome dos arquivos reais
   * SugestÃ£o de fallback

9. **Gerar um JSON bruto** para cada PokÃ©mon:

   ```
   Assets/Raw/pokemon_025_raw.json
   ```

10. Logs automÃ¡ticos para casos incomuns (animaÃ§Ãµes estranhas).

---

# 4.2 **CAMADA B â€“ Editor WPF de RevisÃ£o (manual + rÃ¡pida)**

### Objetivo:

Corrigir offset vertical/horizontal e corpo **sem editar 1025 pokÃ©mon manualmente**, apenas ajustando os que precisam.

### O editor inclui:

* Lista de PokÃ©mon.
* Preview animado (idle/walk).
* Linha do â€œchÃ£oâ€ desenhada.
* Sliders:

  * `groundOffsetY`
  * `centerOffsetX`
* Combobox â€œbodyTypeâ€.
* BotÃ£o â€œAplicar esse offset/preset Ã  famÃ­lia evolutiva".
* BotÃ£o â€œPrÃ³ximo PokÃ©monâ€ (atalho com Enter).
* Marcar como revisado.

### SaÃ­da final:

Um arquivo:

```
Assets/Processed/pokemon_offsets_final.json
```

Contendo TODOS os PokÃ©mon com:

* frameWidth
* frameHeight
* groundOffsetY (definitivo)
* centerOffsetX
* bodyType final
* quais arquivos de sprite usar
* quais animaÃ§Ãµes fallback utilizar

### BenefÃ­cio

VocÃª revisa apenas ~15â€“25% dos pokÃ©mon (os problemÃ¡ticos).
Os demais ficam perfeitos sÃ³ com o pipeline automÃ¡tico.

---

# 4.3 **CAMADA C â€“ App Principal Normalizando Sprites (runtime)**

O app WPF usarÃ¡ apenas o arquivo final e nunca lidarÃ¡ com problemas como:

* sprite pulando verticalmente
* animaÃ§Ã£o mudando de altura
* pÃ©s flutuando
* tamanhos desiguais

### Runtime faz:

* Aplica offsets finais:

  ```
  drawX = worldX - centerOffsetX  
  drawY = worldY - frameHeight + groundOffsetY  
  ```
* O chÃ£o do app Ã© fixo â†’ todos os PokÃ©mon ficam nivelados.
* OrientaÃ§Ã£o: direita = normal, esquerda = flip horizontal.
* Fallbacks:

  * se nÃ£o tem walk â†’ usa idle como walk
  * se tem 1 frame â†’ bob animation automÃ¡tica
  * se nÃ£o tem idle â†’ usa frame de walk parado

---

# 5. ðŸ“… **ROADMAP COMPLETO (FINAL)**

Agora tudo integrado: visÃ£o, funcionalidades e pipeline.

---

# ðŸ”· **FASE 1 â€” CONCEPÃ‡ÃƒO E INFRAESTRUTURA** *(ConcluÃ­da)*

### Objetivo:

Criar base estrutural.

### EntregÃ¡veis:

* âœ… SoluÃ§Ã£o .NET 8 com 4 projetos (App, Core, ConsoleTool, EditorTool).
* âœ… Estrutura de diretÃ³rios dos assets.
* âœ… DefiniÃ§Ã£o de JSONs e modelos.
* âœ… Ambiente pronto para desenvolvimento.

---

# ðŸ”· **FASE 2 â€” PIPELINE DE ASSETS (CONSOLE)** *(Em andamento)*

### Objetivo:

Automatizar o processamento dos 1025 PokÃ©mon.

### EntregÃ¡veis:

* âœ… Varredura completa do SpriteCollab (1025 JSONs; placeholders para faltantes).
* âœ… DetecÃ§Ã£o de frames, grid, altura, pÃ©, centro (heurÃ­stica + offsets).
* âœ… DetecÃ§Ã£o de animaÃ§Ãµes disponÃ­veis.
* âœ… SugestÃ£o automÃ¡tica de bodyType.
* âœ… JSON bruto por PokÃ©mon.
* âœ… Logs de anomalias/erros e dex faltantes.
* âœ… DetecÃ§Ã£o preferencial 8 linhas (walk SpriteCollab), ajustes de grid dinÃ¢mico, offsets usando linhas 3/7 quando disponÃ­veis.
* âœ… Merge com offsets ajustados (Editor) em `Assets/Final/pokemon_offsets_runtime.json`.

---

# ðŸ”· **FASE 3 â€” EDITOR DE REVISÃƒO (WPF)**

### Objetivo:

Ajustar offsets ruins sem esforÃ§o manual massivo.

### EntregÃ¡veis:

* Editor com preview animado.
* Ajuste visual de groundOffset e centerOffset.
* AplicaÃ§Ã£o de presets e â€œcorrigir famÃ­lia inteiraâ€.
* MarcaÃ§Ã£o de status revisado.
* ExportaÃ§Ã£o de JSON final.
* Progresso atual:
* Preview Ãºnico recortando as linhas 3 e 7 do walk, com linha do chÃ£o ajustÃ¡vel.
* Sliders de ground/center offset (prÃ©-visualizaÃ§Ã£o local).
* Leitura automÃ¡tica de `Assets/Raw` e escolha de sprite por dex.
* BotÃµes de salvar ajuste atual, marcar revisado e exportar offsets finais.

---

# ðŸ”· **FASE 4 â€” MÃ“DULO DE RENDERIZAÃ‡ÃƒO E ANIMAÃ‡ÃƒO (APP)**

### Objetivo:

Renderizar qualquer PokÃ©mon perfeito na taskbar.

### EntregÃ¡veis:

* Engine de animaÃ§Ã£o (clips, players).
* Render leve e otimizado.
* AplicaÃ§Ã£o dos offsets finais.
* DireÃ§Ãµes esquerda/direita.
* Sistema de fallback robusto.

---

# ðŸ”· **FASE 5 â€” TASKBAR E MULTI-MONITOR**

### Objetivo:

Integrar pet ao ambiente real do Windows.

### EntregÃ¡veis:

* ServiÃ§o de Taskbar.
* ServiÃ§o de mÃºltiplos monitores.
* Eixo X global.
* Movimento contÃ­nuo.
* OpÃ§Ã£o de inversÃ£o (2â†’1).

---

# ðŸ”· **FASE 6 â€” BEHAVIOR SYSTEM**

### Objetivo:

Vida, humor, personalidade e interaÃ§Ãµes.

### EntregÃ¡veis:

* MÃ¡quina de estados.
* Humores e variaÃ§Ãµes de animaÃ§Ã£o.
* Personalidades com modificadores.
* InteraÃ§Ãµes com Ã­cones e mouse.

---

# ðŸ”· **FASE 7 â€” ENCONTROS, BATALHAS E CAPTURA**

### EntregÃ¡veis:

* Spawn de selvagens.
* Batalha automÃ¡tica.
* Efetividade por tipo.
* PokÃ©bola arrastÃ¡vel.
* Diferentes tipos de pokebolas
* Trocar pokemon do jogador por um que estÃ¡ no pc para ficar de idle.

---

# ðŸ”· **FASE 8 â€” TELAS: MART, CENTER, PC**

### EntregÃ¡veis:

* PokÃ©Mart com inventÃ¡rio.
* PokÃ©Center com cura.
* PC PokÃ©mon com filtro e seleÃ§Ã£o.

---

# ðŸ”· **FASE 9 â€” EVENTOS, MISSÃ•ES E BIOFÃ“RIA MULTI-MONITOR**

### EntregÃ¡veis:

* Biomas por monitor.
* Eventos raros (Ditto, Porygon etc).
* MissÃµes diÃ¡rias/semanais.

---

# ðŸ”· **FASE 10 â€” OTIMIZAÃ‡ÃƒO E RELEASE**

### EntregÃ¡veis:

* Cache inteligente de sprites.
* Descarte automÃ¡tico de bitmaps.
* Perf tuning (meta: 1â€“2% CPU).
* Build final + instalador.
