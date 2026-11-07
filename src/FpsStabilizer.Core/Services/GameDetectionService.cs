using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FpsStabilizer.Core.Models;
using Microsoft.Extensions.Logging;

namespace FpsStabilizer.Core.Services
{
    /// <summary>
    /// Serviço que monitora jogos em execução e aplica perfis automaticamente
    /// </summary>
    public interface IGameDetectionService
    {
        event EventHandler<DetectedGame>? GameDetected;
        event EventHandler<DetectedGame>? GameClosed;
        event EventHandler<DetectedGame>? ProfileApplied;
        event EventHandler<string>? ErrorOccurred;

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        Task RefreshAsync();
        IReadOnlyList<DetectedGame> CurrentGames { get; }
    }

    public class GameDetectionService : IGameDetectionService
    {
        private readonly ILogger<GameDetectionService> _logger;
        private readonly IProcessService _processService;
        private readonly IProfileService _profileService;
        private readonly IRtssService _rtssService;

        private readonly List<DetectedGame> _currentGames = new();
        private readonly Dictionary<int, GameProfile> _appliedProfiles = new();
        private Timer? _detectionTimer;
        private CancellationTokenSource? _cts;

        public event EventHandler<DetectedGame>? GameDetected;
        public event EventHandler<DetectedGame>? GameClosed;
        public event EventHandler<DetectedGame>? ProfileApplied;
        public event EventHandler<string>? ErrorOccurred;

        public IReadOnlyList<DetectedGame> CurrentGames => _currentGames.AsReadOnly();

        public GameDetectionService(
            ILogger<GameDetectionService> logger,
            IProcessService processService,
            IProfileService profileService,
            IRtssService rtssService)
        {
            _logger = logger;
            _processService = processService;
            _profileService = profileService;
            _rtssService = rtssService;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Detecção inicial
            await RefreshAsync();

            // Timer para detecção periódica (a cada 3 segundos)
            _detectionTimer = new Timer(async _ => await RefreshAsync(), null,
                TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

            _logger.LogInformation("Serviço de detecção de jogos iniciado");
        }

        public async Task StopAsync()
        {
            _detectionTimer?.Dispose();
            _detectionTimer = null;
            _cts?.Cancel();
            _cts?.Dispose();

            // Restaura configurações de todos os jogos monitorados
            foreach (var kvp in _appliedProfiles)
            {
                try
                {
                    await _processService.RestoreDefaultsAsync(kvp.Key);
                    if (kvp.Value.UseRtss)
                    {
                        await _rtssService.ClearProfileAsync(kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao restaurar configurações ao parar");
                }
            }

            _appliedProfiles.Clear();
            _currentGames.Clear();

            _logger.LogInformation("Serviço de detecção de jogos parado");
        }

        public async Task RefreshAsync()
        {
            if (_cts?.Token.IsCancellationRequested == true) return;

            try
            {
                var detectedGames = await _processService.DetectGamesAsync();
                var currentPids = _currentGames.Select(g => g.ProcessId).ToHashSet();
                var detectedPids = detectedGames.Select(g => g.ProcessId).ToHashSet();

                // Novos jogos detectados
                var newGames = detectedGames.Where(g => !currentPids.Contains(g.ProcessId)).ToList();
                foreach (var game in newGames)
                {
                    _currentGames.Add(game);
                    _logger.LogInformation("Jogo detectado: {Game}", game);

                    // Tenta encontrar perfil correspondente
                    var profile = await _profileService.GetByProcessNameAsync(game.ProcessName);
                    if (profile != null)
                    {
                        game.MatchedProfileId = profile.Id;
                        game.MatchedProfileName = profile.Name;

                        if (profile.AutoApply)
                        {
                            await ApplyProfileAsync(game, profile);
                        }
                    }

                    GameDetected?.Invoke(this, game);
                }

                // Jogos fechados
                var closedGames = _currentGames.Where(g => !detectedPids.Contains(g.ProcessId)).ToList();
                foreach (var game in closedGames)
                {
                    _currentGames.Remove(game);

                    // Restaura configurações se perfil foi aplicado
                    if (_appliedProfiles.TryGetValue(game.ProcessId, out var appliedProfile))
                    {
                        await _processService.RestoreDefaultsAsync(game.ProcessId);
                        if (appliedProfile.UseRtss)
                        {
                            await _rtssService.ClearProfileAsync(appliedProfile);
                        }
                        _appliedProfiles.Remove(game.ProcessId);
                    }

                    _logger.LogInformation("Jogo fechado: {Game}", game);
                    GameClosed?.Invoke(this, game);
                }

                // Atualiza info de jogos existentes
                foreach (var existingGame in _currentGames)
                {
                    var updated = detectedGames.FirstOrDefault(g => g.ProcessId == existingGame.ProcessId);
                    if (updated != null)
                    {
                        existingGame.CpuUsage = updated.CpuUsage;
                        existingGame.MemoryUsageMB = updated.MemoryUsageMB;
                        existingGame.IsFullscreen = updated.IsFullscreen;
                        existingGame.IsFocused = updated.IsFocused;
                        existingGame.MainWindowTitle = updated.MainWindowTitle;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na detecção de jogos");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private async Task ApplyProfileAsync(DetectedGame game, GameProfile profile)
        {
            try
            {
                var success = await _processService.ApplyProfileAsync(profile, game.ProcessId);
                if (success && profile.UseRtss && profile.FpsLimit.HasValue)
                {
                    await _rtssService.ApplyProfileAsync(profile);
                }

                if (success)
                {
                    _appliedProfiles[game.ProcessId] = profile;
                    _logger.LogInformation("Perfil '{Profile}' aplicado a {Game}", profile.Name, game);
                    ProfileApplied?.Invoke(this, game);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aplicar perfil {Profile} a {Game}", profile.Name, game);
                ErrorOccurred?.Invoke(this, $"Erro ao aplicar perfil: {ex.Message}");
            }
        }
    }
}