using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher;

public class EpicLauncherService
{
private readonly string _legendaryPath;

public EpicLauncherService()
{
    _legendaryPath = FindLegendary()
        ?? throw new FileNotFoundException(
            "Kunde inte hitta legendary.exe.\n\n" +
            "Lägg legendary.exe i GameLauncherns mapp.");
}

public async Task<bool> AuthenticateAccount(
    Window owner,
    AccountDefinition account)
{
    if (account is null ||
        string.IsNullOrWhiteSpace(account.Username))
    {
        MessageBox.Show(
            owner,
            "Epic-kontot saknar användarnamn.",
            "Epic-konto",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    try
    {
        var configName = GetConfigName(account);

        var startInfo = CreateStartInfo(
            $"-c \"{configName}\" auth",
            createNoWindow: false);

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            MessageBox.Show(
                owner,
                "Kunde inte starta Legendary.",
                "Epic-login",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            MessageBox.Show(
                owner,
                "Epic-inloggningen misslyckades.",
                "Epic-login",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            owner,
            $"Epic-inloggningen misslyckades.\n\n{ex.Message}",
            "Epic-login",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        return false;
    }
}

public async Task<bool> IsAuthenticated(
    AccountDefinition account)
{
    try
    {
        var configName = GetConfigName(account);

        var startInfo = CreateStartInfo(
            $"-c \"{configName}\" status",
            createNoWindow: true);

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo);

        if (process is null)
            return false;

        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

public async Task LaunchGame(
    Window owner,
    string appName,
    AccountDefinition? account)
{
    if (string.IsNullOrWhiteSpace(appName))
    {
        MessageBox.Show(
            owner,
            "Epic App Name saknas i games.json.",
            "Epic-konfiguration saknas",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    try
    {
        var configName = GetConfigName(account);

        var arguments =
            $"-c \"{configName}\" " +
            $"launch \"{appName}\" " +
            $"--skip-version-check";

        var startInfo = CreateStartInfo(
            arguments,
            createNoWindow: true);

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            MessageBox.Show(
                owner,
                "Legendary kunde inte startas.",
                "Epic launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            var details =
                string.IsNullOrWhiteSpace(error)
                    ? output
                    : error;

            MessageBox.Show(
                owner,
                $"Epic-spelet kunde inte startas.\n\n" +
                $"Exit code: {process.ExitCode}\n\n" +
                details,
                "Epic launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            owner,
            $"Kunde inte starta Epic-spelet.\n\n{ex.Message}",
            "Epic launch error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

private ProcessStartInfo CreateStartInfo(
    string arguments,
    bool createNoWindow)
{
    return new ProcessStartInfo
    {
        FileName = _legendaryPath,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = createNoWindow,
        WorkingDirectory =
            Path.GetDirectoryName(_legendaryPath)
            ?? AppContext.BaseDirectory
    };
}

private static string GetConfigName(
    AccountDefinition? account)
{
    if (account is null)
        return "own.ini";

    var name = MakeSafeName(account.Username);

    return $"borrow_{name}.ini";
}

private static string MakeSafeName(string value)
{
    foreach (var invalid in Path.GetInvalidFileNameChars())
    {
        value = value.Replace(invalid, '_');
    }

    return string.IsNullOrWhiteSpace(value)
        ? "account"
        : value;
}

private static string? FindLegendary()
{
    var candidates = new[]
    {
        Path.Combine(
            AppContext.BaseDirectory,
            "legendary.exe"),

        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles),
            "Legendary",
            "legendary.exe")
    };

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
            return candidate;
    }

    return null;
}


}