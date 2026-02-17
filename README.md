# ISPing

**Monitor de rede em tempo real para Windows** 

Monitore latência, velocidade de rede, mudanças de IP e muito mais diretamente na bandeja do sistema.

![Image](https://github.com/user-attachments/assets/b7b0bd79-9929-4ec0-8a35-65e68dc3d53d)
![Image](https://github.com/user-attachments/assets/49b464bd-1f87-4dff-82de-02e753e22488)

![Windows](https://img.shields.io/badge/Windows-0078D6?style=flat&logo=windows&logoColor=white)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green.svg)

---

## Visão Geral

O **ISPing** é uma aplicação leve e robusta que reside na bandeja do sistema (system tray) do Windows, fornecendo monitoramento contínuo da sua conexão de rede.

O ícone da bandeja exibe a latência atual em tempo real, com cores indicando a qualidade da conexão, permitindo que você identifique instabilidades instantaneamente sem abrir janelas complexas.

---

## Funcionalidades

### Monitoramento de Ping
- **Ping ICMP ou TCP**: Escolha entre ping ICMP tradicional ou TCP para alvos que bloqueiam ICMP.
- **Alvos pré-configurados**: Google (8.8.8.8), Cloudflare (1.1.1.1) ou endereço personalizado.
- **Intervalos configuráveis**: 1, 3 ou 10 segundos entre pings.
- **Exibição no ícone**: Mostra último ping ou média dos últimos 3 pings.

### Latency Hound
Sistema inteligente de detecção de variações de latência:
- **Detecção automática**: Monitora variações significativas na latência.
- **Tracert automático**: Executa traceroute quando detecta mudanças na rota.
- **Histórico**: Armazena e exibe histórico de tracerts para análise.
- **Configurável**: Ajuste o threshold de variação e cooldown entre scans.

### Monitor de Velocidade de Rede
- **Upload e Download**: Monitora velocidade em tempo real.
- **Jitter**: Calcula variação de latência.
- **Janela flutuante**: Exibe informações sempre visíveis na tela.
- **Seleção de interface**: Escolha qual adaptador de rede monitorar.

### Estatísticas Detalhadas
- Latência mínima, máxima e média.
- Desvio padrão e Jitter.
- Taxa de perda de pacotes.
- Total de pings realizados.
- **Exportação**: Salve dados em CSV e JSON.

### Sistema de Alertas
Receba notificações visuais e sonoras quando:
- Latência ultrapassar um limite configurável.
- Ocorrerem falhas consecutivas de ping.
- Seu IP público mudar.

### Monitoramento de Rotas
- **Detecção de mudanças de rota**: Alerta quando o caminho de rede muda.
- **Log de alterações**: Mantém histórico de mudanças de rota.
- **Visualizador de logs**: Interface para consultar logs passados.

### Scanner de Portas
- **Scan rápido**: Verifica portas comuns (HTTP, SSH, RDP, etc.) no alvo atual.
- **Seleção rápida**: Clique na porta identificada para usar no ping TCP.

### Informações de Rede
- **IP Privado e Público** (Gateway IPv4 e IPv6).
- **Endereço MAC**.
- **Servidor DNS** em uso (com sistema de cache interno).
- **Monitor de Wi-Fi**: SSID, Força do Sinal (dBm), Canal, Frequência e Velocidade do Link.
- **Potas abertas**: Identificar portas abertas facilmente.
 
### Ferramentas Úteis
- **Monitor de Área de Transferência**: Detecta IPs copiados e sugere monitoramento imediato em janela flutuante.
- **Janelas Flutuantes (Always-on-top)**: Monitore múltiplos alvos simultaneamente com auto-fechamento configurável (3s, 10s, 30s ou nunca).

---

## 🚀 Como Usar

1.  Execute o `ISPing.exe`.
2.  O ícone aparecerá na bandeja do sistema (perto do relógio).
3.  Clique com o **botão direito** para acessar o menu de opções.

---

## 📥 Instalação e Execução

### Requisitos
*   Windows 10 ou 11 (Não testado no Windos 11).
*   [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (necessário para rodar).

### Iniciar com Windows de forma automática
Para fazer o ISPing iniciar junto com o Windows:

1.  Pressione `Windows + R` no teclado.
2.  Digite `shell:startup` e pressione Enter.
3.  Copie o arquivo `ISPing.exe` (ou crie um atalho para ele) e cole dentro da pasta que abriu.

---

## 📂 Logs e Configuração

As configurações são persistidas em `AppSettings.cs` entre as sessões.

Os logs são armazenados em:
```bash
%APPDATA%\ISPing\ISPingAppEvents.log    # Eventos Gerais do Programa
%APPDATA%\ISPing\ISPingRouteChanges.log # Registro de Mudanças de Rota
```

---

## 📄 Licença

Este projeto é disponibilizado sob a licença MIT. Sinta-se à livre para usar, modificar e distribuir.


