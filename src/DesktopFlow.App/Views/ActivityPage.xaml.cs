using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class ActivityPage : Page
{
    public ActivityPage(ActivityViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync(CancellationToken.None);
    }

    public ActivityViewModel ViewModel { get; }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelection(HistoryList.SelectedItems.OfType<ActivityRowViewModel>());
    }
}
