using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher;

public class GameLauncherService
{
// =========================================================
// VANLIG STEAM-START
// =========================================================

public async Task LaunchSteamGame(
    Window owner,
    string steamGameId)
{
    if (string.IsNullOrWhiteSpace(steamGameId))
    {
        MessageBox.Show(
            owner,
            "No Steam game ID configured for this title.",
            "Cannot launch via Steam",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://rungameid/{steamGameId}",
            UseShellExecute = true
        });

        await Task.CompletedTask;
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            owner,
            $"Failed to launch Steam game.\n\n{ex.Message}",
            "Launch error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

// =========================================================
// STEAM - LÅNEKONTO
// =========================================================

public async Task LaunchSteamGame(
    Window owner,
    string steamGameId,
    AccountDefinition? account)
{
    if (account is null)
    {
        await LaunchSteamGame(owner, steamGameId);
        return;
    }

    await LaunchSteamWithCredentials(
        owner,
        steamGameId,
        account.Username,
        account.Password);
}

// =========================================================
// STEAM - EGET KONTO
// =========================================================

public async Task LaunchSteamGame(
    Window owner,
    string steamGameId,
    string username,
    string password)
{
    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        MessageBox.Show(
            owner,
            "Steam-användarnamn och lösenord måste anges.",
            "Steam-inloggning saknas",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    await LaunchSteamWithCredentials(
        owner,
        steamGameId,
        username,
        password);
}

// =========================================================
// GEMENSAM STEAM-START MED KONTOVÄXLING
// =========================================================

private static async Task LaunchSteamWithCredentials(
    Window owner,
    string steamGameId,
    string username,
    string password)
{
    if (string.IsNullOrWhiteSpace(steamGameId))
    {
        MessageBox.Show(
            owner,
            "No Steam game ID configured for this title.",
            "Cannot launch via Steam",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return;
    }

    var steamExe = FindSteamExecutable();

    if (steamExe is null)
    {
        MessageBox.Show(
            owner,
            "Could not find steam.exe.",
            "Steam not found",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        return;
    }

    try
    {
        // -------------------------------------------------
        // 1. STÄNG GAMMAL STEAM-SESSION
        // -------------------------------------------------

        StopSteam();

        // -------------------------------------------------
        // 2. VÄNTA PÅ ATT STEAM VERKLIGEN ÄR BORTA
        // -------------------------------------------------

        var steamClosed =
            await WaitForSteamToCloseAsync(10000);

        if (!steamClosed)
        {
            MessageBox.Show(
                owner,
                "Steam kunde inte avslutas helt.\n\n" +
                "Stäng Steam manuellt och försök igen.",
                "Steam körs fortfarande",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // -------------------------------------------------
        // 3. STARTA STEAM MED VALT KONTO
        // -------------------------------------------------

        var arguments =
            $"-silent " +
            $"-login \"{username}\" \"{password}\"";

        var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = steamExe,
                Arguments = arguments,

                UseShellExecute = false,

                WorkingDirectory =
                    Path.GetDirectoryName(steamExe)
                    ?? string.Empty,

                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

        if (process is null)
        {
            MessageBox.Show(
                owner,
                "Steam kunde inte startas.",
                "Steam launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        // -------------------------------------------------
        // 4. VÄNTA PÅ ATT STEAM STARTAT
        //
        // Steam kan behöva ganska lång tid beroende på
        // dator, nätverk och inloggning.
        // -------------------------------------------------

        var steamStarted =
            await WaitForSteamToStartAsync(15000);

        if (!steamStarted)
        {
            MessageBox.Show(
                owner,
                "Steam startade inte inom 15 sekunder.",
                "Steam launch timeout",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // -------------------------------------------------
        // 5. GE STEAM EXTRA TID FÖR INLOGGNING
        // -------------------------------------------------

        await Task.Delay(3000);

        // -------------------------------------------------
        // 6. STARTA SPELET
        // -------------------------------------------------

        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    $"steam://rungameid/{steamGameId}",

                UseShellExecute = true
            });
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            owner,
            $"Failed to launch Steam with the selected account.\n\n" +
            $"{ex.Message}",
            "Launch error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

// =========================================================
// STOPPA STEAM
// =========================================================

private static void StopSteam()
{
    var processes =
        Process.GetProcessesByName("steam");

    foreach (var process in processes)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
            // Processen kan redan ha avslutats.
        }
        finally
        {
            process.Dispose();
        }
    }
}

// =========================================================
// VÄNTA PÅ STEAM-STÄNGNING
// =========================================================

private static async Task<bool> WaitForSteamToCloseAsync(
    int timeoutMilliseconds)
{
    const int interval = 100;

    var elapsed = 0;

    while (elapsed < timeoutMilliseconds)
    {
        if (!Process.GetProcessesByName("steam").Any())
        {
            return true;
        }

        await Task.Delay(interval);

        elapsed += interval;
    }

    return !Process.GetProcessesByName("steam").Any();
}

// =========================================================
// VÄNTA PÅ STEAM-START
// =========================================================

private static async Task<bool> WaitForSteamToStartAsync(
    int timeoutMilliseconds)
{
    const int interval = 250;

    var elapsed = 0;

    while (elapsed < timeoutMilliseconds)
    {
        if (Process.GetProcessesByName("steam").Any())
        {
            return true;
        }

        await Task.Delay(interval);

        elapsed += interval;
    }

    return false;
}

// =========================================================
// HITTA STEAM
// =========================================================

private static string? FindSteamExecutable()
{
    var candidates = new[]
    {
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steam.exe"),

        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles),
            "Steam",
            "steam.exe")
    };

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

// =========================================================
// VANLIG EXECUTABLE
// =========================================================

public void LaunchExecutable(
    Window owner,
    string executablePath)
{
    if (string.IsNullOrWhiteSpace(executablePath))
    {
        MessageBox.Show(
            owner,
            "No executable configured for this game (see games.json).",
            "Cannot launch",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return;
    }

    if (!File.Exists(executablePath))
    {
        MessageBox.Show(
            owner,
            $"The configured executable was not found:\n{executablePath}",
            "File not found",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    try
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = executablePath,

                WorkingDirectory =
                    Path.GetDirectoryName(executablePath)
                    ?? string.Empty,

                UseShellExecute = true
            });
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            owner,
            $"Failed to launch the game executable.\n\n{ex.Message}",
            "Launch error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}


}