# BotFoundry.Telegram — Architecture Document

> **Nota:** Este é o documento arquitetural do projeto. Ele descreve o problema, requisitos, estrutura e decisões de design de forma agnóstica — sem código ou detalhes de implementação. Para contratos de interface, estrutura de pastas e exemplos de uso, consulte o Design Doc.

---

## 1. Contexto e Problema

Desenvolvedores que produzem múltiplos bots Telegram em C# enfrentam um problema recorrente: a cada novo projeto, a mesma infraestrutura é reimplementada do zero — gerenciamento de sessão por usuário, roteamento de comandos, fluxos de conversa multi-step e controle de acesso administrativo.

Essa repetição gera três consequências diretas:

- **Ineficiência:** tempo de desenvolvimento consumido por código não-diferenciador
- **Inconsistência:** cada bot resolve o mesmo problema de forma ligeiramente diferente, aumentando a carga cognitiva ao alternar entre projetos
- **Fragilidade:** sem uma base comum testada, bugs de infraestrutura se repetem em todos os bots

**BotFoundry.Telegram** existe para eliminar esse problema: fornece a infraestrutura comum como uma biblioteca reutilizável, permitindo que o desenvolvedor foque exclusivamente na lógica de domínio de cada bot, amparado por uma base padronizada e resiliente.

---

## 2. Requisitos

### Funcionais

1. Rotear mensagens de comando (`/comando`) para handlers registrados
2. Gerenciar sessão individual por usuário — criar, recuperar e expirar
3. Executar fluxos de conversa multi-step com suporte a bifurcações condicionais
4. Controlar acesso administrativo por lista de identificadores de usuário
5. Expor ponto de extensão para integração com agentes de linguagem (LLM)
6. Permitir troca da estratégia de persistência de sessão sem alterar o código do bot

### Não-Funcionais

1. **Leveza:** footprint de memória e CPU compatível com hardware de baixo custo; escala vertical é responsabilidade da infraestrutura do consumidor
2. **Padronização:** todos os bots construídos com a lib seguem a mesma estrutura de entrada e fluxo
3. **Extensibilidade controlada:** o consumidor estende apenas comportamento de domínio; infraestrutura não é exposta
4. **Testabilidade:** handlers e steps de negócio devem ser testáveis sem dependência do SDK do Telegram

### Operacionais

1. Comunicação via long polling — sem necessidade de endpoint público
2. Um processo por bot — isolamento total entre projetos

### Fora do Escopo (v0.1)

1. Store de sessão externo nativo — a lib fornece `InMemorySessionStore`; implementações externas (Redis, SQL) são responsabilidade do consumidor via contrato plugável
2. Suporte a múltiplos bots no mesmo processo
3. Processamento de callbacks de botões inline, edições de mensagem e mídia enviada pelo usuário — planejados para versões futuras

---

## 3. Restrições e Premissas

### Restrições

Fatores externos que a arquitetura não controla mas deve respeitar:

1. **SDK do Telegram:** a lib depende do pacote `Telegram.Bot` para comunicação com a API do Telegram. Breaking changes entre versões major do SDK são um risco externo gerenciado pelo isolamento arquitetural (ver ADR-003)
2. **Ecossistema .NET:** a lib segue as convenções do ecossistema — injeção de dependência via `Microsoft.Extensions.DependencyInjection`, hosted services via `IHostedService`, e opções via `IOptions<T>`
3. **Long polling:** a comunicação com o Telegram é feita exclusivamente via polling. Ambientes que exigem webhook estão fora do suporte desta versão

### Premissas

Verdades assumidas sobre o contexto de uso que, se mudarem, podem exigir revisão arquitetural:

1. **Uso pessoal:** o consumidor primário é o próprio autor da lib — decisões de DX (Developer Experience) são otimizadas para esse perfil
2. **Um bot por processo:** cada instância da lib serve exatamente um bot com um token de acesso
3. **Mensagens de texto como input primário:** o fluxo de processamento é otimizado para mensagens textuais; outros tipos de input são tratados como secundários
4. **Sessão por `chatId`:** a identidade de um usuário é seu `chatId` do Telegram — não há autenticação externa ou federação de identidade

---

## 4. Visão Arquitetural

**BotFoundry.Telegram** é organizada em duas zonas com responsabilidades distintas e fronteiras explícitas.

### Zona 1 — Infraestrutura (gerenciada pela lib)

Conjunto fixo de módulos que operam de forma transparente ao consumidor. Não pode ser alterado, apenas configurado via opções declarativas no momento do registro.

