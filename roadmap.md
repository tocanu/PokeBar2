# 🧭 **ROADMAP COMPLETO DO PROJETO – DESKTOP POKÉMON PET**

### **C# .NET 8 – WPF – SpriteCollab Integration**
---

# 1. 🎯 **VISÃO DO PROJETO**

O objetivo é desenvolver um **Desktop Pet Pokémon animado**, que vive na **barra de tarefas do Windows**, se movimenta, interage com ícones, possui humor e personalidade, enfrenta Pokémon selvagens, permite captura e possui interfaces de PokéMart, PokéCenter e PC Pokémon.

O sistema utilizará os **sprites oficiais e completos do SpriteCollab**, cobrindo **todos os 1025 Pokémon**, e garantirá que todos funcionem corretamente mesmo com spritesheet incompletos, irregulares ou desalinhados.

O aplicativo deve ser **extremamente leve**, funcionando enquanto o usuário joga ou utiliza o PC, sem impacto perceptível de performance.

---

# 2. 🕹️ **FUNCIONALIDADES DO PRODUTO**

### 2.1 Mascote Animado

* Caminha na barra de tarefas (eixo X).
* Usa animações de andar/idle/sleep/emote conforme disponível.
* Interage com ícones (empurra, senta, abre).
* Coleta moedas.
* Reage ao mouse (puxar, soltar, jogar).

### 2.2 Comportamento e Personalidade

* Estados de humor: feliz, cansado, com fome, irritado, entediado.
* Personalidades: tímido, travesso, preguiçoso (afetando comportamentos).

### 2.3 Multi-Monitor

* Suporte total a múltiplos monitores.
* Caminho contínuo entre telas.
* Opção para ordem invertida (ex.: monitor físico 2 → 1).

### 2.4 Encontros e Batalhas

* Spawns aleatórios de Pokémon selvagens.
* Batalhas automáticas usando tipos/efetividade.
* Jogador arrasta pokébolas para capturar.

### 2.5 Interfaces de Jogo

* PokéCenter (cura).
* PokéMart (loja).
* PC Pokémon (organização e seleção).

---

# 3. 🧩 **DESAFIO TÉCNICO CENTRAL**

Sprites do SpriteCollab:

* Têm **tamanhos diferentes** entre Pokémon.
* Mudam de tamanho a cada frame.
* Mudam verticalmente entre animações (saltos em Y).
* Alguns têm **walk completo**, outros **somente idle**.
* Alguns não têm nem walk nem idle (single frame).
* As folhas seguem padrões diferentes por espécie.

👉 **Usar detecção automática pura = quebra animação.**
👉 **Configurar 1025 Pokémon na mão = impossível.**

---

# 4. 🛠️ **SOLUÇÃO TÉCNICA DEFINITIVA – PIPELINE + EDITOR**

A solução é formada por **3 camadas**, todas integradas:

---

# 4.1 **CAMADA A – Pipeline Console (automático)**

### Objetivo:

Processar **todos os sprites do SpriteCollab** automaticamente e gerar dados brutos úteis.

### O pipeline fará:

1. **Ler cada Pokémon** no diretório do SpriteCollab.

2. Fazer parsing de:

   * `Walk-Anim.png`
   * `Idle-Anim.png`
   * `Sleep.png`
   * Emotes quando existirem

3. **Detectar tamanho dos frames (frameWidth, frameHeight)**

4. **Detectar grid da spritesheet (cols, rows)**

5. **Calcular Ground Offset (groundOffsetY):**

   * Para cada frame, ler de baixo pra cima até achar pixel com alpha > 0.
   * Pegar o maior ou média dos valores.

6. **Calcular centerOffsetX**

   * Centro horizontal baseado nos pixels visíveis.

7. **Detectar bodyType sugerido:**

   * Baseado na altura útil:

     * `Small`, `Medium`, `Tall`, `Long`, `Flying`.

8. **Detectar lacunas:**

   * Tem walk? → true/false
   * Tem idle? → true/false
   * Nome dos arquivos reais
   * Sugestão de fallback

9. **Gerar um JSON bruto** para cada Pokémon:

   ```
   Assets/Raw/pokemon_025_raw.json
   ```

10. Logs automáticos para casos incomuns (animações estranhas).

---

# 4.2 **CAMADA B – Editor WPF de Revisão (manual + rápida)**

### Objetivo:

Corrigir offset vertical/horizontal e corpo **sem editar 1025 pokémon manualmente**, apenas ajustando os que precisam.

### O editor inclui:

* Lista de Pokémon.
* Preview animado (idle/walk).
* Linha do "chão" desenhada.
* Sliders:

  * `groundOffsetY`
  * `centerOffsetX`
* Combobox "bodyType".
* Botão "Aplicar esse offset/preset à família evolutiva".
* Botão "Próximo Pokémon" (atalho com Enter).
* Marcar como revisado.

### Saída final:

Um arquivo:

```
Assets/Processed/pokemon_offsets_final.json
```

Contendo TODOS os Pokémon com:

* frameWidth
* frameHeight
* groundOffsetY (definitivo)
* centerOffsetX
* bodyType final
* quais arquivos de sprite usar
* quais animações fallback utilizar

