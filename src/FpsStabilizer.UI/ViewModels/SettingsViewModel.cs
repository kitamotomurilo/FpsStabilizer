using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FpsStabilizer.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FpsStabilizer.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly IRtssService _rtssService;
        private readonly IProcessService _processService;

        [ObservableProperty]
        private bool _startWithWindows = false;

        [ObservableProperty]
        private bool _startMinimized = false;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _autoStartMonitoring = true;

        [ObservableProperty]
        private int _detectionIntervalSeconds = 3;

        [ObservableProperty]
        private bool _enableRtssIntegration = true;

        [ObservableProperty]
        private string _rtssPath = "";

        [ObservableProperty]
        private bool _isGameModeEnabled = false;

        [ObservableProperty]
        private bool _isGpuSchedulingEnabled = false;

        [ObservableProperty]
        private string _appVersion = "1.0.0";

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IRtssService rtssService,
            IProcessService processService)
        {
            _logger = logger;
            _rtssService = rtssService;
            _processService = processService;

            LoadSettings();
            LoadSystemInfo();
        }

        private void LoadSettings()
        {
            // Carrega do registro
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\FpsStabilizer");
                if (key != null)
                {
                    StartWithWindows = key.GetValue("StartWithWindows") as int? == 1;
                    StartMinimized = key.GetValue("StartMinimized") as int? == 1;
                    MinimizeToTray = key.GetValue("MinimizeToTray") as int? != 0;
                    AutoStartMonitoring = key.GetValue("AutoStartMonitoring") as int? != 0;
                    DetectionIntervalSeconds = key.GetValue("DetectionIntervalSeconds") as int? ?? 3;
                    EnableRtssIntegration = key.GetValue("EnableRtssIntegration") as int? != 0;
                    RtssPath = key.GetValue("RtssPath") as string ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar configurações");
            }

            RtssPath = _rtssService.RtssPath ?? "Não encontrado";
        }

        private void LoadSystemInfo()
        {
            var hardware = _processService.GetHardwareInfo();
            IsGameModeEnabled = hardware.IsGameModeEnabled;
            IsGpuSchedulingEnabled = hardware.IsGpuSchedulingEnabled;
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            SaveSetting("StartWithWindows", value ? 1 : 0);
            SetStartupRegistry(value);
        }

        partial void OnStartMinimizedChanged(bool value)
        {
            SaveSetting("StartMinimized", value ? 1 : 0);
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
            SaveSetting("MinimizeToTray", value ? 1 : 0);
        }

        partial void OnAutoStartMonitoringChanged(bool value)
        {
            SaveSetting("AutoStartMonitoring", value ? 1 : 0);
        }

        partial void OnDetectionIntervalSecondsChanged(int value)
        {
            if (value < 1) value = 1;
            if (value > 60) value = 60;
            DetectionIntervalSeconds = value;
            SaveSetting("DetectionIntervalSeconds", value);
        }

        partial void OnEnableRtssIntegrationChanged(bool value)
        {
            SaveSetting("EnableRtssIntegration", value ? 1 : 0);
        }

        private void SaveSetting(string name, object value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\FpsStabilizer");
                key?.SetValue(name, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar configuração {Name}", name);
            }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                var appName = "FpsStabilizer";
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    key?.SetValue(appName, $"\"{exePath}\" --minimized");
                }
                else
                {
                    key?.DeleteValue(appName, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao configurar inicialização com Windows");
            }
        }

        [RelayCommand]
        private void BrowseRtssPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "RTSS Executable (RTSS.exe)|RTSS.exe",
                Title = "Selecione o RTSS.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                RtssPath = dialog.FileName;
                SaveSetting("RtssPath", RtssPath);
                // Reinicializa serviço RTSS
                // Nota: seria necessário reinjetar ou recarregar o serviço
                MessageBox.Show("Caminho do RTSS atualizado. Reinicie o aplicativo para aplicar.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void EnableGameMode()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                key?.SetValue("AllowAutoGameMode", 1);
                IsGameModeEnabled = true;
                MessageBox.Show("Game Mode ativado. Reinicie o PC para aplicar completamente.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ativar Game Mode");
                MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void EnableGpuScheduling()
        {
            try
            {
                // Requer admin
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Name 'HwSchMode' -Value 2 -Type DWord\"",
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
                MessageBox.Show("Comando executado. Reinicie o PC para aplicar.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ativar GPU Scheduling");
                MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenGameModeSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:gaming-gamemode") { UseShellExecute = true });
        }

        [RelayCommand]
        private void OpenGraphicsSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:display-graphics") { UseShellExecute = true });
        }

        [RelayCommand]
        private void OpenPowerOptions()
        {
            Process.Start(new ProcessStartInfo("powercfg.cpl") { UseShellExecute = true });
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            var result = MessageBox.Show("Restaurar todas as configurações para padrão?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\FpsStabilizer", false);
                    LoadSettings();
                    MessageBox.Show("Configurações restauradas. Reinicie o aplicativo.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao restaurar padrões");
                    MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            // TODO: Implementar verificação de atualizações (GitHub Releases API)
            MessageBox.Show("Verificação de atualizações será implementada em versão futura.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}