| Módulo | Responsabilidade |
|---|---|
| **Hosting** | Inicializa e mantém a conexão com a API do Telegram via long polling |
| **Pipeline** | Orquestra o fluxo de processamento de cada mensagem recebida |
| **Sessão** | Resolve, cria e persiste o estado do usuário a cada interação |
| **Roteamento** | Identifica se a mensagem é um comando novo ou continuação de fluxo ativo |
| **Step Runner** | Executa os steps de um fluxo multi-step, gerenciando transições de estado |
| **Mensageria** | Abstrai o envio de respostas, isolando o SDK do Telegram do restante da lib |

### Zona 2 — Domínio (implementada pelo consumidor)

Conjunto de contratos públicos que o consumidor implementa para expressar a lógica de negócio do seu bot. A lib invoca esses contratos nos momentos adequados do pipeline.

| Contrato | Responsabilidade do consumidor |
|---|---|
| **Topic Handler** | Define o que acontece quando um comando é acionado |
| **Conversation Step** | Define uma etapa de um fluxo — o que perguntar, como validar, como processar |
| **Session Store** | Define onde e como a sessão é persistida |
| **Auth Middleware** | Define se um usuário tem permissão para continuar em um dado contexto |
| **LLM Bridge** | Define como repassar mensagens sem comando ativo para um agente externo |

### Princípio de fronteira

Nenhum tipo da Zona 1 é exposto na API pública da lib. A comunicação entre zonas acontece exclusivamente via contratos da Zona 2. Isso garante que o consumidor nunca depende de detalhes de implementação da infraestrutura — e que a lib pode evoluir internamente sem impacto nos bots existentes.

---

## 5. Fluxos Principais

### Fluxo 1 — Comando novo (`/comando`)

Acionado quando o usuário envia uma mensagem iniciada com `/`.

```
Mensagem recebida pelo Hosting
  → Pipeline filtra: é mensagem de texto? Não → descarta
  → Sessão é resolvida (existe?) ou criada (não existe?)
  → Sessão expirada? Sim → recria. Não → continua
  → BotContext é construído com sessão + mensagem
  → Auth Middleware (se configurado): permitido? Não → descarta
  → Roteamento detecta "/" → busca Topic Handler no registro
  → Handler encontrado? Não → ignora. Sim → inicia fluxo
  → Step Runner executa o primeiro step do handler
  → Resposta enviada via Mensageria
  → Sessão persistida com novo estado
```

### Fluxo 2 — Continuação de fluxo ativo (multi-step)

Acionado quando o usuário já está no meio de um fluxo iniciado anteriormente.

```
Mensagem recebida pelo Hosting
  → Pipeline filtra e resolve sessão (sessão já existe com estado ativo)
  → BotContext construído
  → Auth Middleware (se configurado)
  → Roteamento detecta topic ativo na sessão → delega ao Step Runner
  → Step Runner executa ValidateAsync: válido? Não → repete pergunta
  → Válido → ProcessAsync → Continue | Complete | JumpTo
  → Continue: avança para próximo step, executa AskAsync
  → Complete: limpa estado da sessão, fluxo encerrado
  → JumpTo: redireciona para step nomeado
  → Resposta enviada via Mensageria
  → Sessão persistida com estado atualizado
```

### Fluxo 3 — Mensagem sem comando e sem fluxo ativo (LLM Bridge)

Acionado quando o usuário envia texto livre sem contexto de fluxo ativo.

```
Mensagem recebida pelo Hosting
  → Pipeline filtra e resolve sessão (sem estado ativo)
  → BotContext construído
  → Auth Middleware (se configurado)
  → Roteamento: sem "/" e sem topic ativo → verifica LLM Bridge
  → LLM Bridge configurado? Não → descarta silenciosamente
  → Sim → consumidor repassa mensagem ao agente externo
  → Resposta enviada via Mensageria
  → Sessão persistida
```

---

## 6. ADRs (Architecture Decision Records)

### ADR-001 — Modelo de Pipeline

**Contexto:** A lib precisa processar mensagens de forma padronizada em todos os bots, mas permitir que o consumidor injete comportamento de negócio sem conhecer a infraestrutura interna.

**Opções consideradas:**
- Pipeline linear fixo — sem extensibilidade
- Chain de middlewares pura — extensibilidade total, alta complexidade para o consumidor
- Pipeline híbrido — infraestrutura fixa com ganchos opcionais de domínio

**Decisão:** Pipeline híbrido.

**Rationale:** O consumidor quer padronização, não flexibilidade infinita. A infraestrutura fixa elimina decisões repetitivas a cada bot; os ganchos cobrem os casos reais de extensão sem expor o pipeline interno.

**Trade-off aceito:** Comportamentos fora dos ganchos definidos exigem evolução da lib, não do consumidor.

**Gatilho de revisão:** Se surgir caso de uso legítimo que não se encaixa em nenhum gancho existente por duas vezes consecutivas.

---

### ADR-002 — Estratégia de Persistência de Sessão

