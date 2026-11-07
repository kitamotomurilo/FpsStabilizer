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
    public partial class ProfileEditorViewModel : ObservableObject
    {
        private readonly ILogger<ProfileEditorViewModel> _logger;
        private readonly IProfileService _profileService;
        private readonly IProcessService _processService;

        [ObservableProperty]
        private GameProfile _profile;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _windowTitle = "Novo Perfil";

        [ObservableProperty]
        private ObservableCollection<string> _availableProcesses = new();

        [ObservableProperty]
        private ObservableCollection<int> _availableCores = new();

        [ObservableProperty]
        private string _coreSelectionText = "";

        public ProfileEditorViewModel(
            ILogger<ProfileEditorViewModel> logger,
            IProfileService profileService,
            IProcessService processService)
        {
            _logger = logger;
            _profileService = profileService;
            _processService = processService;

            Profile = new GameProfile();
            IsEditing = false;
            LoadAvailableData();
        }

        public void InitializeForEdit(GameProfile profile)
        {
            Profile = profile.Clone();
            IsEditing = true;
            WindowTitle = $"Editar: {profile.Name}";
            UpdateCoreSelectionText();
        }

        public void InitializeForNew()
        {
            Profile = new GameProfile();
            IsEditing = false;
            WindowTitle = "Novo Perfil";
            UpdateCoreSelectionText();
        }

        private void LoadAvailableData()
        {
            // Processos comuns de jogos
            var commonProcesses = new[]
            {
                "cs2", "csgo", "valorant", "leagueclient", "dota2", "overwatch",
                "apex", "fortnite", "minecraft", "roblox", "steam", "epicgameslauncher",
                "eaapp", "ubisoftconnect", "battle.net", "gog", "riotclient"
            };

            foreach (var proc in commonProcesses)
            {
                AvailableProcesses.Add(proc);
            }

            // Cores disponíveis
            var hardware = _processService.GetHardwareInfo();
            for (int i = 0; i < hardware.CpuLogicalProcessors; i++)
            {
                AvailableCores.Add(i);
            }
        }

        partial void OnProfileChanged(GameProfile value)
        {
            UpdateCoreSelectionText();
        }

        private void UpdateCoreSelectionText()
        {
            if (Profile.CpuAffinity.Enabled && Profile.CpuAffinity.CoreIndices.Count > 0)
            {
                CoreSelectionText = $"Cores: {string.Join(", ", Profile.CpuAffinity.CoreIndices.OrderBy(c => c))}";
            }
            else
            {
                CoreSelectionText = Profile.CpuAffinity.Mode switch
                {
                    AffinityMode.AllCores => "Todos os cores",
                    AffinityMode.PerformanceCoresOnly => "Apenas Performance Cores",
                    AffinityMode.EfficiencyCoresOnly => "Apenas Efficiency Cores",
                    AffinityMode.ExcludeCore0 => "Todos exceto Core 0",
                    _ => "Automático"
                };
            }
        }

        [RelayCommand]
        private void ToggleCore(int coreIndex)
        {
            if (Profile.CpuAffinity.CoreIndices.Contains(coreIndex))
            {
                Profile.CpuAffinity.CoreIndices.Remove(coreIndex);
            }
            else
            {
                Profile.CpuAffinity.CoreIndices.Add(coreIndex);
            }
            Profile.CpuAffinity.CoreIndices = Profile.CpuAffinity.CoreIndices.OrderBy(c => c).ToList();
            Profile.CpuAffinity.Mode = AffinityMode.Custom;
            UpdateCoreSelectionText();
            OnPropertyChanged(nameof(Profile));
        }

        [RelayCommand]
        private void SelectAllCores()
        {
            Profile.CpuAffinity.CoreIndices = new(AvailableCores);
            Profile.CpuAffinity.Mode = AffinityMode.Custom;
            UpdateCoreSelectionText();
            OnPropertyChanged(nameof(Profile));
        }

        [RelayCommand]
        private void ClearCores()
        {
            Profile.CpuAffinity.CoreIndices.Clear();
            Profile.CpuAffinity.Mode = AffinityMode.AllCores;
            UpdateCoreSelectionText();
            OnPropertyChanged(nameof(Profile));
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Profile.Name))
            {
                MessageBox.Show("Nome do perfil é obrigatório", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Profile.ProcessName))
            {
                MessageBox.Show("Nome do processo é obrigatório", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing)
                {
                    await _profileService.UpdateAsync(Profile);
                }
                else
                {
                    await _profileService.AddAsync(Profile);
                }

                Messenger.Send(new ProfileSavedMessage(Profile));
                _logger.LogInformation("Perfil salvo: {Name}", Profile.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar perfil");
                MessageBox.Show($"Erro ao salvar perfil: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Messenger.Send(new ProfileEditorClosedMessage());
        }

        public static class Messenger
        {
            public static event Action<ProfileSavedMessage>? ProfileSaved;
            public static event Action<ProfileEditorClosedMessage>? EditorClosed;

            public static void Send(ProfileSavedMessage msg) => ProfileSaved?.Invoke(msg);
            public static void Send(ProfileEditorClosedMessage msg) => EditorClosed?.Invoke(msg);
        }

        public class ProfileSavedMessage
        {
            public GameProfile Profile { get; }
            public ProfileSavedMessage(GameProfile profile) => Profile = profile;
        }

        public class ProfileEditorClosedMessage { }
    }
}