using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FpsStabilizer.Core.Models;
using Microsoft.Extensions.Logging;

namespace FpsStabilizer.Core.Services
{
    public interface IProcessService
    {
        Task<List<DetectedGame>> DetectGamesAsync();
        Task<bool> SetPriorityAsync(int processId, ProcessPriority priority);
        Task<bool> SetAffinityAsync(int processId, CpuAffinityConfig affinity);
        Task<bool> ApplyProfileAsync(GameProfile profile, int processId);
        Task<bool> RestoreDefaultsAsync(int processId);
        SystemHardwareInfo GetHardwareInfo();
        List<int> GetRecommendedCores(CpuAffinityConfig config);
    }

    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;
        private readonly Dictionary<int, (ProcessPriorityClass Priority, IntPtr Affinity)> _originalStates = new();
        private SystemHardwareInfo? _cachedHardwareInfo;

        public ProcessService(ILogger<ProcessService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Detecta jogos/processos 3D em execução
        /// </summary>
        public async Task<List<DetectedGame>> DetectGamesAsync()
        {
            return await Task.Run(() =>
            {
                var games = new List<DetectedGame>();

                try
                {
                    var processes = Process.GetProcesses()
                        .Where(p => !IsSystemProcess(p))
                        .Where(p => HasGpuActivity(p))
                        .ToList();

                    foreach (var process in processes)
                    {
                        try
                        {
                            var game = DetectedGame.FromProcess(process);

                            // Tenta obter uso de CPU
                            game.CpuUsage = GetProcessCpuUsage(process);
                            game.MemoryUsageMB = process.WorkingSet64 / (1024 * 1024);

                            // Verifica se está em fullscreen
                            game.IsFullscreen = IsProcessFullscreen(process);
                            game.IsFocused = IsProcessFocused(process);

                            games.Add(game);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Erro ao inspecionar processo {Pid}", process.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao detectar jogos");
                }

                return games;
            });
        }

        /// <summary>
        /// Define prioridade do processo
        /// </summary>
        public async Task<bool> SetPriorityAsync(int processId, ProcessPriority priority)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(processId);

                    // Salva estado original se não salvo
                    if (!_originalStates.ContainsKey(processId))
                    {
                        _originalStates[processId] = (process.PriorityClass, process.ProcessorAffinity);
                    }

                    process.PriorityClass = priority switch
                    {
                        ProcessPriority.Idle => ProcessPriorityClass.Idle,
                        ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                        ProcessPriority.Normal => ProcessPriorityClass.Normal,
                        ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                        ProcessPriority.High => ProcessPriorityClass.High,
                        ProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                        _ => ProcessPriorityClass.Normal
                    };

                    _logger.LogInformation("Prioridade definida: PID {Pid} = {Priority}", processId, priority);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao definir prioridade PID {Pid}", processId);
                    return false;
                }
            });
        }

        /// <summary>
        /// Define afinidade de CPU
        /// </summary>
        public async Task<bool> SetAffinityAsync(int processId, CpuAffinityConfig config)
        {
            return await Task.Run(() =>
            {
                if (!config.Enabled) return true;

                try
                {
                    var process = Process.GetProcessById(processId);

                    if (!_originalStates.ContainsKey(processId))
                    {
                        _originalStates[processId] = (process.PriorityClass, process.ProcessorAffinity);
                    }

                    var cores = GetRecommendedCores(config);
                    if (cores.Count == 0) return true;

                    var affinityMask = CalculateAffinityMask(cores);
                    process.ProcessorAffinity = (IntPtr)affinityMask;

                    _logger.LogInformation("Afinidade definida: PID {Pid} = Cores [{Cores}]",
                        processId, string.Join(", ", cores));
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao definir afinidade PID {Pid}", processId);
                    return false;
                }
            });
        }

        /// <summary>
        /// Aplica perfil completo (prioridade + afinidade + variáveis de ambiente)
        /// </summary>
        public async Task<bool> ApplyProfileAsync(GameProfile profile, int processId)
        {
            var success = true;

            success &= await SetPriorityAsync(processId, profile.Priority);
            success &= await SetAffinityAsync(processId, profile.CpuAffinity);

            // Variáveis de ambiente precisam ser definidas ANTES do lançamento
            // Aqui apenas logamos para referência
            if (profile.EnvironmentVariables.Count > 0)
            {
                _logger.LogInformation("Variáveis de ambiente do perfil {Profile}: {Vars}",
                    profile.Name, string.Join(", ", profile.EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            return success;
        }

        /// <summary>
        /// Restaura configurações originais do processo
        /// </summary>
        public async Task<bool> RestoreDefaultsAsync(int processId)
        {
            return await Task.Run(() =>
            {
                if (!_originalStates.TryGetValue(processId, out var original))
                {
                    return true; // Nada para restaurar
                }

                try
                {
                    var process = Process.GetProcessById(processId);
                    process.PriorityClass = original.Priority;
                    process.ProcessorAffinity = original.Affinity;

                    _originalStates.Remove(processId);
                    _logger.LogInformation("Configurações restauradas para PID {Pid}", processId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao restaurar PID {Pid}", processId);
                    return false;
                }
            });
        }

        /// <summary>
        /// Obtém informações de hardware do sistema
        /// </summary>
        public SystemHardwareInfo GetHardwareInfo()
        {
            if (_cachedHardwareInfo != null) return _cachedHardwareInfo;

            var info = new SystemHardwareInfo();

            try
            {
                // CPU Info via WMI
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    info.CpuName = obj["Name"]?.ToString() ?? "Unknown";
                    info.CpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    info.CpuLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);

                    // Detecta big.LITTLE (Intel 12th/13th/14th gen)
                    var arch = obj["Architecture"]?.ToString();
                    info.HasPerformanceCores = info.CpuLogicalProcessors > info.CpuCores;

                    if (info.HasPerformanceCores)
                    {
                        // Estimativa simples: metade performance, metade efficiency
                        info.PerformanceCoreCount = info.CpuCores / 2;
                        info.EfficiencyCoreCount = info.CpuCores - info.PerformanceCoreCount;
                    }
                }

                // GPU Info
                using var gpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in gpuSearcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(info.GpuName) || name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        info.GpuName = name;

                        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = GpuVendor.Nvidia;
                        else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = GpuVendor.Amd;
                        else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) || name.Contains("UHD", StringComparison.OrdinalIgnoreCase) || name.Contains("Iris", StringComparison.OrdinalIgnoreCase) || name.Contains("Arc", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = GpuVendor.Intel;
                        else info.GpuVendor = GpuVendor.Other;
                    }
                }

                // Memória total
                using var memSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in memSearcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] != null)
                    {
                        info.TotalMemoryGB = Math.Round(Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024), 1);
                    }
                }

                // OS Version
                info.OsVersion = Environment.OSVersion.Version.ToString();

                // Game Mode e GPU Scheduling (via registro)
                info.IsGameModeEnabled = IsGameModeEnabled();
                info.IsGpuSchedulingEnabled = IsGpuSchedulingEnabled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter info de hardware");
            }

            _cachedHardwareInfo = info;
            return info;
        }

        /// <summary>
        /// Obtém lista de cores recomendados baseada na configuração
        /// </summary>
        public List<int> GetRecommendedCores(CpuAffinityConfig config)
        {
            var hardware = GetHardwareInfo();
            var allCores = Enumerable.Range(0, hardware.CpuLogicalProcessors).ToList();
            var result = new List<int>();

            if (!config.Enabled) return allCores;

            switch (config.Mode)
            {
                case AffinityMode.AllCores:
                    result = allCores;
                    break;

                case AffinityMode.SpecificCores:
                    result = config.CoreIndices.Where(c => c >= 0 && c < hardware.CpuLogicalProcessors).ToList();
                    break;

                case AffinityMode.PerformanceCoresOnly:
                    if (hardware.HasPerformanceCores && hardware.PerformanceCoreCount > 0)
                    {
                        // Cores de performance geralmente são os primeiros (Intel) ou últimos (ARM)
                        // Para Intel híbrido: cores 0,2,4... são performance (hyperthreading pares)
                        result = allCores.Where((_, i) => i % 2 == 0).Take(hardware.PerformanceCoreCount).ToList();
                    }
                    else
                    {
                        result = allCores;
                    }
                    break;

                case AffinityMode.EfficiencyCoresOnly:
                    if (hardware.HasPerformanceCores && hardware.EfficiencyCoreCount > 0)
                    {
                        result = allCores.Where((_, i) => i % 2 == 1).Take(hardware.EfficiencyCoreCount).ToList();
                    }
                    else
                    {
                        result = new List<int>();
                    }
                    break;

                case AffinityMode.ExcludeCore0:
                    result = allCores.Where(c => c != 0).ToList();
                    break;

                case AffinityMode.Custom:
                    result = config.CoreIndices.Where(c => c >= 0 && c < hardware.CpuLogicalProcessors).ToList();
                    break;
            }

            // Aplica exclusão do core 0 se configurado
            if (config.ExcludeCore0 && result.Contains(0))
            {
                result.Remove(0);
            }

            return result.Distinct().OrderBy(c => c).ToList();
        }

        private bool IsSystemProcess(Process process)
        {
            var systemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System", "Registry", "smss", "csrss", "wininit", "services", "lsass",
                "svchost", "fontdrvhost", "dwm", "taskhostw", "startmenuexperiencehost",
                "shellexperiencehost", "searchindexer", "searchprotocolhost", "searchfilterhost",
                "rundll32", "dllhost", "wmiprvse", "spoolsv", "armsvc", "nissrv", "nvcontainer",
                "rtss", "rtsshooksloader", "msmpeng", "sihost", "textinputhost"
            };

            return systemProcesses.Contains(process.ProcessName) || process.Id <= 4;
        }

        private bool HasGpuActivity(Process process)
        {
            // Heurística: processos com janela visível ou nome conhecido de jogo
            if (process.MainWindowHandle != IntPtr.Zero) return true;

            var knownGamePatterns = new[]
            {
                "game", "launcher", "steam", "epic", "origin", "ubisoft", "battle.net",
                "gog", "ea", "riot", "valorant", "league", "cs2", "csgo", "dota",
                "overwatch", "apex", "fortnite", "minecraft", "roblox", "unity", "unreal"
            };

            var name = process.ProcessName.ToLowerInvariant();
            return knownGamePatterns.Any(p => name.Contains(p));
        }

        private double GetProcessCpuUsage(Process process)
        {
            try
            {
                return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
            }
            catch
            {
                return 0;
            }
        }

        private bool IsProcessFullscreen(Process process)
        {
            try
            {
                var hwnd = process.MainWindowHandle;
                if (hwnd == IntPtr.Zero) return false;

                GetWindowRect(hwnd, out var rect);

                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;
                var screen = monitorInfo.rcMonitor;

                return rect.Left <= screen.Left && rect.Top <= screen.Top &&
                       rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
            }
            catch
            {
                return false;
            }
        }

        private bool IsProcessFocused(Process process)
        {
            try
            {
                return GetForegroundWindow() == process.MainWindowHandle;
            }
            catch
            {
                return false;
            }
        }

        private long CalculateAffinityMask(List<int> cores)
        {
            long mask = 0;
            foreach (var core in cores)
            {
                mask |= (1L << core);
            }
            return mask;
        }

        private bool IsGameModeEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\GameBar");
                return key?.GetValue("AllowAutoGameMode")?.ToString() == "1";
            }
            catch { return false; }
        }

        private bool IsGpuSchedulingEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                return key?.GetValue("HwSchMode")?.ToString() == "2";
            }
            catch { return false; }
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }
}
