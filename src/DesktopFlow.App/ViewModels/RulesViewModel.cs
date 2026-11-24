using System.Collections.ObjectModel;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

public partial class RuleRowViewModel : ObservableObject
{
    public required AutomationRule Rule { get; init; }
    public required string Summary { get; init; }
    public Func<RuleRowViewModel, Task>? Persist { get; init; }

    public bool IsEnabled
    {
        get => Rule.IsEnabled;
        set => Rule.IsEnabled = value;
    }

    public void OnToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (Persist is not null)
        {
            _ = Persist(this);
        }
    }
}

public partial class RulesViewModel : ViewModelBase
{
    private readonly IRuleStore _rules;
    private readonly IRuleEngine _engine;
    private readonly ISettingsStore _settings;
    private readonly IUserPrompt _prompt;

    public RulesViewModel(ILocalizer text, IRuleStore rules, IRuleEngine engine, ISettingsStore settings, IUserPrompt prompt)
        : base(text)
    {
        _rules = rules;
        _engine = engine;
        _settings = settings;
        _prompt = prompt;
    }

    public ObservableCollection<RuleRowViewModel> Rules { get; } = [];
    public ObservableCollection<string> Conflicts { get; } = [];

    public bool HasRules => Rules.Count > 0;
    public bool HasConflicts => Conflicts.Count > 0;

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return GuardAsync(LoadCoreAsync, cancellationToken);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var created = await _prompt.EditRuleAsync(new AutomationRule { Priority = NextPriority() });
        if (created is null)
        {
            return;
        }

        await _rules.SaveAsync(created, CancellationToken.None);
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task EditAsync(RuleRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var edited = await _prompt.EditRuleAsync(row.Rule);
        if (edited is null)
        {
            return;
        }

        await _rules.SaveAsync(edited, CancellationToken.None);
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task DeleteAsync(RuleRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!await _prompt.ConfirmAsync(Text["Rules_Delete"], Text["Rules_DeleteConfirm"]))
        {
            return;
        }

        await _rules.DeleteAsync(row.Rule.Id, CancellationToken.None);
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ToggleAsync(RuleRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await _rules.SaveAsync(row.Rule, CancellationToken.None);
        await LoadConflictsAsync(CancellationToken.None);
    }

    [RelayCommand]
    private Task MoveUpAsync(RuleRowViewModel? row)
    {
        return MoveAsync(row, -1);
    }

    [RelayCommand]
    private Task MoveDownAsync(RuleRowViewModel? row)
    {
        return MoveAsync(row, 1);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var rules = await _rules.GetAllAsync(cancellationToken);
        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(new RuleRowViewModel
            {
                Rule = rule,
                Summary = $"{rule.Name}  ·  {TextMaps.Scope(Text, rule.Scope)}  ·  {rule.MatchValue}  ·  {rule.Priority}",
                Persist = ToggleAsync,
            });
        }

        await LoadConflictsAsync(cancellationToken);
        OnPropertyChanged(nameof(HasRules));
    }

    private async Task LoadConflictsAsync(CancellationToken cancellationToken)
    {
        var preferences = await _settings.GetAsync(cancellationToken);
        var rules = Rules.Select(row => row.Rule).ToList();
        var mappings = preferences.DesktopMappings.Concat(preferences.DownloadsMappings).ToList();
        Conflicts.Clear();
        foreach (var conflict in _engine.FindConflicts(rules, mappings))
        {
            Conflicts.Add(conflict.Detail is not null
                ? Text.Format("Conflict_Mapping", conflict.FirstRule, conflict.Detail, conflict.SecondRule)
                : conflict.SamePriority
                    ? Text.Format("Conflict_Same", conflict.FirstRule, conflict.SecondRule)
                    : Text.Format("Conflict_Override", conflict.FirstRule, conflict.SecondRule));
        }

        if (Conflicts.Count == 0)
        {
            Conflicts.Add(Text["Rules_None"]);
        }

        OnPropertyChanged(nameof(HasConflicts));
    }

    private async Task MoveAsync(RuleRowViewModel? row, int direction)
    {
        if (row is null)
        {
            return;
        }

        var ordered = Rules.Select(item => item.Rule).OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name).ToList();
        var index = ordered.FindIndex(rule => rule.Id == row.Rule.Id);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index].Priority, ordered[target].Priority) = (ordered[target].Priority, ordered[index].Priority);
        if (ordered[index].Priority == ordered[target].Priority)
        {
            ordered[index].Priority += direction < 0 ? -1 : 1;
        }

        await _rules.SaveAsync(ordered[index], CancellationToken.None);
        await _rules.SaveAsync(ordered[target], CancellationToken.None);
        await LoadAsync(CancellationToken.None);
    }

    private int NextPriority()
    {
        return Rules.Count == 0 ? 10 : Rules.Min(row => row.Rule.Priority) - 1;
    }
}
