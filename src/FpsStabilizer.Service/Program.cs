using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using FpsStabilizer.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FpsStabilizer.Service
{
    /// <summary>
    /// Background service para execução na bandeja do sistema (system tray)
    /// Permite monitoramento contínuo sem janela principal aberta
    /// </summary>
    internal class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Single instance check
            var mutex = new System.Threading.Mutex(true, "FpsStabilizerServiceSingleInstance", out var createdNew);
            if (!createdNew)
            {
                MessageBox.Show("FPS Stabilizer Service já está em execução.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var builder = Host.CreateApplicationBuilder(args);

            // Configura logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddEventLog(settings => settings.SourceName = "FpsStabilizer");
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Services
            builder.Services.AddSingleton<IRtssService, RtssService>();
            builder.Services.AddSingleton<IProcessService, ProcessService>();
            builder.Services.AddSingleton<IProfileService, ProfileService>();
            builder.Services.AddSingleton<IGameDetectionService, GameDetectionService>();
            builder.Services.AddHostedService<TrayHostedService>();

            var host = builder.Build();

            try
            {
                await host.RunAsync();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    /// <summary>
    /// Hosted service que gerencia o ícone na bandeja do sistema
    /// </summary>
    public class TrayHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<TrayHostedService> _logger;
        private readonly IGameDetectionService _gameDetectionService;
        private readonly IProfileService _profileService;
        private readonly IProcessService _processService;
        private readonly IRtssService _rtssService;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;
        private bool _isMonitoring = false;

        public TrayHostedService(
            ILogger<TrayHostedService> logger,
            IGameDetectionService gameDetectionService,
            IProfileService profileService,
            IProcessService processService,
            IRtssService rtssService)
        {
            _logger = logger;
            _gameDetectionService = gameDetectionService;
            _profileService = profileService;
            _processService = processService;
            _rtssService = rtssService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Iniciando FPS Stabilizer Service");

            // Carrega perfis
            await _profileService.LoadAsync();

            // Cria ícone na bandeja
            CreateTrayIcon();

            // Inicia monitoramento se configurado
            var autoStart = GetAutoStartSetting();
            if (autoStart)
            {
                await StartMonitoringAsync();
            }

            _logger.LogInformation("Service iniciado com sucesso");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Parando FPS Stabilizer Service");

            if (_isMonitoring)
            {
                await StopMonitoringAsync();
            }

            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();

            _logger.LogInformation("Service parado");
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }

        private void CreateTrayIcon()
        {
            _contextMenu = new System.Windows.Forms.ContextMenuStrip();

            // Status item (não clicável)
            var statusItem = new ToolStripMenuItem("FPS Stabilizer Service")
            {
                Enabled = false,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };
            _contextMenu.Items.Add(statusItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Monitoring toggle
            var monitorItem = new ToolStripMenuItem("Iniciar Monitoramento")
            {
                Name = "MonitorToggle"
            };
            monitorItem.Click += async (s, e) =>
            {
                if (_isMonitoring)
                {
                    await StopMonitoringAsync();
                }
                else
                {
                    await StartMonitoringAsync();
                }
                UpdateMenuState();
            };
            _contextMenu.Items.Add(monitorItem);

            // Show main window
            var showItem = new ToolStripMenuItem("Abrir Interface Principal");
            showItem.Click += (s, e) => LaunchMainApp();
            _contextMenu.Items.Add(showItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Quick profiles
            var profilesItem = new ToolStripMenuItem("Perfis Rápidos");
            _contextMenu.Items.Add(profilesItem);
            RefreshProfilesMenu(profilesItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            var settingsItem = new ToolStripMenuItem("Configurações");
            settingsItem.Click += (s, e) => LaunchMainApp("--settings");
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Sair");
            exitItem.Click += async (s, e) =>
            {
                if (_isMonitoring)
                {
                    await StopMonitoringAsync();
                }
                System.Windows.Forms.Application.Exit();
            };
            _contextMenu.Items.Add(exitItem);

            // Notify Icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = CreateTrayIconImage(),
                Text = "FPS Stabilizer - Monitorando: " + (_isMonitoring ? "Ativo" : "Parado"),
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => LaunchMainApp();
        }

        private System.Drawing.Icon CreateTrayIconImage()
        {
            // Cria ícone simples programaticamente
            var bitmap = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 122, 204));
                g.FillEllipse(brush, 4, 4, 24, 24);
                using var brush2 = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                g.FillEllipse(brush2, 12, 12, 8, 8);
            }
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        private void UpdateMenuState()
        {
            if (_contextMenu?.Items["MonitorToggle"] is ToolStripMenuItem monitorItem)
            {
                monitorItem.Text = _isMonitoring ? "Parar Monitoramento" : "Iniciar Monitoramento";
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"FPS Stabilizer - Monitorando: {(_isMonitoring ? "Ativo" : "Parado")}";
            }
        }

        private void RefreshProfilesMenu(ToolStripMenuItem parentItem)
        {
            parentItem.DropDownItems.Clear();

            foreach (var profile in _profileService.Profiles.Where(p => p.IsActive))
            {
                var item = new ToolStripMenuItem(profile.Name)
                {
                    Tag = profile.Id,
                    Checked = profile.AutoApply
                };
                item.Click += (s, e) =>
                {
                    if (s is ToolStripMenuItem mi && mi.Tag is Guid profileId)
                    {
                        var p = _profileService.Profiles.FirstOrDefault(x => x.Id == profileId);
                        if (p != null)
                        {
                            p.AutoApply = !p.AutoApply;
                            _ = _profileService.UpdateAsync(p);
                            RefreshProfilesMenu(parentItem);
                        }
                    }
                };
                parentItem.DropDownItems.Add(item);
            }

            if (parentItem.DropDownItems.Count == 0)
            {
                var noneItem = new ToolStripMenuItem("(Nenhum perfil configurado)") { Enabled = false };
                parentItem.DropDownItems.Add(noneItem);
            }
        }

        private async Task StartMonitoringAsync()
        {
            try
            {
                await _gameDetectionService.StartAsync();
                _isMonitoring = true;
                UpdateMenuState();
                _logger.LogInformation("Monitoramento iniciado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar monitoramento");
                ShowBalloonTip("Erro", "Falha ao iniciar monitoramento", ToolTipIcon.Error);
            }
        }

        private async Task StopMonitoringAsync()
        {
            try
            {
                await _gameDetectionService.StopAsync();
                _isMonitoring = false;
                UpdateMenuState();
                _logger.LogInformation("Monitoramento parado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar monitoramento");
            }
        }

        private void LaunchMainApp(string? args = null)
        {
            try
            {
                var exePath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                    "..", "FpsStabilizer.UI", "FpsStabilizer.exe");

                exePath = Path.GetFullPath(exePath);

                if (File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = args ?? "",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    // Tenta encontrar na pasta de instalação
                    var installPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs", "FpsStabilizer", "FpsStabilizer.exe");

                    if (File.Exists(installPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = installPath,
                            Arguments = args ?? "",
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        ShowBalloonTip("Aviso", "Interface principal não encontrada. Reinstale o aplicativo.", ToolTipIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao abrir interface principal");
            }
        }

        private bool GetAutoStartSetting()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\FpsStabilizer");
                return key?.GetValue("AutoStartMonitoring") as int? != 0;
            }
            catch { return true; }
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }
    }
}