using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        _baseLegendaryPath = GetBaseLegendaryPath();

        _profilesPath = Path.Combine(
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
        var profilePath = GetProfilePath(account);

        try
        {
            // Eget konto ska ALLTID börja från en ren session.
            if (account is null)
            {
                DeleteDirectorySafe(profilePath);
            }

            PrepareProfile(profilePath);

            /*
             * Viktigt:
             *
             * Kör INTE --disable-webview.
             *
             * Om legendary.exe har WebView-stöd kommer Legendary
             * då att öppna sitt eget Epic-loginfönster och själv
             * hantera authorization-koden.
             *
             * Om executable saknar WebView faller Legendary tillbaka
             * till browser-flödet.
             */
            var result = await RunLegendaryAsync(
                profilePath,
                "auth",
                createNoWindow: false);

            if (result.Process is null)
            {
                ShowError(
                    owner,
                    "Legendary kunde inte startas.",
                    "Epic-login");

                return false;
            }

            if (result.ExitCode != 0)
            {
                ShowError(
                    owner,
                    BuildProcessError(
                        "Epic-inloggningen misslyckades.",
                        result),
                    "Epic-login");

                return false;
            }

            if (!HasCredentials(profilePath))
            {
                ShowError(
                    owner,
                    "Epic-inloggningen slutfördes inte.\n\n" +
                    "Ingen sparad Epic-session hittades.\n\n" +
                    "Om Chrome öppnades och visade JSON betyder det " +
                    "att din legendary.exe använder browser-fallbacken " +
                    "i stället för inbyggt WebView.",
                    "Epic-login");

                return false;
            }

            return await IsAuthenticatedInternal(profilePath);
        }
        catch (Exception ex)
        {
            ShowError(
                owner,
                $"Epic-inloggningen kunde inte genomföras.\n\n{ex.Message}",
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
         * Eget konto har ingen permanent profil.
         *
         * Det autentiseras när användaren startar ett spel.
         */
        if (account is null)
            return false;

        var profilePath = GetProfilePath(account);

        try
        {
            if (!HasCredentials(profilePath))
                return false;

            return await IsAuthenticatedInternal(profilePath);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsAuthenticatedInternal(
        string profilePath)
    {
        if (!HasCredentials(profilePath))
            return false;

        PrepareProfile(profilePath);

        var result = await RunLegendaryAsync(
            profilePath,
            "status",
            createNoWindow: true);

        if (result.Process is null)
            return false;

        /*
         * Legendary kan returnera 0 även när inget konto
         * är inloggat, därför kontrollerar vi output också.
         */
        if (result.ExitCode != 0)
            return false;

        var combinedOutput =
            result.Output + Environment.NewLine + result.Error;

        if (combinedOutput.Contains(
                "<not logged in>",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        /*
         * Om statuskommandot lyckades och vi har credentials
         * betraktar vi profilen som autentiserad.
         */
        return true;
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

        var isOwnAccount = account is null;
        var profilePath = GetProfilePath(account);

        try
        {
            // =====================================================
            // EGET KONTO
            // =====================================================

            /*
             * Eget konto ska ALLTID logga in.
             *
             * Vi raderar därför eventuell gammal session först.
             */
            if (isOwnAccount)
            {
                var authenticated =
                    await AuthenticateAccount(owner, null);

                if (!authenticated)
                    return false;

                profilePath = GetProfilePath(null);
            }

            // =====================================================
            // LÅNEKONTO
            // =====================================================

            /*
             * Lånekonto ska redan ha en sparad Legendary-profil.
             *
             * Ingen ny login ska visas.
             */
            else
            {
                if (!HasCredentials(profilePath))
                {
                    ShowError(
                        owner,
                        "Det här lånekontot har ingen sparad Epic-inloggning.\n\n" +
                        "Kontakta administratören eller logga in kontot igen.",
                        "Epic-login");

                    return false;
                }

                if (!await IsAuthenticatedInternal(profilePath))
                {
                    ShowError(
                        owner,
                        "Lånekontots Epic-session har gått ut eller är ogiltig.",
                        "Epic-login");

                    return false;
                }
            }

            // =====================================================
            // STARTA SPELET
            // =====================================================

            PrepareProfile(profilePath);

            var arguments =
                $"launch \"{EscapeArgument(appName)}\" " +
                "--skip-version-check";

            var result = await RunLegendaryAsync(
                profilePath,
                arguments,
                createNoWindow: true);

            if (result.Process is null)
            {
                ShowError(
                    owner,
                    "Legendary kunde inte startas.",
                    "Epic launch error");

                return false;
            }

            if (result.ExitCode != 0)
            {
                ShowError(
                    owner,
                    BuildProcessError(
                        "Epic-spelet kunde inte startas.",
                        result),
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
            // =====================================================
            // EGET KONTO
            // =====================================================

            /*
             * OwnSession ska aldrig sparas till nästa start.
             *
             * Nästa gång användaren startar ett spel måste
             * AuthenticateAccount() därför köras igen.
             *
             * Lånekontots profil rörs inte.
             */
            if (isOwnAccount)
            {
                DeleteDirectorySafe(profilePath);
            }
        }
    }

    // =========================================================
    // LOGOUT / RENSNING
    // =========================================================

    public void Logout(
        AccountDefinition? account)
    {
        var profilePath = GetProfilePath(account);

        DeleteDirectorySafe(profilePath);
    }

    // =========================================================
    // PROFIL
    // =========================================================

    private string GetProfilePath(
        AccountDefinition? account)
    {
        /*
         * Eget konto använder en separat session.
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
         * Kopiera INTE user.json.
         *
         * user.json hör till den specifika profilen och
         * innehåller Epic-sessionen.
         *
         * Vi kopierar bara installationsinformationen.
         */
        CopyFileIfExists(
            Path.Combine(
                _baseLegendaryPath,
                "installed.json"),
            Path.Combine(
                profilePath,
                "installed.json"));
    }

    private static bool HasCredentials(
        string profilePath)
    {
        /*
         * Legendary använder user.json för sparad
         * autentiseringsdata.
         */
        var userJson =
            Path.Combine(
                profilePath,
                "user.json");

        return File.Exists(userJson) &&
               new FileInfo(userJson).Length > 0;
    }

    // =========================================================
    // LEGENDARY PROCESS
    // =========================================================

    private async Task<LegendaryResult> RunLegendaryAsync(
        string profilePath,
        string arguments,
        bool createNoWindow)
    {
        var startInfo =
            CreateStartInfo(
                profilePath,
                arguments,
                createNoWindow);

        /*
         * Vi läser stdout/stderr själva så att vi kan visa
         * ett användbart fel om Legendary misslyckas.
         */
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process =
            Process.Start(startInfo);

        if (process is null)
        {
            return new LegendaryResult(
                null,
                -1,
                string.Empty,
                "Process.Start returnerade null.");
        }

        /*
         * Läs stdout och stderr samtidigt.
         *
         * Det minskar risken för deadlock om Legendary
         * skriver mycket till stderr.
         */
        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output =
            await outputTask;

        var error =
            await errorTask;

        return new LegendaryResult(
            process,
            process.ExitCode,
            output,
            error);
    }

    private ProcessStartInfo CreateStartInfo(
        string profilePath,
        string arguments,
        bool createNoWindow)
    {
        var workingDirectory =
            Path.GetDirectoryName(
                _legendaryPath)
            ?? AppContext.BaseDirectory;

        var startInfo =
            new ProcessStartInfo
            {
                FileName = _legendaryPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                WorkingDirectory = workingDirectory
            };

        /*
         * Detta är det centrala för separata Legendary-profiler.
         *
         * All Legendary-login-data ska därför läsas och
         * skrivas i profilePath.
         */
        startInfo.Environment[
            "LEGENDARY_CONFIG_PATH"] =
            profilePath;

        return startInfo;
    }

    // =========================================================
    // RESULTAT FRÅN LEGENDARY
    // =========================================================

    private sealed class LegendaryResult
    {
        public Process? Process { get; }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        public LegendaryResult(
            Process? process,
            int exitCode,
            string output,
            string error)
        {
            Process = process;
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }

    private static string BuildProcessError(
        string message,
        LegendaryResult result)
    {
        var details =
            !string.IsNullOrWhiteSpace(result.Error)
                ? result.Error.Trim()
                : result.Output.Trim();

        if (string.IsNullOrWhiteSpace(details))
        {
            return message +
                   "\n\nExit code: " +
                   result.ExitCode;
        }

        return
            message +
            "\n\nExit code: " +
            result.ExitCode +
            "\n\n" +
            details;
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

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(
                    path,
                    recursive: true);

                return;
            }
            catch
            {
                if (attempt < 2)
                {
                    System.Threading.Thread.Sleep(250);
                }
            }
        }
    }

    // =========================================================
    // LEGENDARY ROOT
    // =========================================================

    private static string GetBaseLegendaryPath()
    {
        /*
         * För Windows är %APPDATA% en bättre fallback för
         * Legendarys vanliga konfigurationskatalog än att
         * hårdkoda ~/.config.
         */
        var appData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        /*
         * Testa de vanligaste Windows-platserna.
         */
        var candidates =
            new[]
            {
                Path.Combine(
                    appData,
                    "legendary"),

                Path.Combine(
                    localAppData,
                    "legendary"),

                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".config",
                    "legendary")
            };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        /*
         * Returnera första kandidaten även om den inte finns.
         * PrepareProfile() fungerar då ändå och installed.json
         * är bara en extra funktion.
         */
        return candidates[0];
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

        /*
         * Undvik väldigt långa Windows-filnamn.
         */
        value = value.Trim();

        if (value.Length > 100)
        {
            value = value[..100];
        }

        return string.IsNullOrWhiteSpace(value)
            ? "account"
            : value;
    }

    // =========================================================
    // ARGUMENT
    // =========================================================

    private static string EscapeArgument(
        string value)
    {
        /*
         * appName kommer normalt från games.json.
         *
         * Escape:a citattecken och backslashes så att
         * ProcessStartInfo.Arguments inte får felaktig quoting.
         */
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
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
                    "legendary.exe"),

                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
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