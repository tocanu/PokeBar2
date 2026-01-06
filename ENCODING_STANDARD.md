# 📝 Padrão de Encoding do Projeto

## ✅ Estado Atual

Todos os arquivos do projeto foram padronizados para usar **UTF-8**:

### Arquivos de Código (C# e XAML)
- **Encoding:** UTF-8 **com BOM** (Byte Order Mark)
- **Arquivos:** `*.cs`, `*.xaml`
- **Total:** 118 arquivos convertidos
- **Razão:** Compatibilidade com Visual Studio e ferramentas Microsoft

### Arquivos de Dados e Documentação
- **Encoding:** UTF-8 **sem BOM**
- **Arquivos:** `*.json`, `*.md`, `*.txt`
- **Total:** 1076 arquivos convertidos
- **Razão:** Padrão web e compatibilidade cross-platform

## 🔧 Configuração Automática

O arquivo `.editorconfig` na raiz do projeto garante que:
- Novos arquivos usem UTF-8 automaticamente
- Indentação seja consistente
- Finais de linha sejam CRLF (padrão Windows)
- Espaços em branco sejam removidos

## 🛡️ Prevenção de Problemas

### Antes (problemas potenciais):
- ❌ Mojibake em comentários (ã§ → ç)
- ❌ Encoding misto no projeto
- ❌ Problemas ao compartilhar código

### Depois (garantido):
- ✅ Todos os caracteres especiais funcionam (🎯, Pokémon, →)
- ✅ Encoding consistente em todo o projeto
- ✅ Compatibilidade com Git e IDEs modernas
- ✅ Sem problemas de internacionalização

## 📋 Checklist para Desenvolvedores

Ao criar novos arquivos:
- [ ] Verifique que o Visual Studio está configurado para UTF-8 com BOM (.cs/.xaml)
- [ ] Use UTF-8 sem BOM para JSON/MD (automático se usar .editorconfig)
- [ ] Não copie código de fontes com encoding desconhecido sem verificar
- [ ] Teste caracteres especiais em comentários/strings

## 🔍 Como Verificar

```powershell
# Verificar encoding de um arquivo específico
$bytes = [System.IO.File]::ReadAllBytes("arquivo.cs")
if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "UTF-8 com BOM ✅"
}
```

## ⚠️ Atenção

**NÃO modifique o encoding de:**
- Arquivos binários (`.png`, `.dll`, etc.)
- Pasta `SpriteCollab/` (mantém encoding original do repositório upstream)
- Arquivos `bin/` e `obj/` (gerados automaticamente)

---

**Status:** ✅ Completo
**Data:** Janeiro 2026
**Próxima revisão:** Não necessária (mantido pelo .editorconfig)
