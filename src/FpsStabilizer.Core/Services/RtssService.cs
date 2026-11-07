using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FpsStabilizer.Core.Models;
using Microsoft.Extensions.Logging;

namespace FpsStabilizer.Core.Services
{
    /// <summary>
    /// Serviço de integração com RivaTuner Statistics Server (RTSS)
    /// RTSS é o padrão ouro para limitar FPS em user-mode no Windows
    /// </summary>
    public interface IRtssService
    {
        bool IsAvailable { get; }
        string? RtssPath { get; }
        Task<bool> SetFpsLimitAsync(string processName, int fpsLimit);
        Task<bool> RemoveFpsLimitAsync(string processName);
        Task<Dictionary<string, int>> GetCurrentLimitsAsync();
        Task<bool> ApplyProfileAsync(GameProfile profile);
        Task<bool> ClearProfileAsync(GameProfile profile);
    }

    public class RtssService : IRtssService
    {
        private readonly ILogger<RtssService> _logger;
        private string? _rtssPath;
        private readonly object _lock = new();

        // Constantes RTSS Shared Memory
        private const string RTSS_SHARED_MEMORY_NAME = "RTSSSharedMemoryV2";
        private const int RTSS_SHARED_MEMORY_SIZE = 0x10000;

        public bool IsAvailable => !string.IsNullOrEmpty(_rtssPath) && File.Exists(_rtssPath);

        public string? RtssPath => _rtssPath;

        public RtssService(ILogger<RtssService> logger)
        {
            _logger = logger;
            DetectRtss();
        }

        private void DetectRtss()
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RivaTuner Statistics Server", "RTSS.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RivaTuner Statistics Server", "RTSS.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RivaTuner Statistics Server", "RTSS.exe"),
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _rtssPath = path;
                    _logger.LogInformation("RTSS encontrado em: {Path}", path);
                    return;
                }
            }

            // Tenta encontrar via registro
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        if (displayName?.Contains("RivaTuner", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                            if (!string.IsNullOrEmpty(installLocation))
                            {
                                var exePath = Path.Combine(installLocation, "RTSS.exe");
                                if (File.Exists(exePath))
                                {
                                    _rtssPath = exePath;
                                    _logger.LogInformation("RTSS encontrado via registro: {Path}", exePath);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao buscar RTSS no registro");
            }

            _logger.LogWarning("RTSS não encontrado. Limite de FPS via RTSS não disponível.");
        }

        /// <summary>
        /// Define limite de FPS para um processo via linha de comando RTSS
        /// </summary>
        public async Task<bool> SetFpsLimitAsync(string processName, int fpsLimit)
        {
            if (!IsAvailable)
            {
                _logger.LogWarning("RTSS não disponível para definir limite de FPS");
                return false;
            }

            if (fpsLimit <= 0)
            {
                return await RemoveFpsLimitAsync(processName);
            }

            try
            {
                // RTSS aceita parâmetros de linha de comando para perfis
                // Formato: RTSS.exe -p "ProcessName" -f <fps>
                var args = $"-p \"{processName}\" -f {fpsLimit}";
                return await RunRtssCommandAsync(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir limite FPS {Fps} para {Process}", fpsLimit, processName);
                return false;
            }
        }

        /// <summary>
        /// Remove limite de FPS para um processo
        /// </summary>
        public async Task<bool> RemoveFpsLimitAsync(string processName)
        {
            if (!IsAvailable) return false;

            try
            {
                // -f 0 remove o limite
                var args = $"-p \"{processName}\" -f 0";
                return await RunRtssCommandAsync(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover limite FPS para {Process}", processName);
                return false;
            }
        }

        /// <summary>
        /// Aplica um perfil completo de jogo via RTSS
        /// </summary>
        public async Task<bool> ApplyProfileAsync(GameProfile profile)
        {
            if (!IsAvailable || !profile.UseRtss || !profile.FpsLimit.HasValue)
            {
                return true; // Não é erro se RTSS não for usado
            }

            return await SetFpsLimitAsync(profile.ProcessName, profile.FpsLimit.Value);
        }

        /// <summary>
        /// Limpa configurações do perfil no RTSS
        /// </summary>
        public async Task<bool> ClearProfileAsync(GameProfile profile)
        {
            if (!IsAvailable || !profile.UseRtss)
            {
                return true;
            }

            return await RemoveFpsLimitAsync(profile.ProcessName);
        }

        /// <summary>
        /// Obtém limites atuais configurados no RTSS (via Shared Memory)
        /// </summary>
        public async Task<Dictionary<string, int>> GetCurrentLimitsAsync()
        {
            var result = new Dictionary<string, int>();

            if (!IsAvailable)
            {
                return result;
            }

            try
            {
                // RTSS expõe dados via Shared Memory
                // Para simplicidade, usamos abordagem de arquivo de config
                var configPath = GetRtssConfigPath();
                if (File.Exists(configPath))
                {
                    var lines = await File.ReadAllLinesAsync(configPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("FramerateLimit="))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2 && int.TryParse(parts[1], out var fps))
                            {
                                // Não temos o nome do processo facilmente aqui sem parser completo
                                result["_global"] = fps;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao ler limites RTSS");
            }

            return result;
        }

        private async Task<bool> RunRtssCommandAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = _rtssPath!,
                            Arguments = arguments,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        using var process = Process.Start(startInfo);
                        if (process == null) return false;

                        var exited = process.WaitForExit(5000);
                        if (!exited)
                        {
                            process.Kill();
                            _logger.LogWarning("RTSS command timeout: {Args}", arguments);
                            return false;
                        }

                        return process.ExitCode == 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao executar RTSS: {Args}", arguments);
                        return false;
                    }
                }
            });
        }

        private string GetRtssConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "RivaTuner Statistics Server", "Profiles", "Global");
        }
    }

    /// <summary>
    /// Implementação mock para testes/desenvolvimento sem RTSS instalado
    /// </summary>
    public class MockRtssService : IRtssService
    {
        private readonly Dictionary<string, int> _limits = new();
        private readonly ILogger<MockRtssService> _logger;

        public bool IsAvailable => true;
        public string? RtssPath => "MOCK";

        public MockRtssService(ILogger<MockRtssService> logger)
        {
            _logger = logger;
        }

        public Task<bool> SetFpsLimitAsync(string processName, int fpsLimit)
        {
            _logger.LogInformation("[MOCK] Set FPS limit: {Process} = {Fps}", processName, fpsLimit);
            if (fpsLimit > 0) _limits[processName] = fpsLimit;
            else _limits.Remove(processName);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveFpsLimitAsync(string processName)
        {
            _logger.LogInformation("[MOCK] Remove FPS limit: {Process}", processName);
            _limits.Remove(processName);
            return Task.FromResult(true);
        }

        public Task<Dictionary<string, int>> GetCurrentLimitsAsync()
        {
            return Task.FromResult(new Dictionary<string, int>(_limits));
        }

        public Task<bool> ApplyProfileAsync(GameProfile profile)
        {
            if (profile.UseRtss && profile.FpsLimit.HasValue)
            {
                return SetFpsLimitAsync(profile.ProcessName, profile.FpsLimit.Value);
            }
            return Task.FromResult(true);
        }

        public Task<bool> ClearProfileAsync(GameProfile profile)
        {
            return RemoveFpsLimitAsync(profile.ProcessName);
        }
    }
}