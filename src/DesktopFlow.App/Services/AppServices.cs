using System.Diagnostics;
using DesktopFlow.App.Localization;
using DesktopFlow.Core.Models;
using DesktopFlow.Core.Organization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Services;

public interface IUiDispatcher
{
    void Attach(DispatcherQueue queue);

    void Enqueue(Action action);
}

public sealed class WinUiDispatcher : IUiDispatcher
{
    private DispatcherQueue? _queue;

    public void Attach(DispatcherQueue queue)
    {
        _queue = queue;
    }

    public void Enqueue(Action action)
    {
        if (_queue is null || _queue.HasThreadAccess)
        {
            action();
            return;
        }

        _queue.TryEnqueue(() => action());
    }
}

public interface IUserPrompt
{
    void Attach(XamlRoot root);

    Task ShowAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    Task<bool> ConfirmMovesAsync(IReadOnlyList<PlannedMove> moves);

    Task<bool> ConfirmAlternateAsync();

    Task<AutomationRule?> EditRuleAsync(AutomationRule draft);
}

public sealed class DialogService : IUserPrompt
{
    private readonly ILocalizer _text;
    private XamlRoot? _root;

    public DialogService(ILocalizer text)
    {
        _text = text;
    }

    public void Attach(XamlRoot root)
    {
        _root = root;
    }

    public async Task ShowAsync(string title, string message)
    {
        var dialog = Create(title, message);
        dialog.CloseButtonText = _text["Dialog_OK"];
        await dialog.ShowAsync();
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = Create(title, message);
        dialog.PrimaryButtonText = _text["Dialog_Confirm"];
        dialog.CloseButtonText = _text["Dialog_Cancel"];
        dialog.DefaultButton = ContentDialogButton.Primary;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmMovesAsync(IReadOnlyList<PlannedMove> moves)
    {
        var moving = moves.Where(move => move.WillMove).Take(40).ToList();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = _text.Format("Dialog_OrganizeBody", moves.Count(move => move.WillMove), moves.Count(move => !move.WillMove)),
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        var list = new ListView { MaxHeight = 320, SelectionMode = ListViewSelectionMode.None };
        foreach (var move in moving)
        {
            list.Items.Add($"{move.FileName}  →  {move.RelativeFolder}");
        }

        panel.Children.Add(list);
        var dialog = Create(_text["Dialog_OrganizeTitle"], panel);
        dialog.PrimaryButtonText = _text["Dialog_Confirm"];
        dialog.CloseButtonText = _text["Dialog_Cancel"];
        dialog.DefaultButton = ContentDialogButton.Close;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public Task<bool> ConfirmAlternateAsync()
    {
        return ConfirmAsync(_text["Activity_Undo"], _text["Dialog_Alternate"]);
    }

    public async Task<AutomationRule?> EditRuleAsync(AutomationRule draft)
    {
        var name = Box(draft.Name);
        var value = Box(draft.MatchValue);
        var folder = Box(draft.RelativePath);
        var priority = new NumberBox
        {
            Value = draft.Priority,
            Minimum = 1,
            Maximum = 10000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var enabled = new ToggleSwitch { IsOn = draft.IsEnabled };
        var scope = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        scope.Items.Add(CreateOption(MonitorScope.Desktop, _text["Scope_Desktop"]));
        scope.Items.Add(CreateOption(MonitorScope.Downloads, _text["Scope_Downloads"]));
        scope.SelectedIndex = draft.Scope == MonitorScope.Downloads ? 1 : 0;

        var match = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in Enum.GetValues<RuleMatchType>())
        {
            match.Items.Add(CreateOption(item, _text[MatchKey(item)]));
        }

        match.SelectedIndex = (int)draft.MatchType;

        var action = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        action.Items.Add(CreateOption(RuleActionType.MoveToRelativeFolder, _text["Action_Move"]));
        action.Items.Add(CreateOption(RuleActionType.Skip, _text["Action_Skip"]));
        action.SelectedIndex = draft.ActionType == RuleActionType.Skip ? 1 : 0;

        var error = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 224, 82, 82)),
        };
        var form = new StackPanel { Spacing = 10, MinWidth = 420 };
        form.Children.Add(Label(_text["Rules_Name"]));
        form.Children.Add(name);
        form.Children.Add(Label(_text["Rules_Scope"]));
        form.Children.Add(scope);
        form.Children.Add(Label(_text["Rules_Match"]));
        form.Children.Add(match);
        form.Children.Add(Label(_text["Rules_Value"]));
        form.Children.Add(value);
        form.Children.Add(Label(_text["Rules_Action"]));
        form.Children.Add(action);
        form.Children.Add(Label(_text["Rules_Folder"]));
        form.Children.Add(folder);
        form.Children.Add(Label(_text["Rules_Priority"]));
        form.Children.Add(priority);
        form.Children.Add(new TextBlock { Text = _text["Rules_Enabled"] });
        form.Children.Add(enabled);
        form.Children.Add(error);

