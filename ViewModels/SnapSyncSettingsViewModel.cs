using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SnapSync.Core;

namespace Flow.Launcher.Plugin.SnapSync.ViewModels;

/// <summary>
/// Simple command implementation for WPF data binding supporting both parameterless and parameterized actions.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = _ => execute();
        _canExecute = canExecute != null ? _ => canExecute() : null;
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>
/// View model representing an editable destination path entry.
/// </summary>
public sealed class DestinationEntryViewModel : INotifyPropertyChanged
{
    private string _path = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path
    {
        get => _path;
        set => SetField(ref _path, value);
    }

    public DestinationEntryViewModel(string path = "")
    {
        _path = path;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model representing an editable sync item within a profile.
/// </summary>
public sealed class SyncItemViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _enabled = true;
    private string _source = string.Empty;
    private SyncItemType _itemType = SyncItemType.Auto;
    private string _exclusionsText = string.Empty;
    private ComparisonMode _comparisonMode = ComparisonMode.Fast;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public string Source
    {
        get => _source;
        set => SetField(ref _source, value);
    }

    public ObservableCollection<DestinationEntryViewModel> Destinations { get; } = [];

    public SyncItemType ItemType
    {
        get => _itemType;
        set => SetField(ref _itemType, value);
    }

    public string ExclusionsText
    {
        get => _exclusionsText;
        set => SetField(ref _exclusionsText, value);
    }

    public ComparisonMode ComparisonMode
    {
        get => _comparisonMode;
        set => SetField(ref _comparisonMode, value);
    }

    public SyncItemViewModel(SyncItem? item = null)
    {
        Id = item?.Id ?? Guid.NewGuid().ToString("N");
        if (item != null)
        {
            _name = item.Name ?? string.Empty;
            _enabled = item.Enabled;
            _source = item.Source ?? string.Empty;
            if (item.Destinations != null)
            {
                foreach (var d in item.Destinations)
                {
                    Destinations.Add(new DestinationEntryViewModel(d));
                }
            }
            _itemType = item.ItemType;
            _comparisonMode = item.ComparisonMode;
            _exclusionsText = item.Exclusions != null ? string.Join(Environment.NewLine, item.Exclusions) : string.Empty;
        }

        if (Destinations.Count == 0)
        {
            Destinations.Add(new DestinationEntryViewModel());
        }
    }

    public SyncItem ToModel()
    {
        var rawDestinations = Destinations
            .Select(d => d.Path?.Trim() ?? string.Empty)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var exclusionList = (ExclusionsText ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return new SyncItem
        {
            Id = Id,
            Name = Name?.Trim() ?? string.Empty,
            Enabled = Enabled,
            Source = Source?.Trim() ?? string.Empty,
            Destinations = rawDestinations,
            ItemType = ItemType,
            ComparisonMode = ComparisonMode,
            Exclusions = exclusionList
        };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model representing an editable sync profile containing items.
/// </summary>
public sealed class SyncProfileViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _enabled = true;
    private string? _description;
    private SyncItemViewModel? _selectedItem;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public ObservableCollection<SyncItemViewModel> Items { get; } = [];

    public SyncItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public SyncProfileViewModel(SyncProfile? profile = null)
    {
        Id = profile?.Id ?? Guid.NewGuid().ToString("N");
        if (profile != null)
        {
            _name = profile.Name ?? string.Empty;
            _enabled = profile.Enabled;
            _description = profile.Description;
            if (profile.Items != null)
            {
                foreach (var it in profile.Items)
                {
                    Items.Add(new SyncItemViewModel(it));
                }
            }
        }
        SelectedItem = Items.FirstOrDefault();
    }

    public SyncProfile ToModel()
    {
        return new SyncProfile
        {
            Id = Id,
            Name = Name?.Trim() ?? string.Empty,
            Enabled = Enabled,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Items = Items.Select(i => i.ToModel()).ToList()
        };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for managing the full profile catalog, items, multiple destinations, and settings.
/// </summary>
public sealed class SnapSyncSettingsViewModel : INotifyPropertyChanged
{
    private readonly SnapSyncConfiguration _configuration;
    private readonly Action _saveAction;
    private readonly ISyncEngine _syncEngine;

    private SyncProfileViewModel? _selectedProfile;
    private string _statusMessage = string.Empty;
    private Brush _statusColor = Brushes.Black;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SyncProfileViewModel> Profiles { get; } = [];

    public SyncProfileViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value))
            {
                if (_selectedProfile != null && _selectedProfile.SelectedItem == null)
                {
                    _selectedProfile.SelectedItem = _selectedProfile.Items.FirstOrDefault();
                }
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public Brush StatusColor
    {
        get => _statusColor;
        set => SetField(ref _statusColor, value);
    }

    // Profile Commands
    public ICommand AddProfileCommand { get; }
    public ICommand DuplicateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand MoveProfileUpCommand { get; }
    public ICommand MoveProfileDownCommand { get; }

    // Item Commands
    public ICommand AddItemCommand { get; }
    public ICommand DuplicateItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand MoveItemUpCommand { get; }
    public ICommand MoveItemDownCommand { get; }
    public ICommand BrowseSourceFileCommand { get; }

    // Destination Commands
    public ICommand AddDestinationCommand { get; }
    public ICommand RemoveDestinationCommand { get; }
    public ICommand MoveDestinationUpCommand { get; }
    public ICommand MoveDestinationDownCommand { get; }
    public ICommand BrowseDestinationCommand { get; }

    // Save Command
    public ICommand SaveCommand { get; }

    public SnapSyncSettingsViewModel(SnapSyncConfiguration configuration, Action saveAction, ISyncEngine? syncEngine = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _saveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
        _syncEngine = syncEngine ?? new SyncEngine();

        LoadFromConfiguration();

        AddProfileCommand = new RelayCommand(AddProfile);
        DuplicateProfileCommand = new RelayCommand(DuplicateProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        MoveProfileUpCommand = new RelayCommand(MoveProfileUp);
        MoveProfileDownCommand = new RelayCommand(MoveProfileDown);

        AddItemCommand = new RelayCommand(AddItem);
        DuplicateItemCommand = new RelayCommand(DuplicateItem);
        DeleteItemCommand = new RelayCommand(DeleteItem);
        MoveItemUpCommand = new RelayCommand(MoveItemUp);
        MoveItemDownCommand = new RelayCommand(MoveItemDown);
        BrowseSourceFileCommand = new RelayCommand(BrowseSourceFile);

        AddDestinationCommand = new RelayCommand(AddDestination);
        RemoveDestinationCommand = new RelayCommand(RemoveDestination);
        MoveDestinationUpCommand = new RelayCommand(MoveDestinationUp);
        MoveDestinationDownCommand = new RelayCommand(MoveDestinationDown);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination);

        SaveCommand = new RelayCommand(Save);
    }

    private void LoadFromConfiguration()
    {
        Profiles.Clear();
        if (_configuration.Profiles != null)
        {
            foreach (var profile in _configuration.Profiles)
            {
                Profiles.Add(new SyncProfileViewModel(profile));
            }
        }
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void AddProfile()
    {
        var baseName = "New Profile";
        var candidateName = baseName;
        var counter = 2;
        while (Profiles.Any(p => string.Equals(p.Name.Trim(), candidateName, StringComparison.OrdinalIgnoreCase)))
        {
            candidateName = $"{baseName} {counter++}";
        }

        var newProfile = new SyncProfileViewModel
        {
            Name = candidateName,
            Enabled = true
        };
        newProfile.Items.Add(new SyncItemViewModel
        {
            Name = "Item 1",
            Enabled = true
        });
        newProfile.SelectedItem = newProfile.Items[0];

        Profiles.Add(newProfile);
        SelectedProfile = newProfile;
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile == null) return;

        var sourceProfile = SelectedProfile;
        var copyBaseName = $"{sourceProfile.Name} (Copy)";
        var candidateName = copyBaseName;
        var counter = 2;
        while (Profiles.Any(p => string.Equals(p.Name.Trim(), candidateName, StringComparison.OrdinalIgnoreCase)))
        {
            candidateName = $"{sourceProfile.Name} (Copy {counter++})";
        }

        var newProfile = new SyncProfileViewModel
        {
            Name = candidateName,
            Enabled = sourceProfile.Enabled,
            Description = sourceProfile.Description
        };

        foreach (var item in sourceProfile.Items)
        {
            var newItem = new SyncItemViewModel
            {
                Name = item.Name,
                Enabled = item.Enabled,
                Source = item.Source,
                ItemType = item.ItemType,
                ComparisonMode = item.ComparisonMode,
                ExclusionsText = item.ExclusionsText
            };
            newItem.Destinations.Clear();
            foreach (var d in item.Destinations)
            {
                newItem.Destinations.Add(new DestinationEntryViewModel(d.Path));
            }
            newProfile.Items.Add(newItem);
        }
        newProfile.SelectedItem = newProfile.Items.FirstOrDefault();

        var index = Profiles.IndexOf(sourceProfile);
        Profiles.Insert(index + 1, newProfile);
        SelectedProfile = newProfile;
    }

    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete profile '{SelectedProfile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var index = Profiles.IndexOf(SelectedProfile);
            Profiles.RemoveAt(index);
            SelectedProfile = Profiles.Count > 0 ? Profiles[Math.Min(index, Profiles.Count - 1)] : null;
        }
    }

    private void MoveProfileUp()
    {
        if (SelectedProfile == null) return;
        var index = Profiles.IndexOf(SelectedProfile);
        if (index > 0)
        {
            Profiles.Move(index, index - 1);
        }
    }

    private void MoveProfileDown()
    {
        if (SelectedProfile == null) return;
        var index = Profiles.IndexOf(SelectedProfile);
        if (index >= 0 && index < Profiles.Count - 1)
        {
            Profiles.Move(index, index + 1);
        }
    }

    private void AddItem()
    {
        if (SelectedProfile == null) return;

        var baseName = "New Item";
        var candidateName = baseName;
        var counter = 2;
        while (SelectedProfile.Items.Any(i => string.Equals(i.Name.Trim(), candidateName, StringComparison.OrdinalIgnoreCase)))
        {
            candidateName = $"{baseName} {counter++}";
        }

        var newItem = new SyncItemViewModel
        {
            Name = candidateName,
            Enabled = true
        };

        SelectedProfile.Items.Add(newItem);
        SelectedProfile.SelectedItem = newItem;
    }

    private void DuplicateItem()
    {
        if (SelectedProfile?.SelectedItem == null) return;

        var sourceItem = SelectedProfile.SelectedItem;
        var copyBaseName = $"{sourceItem.Name} (Copy)";
        var candidateName = copyBaseName;
        var counter = 2;
        while (SelectedProfile.Items.Any(i => string.Equals(i.Name.Trim(), candidateName, StringComparison.OrdinalIgnoreCase)))
        {
            candidateName = $"{sourceItem.Name} (Copy {counter++})";
        }

        var newItem = new SyncItemViewModel
        {
            Name = candidateName,
            Enabled = sourceItem.Enabled,
            Source = sourceItem.Source,
            ItemType = sourceItem.ItemType,
            ComparisonMode = sourceItem.ComparisonMode,
            ExclusionsText = sourceItem.ExclusionsText
        };
        newItem.Destinations.Clear();
        foreach (var d in sourceItem.Destinations)
        {
            newItem.Destinations.Add(new DestinationEntryViewModel(d.Path));
        }

        var index = SelectedProfile.Items.IndexOf(sourceItem);
        SelectedProfile.Items.Insert(index + 1, newItem);
        SelectedProfile.SelectedItem = newItem;
    }

    private void DeleteItem()
    {
        if (SelectedProfile?.SelectedItem == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete item '{SelectedProfile.SelectedItem.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var index = SelectedProfile.Items.IndexOf(SelectedProfile.SelectedItem);
            SelectedProfile.Items.RemoveAt(index);
            SelectedProfile.SelectedItem = SelectedProfile.Items.Count > 0
                ? SelectedProfile.Items[Math.Min(index, SelectedProfile.Items.Count - 1)]
                : null;
        }
    }

    private void MoveItemUp()
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var index = SelectedProfile.Items.IndexOf(SelectedProfile.SelectedItem);
        if (index > 0)
        {
            SelectedProfile.Items.Move(index, index - 1);
        }
    }

    private void MoveItemDown()
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var index = SelectedProfile.Items.IndexOf(SelectedProfile.SelectedItem);
        if (index >= 0 && index < SelectedProfile.Items.Count - 1)
        {
            SelectedProfile.Items.Move(index, index + 1);
        }
    }

