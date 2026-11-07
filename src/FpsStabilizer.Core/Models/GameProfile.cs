using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FpsStabilizer.Core.Models
{
    /// <summary>
    /// Perfil de configuração para um jogo específico
    /// </summary>
    public class GameProfile
    {
        [JsonProperty("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("processName")]
        public string ProcessName { get; set; } = string.Empty;

        [JsonProperty("executablePath")]
        public string? ExecutablePath { get; set; }

        [JsonProperty("fpsLimit")]
        public int? FpsLimit { get; set; }

        [JsonProperty("useRtss")]
        public bool UseRtss { get; set; } = true;

        [JsonProperty("priority")]
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;

        [JsonProperty("cpuAffinity")]
        public CpuAffinityConfig CpuAffinity { get; set; } = new();

        [JsonProperty("environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        [JsonProperty("launchArguments")]
        public string LaunchArguments { get; set; } = string.Empty;

        [JsonProperty("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        [JsonProperty("autoApply")]
        public bool AutoApply { get; set; } = true;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ProcessName : Name;

        public GameProfile Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<GameProfile>(json)!;
        }

        public void UpdateTimestamp() => UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Prioridade do processo
    /// </summary>
    public enum ProcessPriority
    {
        Idle = 0,
        BelowNormal = 1,
        Normal = 2,
        AboveNormal = 3,
        High = 4,
        RealTime = 5
    }

    /// <summary>
    /// Configuração de afinidade de CPU
    /// </summary>
    public class CpuAffinityConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("mode")]
        public AffinityMode Mode { get; set; } = AffinityMode.AllCores;

        [JsonProperty("coreIndices")]
        public List<int> CoreIndices { get; set; } = new();

        [JsonProperty("excludeCore0")]
        public bool ExcludeCore0 { get; set; } = true;

        [JsonProperty("preferPerformanceCores")]
        public bool PreferPerformanceCores { get; set; } = true;

        [JsonProperty("preferEfficiencyCores")]
        public bool PreferEfficiencyCores { get; set; } = false;
    }

    /// <summary>
    /// Modo de afinidade
    /// </summary>
    public enum AffinityMode
    {
        AllCores = 0,
        SpecificCores = 1,
        PerformanceCoresOnly = 2,
        EfficiencyCoresOnly = 3,
        ExcludeCore0 = 4,
        Custom = 5
    }
}