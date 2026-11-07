using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FpsStabilizer.Core.Services;
using FpsStabilizer.UI.ViewModels;
using FpsStabilizer.UI.Views;

namespace FpsStabilizer.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configura DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Inicializa logging
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("FpsStabilizer iniciado");

            // Verifica argumentos de linha de comando
            var args = Environment.GetCommandLineArgs();
            bool startMinimized = args.Contains("/start") || args.Contains("--start") || args.Contains("-start");
            bool startWithWindows = args.Contains("/startup") || args.Contains("--startup");

            // Verifica se já está rodando (single instance)
            var mutex = new System.Threading.Mutex(true, "FpsStabilizerSingleInstance", out var createdNew);
            if (!createdNew)
            {
                logger.LogWarning("Instância já em execução");
                if (!startMinimized)
                {
                    MessageBox.Show("FpsStabilizer já está em execução.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                Shutdown();
                return;
            }

            // Cria janela principal
            var mainWindow = Services.GetRequiredService<MainWindow>();

            if (startMinimized || startWithWindows)
            {
                // Inicia minimizado na bandeja (não mostra janela)
                logger.LogInformation("Iniciando minimizado na bandeja do sistema");
                mainWindow.Hide();
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
            }
            else
            {
                // Mostra janela normalmente
                mainWindow.Show();
            }

            // Inicia monitoramento automaticamente se configurado
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            if (startMinimized || startWithWindows)
            {
                _ = mainViewModel.StartMonitoringAsync();
            }

            // Mantém mutex vivo
            GC.KeepAlive(mutex);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = Services?.GetService<ILogger<App>>();
            logger?.LogInformation("FpsStabilizer finalizado");
            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Core Services
            services.AddSingleton<IRtssService, RtssService>();
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IProfileService, ProfileService>();
            services.AddSingleton<IGameDetectionService, GameDetectionService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<ProfileEditorViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Views
            services.AddTransient<MainWindow>();
            services.AddTransient<ProfileEditorWindow>();
            services.AddTransient<SettingsWindow>();
        }
    }
}