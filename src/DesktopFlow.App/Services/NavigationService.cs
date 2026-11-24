using DesktopFlow.App.ViewModels;
using DesktopFlow.App.Views;
using DesktopFlow.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Services;

public sealed class NavigationService
{
    private readonly IServiceProvider _services;
    private Frame? _frame;
    private NavigationView? _navigation;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public void Attach(Frame frame, NavigationView navigation)
    {
        _frame = frame;
        _navigation = navigation;
    }

    public void Navigate(string tag)
    {
        if (_frame is null)
        {
            return;
        }

        _frame.Content = tag switch
        {
            "overview" => new OverviewPage(_services.GetRequiredService<OverviewViewModel>()),
            "desktop" => CreateFolder(MonitorScope.Desktop),
            "downloads" => CreateFolder(MonitorScope.Downloads),
            "organizer" => new OrganizerPage(_services.GetRequiredService<OrganizerViewModel>()),
            "rules" => new RulesPage(_services.GetRequiredService<RulesViewModel>()),
            "activity" => new ActivityPage(_services.GetRequiredService<ActivityViewModel>()),
            "settings" => new SettingsPage(_services.GetRequiredService<SettingsViewModel>()),
            "setup" => new SetupPage(_services.GetRequiredService<SetupViewModel>()),
            _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unknown page."),
        };
        Select(tag);
    }

    public void SetPaneVisible(bool visible)
    {
        if (_navigation is not null)
        {
            _navigation.IsPaneVisible = visible;
        }
    }

    private FolderPage CreateFolder(MonitorScope scope)
    {
        var viewModel = _services.GetRequiredService<FolderViewModel>();
        viewModel.Initialize(scope);
        return new FolderPage(viewModel);
    }

    private void Select(string tag)
    {
        if (_navigation is null || tag == "setup")
        {
            return;
        }

        var match = _navigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag as string == tag);
        if (match is not null)
        {
            _navigation.SelectedItem = match;
        }
    }
}
