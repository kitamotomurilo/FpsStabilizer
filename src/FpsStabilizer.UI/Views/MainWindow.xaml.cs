using System;
using System.Windows;
using System.Windows.Input;
using FpsStabilizer.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FpsStabilizer.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _services;
        private ProfileEditorWindow? _profileEditorWindow;
        private SettingsWindow? _settingsWindow;

        public MainWindow(MainViewModel viewModel, IServiceProvider services)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _services = services;
            DataContext = _viewModel;

            // Subscribe to messenger events
            MainViewModel.Messenger.OpenProfileEditorRequested += OnOpenProfileEditor;
            MainViewModel.Messenger.OpenSettingsRequested += OnOpenSettings;

            Loaded += OnLoaded;
            Closing += OnClosing;
            StateChanged += OnStateChanged;

            // Handle keyboard shortcuts
            KeyDown += OnKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Restore window state if needed
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Minimize to tray instead of closing if enabled
            if (_viewModel is MainViewModel vm)
            {
                // Note: MinimizeToTray setting would be checked here
                // For now, just close normally
            }

            MainViewModel.Messenger.OpenProfileEditorRequested -= OnOpenProfileEditor;
            MainViewModel.Messenger.OpenSettingsRequested -= OnOpenSettings;

            // Stop monitoring
            _ = _viewModel.StopMonitoringAsync();
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Could minimize to tray here if enabled
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+R = Refresh
            if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _viewModel.RefreshGamesCommand.Execute(null);
                e.Handled = true;
            }

            // Ctrl+N = New Profile
            if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _viewModel.OpenProfileEditorCommand.Execute(null);
                e.Handled = true;
            }

            // Escape = Close dialogs
            if (e.Key == Key.Escape)
            {
                if (_profileEditorWindow != null)
                {
                    _profileEditorWindow.Close();
                    e.Handled = true;
                }
                else if (_settingsWindow != null)
                {
                    _settingsWindow.Close();
                    e.Handled = true;
                }
            }
        }

        private void OnOpenProfileEditor(MainViewModel.OpenProfileEditorMessage msg)
        {
            if (_profileEditorWindow == null)
            {
                _profileEditorWindow = _services.GetRequiredService<ProfileEditorWindow>();
                _profileEditorWindow.Owner = this;
                _profileEditorWindow.Closed += (s, e) => _profileEditorWindow = null;

                // Subscribe to profile editor events
                ProfileEditorViewModel.Messenger.ProfileSaved += OnProfileSaved;
                ProfileEditorViewModel.Messenger.EditorClosed += OnProfileEditorClosed;
            }

            // Initialize for new profile
            if (_profileEditorWindow.DataContext is ProfileEditorViewModel pem)
            {
                pem.InitializeForNew();
            }

            _profileEditorWindow.Show();
            _profileEditorWindow.Activate();
        }

        private void OnProfileSaved(ProfileEditorViewModel.ProfileSavedMessage msg)
        {
            _profileEditorWindow?.Close();
        }

        private void OnProfileEditorClosed(ProfileEditorViewModel.ProfileEditorClosedMessage msg)
        {
            ProfileEditorViewModel.Messenger.ProfileSaved -= OnProfileSaved;
            ProfileEditorViewModel.Messenger.EditorClosed -= OnProfileEditorClosed;
            _profileEditorWindow = null;
        }

        private void OnOpenSettings(MainViewModel.OpenSettingsMessage msg)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = _services.GetRequiredService<SettingsWindow>();
                _settingsWindow.Owner = this;
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
    }
}