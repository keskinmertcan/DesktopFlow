using DesktopFlow.App.Localization;

namespace DesktopFlow.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase(ILocalizer text)
    {
        Text = text;
        text.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            OnLanguageChanged();
        };
    }

    protected ILocalizer Text { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanRun => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRun));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    protected virtual void OnLanguageChanged()
    {
    }

    protected async Task GuardAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await work(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
