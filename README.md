#  **ISPing – Monitoramento de Latência e Ferramentas para ISPs**

![image](https://github.com/user-attachments/assets/b7b0bd79-9929-4ec0-8a35-65e68dc3d53d)

![image](https://github.com/user-attachments/assets/49b464bd-1f87-4dff-82de-02e753e22488)



# ISPing

**Monitor de rede em tempo real para Windows** — Monitore latência, velocidade de rede, mudanças de IP e muito mais diretamente na bandeja do sistema.

![Windows](https://img.shields.io/badge/Windows-0078D6?style=flat&logo=windows&logoColor=white)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green.svg)

---

## Visão Geral

O **ISPing** é uma aplicação leve que reside na bandeja do sistema (system tray) do Windows e fornece monitoramento contínuo da sua conexão de rede. 
 
O ícone da bandeja exibe a latência atual em tempo real, com cores indicando a qualidade da conexão.


---

## Funcionalidades

### Monitoramento de Ping

- **Ping ICMP ou TCP** — Escolha entre ping ICMP tradicional ou TCP para alvos que bloqueiam ICMP
- **Alvos pré-configurados** — Google (8.8.8.8), Cloudflare (1.1.1.1) ou endereço personalizado
- **Intervalos configuráveis** — 1, 3 ou 10 segundos entre pings
- **Exibição no ícone** — Mostra último ping ou média dos últimos 3 pings

### Latency Hound

Sistema inteligente de detecção de variações de latência:

- **Detecção automática** — Monitora variações significativas na latência
- **Tracert automático** — Executa traceroute quando detecta mudanças
- **Histórico** — Armazena e exibe histórico de tracerts
- **Configurável** — Ajuste o threshold de variação e cooldown entre scans

### Monitor de Velocidade de Rede

- **Upload e Download** — Monitora velocidade em tempo real
- **Jitter** — Calcula variação de latência
- **Janela flutuante** — Exibe informações sempre visíveis na tela
- **Seleção de interface** — Escolha qual adaptador de rede monitorar

### Estatísticas Detalhadas

- **Latência mínima, máxima e média**
- **Desvio padrão**
- **Taxa de perda de pacotes**
- **Total de pings realizados**
- **Exportação para CSV e JSON**

### Sistema de Alertas

Receba notificações quando:

- Latência ultrapassar um limite configurável
- Ocorrerem falhas consecutivas de ping
- Seu IP público mudar

### Monitoramento de Rotas

- **Detecção de mudanças de rota** — Alerta quando o caminho de rede muda
- **Log de alterações** — Mantém histórico de mudanças de rota
- **Visualizador de logs** — Interface para consultar logs

### Scanner de Portas

- **Scan rápido** — Verifica portas comuns no alvo atual
- **Portas identificadas** — Mostra nome do serviço (HTTP, SSH, RDP, etc.)
- **Seleção rápida** — Clique na porta para usar no ping TCP

### Informações de Rede

- **IP Privado (IPv4 e IPv6)**
- **IP Público (IPv4 e IPv6)**
- **Endereço MAC**
- **Servidor DNS em uso**

### Monitor de Área de Transferência

- Detecta automaticamente quando você copia um endereço IP
- Oferece opção de abrir janela de ping flutuante para o IP copiado

### Janelas Flutuantes de Ping

- Janelas always-on-top para monitorar múltiplos alvos
- Auto-fechamento configurável (3s, 10s, 30s ou nunca)

### Requisitos

- Windows 10/11
- .NET 9.0 Runtime

## Como Usar

1. Execute o `ISPing.exe`
2. O ícone aparecerá na bandeja do sistema
3. Clique com o **botão direito** para acessar o menu de opções
4. Clique com o **botão esquerdo** para copiar a latência atual

### Menu de Contexto

| Opção | Descrição |
|-------|-----------|
| **Alvo do Ping** | Seleciona o destino do ping |
| **Tipo de Ping** | Alterna entre ICMP e TCP |
| **Escanear Portas** | Verifica portas abertas no alvo |
| **Intervalo** | Define frequência do ping |
| **Exibição do Ping** | Último ping ou média |
| **Monitorar IPs na Área de Transferência** | Detecta IPs copiados |
| **Monitorar Velocidade de Rede/Jitter** | Ativa monitor de velocidade |
| **Logar/Monitorar Mudanças de Rota** | Ativa monitoramento de rotas |
| **Latency Hound** | Configura detecção de variações |
| **Ver Estatísticas** | Exibe estatísticas detalhadas |
| **Configurar Alertas** | Define thresholds de alerta |
| **Exportar Dados** | Salva dados em CSV ou JSON |

---

### Configurações Salvas

- Alvo do ping
- Intervalo entre pings
- Tipo de ping (ICMP/TCP)
- Porta TCP personalizada
- Estado dos monitores
- Configurações de alertas
- Configurações do Latency Hound

---

## Logs

Os logs são armazenados em:

```
%APPDATA%\ISPing\ISPingAppEvents.log    # Eventos da aplicação
%APPDATA%\ISPing\ISPingRouteChanges.log # Mudanças de rota
```

---

## Tecnologias Utilizadas

- **C# / .NET 9.0**
- **Windows Forms**
- **System.Net.NetworkInformation** para ping ICMP
- **System.Net.Sockets** para ping TCP
- **PerformanceCounter** para monitoramento de velocidade



## Iniciar com Windows:
  
   Aperte Botão Windows + R
   
   Escreva na caixa: shell:startup
    
   Cole o arquivo ISPing.exe

![image](https://github.com/user-attachments/assets/ead2bdaa-9fc6-4d96-a8d2-927e04e8f2bb)
