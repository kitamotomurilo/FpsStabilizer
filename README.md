# FPS Stabilizer

Gerenciador de perfis de performance para jogos no Windows. Permite configurar limite de FPS, prioridade de processo e afinidade de CPU por jogo, com aplicação automática via detecção de processos.

## Funcionalidades

- **Limite de FPS via RTSS** - Integração com RivaTuner Statistics Server para limitar FPS com precisão
- **Prioridade de Processo** - Define prioridade (Idle → RealTime) para o processo do jogo
- **Afinidade de CPU** - Configura quais cores lógicos o jogo pode usar (Performance Cores, Efficiency Cores, exclusão do Core 0, etc.)
- **Detecção Automática** - Identifica jogos em execução e aplica perfis correspondentes
- **Perfis por Jogo** - Configurações salvas individualmente por processo
- **System Tray** - Serviço em background que monitora sem janela aberta
- **Otimizações Windows** - Atalhos para Game Mode, GPU Scheduling, Opções de Energia

## Requisitos

- Windows 10/11 (x64)
- .NET 8 Runtime (`winget install Microsoft.DotNet.Runtime.8`)
- **RTSS (RivaTuner Statistics Server)** - Opcional, mas necessário para limite de FPS
  - Download: https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html

## Instalação

### Via Release (Recomendado)
1. Baixe o último release da página de releases
2. Execute `FpsStabilizer.exe`
3. Opcional: Configure para iniciar com Windows nas configurações

### Compilar do Source
```powershell
# Pré-requisitos
winget install Microsoft.DotNet.SDK.8

# Build
dotnet build FpsStabilizer.sln --configuration Release

# Executável em: src/FpsStabilizer.UI/bin/Release/net8.0-windows/FpsStabilizer.exe
```

## Uso

### Primeira Execução
1. Abra o FPS Stabilizer
2. Instale o RTSS se quiser limite de FPS
3. Vá em **Configurações** → verifique se RTSS está detectado
4. Clique em **Perfis** → **Novo Perfil** para criar seu primeiro perfil

### Criando um Perfil
1. **Nome**: Identificador (ex: "CS2 Competitivo")
2. **Processo**: Nome do executável (ex: `cs2`, `valorant`, `minecraft`)
3. **Limite FPS**: Ative RTSS e defina o limite (ex: 144, 240, 60)
4. **Prioridade**: Recomendado **High** para jogos competitivos
5. **Afinidade CPU**:
   - *Performance Cores Only*: Apenas P-cores (Intel 12/13/14 gen) - recomendado
   - *Exclude Core 0*: Libera Core 0 para sistema/DRM
   - *Custom*: Selecione cores manualmente
6. **Auto-aplicar**: Marque para aplicar automaticamente quando o jogo abrir

### Perfis Pré-definidos
- **Competitivo**: 144 FPS, High priority, Performance Cores only
- **Equilibrado**: 60 FPS, Above Normal, Exclude Core 0
- **Economia**: 30 FPS, Normal, Efficiency Cores only

## Como Funciona

### Detecção de Jogos
O serviço escaneia processos a cada 3s (configurável) procurando por:
- Processos com janela visível
- Nomes conhecidos de jogos/launchers
- Atividade de GPU (heurística)

### Aplicação de Perfil
Quando um jogo é detectado e tem perfil correspondente:
1. Define prioridade do processo via `Process.PriorityClass`
2. Define afinidade via `Process.ProcessorAffinity` (bitmask)
3. Envia comando ao RTSS para limite de FPS (se configurado)
4. Restaura configurações originais quando jogo fecha

### RTSS Integration
O RTSS é controlado via linha de comando:
```
RTSS.exe -p "ProcessName" -f 144
```
Isso cria/atualiza perfil no RTSS para o processo específico.

## Estrutura do Projeto

```
FpsStabilizer/
├── src/
│   ├── FpsStabilizer.Core/       # Lógica de negócio, modelos, serviços
│   │   ├── Models/               # GameProfile, DetectedGame, SystemHardwareInfo
│   │   └── Services/             # RtssService, ProcessService, ProfileService, GameDetectionService
│   ├── FpsStabilizer.UI/         # WPF Application (MVVM)
│   │   ├── ViewModels/           # MainViewModel, ProfileEditorViewModel, SettingsViewModel
│   │   ├── Views/                # MainWindow, ProfileEditorWindow, SettingsWindow
│   │   ├── Styles/               # Dark theme, Modern controls
│   │   └── Converters/           # Value converters
│   └── FpsStabilizer.Service/    # Background service (System Tray)
├── assets/                       # Icons, resources
└── tests/                        # Unit tests (futuro)
```

## Configurações Avançadas

### Variáveis de Ambiente por Perfil
Adicione no editor de perfil (aba Avançado):
```
DXVK_HUD=1
MESA_GL_VERSION_OVERRIDE=4.6
```

### Inicialização com Windows
Ativado em Configurações → Geral → "Iniciar com o Windows"
Cria entrada em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

### Dados Armazenados
- Perfis: `%LOCALAPPDATA%\FpsStabilizer\profiles.json`
- Configurações: `HKCU\Software\FpsStabilizer`
- Logs: Event Viewer → Applications and Services Logs → FpsStabilizer

## Troubleshooting

### RTSS não detectado
1. Instale RTSS (incluído no MSI Afterburner ou standalone)
2. Reinicie FPS Stabilizer
3. Em Configurações, clique "Procurar..." e selecione `RTSS.exe`

### Perfil não aplicado
- Verifique se nome do processo está correto (Task Manager → Details → Name)
- Confirme se "Auto-aplicar" está marcado no perfil
- Verifique logs no Event Viewer

### Jogo não detectado
- Alguns jogos anti-cheat ocultam processos
- Tente adicionar o caminho completo do executável no perfil
- Verifique se o jogo roda como Admin (FPS Stabilizer também precisa rodar como Admin)

### Afinidade não funciona
- Requer permissões para modificar `ProcessorAffinity`
- Alguns jogos resetam afinidade internamente
- Teste com `start /affinity` manual primeiro

## Limitações Conhecidas

- **Overlay próprio**: Não implementado (requer hook DirectX/Vulkan, driver assinado)
- **Latência real**: Não mede frame time/latência (use PresentMon/CapFrameX)
- **Anti-cheat**: Jogos com kernel anti-cheat (Valorant, Faceit) podem bloquear modificações
- **Multi-GPU**: Não gerencia explicitamente GPUs múltiplas

## Contribuindo

1. Fork o repositório
2. Crie branch: `git checkout -b feature/nova-funcionalidade`
3. Commit: `git commit -am 'Adiciona nova funcionalidade'`
4. Push: `git push origin feature/nova-funcionalidade`
5. Abra Pull Request

## Licença

MIT License - Veja [LICENSE](LICENSE) para detalhes.

## Agradecimentos

- **RTSS/RivaTuner** - Unwinder (guru3d.com) - Base para limite de FPS
- **Special K** - Kaldaien - Referência de otimizações avançadas
- **CommunityToolkit.Mvvm** - Microsoft - Framework MVVM
- **Hardcodet.NotifyIcon.Wpf** - Philipp Sumi - System tray icon

---

**Nota**: Este é um gerenciador de perfis user-mode. Para estabilização real de frame pacing (frame time consistency), ferramentas como **Special K**, **NVIDIA Frame Pacing** ou **AMD Radeon Chill** operam em nível de driver/API gráfica.