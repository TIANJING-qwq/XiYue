using ReactiveUI;
using SBtools.Models;
using System.Reactive;

namespace SBtools.ViewModels;

public class SettingsTabViewModel : ViewModelBase
{
    private readonly ConfigManager _config;
    private readonly WiFiAuthenticator _authenticator;
    private string _username = string.Empty;
    private string _password = string.Empty;

    public string Username
    {
        get => _username;
        set { this.RaiseAndSetIfChanged(ref _username, value); }
    }

    public string Password
    {
        get => _password;
        set { this.RaiseAndSetIfChanged(ref _password, value); }
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public SettingsTabViewModel(ConfigManager config, WiFiAuthenticator authenticator)
    {
        _config = config;
        _authenticator = authenticator;
        Username = _config.Username;
        Password = _config.Password;

        SaveCommand = ReactiveCommand.Create(SaveSettings);
    }

    private void SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            return;
        _config.Username = Username;
        _config.Password = Password;
        _authenticator.UpdateCredentials(Username, Password);
        // 可加提示
    }
}