**Contexto:** Bots de interação rápida não precisam de persistência durável; bots de conversa contínua precisam sobreviver a reinicializações. Ambos os casos coexistem no ecossistema do consumidor.

**Opções consideradas:**
- Sempre in-memory — simples, sem resiliência a reinicializações
- Sempre persistido — robusto, mas com overhead desnecessário para casos simples
- Plugável via contrato `ISessionStore`, com in-memory como padrão por convenção DI

**Decisão:** `ISessionStore` plugável; `InMemorySessionStore` registrado automaticamente apenas se nenhuma implementação foi registrada antes pelo consumidor.

**Rationale:** O consumidor escolhe a estratégia adequada para cada bot sem que a lib imponha overhead. A convenção DI é o padrão idiomático do ecossistema .NET.

**Trade-off aceito:** A lib não fornece implementação de store externo — o consumidor é responsável por implementar quando necessário.

**Gatilho de revisão:** Se o consumidor implementar store durável em dois ou mais bots, avaliar pacote complementar oficial.

---

### ADR-003 — Isolamento do SDK Telegram.Bot

**Contexto:** O pacote `Telegram.Bot` tem histórico de breaking changes entre versões major. Expor seus tipos na API pública amarraria o consumidor a uma versão específica e tornaria handlers não-testáveis.

**Opções consideradas:**
- Usar `Telegram.Bot` diretamente em toda a lib
- Isolar em dois componentes internos dedicados, expondo apenas abstrações próprias

**Decisão:** Apenas dois componentes internos de infraestrutura referenciam `Telegram.Bot`: o adapter de mensageria (`TelegramMessageSender`) e o adapter de polling (`TelegramPollingAdapter`). Todo o restante da lib e toda a API pública usam tipos agnósticos.

**Rationale:** Atualizações do SDK ficam contidas em dois pontos bem definidos. Handlers e steps do consumidor são 100% testáveis sem depender do Telegram.

**Trade-off aceito:** Funcionalidades avançadas do SDK precisam ser explicitamente expostas via contrato de mensageria — não estão disponíveis automaticamente.

**Gatilho de revisão:** Quando o consumidor precisar de capacidade do SDK não exposta pelo contrato de mensageria por duas vezes consecutivas.

---

### ADR-004 — Um Processo por Bot

**Contexto:** O consumidor opera múltiplos bots independentes, cada um com domínio, log e ciclo de vida próprios.

**Opções consideradas:**
- Suporte a múltiplos bots no mesmo processo
- Um processo por bot

**Decisão:** Um processo por bot. A lib não suporta múltiplos tokens ou clientes Telegram na mesma instância.

**Rationale:** Isolamento total entre bots — falha em um não afeta os demais. Deployment e ciclo de vida independentes. A complexidade de múltiplos bots no mesmo processo não tem retorno real para o caso de uso atual.

**Trade-off aceito:** Sem economia de recursos por colocação de processos.

**Gatilho de revisão:** Se o consumidor precisar colocar múltiplos bots no mesmo processo por restrição de recursos de hardware.

---

## 7. Fora do Escopo

Esta seção define explicitamente o que **BotFoundry.Telegram** não faz na v0.1. Seu propósito é evitar suposições incorretas sobre as capacidades da lib e orientar decisões de evolução futura.

### Fora do escopo permanente

Capacidades que contradizem decisões arquiteturais fundamentais e não serão incorporadas sem revisão de ADR:

1. **Múltiplos bots no mesmo processo** — viola o princípio de isolamento por processo (ADR-004)
2. **Exposição direta de tipos do SDK Telegram.Bot** — viola o isolamento de SDK (ADR-003)
3. **Webhook** — requer endpoint HTTP público, fora das restrições operacionais definidas

### Fora do escopo da v0.1 — planejados para versões futuras

Capacidades válidas e desejadas, adiadas por não serem essenciais ao núcleo da lib:

1. Processamento de callbacks de botões inline
2. Processamento de edições de mensagem
3. Processamento de mídia enviada pelo usuário (imagens, áudio, documentos)
4. Implementações nativas de store externo (Redis, SQLite) — entregues como pacotes complementares quando houver demanda recorrente

### Fora do escopo por design

Responsabilidades que pertencem ao domínio do consumidor, não à lib:

1. **Lógica de negócio de qualquer bot** — handlers, steps e regras de domínio são sempre responsabilidade do consumidor
2. **Integração interna com LLMs** — a lib expõe o gancho; a integração real é implementada pelo consumidor
3. **Autenticação externa** — a lib expõe o middleware de auth; a fonte de verdade é responsabilidade do consumidor
4. **Escala horizontal** — a lib é projetada para instância única; balanceamento e orquestração são responsabilidade da infraestrutura do consumidor
