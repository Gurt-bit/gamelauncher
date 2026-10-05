using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher;

public class EpicLauncherService
{
    private readonly string _legendaryPath;
    private readonly string _baseLegendaryPath;
    private readonly string _profilesPath;

    public EpicLauncherService()
    {
        _legendaryPath =
            FindLegendary()
            ?? throw new FileNotFoundException(
                "Kunde inte hitta legendary.exe.\n\n" +
                "Lägg legendary.exe i GameLauncherns mapp.");

        _baseLegendaryPath =
            GetBaseLegendaryPath();

        _profilesPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "GameLauncher",
                "EpicProfiles");

        Directory.CreateDirectory(_profilesPath);
    }

    // =========================================================
    // AUTENTISERA KONTO
    // =========================================================

    public async Task<bool> AuthenticateAccount(
        Window owner,
        AccountDefinition? account)
    {
        var profilePath =
            GetProfilePath(account);

        try
        {
            /*
             * EGET KONTO
             *
             * Börja alltid med en helt ren profil.
             * Tidigare användares Epic-session kan därför
             * inte återanvändas.
             */
            if (account is null)
            {
                DeleteDirectorySafe(profilePath);
            }

            PrepareProfile(profilePath);

            var startInfo =
                CreateStartInfo(
                    profilePath,
                    "auth",
                    createNoWindow: false);

            using var process =
                Process.Start(startInfo);

            if (process is null)
            {
                ShowError(
                    owner,
                    "Kunde inte starta Legendary.",
                    "Epic-login");

                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                ShowError(
                    owner,
                    "Epic-inloggningen misslyckades.",
                    "Epic-login");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowError(
                owner,
                $"Epic-inloggningen misslyckades.\n\n{ex.Message}",
                "Epic-login");

            return false;
        }
    }

    // =========================================================
    // KONTROLLERA LOGIN
    // =========================================================

    public async Task<bool> IsAuthenticated(
        AccountDefinition? account)
    {
        /*
         * Eget konto ska alltid kräva ny inloggning.
         */
        if (account is null)
            return false;

        var profilePath =
            GetProfilePath(account);

        try
        {
            PrepareProfile(profilePath);

            var startInfo =
                CreateStartInfo(
                    profilePath,
                    "status",
                    createNoWindow: true);

            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using var process =
                Process.Start(startInfo);

            if (process is null)
                return false;

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output =
                await outputTask;

            /*
             * Legendary returnerar exit code 0 även när
             * inget Epic-konto är inloggat.
             *
             * Därför kontrollerar vi stdout.
             */
            return
                process.ExitCode == 0 &&
                !output.Contains(
                    "Epic account: <not logged in>",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // =========================================================
    // STARTA SPEL
    // =========================================================

    public async Task<bool> LaunchGame(
        Window owner,
        string appName,
        AccountDefinition? account)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            ShowWarning(
                owner,
                "Epic App Name saknas i games.json.",
                "Epic-konfiguration saknas");

            return false;
        }

        var profilePath =
            GetProfilePath(account);

        /*
         * Eget konto:
         *   Temporär profil.
         *
         * Lånekonto:
         *   Permanent profil.
         */
        var temporaryProfile =
            account is null;

        try
        {
            PrepareProfile(profilePath);

            var arguments =
                $"launch \"{appName}\" " +
                "--skip-version-check";

            var startInfo =
                CreateStartInfo(
                    profilePath,
                    arguments,
                    createNoWindow: true);

            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using var process =
                Process.Start(startInfo);

            if (process is null)
            {
                ShowError(
                    owner,
                    "Legendary kunde inte startas.",
                    "Epic launch error");

                return false;
            }

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            var errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output =
                await outputTask;

            var error =
                await errorTask;

            if (process.ExitCode != 0)
            {
                var details =
                    string.IsNullOrWhiteSpace(error)
                        ? output
                        : error;

                ShowError(
                    owner,
                    "Epic-spelet kunde inte startas.\n\n" +
                    $"Exit code: {process.ExitCode}\n\n" +
                    details,
                    "Epic launch error");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowError(
                owner,
                $"Kunde inte starta Epic-spelet.\n\n{ex.Message}",
                "Epic launch error");

            return false;
        }
        finally
        {
            /*
             * EGET KONTO:
             *
             * Ta bort Epic-profilen efter launch.
             *
             * LÅNEKONTO:
             *
             * Profilen lämnas kvar.
             */
            if (temporaryProfile)
            {
                DeleteDirectorySafe(profilePath);
            }
        }
    }

    // =========================================================
    // PROFIL
    // =========================================================

    private string GetProfilePath(
        AccountDefinition? account)
    {
        /*
         * Eget konto får en temporär profil.
         */
        if (account is null)
        {
            return Path.Combine(
                _profilesPath,
                "OwnSession");
        }

        /*
         * Lånekonton får varsin permanent profil.
         */
        var safeName =
            MakeSafeName(account.Username);

        return Path.Combine(
            _profilesPath,
            "Borrowed",
            safeName);
    }

    private void PrepareProfile(
        string profilePath)
    {
        Directory.CreateDirectory(profilePath);

        /*
         * Legendary behöver installed.json för att veta
         * vilka spel som redan är installerade och var de finns.
         *
         * VIKTIGT:
         *
         * Vi kopierar INTE user.json.
         *
         * user.json innehåller Epic-kontots session.
         *
         * installed.json innehåller däremot information
         * om installerade spel.
         */
        CopyFileIfExists(
            Path.Combine(
                _baseLegendaryPath,
                "installed.json"),
            Path.Combine(
                profilePath,
                "installed.json"));
    }

    // =========================================================
    // LEGENDARY PROCESS
    // =========================================================

    private ProcessStartInfo CreateStartInfo(
        string profilePath,
        string arguments,
        bool createNoWindow)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    _legendaryPath,

                Arguments =
                    arguments,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    createNoWindow,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        _legendaryPath)
                    ?? AppContext.BaseDirectory
            };

        /*
         * Legendary använder denna katalog för sin
         * konfiguration och user.json.
         */
        startInfo.Environment[
            "LEGENDARY_CONFIG_PATH"] =
            profilePath;

        return startInfo;
    }

    // =========================================================
    // LEGENDARY ROOT
    // =========================================================

    private static string GetBaseLegendaryPath()
    {
        /*
         * Detta är den vanliga Legendary-profilen.
         *
         * Vi använder den endast som källa för
         * installationsinformation.
         *
         * user.json kopieras aldrig.
         */
        var userProfile =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        return Path.Combine(
            userProfile,
            ".config",
            "legendary");
    }

    // =========================================================
    // FILHANTERING
    // =========================================================

    private static void CopyFileIfExists(
        string source,
        string destination)
    {
        if (!File.Exists(source))
            return;

        var directory =
            Path.GetDirectoryName(destination);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(
            source,
            destination,
            overwrite: true);
    }

    private static void DeleteDirectorySafe(
        string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(
                path,
                recursive: true);

            return;
        }
        catch
        {
            // Legendary kan fortfarande hålla en fil öppen.
        }

        try
        {
            System.Threading.Thread.Sleep(250);

            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch
        {
            /*
             * Cleanup-fel ignoreras.
             *
             * Nästa gång eget konto används försöker
             * AuthenticateAccount() radera profilen igen.
             */
        }
    }

    // =========================================================
    // SÄKERT PROFILNAMN
    // =========================================================

    private static string MakeSafeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "account";

        foreach (var invalid
                 in Path.GetInvalidFileNameChars())
        {
            value =
                value.Replace(
                    invalid,
                    '_');
        }

        return value;
    }

    // =========================================================
    // HITTA LEGENDARY
    // =========================================================

    private static string? FindLegendary()
    {
        var candidates =
            new[]
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

        foreach (var candidate
                 in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    // =========================================================
    // UI
    // =========================================================

    private static void ShowWarning(
        Window owner,
        string message,
        string title)
    {
        MessageBox.Show(
            owner,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void ShowError(
        Window owner,
        string message,
        string title)
    {
        MessageBox.Show(
            owner,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
