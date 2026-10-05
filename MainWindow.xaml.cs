using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GameLauncher;

public partial class MainWindow : Window
{
    private GameDefinition? _selectedGame;

    private GameDefinition[] _games =
        Array.Empty<GameDefinition>();

    private AccountDefinition[] _accounts =
        Array.Empty<AccountDefinition>();

    private DiscordChannel[] _discordChannels =
        Array.Empty<DiscordChannel>();

    private readonly GameLauncherService _launcher = new();

    private EpicLauncherService? _epicLauncher;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closing += OnClosing;
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        LoadConfiguration();

        GamesGrid.ItemsSource = _games;
    }

    private void OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void GamesGrid_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        try
        {
            DependencyObject? element =
                e.OriginalSource as DependencyObject;

            while (element != null)
            {
                if (element is FrameworkElement frameworkElement &&
                    frameworkElement.DataContext is GameDefinition game)
                {
                    _selectedGame = game;

                    ShowLaunchDialogAndLaunch();

                    e.Handled = true;

                    return;
                }

                element =
                    VisualTreeHelper.GetParent(element);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Fel när spelet klickades:\n\n" +
                ex.GetType().Name +
                "\n\n" +
                ex.Message,
                "Game Launcher - fel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowLaunchDialogAndLaunch()
    {
        if (_selectedGame is null)
            return;

        var platform =
            !string.IsNullOrWhiteSpace(_selectedGame.Platform)
                ? _selectedGame.Platform
                : !string.IsNullOrWhiteSpace(_selectedGame.SteamId)
                    ? "steam"
                    : null;

        if (string.IsNullOrWhiteSpace(platform))
        {
            _launcher.LaunchExecutable(
                this,
                _selectedGame.ExecutablePath);

            return;
        }

        var accountsForPlatform = _accounts
            .Where(a =>
                string.Equals(
                    a.Platform,
                    platform,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var dialog = new AccountDialog(
            _selectedGame.Title,
            platform,
            accountsForPlatform)
        {
            Owner = this
        };

        var result = dialog.ShowDialog();

        if (result != true ||
            dialog.Choice == LaunchChoice.None)
        {
            return;
        }

        var borrowAccount =
            dialog.Choice == LaunchChoice.BorrowAccount
                ? dialog.SelectedBorrowAccount
                : null;

        if (platform.Equals(
                "steam",
                StringComparison.OrdinalIgnoreCase))
        {
            LaunchSteam(
                borrowAccount);

            return;
        }

        if (platform.Equals(
                "epic",
                StringComparison.OrdinalIgnoreCase))
        {
            LaunchEpic(
                borrowAccount);

            return;
        }

        _launcher.LaunchExecutable(
            this,
            _selectedGame.ExecutablePath);
    }

    private async void LaunchSteam(
        AccountDefinition? borrowAccount)
    {
        if (_selectedGame is null ||
            string.IsNullOrWhiteSpace(_selectedGame.SteamId))
        {
            return;
        }

        if (borrowAccount is not null)
        {
            await _launcher.LaunchSteamGame(
                this,
                _selectedGame.SteamId,
                borrowAccount);
        }
        else
        {
            // Eget Steam-konto
            // AccountDialog innehåller användarnamn/lösenord.
            //
            // Lägg till detta när dialogens värden exponeras.
            await _launcher.LaunchSteamGame(
                this,
                _selectedGame.SteamId);
        }
    }

    private async void LaunchEpic(
        AccountDefinition? borrowAccount)
    {
        if (_selectedGame is null)
            return;

        if (string.IsNullOrWhiteSpace(
                _selectedGame.EpicAppName))
        {
            MessageBox.Show(
                this,
                "Epic App Name saknas för spelet.\n\n" +
                "Lägg till \"epicAppName\" i games.json.",
                "Epic-konfiguration saknas",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try
        {
            _epicLauncher ??=
                new EpicLauncherService();

            // =============================================
            // LÅNEKONTO
            // =============================================

            if (borrowAccount is not null)
            {
                var authenticated =
                    await _epicLauncher.IsAuthenticated(
                        borrowAccount);

                if (!authenticated)
                {
                    var result =
                        MessageBox.Show(
                            this,
                            $"Epic-kontot \"{borrowAccount.Username}\" " +
                            "är inte inloggat.\n\n" +
                            "Vill du logga in nu?",
                            "Epic-konto",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;

                    authenticated =
                        await _epicLauncher.AuthenticateAccount(
                            this,
                            borrowAccount);

                    if (!authenticated)
                        return;
                }
            }

            // =============================================
            // EGET KONTO
            // =============================================
            //
            // account == null
            //
            // EpicLauncherService skapar då alltid en
            // helt ny temporär profil.
            //
            // Därför kan tidigare användares Epic-session
            // aldrig återanvändas.
            //
            // =============================================

            await _epicLauncher.LaunchGame(
                this,
                _selectedGame.EpicAppName,
                borrowAccount);
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Legendary saknas",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Epic-starten misslyckades.\n\n{ex.Message}",
                "Epic-fel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }





    private void Admin_Click(
        object sender,
        RoutedEventArgs e)
    {
        var login =
            new AdminLoginWindow
            {
                Owner = this
            };

        var result =
            login.ShowDialog();

        if (result == true &&
            login.IsAuthorized)
        {
            var admin =
                new AdminWindow
                {
                    Owner = this
                };

            admin.ShowDialog();
        }
    }

    private void Discord_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_discordChannels.Length == 0 ||
            DiscordChannelCombo.SelectedItem
                is not DiscordChannel channel ||
            string.IsNullOrWhiteSpace(channel.Url))
        {
            MessageBox.Show(
                this,
                "Ingen Discord-kanal är konfigurerad eller vald. " +
                "Lägg till kanaler i discord.json.",
                "Discord saknas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = channel.Url,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Kunde inte öppna Discord-länken:\n{ex.Message}",
                "Fel vid öppning",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnClosing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        if (!Environment.HasShutdownStarted)
        {
            e.Cancel = true;
        }
    }

    private void LoadConfiguration()
    {
        var baseDir =
            AppContext.BaseDirectory;

        var gamesPath =
            Path.Combine(
                baseDir,
                "games.json");

        var accountsPath =
            Path.Combine(
                baseDir,
                "accounts.json");

        var discordPath =
            Path.Combine(
                baseDir,
                "discord.json");

        // =========================================
        // GAMES
        // =========================================

        if (File.Exists(gamesPath))
        {
            try
            {
                var json =
                    File.ReadAllText(gamesPath);

                _games =
                    JsonSerializer.Deserialize<
                        GameDefinition[]>(json)
                    ?? Array.Empty<GameDefinition>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to load games.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================
        // ACCOUNTS
        // =========================================

        if (File.Exists(accountsPath))
        {
            try
            {
                var json =
                    File.ReadAllText(accountsPath);

                _accounts =
                    JsonSerializer.Deserialize<
                        AccountDefinition[]>(json)
                    ?? Array.Empty<AccountDefinition>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to load accounts.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================
        // DISCORD
        // =========================================

        if (File.Exists(discordPath))
        {
            try
            {
                var json =
                    File.ReadAllText(discordPath);

                _discordChannels =
                    JsonSerializer.Deserialize<
                        DiscordChannel[]>(json)
                    ?? Array.Empty<DiscordChannel>();

                if (_discordChannels.Length > 0)
                {
                    DiscordChannelCombo.ItemsSource =
                        _discordChannels;

                    DiscordChannelCombo.SelectedIndex =
                        0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Failed to load discord.json:\n{ex.Message}",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
