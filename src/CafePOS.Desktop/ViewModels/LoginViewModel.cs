using CafePOS.Core.Contracts;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafePOS.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly AppSettingsStore _settings;
    private readonly AuthSession _session;
    private readonly BrandingService _branding;

    public LoginViewModel(ApiClient api, AppSettingsStore settings, AuthSession session, BrandingService branding)
    {
        _api = api;
        _settings = settings;
        _session = session;
        _branding = branding;
        ServerUrl = settings.ServerUrl;
        Email = settings.LastEmail;
        RememberEmail = !string.IsNullOrWhiteSpace(settings.LastEmail);
    }

    public BrandingService Branding => _branding;

    public async Task InitializeBrandingAsync()
    {
        _api.SetServerUrl(ServerUrl);
        await _branding.LoadAsync();
    }

    [ObservableProperty] private string _serverUrl = "http://localhost:5080";
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _rememberEmail = true;
    [ObservableProperty] private string _versionText = "CafePOS 1.0";

    public UserDto? SignedInUser => _session.User;

    public async Task<bool> LoginAsync()
    {
        var ok = false;
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
                throw new ApiException("Enter the server address.");
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                throw new ApiException("Enter your email and password.");

            _api.SetServerUrl(ServerUrl);
            var response = await _api.LoginAsync(Email.Trim(), Password);
            _settings.ServerUrl = _api.ServerUrl;
            _settings.LastEmail = RememberEmail ? Email.Trim() : string.Empty;
            _settings.Save();
            ok = response.User is not null;
            if (ok) await _branding.LoadAsync();
        });
        return ok;
    }

    public Task ReloadAfterPasswordChangeAsync() => RunAsync(async () =>
    {
        var user = await _api.MeAsync();
        _session.UpdateUser(user);
    });
}