### Benefício

Você revisa apenas ~15–25% dos pokémon (os problemáticos).
Os demais ficam perfeitos só com o pipeline automático.

---

# 4.3 **CAMADA C – App Principal Normalizando Sprites (runtime)**

O app WPF usará apenas o arquivo final e nunca lidará com problemas como:

* sprite pulando verticalmente
* animação mudando de altura
* pés flutuando
* tamanhos desiguais

### Runtime faz:

* Aplica offsets finais:

  ```
  drawX = worldX - centerOffsetX  
  drawY = worldY - frameHeight + groundOffsetY  
  ```
* O chão do app é fixo → todos os Pokémon ficam nivelados.
* Orientação: direita = normal, esquerda = flip horizontal.
* Fallbacks:

  * se não tem walk → usa idle como walk
  * se tem 1 frame → bob animation automática
  * se não tem idle → usa frame de walk parado

---

# 5. 📅 **ROADMAP COMPLETO (FINAL)**

Agora tudo integrado: visão, funcionalidades e pipeline.

---

# 🔷 **FASE 1 — CONCEPÇÃO E INFRAESTRUTURA** *(Concluída)*

### Objetivo:

Criar base estrutural.

### Entregáveis:

* ✅ Solução .NET 8 com 4 projetos (App, Core, ConsoleTool, EditorTool).
* ✅ Estrutura de diretórios dos assets.
* ✅ Definição de JSONs e modelos.
* ✅ Ambiente pronto para desenvolvimento.

---

# 🔷 **FASE 2 — PIPELINE DE ASSETS (CONSOLE)** *(Em andamento)*

### Objetivo:

Automatizar o processamento dos 1025 Pokémon.

### Entregáveis:

* ✅ Varredura completa do SpriteCollab (1025 JSONs; placeholders para faltantes).
* ✅ Detecção de frames, grid, altura, pé, centro (heurística + offsets).
* ✅ Detecção de animações disponíveis.
* ✅ Sugestão automática de bodyType.
* ✅ JSON bruto por Pokémon.
* ✅ Logs de anomalias/erros e dex faltantes.
* ✅ Detecção preferencial 8 linhas (walk SpriteCollab), ajustes de grid dinâmico, offsets usando linhas 3/7 quando disponíveis.
* ✅ Merge com offsets ajustados (Editor) em `Assets/Final/pokemon_offsets_runtime.json`.

---

# 🔷 **FASE 3 — EDITOR DE REVISÃO (WPF)**

### Objetivo:

Ajustar offsets ruins sem esforço manual massivo.

### Entregáveis:

* Editor com preview animado.
* Ajuste visual de groundOffset e centerOffset.
* Aplicação de presets e "corrigir família inteira".
* Marcação de status revisado.
* Exportação de JSON final.
* Progresso atual:
* Preview único recortando as linhas 3 e 7 do walk, com linha do chão ajustável.
* Sliders de ground/center offset (pré-visualização local).
* Leitura automática de `Assets/Raw` e escolha de sprite por dex.
* Botões de salvar ajuste atual, marcar revisado e exportar offsets finais.

---

# 🔷 **FASE 4 — MÓDULO DE RENDERIZAÇÃO E ANIMAÇÃO (APP)**

### Objetivo:

Renderizar qualquer Pokémon perfeito na taskbar.

### Entregáveis:

* Engine de animação (clips, players).
* Render leve e otimizado.
* Aplicação dos offsets finais.
* Direções esquerda/direita.
* Sistema de fallback robusto.

---

# 🔷 **FASE 5 — TASKBAR E MULTI-MONITOR**

### Objetivo:

Integrar pet ao ambiente real do Windows.

### Entregáveis:

* Serviço de Taskbar.
* Serviço de múltiplos monitores.
* Eixo X global.
* Movimento contínuo.
* Opção de inversão (2→1).

---

# 🔷 **FASE 6 — BEHAVIOR SYSTEM**

### Objetivo:

Vida, humor, personalidade e interações.

### Entregáveis:

* Máquina de estados.
* Humores e variações de animação.
* Personalidades com modificadores.
* Interações com ícones e mouse.

---

# 🔷 **FASE 7 — ENCONTROS, BATALHAS E CAPTURA**

### Entregáveis:

* Spawn de selvagens.
* Batalha automática.
* Efetividade por tipo.
* Pokébola arrastável.
* Diferentes tipos de pokebolas
* Trocar pokemon do jogador por um que está no pc para ficar de idle.

---

# 🔷 **FASE 8 — TELAS: MART, CENTER, PC**

### Entregáveis:

* PokéMart com inventário.
* PokéCenter com cura.
* PC Pokémon com filtro e seleção.

---

# 🔷 **FASE 9 — EVENTOS, MISSÕES E BIOFÓRIA MULTI-MONITOR**

### Entregáveis:

* Biomas por monitor.
* Eventos raros (Ditto, Porygon etc).
* Missões diárias/semanais.

---

# 🔷 **FASE 10 — OTIMIZAÇÃO E RELEASE**

### Entregáveis:

* Cache inteligente de sprites.
* Descarte automático de bitmaps.
* Perf tuning (meta: 1–2% CPU).
* Build final + instalador.
