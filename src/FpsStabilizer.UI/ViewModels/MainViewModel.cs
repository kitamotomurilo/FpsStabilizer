using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FpsStabilizer.Core.Models;
using FpsStabilizer.Core.Services;
using Microsoft.Extensions.Logging;

namespace FpsStabilizer.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IGameDetectionService _gameDetectionService;
        private readonly IProfileService _profileService;
        private readonly IProcessService _processService;
        private readonly IRtssService _rtssService;

        [ObservableProperty]
        private bool _isMonitoring = false;

        [ObservableProperty]
        private string _statusText = "Pronto";

        [ObservableProperty]
        private ObservableCollection<DetectedGame> _detectedGames = new();

        [ObservableProperty]
        private ObservableCollection<GameProfile> _profiles = new();

        [ObservableProperty]
        private DetectedGame? _selectedGame;

        [ObservableProperty]
        private GameProfile? _selectedProfile;

        [ObservableProperty]
        private SystemHardwareInfo? _hardwareInfo;

        [ObservableProperty]
        private bool _isRtssAvailable = false;

        [ObservableProperty]
        private string _rtssPath = "Não encontrado";

        public MainViewModel(
            ILogger<MainViewModel> logger,
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

            // Subscreve eventos
            _gameDetectionService.GameDetected += OnGameDetected;
            _gameDetectionService.GameClosed += OnGameClosed;
            _gameDetectionService.ProfileApplied += OnProfileApplied;
            _gameDetectionService.ErrorOccurred += OnErrorOccurred;

            // Carrega perfis
            _ = LoadProfilesAsync();

            // Carrega info de hardware
            HardwareInfo = _processService.GetHardwareInfo();

            // Verifica RTSS
            IsRtssAvailable = _rtssService.IsAvailable;
            RtssPath = _rtssService.RtssPath ?? "Não encontrado";

            // Inicia monitoramento automaticamente
            _ = StartMonitoringAsync();
        }

        private async Task LoadProfilesAsync()
        {
            await _profileService.LoadAsync();
            Profiles.Clear();
            foreach (var profile in _profileService.Profiles)
            {
                Profiles.Add(profile);
            }
        }

        [RelayCommand]

        public async Task StartMonitoringAsync()
        {
            try
            {
                IsMonitoring = true;
                StatusText = "Monitorando jogos...";
                await _gameDetectionService.StartAsync();

                // Atualiza lista inicial
                foreach (var game in _gameDetectionService.CurrentGames)
                {
                    DetectedGames.Add(game);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar monitoramento");
                StatusText = "Erro no monitoramento";
                IsMonitoring = false;
            }
        }

        [RelayCommand]
        public async Task StopMonitoringAsync()
        {
            try
            {
                await _gameDetectionService.StopAsync();
                IsMonitoring = false;
                StatusText = "Monitoramento parado";
                DetectedGames.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar monitoramento");
            }
        }

        [RelayCommand]
        private async Task RefreshGamesAsync()
        {
            try
            {
                StatusText = "Atualizando...";
                await _gameDetectionService.RefreshAsync();

                DetectedGames.Clear();
                foreach (var game in _gameDetectionService.CurrentGames)
                {
                    DetectedGames.Add(game);
                }

                StatusText = $"{DetectedGames.Count} jogo(s) detectado(s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar jogos");
                StatusText = "Erro ao atualizar";
            }
        }

        [RelayCommand]
        private async Task ApplyProfileToSelectedAsync()
        {
            if (SelectedGame == null || SelectedProfile == null) return;

            try
            {
                var success = await _processService.ApplyProfileAsync(SelectedProfile, SelectedGame.ProcessId);
                if (success && SelectedProfile.UseRtss && SelectedProfile.FpsLimit.HasValue)
                {
                    await _rtssService.ApplyProfileAsync(SelectedProfile);
                }

                SelectedGame.MatchedProfileId = SelectedProfile.Id;
                SelectedGame.MatchedProfileName = SelectedProfile.Name;

                StatusText = $"Perfil '{SelectedProfile.Name}' aplicado a {SelectedGame.ProcessName}";
                _logger.LogInformation("Perfil aplicado manualmente: {Profile} -> {Game}", SelectedProfile.Name, SelectedGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aplicar perfil");
                StatusText = "Erro ao aplicar perfil";
                MessageBox.Show($"Erro ao aplicar perfil: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveProfileFromSelectedAsync()
        {
            if (SelectedGame == null) return;

            try
            {
                await _processService.RestoreDefaultsAsync(SelectedGame.ProcessId);

                if (SelectedGame.MatchedProfileId.HasValue)
                {
                    var profile = Profiles.FirstOrDefault(p => p.Id == SelectedGame.MatchedProfileId.Value);
                    if (profile != null && profile.UseRtss)
                    {
                        await _rtssService.ClearProfileAsync(profile);
                    }
                }

                SelectedGame.MatchedProfileId = null;
                SelectedGame.MatchedProfileName = null;

                StatusText = $"Perfil removido de {SelectedGame.ProcessName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover perfil");
                StatusText = "Erro ao remover perfil";
            }
        }

        [RelayCommand]
        private void OpenProfileEditor()
        {
            // Será implementado via dialog service
            Messenger.Send(new OpenProfileEditorMessage());
        }

        [RelayCommand]
        private void OpenSettings()
        {
            Messenger.Send(new OpenSettingsMessage());
        }

        [RelayCommand]
        private async Task ExportProfilesAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = "json",
                FileName = $"FpsStabilizer_Profiles_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _profileService.ExportAsync(dialog.FileName, Profiles);
                    StatusText = "Perfis exportados com sucesso";
                    MessageBox.Show("Perfis exportados com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao exportar perfis");
                    MessageBox.Show($"Erro ao exportar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task ImportProfilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var imported = await _profileService.ImportAsync(dialog.FileName);
                    foreach (var profile in imported)
                    {
                        await _profileService.AddAsync(profile);
                        Profiles.Add(profile);
                    }

                    StatusText = $"{imported.Count} perfil(is) importado(s)";
                    MessageBox.Show($"{imported.Count} perfil(is) importado(s) com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao importar perfis");
                    MessageBox.Show($"Erro ao importar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnGameDetected(object? sender, DetectedGame game)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DetectedGames.Add(game);
                StatusText = $"{DetectedGames.Count} jogo(s) detectado(s)";
            });
        }

        private void OnGameClosed(object? sender, DetectedGame game)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DetectedGames.Remove(game);
                StatusText = $"{DetectedGames.Count} jogo(s) detectado(s)";
            });
        }

        private void OnProfileApplied(object? sender, DetectedGame game)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Perfil aplicado a {game.ProcessName}";
            });
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Erro: {error}";
                _logger.LogWarning("Erro do serviço de detecção: {Error}", error);
            });
        }

        // Messenger simples para comunicação entre ViewModels
        public static class Messenger
        {
            public static event Action<OpenProfileEditorMessage>? OpenProfileEditorRequested;
            public static event Action<OpenSettingsMessage>? OpenSettingsRequested;

            public static void Send(OpenProfileEditorMessage msg) => OpenProfileEditorRequested?.Invoke(msg);
            public static void Send(OpenSettingsMessage msg) => OpenSettingsRequested?.Invoke(msg);
        }

        public class OpenProfileEditorMessage { }
        public class OpenSettingsMessage { }
    }
}