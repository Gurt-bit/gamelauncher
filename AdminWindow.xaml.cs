using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace GameLauncher;

public partial class AdminWindow : Window
{
    public ObservableCollection<GameDefinition> Games { get; } = new();

    public AdminWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadGames();
    }

    private void LoadGames()
    {
        var baseDir = AppContext.BaseDirectory;
        var gamesPath = Path.Combine(baseDir, "games.json");

        if (!File.Exists(gamesPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(gamesPath);
            var items = JsonSerializer.Deserialize<GameDefinition[]>(json) ?? Array.Empty<GameDefinition>();

            Games.Clear();
            foreach (var game in items)
            {
                Games.Add(game);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Kunde inte läsa games.json:\n{ex.Message}",
                "Fel vid inläsning",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveGames_Click(object sender, RoutedEventArgs e)
    {
        var baseDir = AppContext.BaseDirectory;
        var gamesPath = Path.Combine(baseDir, "games.json");

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var data = Games.ToArray();
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(gamesPath, json);

            MessageBox.Show(this,
                "Spelen har sparats till games.json.\nStarta om launchern för att ladda om listan.",
                "Sparat",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Kunde inte spara games.json:\n{ex.Message}",
                "Fel vid sparande",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseLauncher_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

