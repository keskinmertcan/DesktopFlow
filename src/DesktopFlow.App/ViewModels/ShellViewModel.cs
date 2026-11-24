using System.Reflection;
using DesktopFlow.App.Localization;
using DesktopFlow.App.Services;
using DesktopFlow.Core;
using DesktopFlow.Core.Abstractions;

namespace DesktopFlow.App.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IUpdateChecker _updates;
    private readonly IShellLauncher _shell;

    public ShellViewModel(ILocalizer text, IUpdateChecker updates, IShellLauncher shell)
        : base(text)
    {
        _updates = updates;
        _shell = shell;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionLabel = text.Format("Footer_Version", version);
    }

    public string VersionLabel { get; private set; }

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    protected override void OnLanguageChanged()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionLabel = Text.Format("Footer_Version", version);
        OnPropertyChanged(nameof(VersionLabel));
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            _shell.OpenUrl(ProductInfo.RepositoryUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        await GuardAsync(async cancellationToken =>
        {
            UpdateStatus = Text["Update_Checking"];
            var result = await _updates.CheckAsync(cancellationToken);
            UpdateStatus = result.Message ?? string.Empty;
            if (result.UpdateAvailable && !string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                _shell.OpenUrl(ProductInfo.RepositoryUrl + "/releases/latest");
            }
        }, CancellationToken.None);
    }
}
