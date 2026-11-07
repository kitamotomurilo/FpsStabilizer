using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace FpsStabilizer.Core.Models
{
    /// <summary>
    /// Representa um jogo detectado em execução
    /// </summary>
    public class DetectedGame
    {
        [JsonProperty("processId")]
        public int ProcessId { get; set; }

        [JsonProperty("processName")]
        public string ProcessName { get; set; } = string.Empty;

        [JsonProperty("mainWindowTitle")]
        public string MainWindowTitle { get; set; } = string.Empty;

        [JsonProperty("executablePath")]
        public string ExecutablePath { get; set; } = string.Empty;

        [JsonProperty("workingDirectory")]
        public string WorkingDirectory { get; set; } = string.Empty;

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("cpuUsage")]
        public double CpuUsage { get; set; }

        [JsonProperty("memoryUsageMB")]
        public long MemoryUsageMB { get; set; }

        [JsonProperty("isFullscreen")]
        public bool IsFullscreen { get; set; }

        [JsonProperty("isFocused")]
        public bool IsFocused { get; set; }

        [JsonProperty("gpuUsage")]
        public double? GpuUsage { get; set; }

        [JsonProperty("matchedProfileId")]
        public Guid? MatchedProfileId { get; set; }

        [JsonProperty("matchedProfileName")]
        public string? MatchedProfileName { get; set; }

        [JsonIgnore]
        public TimeSpan Uptime => DateTime.Now - StartTime;

        public static DetectedGame FromProcess(Process process)
        {
            var game = new DetectedGame
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = process.StartTime,
            };

            try
            {
                game.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                game.WorkingDirectory = System.IO.Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty;
                game.MainWindowTitle = process.MainWindowTitle;
            }
            catch
            {
                // Acesso negado a processos do sistema
            }

            return game;
        }

        public override string ToString() => $"{ProcessName} (PID: {ProcessId})";
    }

    /// <summary>
    /// Informações de hardware do sistema
    /// </summary>
    public class SystemHardwareInfo
    {
        [JsonProperty("cpuName")]
        public string CpuName { get; set; } = string.Empty;

        [JsonProperty("cpuCores")]
        public int CpuCores { get; set; }

        [JsonProperty("cpuLogicalProcessors")]
        public int CpuLogicalProcessors { get; set; }

        [JsonProperty("hasPerformanceCores")]
        public bool HasPerformanceCores { get; set; }

        [JsonProperty("performanceCoreCount")]
        public int PerformanceCoreCount { get; set; }

        [JsonProperty("efficiencyCoreCount")]
        public int EfficiencyCoreCount { get; set; }

        [JsonProperty("gpuName")]
        public string GpuName { get; set; } = string.Empty;

        [JsonProperty("gpuVendor")]
        public GpuVendor GpuVendor { get; set; }

        [JsonProperty("totalMemoryGB")]
        public double TotalMemoryGB { get; set; }

        [JsonProperty("osVersion")]
        public string OsVersion { get; set; } = string.Empty;

        [JsonProperty("isGameModeEnabled")]
        public bool IsGameModeEnabled { get; set; }

        [JsonProperty("isGpuSchedulingEnabled")]
        public bool IsGpuSchedulingEnabled { get; set; }
    }

    public enum GpuVendor
    {
        Unknown = 0,
        Nvidia = 1,
        Amd = 2,
        Intel = 3,
        Other = 4
    }
}