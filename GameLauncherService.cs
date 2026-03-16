using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GameLauncher;

public class GameLauncherService
{
    public void LaunchSteamGame(Window owner, string steamGameId)
    {
        if (string.IsNullOrWhiteSpace(steamGameId))
        {
            MessageBox.Show(owner,
                "No Steam game ID configured for this title.",
                "Cannot launch via Steam",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var uri = $"steam://rungameid/{steamGameId}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner,
                $"Failed to launch Steam game with ID {steamGameId}.\n\n{ex.Message}",
                "Launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void LaunchSteamGame(Window owner, string steamGameId, AccountDefinition? account)
    {
        if (account is null)
        {
            LaunchSteamGame(owner, steamGameId);
            return;
        }

        // Try to locate Steam client executable.
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe")
        };

        var steamExe = Array.Find(candidates, File.Exists);
        if (steamExe is null)
        {
            MessageBox.Show(owner,
                "Could not find steam.exe. Falling back to current logged-in Steam user.",
                "Steam not found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            LaunchSteamGame(owner, steamGameId);
            return;
        }

        try
        {
            // Launch Steam with explicit login and app id.
            var args = $"-login \"{account.Username}\" \"{account.Password}\" -applaunch {steamGameId}";

            Process.Start(new ProcessStartInfo
            {
                FileName = steamExe,
                Arguments = args,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner,
                $"Failed to launch Steam with borrow account.\n\n{ex.Message}",
                "Launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void LaunchExecutable(Window owner, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            MessageBox.Show(owner,
                "No executable configured for this game (see games.json).",
                "Cannot launch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!File.Exists(executablePath))
        {
            MessageBox.Show(owner,
                $"The configured executable was not found:\n{executablePath}",
                "File not found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner,
                $"Failed to launch the game executable.\n\n{ex.Message}",
                "Launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

