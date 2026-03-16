using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace GameLauncher;

public partial class MainWindow : Window
{
    private GameDefinition? _selectedGame;
    private GameDefinition[] _games = System.Array.Empty<GameDefinition>();
    private AccountDefinition[] _accounts = System.Array.Empty<AccountDefinition>();
    private DiscordChannel[] _discordChannels = System.Array.Empty<DiscordChannel>();
    private readonly GameLauncherService _launcher = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadConfiguration();
        GamesGrid.ItemsSource = _games;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void GamesGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as System.Windows.DependencyObject;
        while (originalSource != null && originalSource is not System.Windows.FrameworkElement { DataContext: GameDefinition })
        {
            originalSource = System.Windows.Media.VisualTreeHelper.GetParent(originalSource);
        }

        if (originalSource is not System.Windows.FrameworkElement { DataContext: GameDefinition game })
        {
            return;
        }

        _selectedGame = game;

        if (_selectedGame is null)
        {
            return;
        }

        ShowLaunchDialogAndLaunch();
    }

    private void ShowLaunchDialogAndLaunch()
    {
        if (_selectedGame is null)
            return;

        // Figure out platform for this game (steam / epic etc.)
        var platform = !string.IsNullOrWhiteSpace(_selectedGame.Platform)
            ? _selectedGame.Platform
            : !string.IsNullOrWhiteSpace(_selectedGame.SteamId)
                ? "steam"
                : "epic";

        var accountsForPlatform = _accounts
            .Where(a => string.Equals(a.Platform, platform, System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!accountsForPlatform.Any())
        {
            if (!string.IsNullOrWhiteSpace(_selectedGame.SteamId))
            {
                _launcher.LaunchSteamGame(this, _selectedGame.SteamId);
            }
            else
            {
                _launcher.LaunchExecutable(this, _selectedGame.ExecutablePath);
            }

            return;
        }

        var dialog = new AccountDialog(_selectedGame.Title, accountsForPlatform)
        {
            Owner = this
        };

        var result = dialog.ShowDialog();
        if (result != true || dialog.Choice == LaunchChoice.None)
        {
            return;
        }

        AccountDefinition? borrowAccount = dialog.Choice == LaunchChoice.BorrowAccount
            ? dialog.SelectedBorrowAccount
            : null;

        // Prefer launching via Steam if a Steam ID is configured; otherwise fall back to the executable path.
        if (!string.IsNullOrWhiteSpace(_selectedGame.SteamId))
        {
            if (platform.Equals("steam", System.StringComparison.OrdinalIgnoreCase) && borrowAccount is not null)
            {
                _launcher.LaunchSteamGame(this, _selectedGame.SteamId, borrowAccount);
            }
            else
            {
                _launcher.LaunchSteamGame(this, _selectedGame.SteamId);
            }
        }
        else
        {
            _launcher.LaunchExecutable(this, _selectedGame.ExecutablePath);
        }
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        var login = new AdminLoginWindow
        {
            Owner = this
        };

        var result = login.ShowDialog();
        if (result == true && login.IsAuthorized)
        {
            var admin = new AdminWindow
            {
                Owner = this
            };
            admin.ShowDialog();
        }
    }

    private void Discord_Click(object sender, RoutedEventArgs e)
    {
        if (_discordChannels.Length == 0 || DiscordChannelCombo.SelectedItem is not DiscordChannel channel || string.IsNullOrWhiteSpace(channel.Url))
        {
            MessageBox.Show(this,
                "Ingen Discord-kanal är konfigurerad eller vald. Lägg till kanaler i discord.json.",
                "Discord saknas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = channel.Url,
                UseShellExecute = true
            });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this,
                $"Kunde inte öppna Discord-länken:\n{ex.Message}",
                "Fel vid öppning",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Förhindra att användaren stänger fönstret med t.ex. Alt+F4.
        // Launchern stängs istället via Admin-fönstret (Application.Current.Shutdown).
        if (Application.Current is { ShutdownMode: ShutdownMode.OnExplicitShutdown })
        {
            // Om vi någon gång byter ShutdownMode kan detta anpassas,
            // men just nu blockerar vi alla försök att stänga här.
        }

        if (!System.Environment.HasShutdownStarted)
        {
            e.Cancel = true;
        }
    }

    private void LoadConfiguration()
    {
        var baseDir = System.AppContext.BaseDirectory;

        var gamesPath = Path.Combine(baseDir, "games.json");
        var accountsPath = Path.Combine(baseDir, "accounts.json");
        var discordPath = Path.Combine(baseDir, "discord.json");

        if (File.Exists(gamesPath))
        {
            try
            {
                var json = File.ReadAllText(gamesPath);
                _games = JsonSerializer.Deserialize<GameDefinition[]>(json) ?? System.Array.Empty<GameDefinition>();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to load games.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        if (File.Exists(accountsPath))
        {
            try
            {
                var json = File.ReadAllText(accountsPath);
                _accounts = JsonSerializer.Deserialize<AccountDefinition[]>(json) ?? System.Array.Empty<AccountDefinition>();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to load accounts.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        if (File.Exists(discordPath))
        {
            try
            {
                var json = File.ReadAllText(discordPath);
                _discordChannels = JsonSerializer.Deserialize<DiscordChannel[]>(json) ?? System.Array.Empty<DiscordChannel>();

                if (_discordChannels.Length > 0)
                {
                    DiscordChannelCombo.ItemsSource = _discordChannels;
                    DiscordChannelCombo.SelectedIndex = 0;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to load discord.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

