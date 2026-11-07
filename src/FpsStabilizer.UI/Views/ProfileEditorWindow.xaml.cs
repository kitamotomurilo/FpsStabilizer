using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FpsStabilizer.UI.ViewModels;

namespace FpsStabilizer.UI.Views
{
    /// <summary>
    /// Interaction logic for ProfileEditorWindow.xaml
    /// </summary>
    public partial class ProfileEditorWindow : Window
    {
        public ProfileEditorWindow(ProfileEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Atualiza subtítulo quando IsEditing muda
            if (viewModel != null)
            {
                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(viewModel.IsEditing))
                    {
                        UpdateSubtitle(viewModel.IsEditing);
                    }
                };
                UpdateSubtitle(viewModel.IsEditing);
            }
        }

        private void UpdateSubtitle(bool isEditing)
        {
            if (SubtitleText != null)
            {
                SubtitleText.Text = isEditing ? "Editar perfil existente" : "Criar novo perfil";
            }
        }

        private void PresetFps_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out var fps))
            {
                if (DataContext is ProfileEditorViewModel vm)
                {
                    vm.Profile.FpsLimit = fps > 0 ? fps : null;
                }
            }
        }

        private void CoreCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is int coreIndex)
            {
                if (DataContext is ProfileEditorViewModel vm)
                {
                    vm.ToggleCoreCommand.Execute(coreIndex);
                }
            }
        }
    }
}