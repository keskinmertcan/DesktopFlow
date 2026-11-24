using System.Collections.ObjectModel;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public sealed class FilterOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public override string ToString() => Label;
}

public partial class ActivityRowViewModel
{
    public required long Id { get; init; }
    public required string When { get; init; }
    public required string FileName { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public required string Details { get; init; }
    public bool CanUndo { get; init; }
}

public partial class ActivityViewModel : ViewModelBase
{
    private readonly IActivityStore _activities;
    private readonly IUndoService _undo;
    private readonly IUserPrompt _prompt;

    public ActivityViewModel(ILocalizer text, IActivityStore activities, IUndoService undo, IUserPrompt prompt)
        : base(text)
    {
        _activities = activities;
        _undo = undo;
        _prompt = prompt;
        RebuildFilters();
    }

    public ObservableCollection<ActivityRowViewModel> Rows { get; } = [];
    public ObservableCollection<FilterOption> Kinds { get; } = [];
    public ObservableCollection<FilterOption> Statuses { get; } = [];
    public ObservableCollection<FilterOption> Categories { get; } = [];
    public List<long> SelectedIds { get; } = [];

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private DateTimeOffset? _from;
    [ObservableProperty] private DateTimeOffset? _to;
    [ObservableProperty] private FilterOption? _selectedKind;
    [ObservableProperty] private FilterOption? _selectedStatus;
    [ObservableProperty] private FilterOption? _selectedCategory;

    public bool HasRows => Rows.Count > 0;

    public void SetSelection(IEnumerable<ActivityRowViewModel> rows)
    {
        SelectedIds.Clear();
        SelectedIds.AddRange(rows.Where(row => row.CanUndo).Select(row => row.Id));
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(LoadCoreAsync, cancellationToken);
    }

    [RelayCommand]
    private Task ApplyAsync()
    {
        return LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task UndoAsync(ActivityRowViewModel? row)
    {
        return row is null ? Task.CompletedTask : UndoManyAsync([row.Id]);
    }

    [RelayCommand]
    private Task UndoSelectedAsync()
    {
        return UndoManyAsync(SelectedIds.ToArray());
    }

    protected override void OnLanguageChanged()
    {
        RebuildFilters();
        _ = LoadAsync(CancellationToken.None);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (Categories.Count == 0)
        {
            RebuildFilters();
        }

        var categories = await _activities.GetCategoriesAsync(cancellationToken);
        var selectedCategory = SelectedCategory?.Id;
        Categories.Clear();
        Categories.Add(new FilterOption { Id = string.Empty, Label = Text["Activity_Any"] });
        foreach (var category in categories)
        {
            Categories.Add(new FilterOption { Id = category, Label = category });
        }

        SelectedCategory = Categories.FirstOrDefault(option => option.Id == selectedCategory) ?? Categories[0];
        var query = new ActivityQuery
        {
            FileNameContains = Search,
            From = From,
            To = To?.AddDays(1).AddTicks(-1),
            Kind = Parse<ActivityKind>(SelectedKind?.Id),
            Status = Parse<ActivityStatus>(SelectedStatus?.Id),
            CategoryKey = string.IsNullOrWhiteSpace(SelectedCategory?.Id) ? null : SelectedCategory.Id,
            Limit = 500,
        };
        var records = await _activities.QueryAsync(query, cancellationToken);
        Rows.Clear();
        foreach (var record in records)
        {
            Rows.Add(new ActivityRowViewModel
            {
                Id = record.Id,
                When = DisplayText.When(record.Timestamp),
                FileName = record.FileName,
                From = record.SourcePath,
                To = record.DestinationPath,
                Kind = TextMaps.Kind(Text, record.Kind),
                Status = TextMaps.Status(Text, record.Status),
                Details = record.ErrorMessage ?? string.Empty,
                CanUndo = record.CanUndo && !record.IsUndone && record.Status == ActivityStatus.Succeeded,
            });
        }

        OnPropertyChanged(nameof(HasRows));
    }

    private async Task UndoManyAsync(IReadOnlyList<long> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await GuardAsync(async cancellationToken =>
        {
            var needsAlternate = new List<long>();
            foreach (var id in ids)
            {
                var result = await _undo.UndoAsync(id, false, cancellationToken);
                if (result.Status == UndoStatus.NeedsAlternateName)
                {
                    needsAlternate.Add(id);
                }
                else if (result.Status == UndoStatus.Failed)
                {
                    ErrorMessage = result.ErrorMessage ?? Text["Result_Failed"];
                }
            }

            if (needsAlternate.Count > 0 && await _prompt.ConfirmAlternateAsync())
            {
                foreach (var id in needsAlternate)
                {
                    var result = await _undo.UndoAsync(id, true, cancellationToken);
                    if (result.Status == UndoStatus.Failed)
                    {
                        ErrorMessage = result.ErrorMessage ?? Text["Result_Failed"];
                    }
                }
            }

            await LoadCoreAsync(cancellationToken);
        }, CancellationToken.None);
    }

    private void RebuildFilters()
    {
        Kinds.Clear();
        Statuses.Clear();
        Kinds.Add(new FilterOption { Id = string.Empty, Label = Text["Activity_Any"] });
        Statuses.Add(new FilterOption { Id = string.Empty, Label = Text["Activity_Any"] });
        foreach (var kind in Enum.GetValues<ActivityKind>())
        {
            Kinds.Add(new FilterOption { Id = kind.ToString(), Label = TextMaps.Kind(Text, kind) });
        }

        foreach (var status in Enum.GetValues<ActivityStatus>())
        {
            Statuses.Add(new FilterOption { Id = status.ToString(), Label = TextMaps.Status(Text, status) });
        }

        SelectedKind ??= Kinds[0];
        SelectedStatus ??= Statuses[0];
    }

    private static TEnum? Parse<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;
    }
}