        var dialog = Create(draft.Name.Length == 0 ? _text["Rules_Add"] : _text["Rules_Edit"], form);
        dialog.PrimaryButtonText = _text["Rules_Save"];
        dialog.CloseButtonText = _text["Dialog_Cancel"];
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.PrimaryButtonClick += (sender, args) =>
        {
            var selectedMatch = ((Option<RuleMatchType>)match.SelectedItem).Value;
            var selectedAction = ((Option<RuleActionType>)action.SelectedItem).Value;
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                error.Text = _text["Validation_Name"];
                args.Cancel = true;
                return;
            }

            if (selectedMatch != RuleMatchType.Always && string.IsNullOrWhiteSpace(value.Text))
            {
                error.Text = _text["Validation_Value"];
                args.Cancel = true;
                return;
            }

            if (selectedAction == RuleActionType.MoveToRelativeFolder
                && !DestinationBuilder.TryCombineUnderRoot(@"C:\DesktopFlow", folder.Text, out _, out _))
            {
                error.Text = _text["Validation_Folder"];
                args.Cancel = true;
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        draft.Name = name.Text.Trim();
        draft.Scope = ((Option<MonitorScope>)scope.SelectedItem).Value;
        draft.MatchType = ((Option<RuleMatchType>)match.SelectedItem).Value;
        draft.MatchValue = value.Text.Trim();
        draft.ActionType = ((Option<RuleActionType>)action.SelectedItem).Value;
        draft.RelativePath = folder.Text.Trim().Replace('\\', '/');
        draft.Priority = double.IsNaN(priority.Value) ? draft.Priority : (int)priority.Value;
        draft.IsEnabled = enabled.IsOn;
        return draft;
    }

    private ContentDialog Create(string title, object content)
    {
        return new ContentDialog
        {
            Title = title,
            Content = content,
            XamlRoot = _root ?? throw new InvalidOperationException("The window is not ready."),
        };
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock { Text = text, Opacity = 0.8 };
    }

    private static TextBox Box(string text)
    {
        return new TextBox { Text = text };
    }

    private static Option<T> CreateOption<T>(T value, string label)
        where T : notnull
    {
        return new Option<T>(value, label);
    }

    private static string MatchKey(RuleMatchType matchType)
    {
        return matchType switch
        {
            RuleMatchType.ExtensionEquals => "Match_ExtensionEquals",
            RuleMatchType.ExtensionIn => "Match_ExtensionIn",
            RuleMatchType.NameContains => "Match_NameContains",
            RuleMatchType.NameStartsWith => "Match_NameStartsWith",
            RuleMatchType.NameEquals => "Match_NameEquals",
            RuleMatchType.SizeGreaterThan => "Match_SizeGreaterThan",
            RuleMatchType.SizeLessThan => "Match_SizeLessThan",
            RuleMatchType.Always => "Match_Always",
            _ => throw new UnreachableException(matchType.ToString()),
        };
    }

    private sealed class Option<T> where T : notnull
    {
        public Option(T value, string label)
        {
            Value = value;
            Label = label;
        }

        public T Value { get; }

        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}

public static class DisplayText
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    public static string When(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("g");
    }
}

public static class CrashLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopFlow",
                "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "crash.log"), exception + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public static class SingleInstance
{
    private const string MutexName = @"Local\DesktopFlow.SingleInstance";
    private const string EventName = @"Local\DesktopFlow.Show";

    public static bool TryBecomePrimary(out Mutex? mutex)
    {
        mutex = new Mutex(true, MutexName, out var created);
        if (created)
        {
            return true;
        }

        mutex.Dispose();
        mutex = null;
        try
        {
            using var handle = EventWaitHandle.OpenExisting(EventName);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }

        return false;
    }

    public static EventWaitHandle CreateShowEvent()
    {
        return new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }
}