    private void BrowseSourceFile()
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var item = SelectedProfile.SelectedItem;

        var chooseFolder = item.ItemType == SyncItemType.Directory;
        if (item.ItemType == SyncItemType.Auto)
        {
            var choice = MessageBox.Show(
                "Do you want to select a Folder? Click 'Yes' for Folder, 'No' for File.",
                "Select Source Type",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) return;
            chooseFolder = choice == MessageBoxResult.Yes;
        }

        if (chooseFolder)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Source Folder"
            };
            if (folderDialog.ShowDialog() == true)
            {
                item.Source = folderDialog.FolderName;
            }
        }
        else
        {
            var fileDialog = new OpenFileDialog
            {
                Title = "Select Source File",
                CheckFileExists = true
            };
            if (fileDialog.ShowDialog() == true)
            {
                item.Source = fileDialog.FileName;
            }
        }
    }

    private void AddDestination()
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var newEntry = new DestinationEntryViewModel();
        SelectedProfile.SelectedItem.Destinations.Add(newEntry);
    }

    private void RemoveDestination(object? parameter)
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var item = SelectedProfile.SelectedItem;
        var entry = parameter as DestinationEntryViewModel;
        if (entry == null) return;

        if (item.Destinations.Count <= 1)
        {
            entry.Path = string.Empty;
            return;
        }

        item.Destinations.Remove(entry);
    }

    private void MoveDestinationUp(object? parameter)
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var item = SelectedProfile.SelectedItem;
        var entry = parameter as DestinationEntryViewModel;
        if (entry == null) return;

        var index = item.Destinations.IndexOf(entry);
        if (index > 0)
        {
            item.Destinations.Move(index, index - 1);
        }
    }

    private void MoveDestinationDown(object? parameter)
    {
        if (SelectedProfile?.SelectedItem == null) return;
        var item = SelectedProfile.SelectedItem;
        var entry = parameter as DestinationEntryViewModel;
        if (entry == null) return;

        var index = item.Destinations.IndexOf(entry);
        if (index >= 0 && index < item.Destinations.Count - 1)
        {
            item.Destinations.Move(index, index + 1);
        }
    }

    private void BrowseDestination(object? parameter)
    {
        var entry = parameter as DestinationEntryViewModel;
        if (entry == null || SelectedProfile?.SelectedItem == null) return;
        var item = SelectedProfile.SelectedItem;

        var chooseFolder = item.ItemType == SyncItemType.Directory;
        if (item.ItemType == SyncItemType.Auto)
        {
            var choice = MessageBox.Show(
                "Do you want to select a Folder? Click 'Yes' for Folder, 'No' for File.",
                "Select Destination Type",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) return;
            chooseFolder = choice == MessageBoxResult.Yes;
        }

        if (chooseFolder)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };
            if (folderDialog.ShowDialog() == true)
            {
                entry.Path = folderDialog.FolderName;
            }
        }
        else
        {
            var fileDialog = new SaveFileDialog
            {
                Title = "Select Destination File",
                CheckPathExists = true
            };
            if (fileDialog.ShowDialog() == true)
            {
                entry.Path = fileDialog.FileName;
            }
        }
    }

    public void Save()
    {
        var candidateProfiles = Profiles.Select(p => p.ToModel()).ToList();
        var candidateConfig = new SnapSyncConfiguration
        {
            SchemaVersion = _configuration.SchemaVersion,
            Profiles = candidateProfiles
        };

        var validationErrors = _syncEngine.Validate(candidateConfig);
        if (validationErrors.Count > 0)
        {
            StatusColor = Brushes.Red;
            StatusMessage = string.Join("; ", validationErrors.Select(e => e.Message));
            return;
        }

        var allWarnings = new List<string>();
        foreach (var p in candidateProfiles.Where(p => p.Enabled))
        {
            allWarnings.AddRange(_syncEngine.GetAvailabilityWarnings(p));
        }

        var previousProfiles = _configuration.Profiles;
        _configuration.Profiles = candidateProfiles;

        try
        {
            _saveAction();

            if (allWarnings.Count > 0)
            {
                StatusColor = Brushes.DarkOrange;
                StatusMessage = $"Saved with warnings: {string.Join("; ", allWarnings)}";
            }
            else
            {
                StatusColor = Brushes.DarkGreen;
                StatusMessage = "Settings saved successfully.";
            }
        }
        catch (Exception ex)
        {
            _configuration.Profiles = previousProfiles;
            StatusColor = Brushes.Red;
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
