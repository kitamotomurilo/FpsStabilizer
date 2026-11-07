using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FpsStabilizer.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FpsStabilizer.Core.Services
{
    public interface IProfileService
    {
        ObservableCollection<GameProfile> Profiles { get; }
        Task LoadAsync();
        Task SaveAsync();
        Task<GameProfile?> GetByProcessNameAsync(string processName);
        Task AddAsync(GameProfile profile);
        Task UpdateAsync(GameProfile profile);
        Task DeleteAsync(Guid id);
        Task ExportAsync(string filePath, IEnumerable<GameProfile> profiles);
        Task<List<GameProfile>> ImportAsync(string filePath);
    }

    public class ProfileService : IProfileService
    {
        private readonly ILogger<ProfileService> _logger;
        private readonly string _profilesPath;
        private readonly object _lock = new();

        public ObservableCollection<GameProfile> Profiles { get; } = new();

        public ProfileService(ILogger<ProfileService> logger)
        {
            _logger = logger;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "FpsStabilizer");
            Directory.CreateDirectory(appFolder);
            _profilesPath = Path.Combine(appFolder, "profiles.json");

            // Carrega perfis padrão se arquivo não existir
            if (!File.Exists(_profilesPath))
            {
                CreateDefaultProfiles();
            }
        }

        private void CreateDefaultProfiles()
        {
            var defaults = new List<GameProfile>
            {
                new GameProfile
                {
                    Name = "Competitivo (Alta Performance)",
                    ProcessName = "",
                    FpsLimit = 144,
                    UseRtss = true,
                    Priority = ProcessPriority.High,
                    CpuAffinity = new CpuAffinityConfig
                    {
                        Enabled = true,
                        Mode = AffinityMode.PerformanceCoresOnly,
                        ExcludeCore0 = true
                    },
                    AutoApply = false
                },
                new GameProfile
                {
                    Name = "Equilibrado (60 FPS)",
                    ProcessName = "",
                    FpsLimit = 60,
                    UseRtss = true,
                    Priority = ProcessPriority.AboveNormal,
                    CpuAffinity = new CpuAffinityConfig
                    {
                        Enabled = true,
                        Mode = AffinityMode.ExcludeCore0
                    },
                    AutoApply = false
                },
                new GameProfile
                {
                    Name = "Economia de Energia (30 FPS)",
                    ProcessName = "",
                    FpsLimit = 30,
                    UseRtss = true,
                    Priority = ProcessPriority.Normal,
                    CpuAffinity = new CpuAffinityConfig
                    {
                        Enabled = true,
                        Mode = AffinityMode.EfficiencyCoresOnly
                    },
                    AutoApply = false
                }
            };

            foreach (var profile in defaults)
            {
                Profiles.Add(profile);
            }

            _ = SaveAsync();
        }

        public async Task LoadAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        if (!File.Exists(_profilesPath))
                        {
                            CreateDefaultProfiles();
                            return;
                        }

                        var json = File.ReadAllText(_profilesPath);
                        var profiles = JsonConvert.DeserializeObject<List<GameProfile>>(json);

                        if (profiles != null)
                        {
                            Profiles.Clear();
                            foreach (var profile in profiles.OrderBy(p => p.Name))
                            {
                                Profiles.Add(profile);
                            }
                        }

                        _logger.LogInformation("Carregados {Count} perfis de {Path}", Profiles.Count, _profilesPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao carregar perfis");
                        CreateDefaultProfiles();
                    }
                }
            });
        }

        public async Task SaveAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(Profiles, Formatting.Indented,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                DefaultValueHandling = DefaultValueHandling.Ignore
                            });

                        var tempPath = _profilesPath + ".tmp";
                        File.WriteAllText(tempPath, json);
                        File.Move(tempPath, _profilesPath, true);

                        _logger.LogDebug("Perfis salvos: {Count}", Profiles.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao salvar perfis");
                    }
                }
            });
        }

        public async Task<GameProfile?> GetByProcessNameAsync(string processName)
        {
            await LoadAsync();

            return Profiles.FirstOrDefault(p =>
                p.IsActive &&
                p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddAsync(GameProfile profile)
        {
            profile.CreatedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            Profiles.Add(profile);
            await SaveAsync();

            _logger.LogInformation("Perfil adicionado: {Name} ({Id})", profile.Name, profile.Id);
        }

        public async Task UpdateAsync(GameProfile profile)
        {
            var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existing != null)
            {
                var index = Profiles.IndexOf(existing);
                profile.UpdatedAt = DateTime.UtcNow;
                profile.CreatedAt = existing.CreatedAt;
                Profiles[index] = profile;
                await SaveAsync();

                _logger.LogInformation("Perfil atualizado: {Name} ({Id})", profile.Name, profile.Id);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var profile = Profiles.FirstOrDefault(p => p.Id == id);
            if (profile != null)
            {
                Profiles.Remove(profile);
                await SaveAsync();

                _logger.LogInformation("Perfil removido: {Name} ({Id})", profile.Name, id);
            }
        }

        public async Task ExportAsync(string filePath, IEnumerable<GameProfile> profiles)
        {
            await Task.Run(() =>
            {
                try
                {
                    var exportData = new
                    {
                        Version = "1.0",
                        ExportedAt = DateTime.UtcNow,
                        Profiles = profiles.ToList()
                    };

                    var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                    File.WriteAllText(filePath, json);

                    _logger.LogInformation("Perfis exportados para: {Path}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao exportar perfis");
                    throw;
                }
            });
        }

        public async Task<List<GameProfile>> ImportAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var token = JToken.Parse(json);

                    List<GameProfile> imported;

                    if (token is JObject obj && obj["Profiles"] != null)
                    {
                        // Formato novo com metadados
                        imported = obj["Profiles"]!.ToObject<List<GameProfile>>() ?? new List<GameProfile>();
                    }
                    else
                    {
                        // Formato antigo (array direto)
                        imported = token.ToObject<List<GameProfile>>() ?? new List<GameProfile>();
                    }

                    // Regenera IDs para evitar conflitos
                    foreach (var profile in imported)
                    {
                        profile.Id = Guid.NewGuid();
                        profile.CreatedAt = DateTime.UtcNow;
                        profile.UpdatedAt = DateTime.UtcNow;
                    }

                    _logger.LogInformation("Importados {Count} perfis de: {Path}", imported.Count, filePath);
                    return imported;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao importar perfis");
                    throw;
                }
            });
        }
    }
}