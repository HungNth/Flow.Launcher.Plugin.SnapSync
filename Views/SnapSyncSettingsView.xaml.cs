using System.Windows.Controls;
using Flow.Launcher.Plugin.SnapSync.ViewModels;

namespace Flow.Launcher.Plugin.SnapSync.Views;

/// <summary>
/// Interaction logic for SnapSyncSettingsView.xaml.
/// </summary>
public partial class SnapSyncSettingsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapSyncSettingsView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model for the settings control.</param>
    public SnapSyncSettingsView(SnapSyncSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
