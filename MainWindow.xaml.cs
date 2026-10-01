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

    private async void ShowLaunchDialogAndLaunch()
    {
    try
    {
    if (_selectedGame is null)
    {
    return;
    }

        var platform =
            !string.IsNullOrWhiteSpace(_selectedGame.Platform)
                ? _selectedGame.Platform
                : !string.IsNullOrWhiteSpace(_selectedGame.SteamId)
                    ? "steam"
                    : "epic";

        var accountsForPlatform = _accounts
            .Where(a =>
                a != null &&
                string.Equals(
                    a.Platform,
                    platform,
                    System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var dialog = new AccountDialog(
            _selectedGame.Title,
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

        // -------------------------------------------------
        // VISA LOADING
        // -------------------------------------------------

        LaunchLoadingOverlay.Visibility =
            Visibility.Visible;

        try
        {
            // -------------------------------------------------
            // STEAM
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(_selectedGame.SteamId))
            {
                if (dialog.Choice ==
                    LaunchChoice.BorrowAccount)
                {
                    if (dialog.SelectedBorrowAccount is null)
                    {
                        MessageBox.Show(
                            this,
                            "Inget lånekonto valdes.",
                            "Lånekonto saknas",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    await _launcher.LaunchSteamGame(
                        this,
                        _selectedGame.SteamId,
                        dialog.SelectedBorrowAccount);

                    return;
                }

                if (dialog.Choice ==
                    LaunchChoice.UseMyAccount)
                {
                    if (string.IsNullOrWhiteSpace(
                            dialog.MyUsername) ||
                        string.IsNullOrWhiteSpace(
                            dialog.MyPassword))
                    {
                        MessageBox.Show(
                            this,
                            "Steam-användarnamn och lösenord måste anges.",
                            "Steam-inloggning saknas",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    await _launcher.LaunchSteamGame(
                        this,
                        _selectedGame.SteamId,
                        dialog.MyUsername,
                        dialog.MyPassword);

                    return;
                }

                return;
            }

            // -------------------------------------------------
            // EJ STEAM
            // -------------------------------------------------

            _launcher.LaunchExecutable(
                this,
                _selectedGame.ExecutablePath);
        }
        finally
        {
            LaunchLoadingOverlay.Visibility =
                Visibility.Collapsed;
        }
    }
    catch (Exception ex)
    {
        LaunchLoadingOverlay.Visibility =
            Visibility.Collapsed;

        MessageBox.Show(
            this,
            "Ett fel uppstod när spelet skulle startas.\n\n" +
            ex.GetType().Name +
            "\n\n" +
            ex.Message,
            "Game Launcher - fel",
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
