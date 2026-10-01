using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SnapSync.Core;

namespace Flow.Launcher.Plugin.SnapSync.ViewModels;

/// <summary>
/// Simple command implementation for WPF data binding.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <param name="canExecute">The optional predicate determining whether execution is permitted.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// View model for editing and saving SnapSync settings.
/// </summary>
public sealed class SnapSyncSettingsViewModel : INotifyPropertyChanged
{
    private readonly SnapSyncConfiguration _configuration;
    private readonly Action _saveAction;

    private string _profileName = string.Empty;
    private bool _profileEnabled = true;
    private string? _profileDescription;

    private string _itemName = string.Empty;
    private bool _itemEnabled = true;
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;

    private string _statusMessage = string.Empty;
    private Brush _statusColor = Brushes.Black;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapSyncSettingsViewModel"/> class.
    /// </summary>
    /// <param name="configuration">The configuration model to edit.</param>
    /// <param name="saveAction">The callback invoked to persist configuration changes.</param>
    public SnapSyncSettingsViewModel(SnapSyncConfiguration configuration, Action saveAction)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _saveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));

        LoadFromConfiguration();

        BrowseSourceFileCommand = new RelayCommand(BrowseSourceFile);
        BrowseDestinationFileCommand = new RelayCommand(BrowseDestinationFile);
        SaveCommand = new RelayCommand(Save);
    }

    /// <summary>
    /// Gets or sets the name of the sync profile.
    /// </summary>
    public string ProfileName
    {
        get => _profileName;
        set => SetField(ref _profileName, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the sync profile is enabled.
    /// </summary>
    public bool ProfileEnabled
    {
        get => _profileEnabled;
        set => SetField(ref _profileEnabled, value);
    }

    /// <summary>
    /// Gets or sets the description of the sync profile.
    /// </summary>
    public string? ProfileDescription
    {
        get => _profileDescription;
        set => SetField(ref _profileDescription, value);
    }

    /// <summary>
    /// Gets or sets the name of the file sync item.
    /// </summary>
    public string ItemName
    {
        get => _itemName;
        set => SetField(ref _itemName, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the sync item is enabled.
    /// </summary>
    public bool ItemEnabled
    {
        get => _itemEnabled;
        set => SetField(ref _itemEnabled, value);
    }

    /// <summary>
    /// Gets or sets the authoritative source file path.
    /// </summary>
    public string SourcePath
    {
        get => _sourcePath;
        set => SetField(ref _sourcePath, value);
    }

    /// <summary>
    /// Gets or sets the target destination file path.
    /// </summary>
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetField(ref _destinationPath, value);
    }

    /// <summary>
    /// Gets or sets the status or validation feedback message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets the color of the status message.
    /// </summary>
    public Brush StatusColor
    {
        get => _statusColor;
        set => SetField(ref _statusColor, value);
    }

    /// <summary>
    /// Gets the command to open a file dialog for the source file.
    /// </summary>
    public ICommand BrowseSourceFileCommand { get; }

    /// <summary>
    /// Gets the command to open a file dialog for the destination file.
    /// </summary>
    public ICommand BrowseDestinationFileCommand { get; }

    /// <summary>
    /// Gets the command to validate and save the settings.
    /// </summary>
    public ICommand SaveCommand { get; }

    private void LoadFromConfiguration()
    {
        var profile = _configuration.Profiles.FirstOrDefault();
        if (profile != null)
        {
            _profileName = profile.Name;
            _profileEnabled = profile.Enabled;
            _profileDescription = profile.Description;

            var item = profile.Items.FirstOrDefault();
            if (item != null)
            {
                _itemName = item.Name;
                _itemEnabled = item.Enabled;
                _sourcePath = item.Source;
                _destinationPath = item.Destinations.FirstOrDefault() ?? string.Empty;
            }
        }
    }

    private void BrowseSourceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Source File",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FileName;
        }
    }

    private void BrowseDestinationFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select Destination File",
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationPath = dialog.FileName;
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            StatusColor = Brushes.Red;
            StatusMessage = "Profile name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            StatusColor = Brushes.Red;
            StatusMessage = "Source file path is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusColor = Brushes.Red;
            StatusMessage = "Destination file path is required.";
            return;
        }

        var profile = _configuration.Profiles.FirstOrDefault();
        if (profile == null)
        {
            profile = new SyncProfile();
            _configuration.Profiles.Add(profile);
        }

        profile.Name = ProfileName.Trim();
        profile.Enabled = ProfileEnabled;
        profile.Description = string.IsNullOrWhiteSpace(ProfileDescription) ? null : ProfileDescription.Trim();

        var item = profile.Items.FirstOrDefault();
        if (item == null)
        {
            item = new SyncItem
            {
                ItemType = SyncItemType.File,
                ComparisonMode = ComparisonMode.Fast
            };
            profile.Items.Add(item);
        }

        item.Name = string.IsNullOrWhiteSpace(ItemName) ? profile.Name : ItemName.Trim();
        item.Enabled = ItemEnabled;
        item.Source = SourcePath.Trim();
        item.Destinations = [DestinationPath.Trim()];
        item.ItemType = SyncItemType.File;
        item.ComparisonMode = ComparisonMode.Fast;

        try
        {
            _saveAction();
            StatusColor = Brushes.DarkGreen;
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusColor = Brushes.Red;
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
