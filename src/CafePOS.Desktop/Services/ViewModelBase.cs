using System.Net.Http;
using CafePOS.Core.Contracts;

namespace CafePOS.Desktop.Services;

public interface IActivatable
{
    Task InitializeAsync();
}

public abstract partial class ViewModelBase : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isBusy;
    private string? _errorMessage;
    private string? _statusMessage;

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        protected set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public virtual Task InitializeAsync() => Task.CompletedTask;

    protected async Task RunAsync(Func<Task> action, string? successMessage = null)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await action();
            if (successMessage is not null) StatusMessage = successMessage;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Errors.Count > 0
                ? $"{ex.Message} {string.Join(" ", ex.Errors.SelectMany(e => e.Value))}"
                : ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Cannot reach the CafePOS server. Check the network connection and the server address in Settings.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The server took too long to respond. Please try again.";
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
