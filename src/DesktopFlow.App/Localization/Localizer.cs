using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopFlow.App.Localization;

public interface ILocalizer
{
    string Language { get; }

    string this[string key] { get; }

    event EventHandler? LanguageChanged;

    void SetLanguage(string language);

    string Format(string key, params object[] args);
}

public sealed partial class TranslationSource : ObservableObject, ILocalizer
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fallback = new(StringComparer.Ordinal);

    public TranslationSource()
    {
        LoadInto(_fallback, "en-US");
        SetLanguage("en-US");
    }

    public string Language { get; private set; } = "en-US";

    public event EventHandler? LanguageChanged;

    public string this[string key]
    {
        get
        {
            if (_values.TryGetValue(key, out var value))
            {
                return value;
            }

            if (_fallback.TryGetValue(key, out var english))
            {
                return english;
            }

            return key;
        }
    }

    public void SetLanguage(string language)
    {
        var culture = language.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr-TR" : "en-US";
        LoadInto(_values, culture);
        foreach (var pair in _fallback)
        {
            _values.TryAdd(pair.Key, pair.Value);
        }

        Language = culture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }

    public string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, this[key], args);
        }
        catch (FormatException)
        {
            return this[key];
        }
    }

    private static void LoadInto(Dictionary<string, string> target, string culture)
    {
        target.Clear();
        var path = Path.Combine(AppContext.BaseDirectory, "Strings", culture + ".json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (map is null)
        {
            return;
        }

        foreach (var pair in map)
        {
            target[pair.Key] = pair.Value;
        }
    }
}
