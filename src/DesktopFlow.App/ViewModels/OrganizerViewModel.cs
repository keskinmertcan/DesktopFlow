using System.Collections.ObjectModel;
using DesktopFlow.App.Localization;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;

namespace DesktopFlow.App.ViewModels;

public partial class MappingGroupItem : ObservableObject
{
    public string CategoryKey { get; set; } = string.Empty;

    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private string _extensions = string.Empty;
}

public partial class OrganizerViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly IMonitorController _monitor;

    public OrganizerViewModel(ILocalizer text, ISettingsStore settings, IMonitorController monitor)
        : base(text)
    {
        _settings = settings;
        _monitor = monitor;
    }

    public ObservableCollection<MappingGroupItem> Groups { get; } = [];

    [ObservableProperty]
    private bool _editingDownloads;

    public string ScopeLabel => EditingDownloads ? Text["Organizer_Downloads"] : Text["Organizer_Desktop"];

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(async token =>
        {
            var preferences = await _settings.GetAsync(token);
            Fill(preferences.MappingsFor(EditingDownloads ? MonitorScope.Downloads : MonitorScope.Desktop));
        }, cancellationToken);
    }

    [RelayCommand]
    private Task UseDesktopAsync()
    {
        EditingDownloads = false;
        OnPropertyChanged(nameof(ScopeLabel));
        return LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task UseDownloadsAsync()
    {
        EditingDownloads = true;
        OnPropertyChanged(nameof(ScopeLabel));
        return LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await GuardAsync(async cancellationToken =>
        {
            var preferences = await _settings.GetAsync(cancellationToken);
            var root = EditingDownloads ? preferences.DownloadsPath : preferences.DesktopPath;
            var mappings = new List<ExtensionMapping>();
            foreach (var group in Groups)
            {
                if (!DestinationBuilder.TryCombineUnderRoot(string.IsNullOrWhiteSpace(root) ? @"C:\DesktopFlow" : root, group.RelativePath, out _, out var error))
                {
                    ErrorMessage = error ?? Text["Validation_Folder"];
                    return;
                }

                var extensions = group.Extensions.Split([',', ';', ' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (extensions.Length == 0)
                {
                    ErrorMessage = Text["Validation_Extensions"];
                    return;
                }

                var category = string.IsNullOrWhiteSpace(group.CategoryKey)
                    ? group.RelativePath.Trim().Replace('\\', '/').Replace('/', '.').ToLowerInvariant()
                    : group.CategoryKey;
                foreach (var extension in extensions.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    mappings.Add(new ExtensionMapping
                    {
                        Extension = FileClassifier.NormalizeExtension(extension),
                        CategoryKey = category,
                        RelativePath = group.RelativePath.Trim().Replace('\\', '/'),
                    });
                }
            }

            if (EditingDownloads)
            {
                preferences.DownloadsMappings = mappings;
            }
            else
            {
                preferences.DesktopMappings = mappings;
            }

            await _settings.SaveAsync(preferences, cancellationToken);
            _monitor.Reload();
            ErrorMessage = string.Empty;
        }, CancellationToken.None);
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        var preferences = await _settings.GetAsync(CancellationToken.None);
        if (EditingDownloads)
        {
            preferences.DownloadsMappings = DefaultTaxonomy.CreateDownloadsMappings();
        }
        else
        {
            preferences.DesktopMappings = DefaultTaxonomy.CreateDesktopMappings();
        }

        await _settings.SaveAsync(preferences, CancellationToken.None);
        await LoadAsync(CancellationToken.None);
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(ScopeLabel));
    }

    private void Fill(IReadOnlyList<ExtensionMapping> mappings)
    {
        Groups.Clear();
        foreach (var group in mappings.GroupBy(mapping => mapping.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(new MappingGroupItem
            {
                CategoryKey = group.First().CategoryKey,
                RelativePath = group.Key,
                Extensions = string.Join(", ", group.Select(mapping => mapping.Extension)),
            });
        }
    }
}
