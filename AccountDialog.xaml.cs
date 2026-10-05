using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GameLauncher;

public enum LaunchChoice
{
    None,
    UseMyAccount,
    BorrowAccount
}

public partial class AccountDialog : Window
{
    private readonly IReadOnlyList<AccountDefinition> _accountsForGame;
    private readonly string _platform;

    public LaunchChoice Choice { get; private set; } =
        LaunchChoice.None;

    public AccountDefinition? SelectedBorrowAccount
    {
        get;
        private set;
    }

    public string? MyUsername
    {
        get;
        private set;
    }

    public string? MyPassword
    {
        get;
        private set;
    }

    public AccountDialog(
        string gameTitle,
        string platform,
        IReadOnlyList<AccountDefinition> accountsForGame)
    {
        InitializeComponent();

        _platform = platform;
        _accountsForGame = accountsForGame;

        if (_platform.Equals(
                "epic",
                StringComparison.OrdinalIgnoreCase))
        {
            MyAccountPanel.Visibility =
                Visibility.Collapsed;

            MyUsernameLabel.Visibility =
                Visibility.Collapsed;

            MyUsernameTextBox.Visibility =
                Visibility.Collapsed;

            MyPasswordLabel.Visibility =
                Visibility.Collapsed;

            MyPasswordBox.Visibility =
                Visibility.Collapsed;
        }


        TitleText.Text = $"Starta \"{gameTitle}\"";

        var isEpic =
            platform.Equals(
                "epic",
                StringComparison.OrdinalIgnoreCase);

        var displayName = isEpic
            ? "Epic Games"
            : "Steam";

        MyUsernameLabel.Text =
            $"{displayName}-användarnamn";

        MyPasswordLabel.Text =
            $"{displayName}-lösenord";

        // Epic använder Legendarys egna autentiseringsflöde.
        // Vi ska därför inte fråga efter Epic-lösenord här.
        if (isEpic)
        {
            MyAccountPanel.Visibility =
                Visibility.Collapsed;

            MyUsernameLabel.Visibility =
                Visibility.Collapsed;

            MyUsernameTextBox.Visibility =
                Visibility.Collapsed;

            MyPasswordLabel.Visibility =
                Visibility.Collapsed;

            MyPasswordBox.Visibility =
                Visibility.Collapsed;
        }

        if (_accountsForGame.Any())
        {
            var first = _accountsForGame.First();

            BorrowAccountDetails.Text =
                $"Låna konto:\n{first.Username}";
        }
        else
        {
            BorrowAccountDetails.Text =
                "Inga lånekonton är konfigurerade.";

            BorrowAccountRadio.IsEnabled = false;
        }
    }

    private void AccountType_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (MyAccountPanel == null)
            return;

        if (_platform.Equals(
                "epic",
                StringComparison.OrdinalIgnoreCase))
        {
            MyAccountPanel.Visibility =
                Visibility.Collapsed;

            return;
        }

        MyAccountPanel.Visibility =
            UseMyAccountRadio.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }


    private void Launch_Click(
        object sender,
        RoutedEventArgs e)
    {
        // =================================================
        // EPIC
        // =================================================

        if (_platform.Equals(
                "epic",
                StringComparison.OrdinalIgnoreCase))
        {
            if (UseMyAccountRadio.IsChecked == true)
            {
                Choice =
                    LaunchChoice.UseMyAccount;

                DialogResult = true;
                return;
            }

            if (BorrowAccountRadio.IsChecked == true)
            {
                if (!_accountsForGame.Any())
                {
                    MessageBox.Show(
                        this,
                        "Inga lånekonton är konfigurerade för detta spel.",
                        "Inga konton",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                SelectedBorrowAccount =
                    _accountsForGame.First();

                Choice =
                    LaunchChoice.BorrowAccount;

                DialogResult = true;
                return;
            }

            return;
        }

        // =================================================
        // STEAM
        // =================================================

        if (UseMyAccountRadio.IsChecked == true)
        {
            var username =
                MyUsernameTextBox.Text.Trim();

            var password =
                MyPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(
                    this,
                    "Ange ditt Steam-användarnamn.",
                    "Användarnamn saknas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MyUsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    this,
                    "Ange ditt Steam-lösenord.",
                    "Lösenord saknas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MyPasswordBox.Focus();
                return;
            }

            MyUsername = username;
            MyPassword = password;

            Choice =
                LaunchChoice.UseMyAccount;

            DialogResult = true;
            return;
        }

        // =================================================
        // LÅNA STEAM-KONTO
        // =================================================

        if (BorrowAccountRadio.IsChecked == true)
        {
            if (!_accountsForGame.Any())
            {
                MessageBox.Show(
                    this,
                    "Inga lånekonton är konfigurerade för detta spel.",
                    "Inga konton",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            SelectedBorrowAccount =
                _accountsForGame.First();

            Choice =
                LaunchChoice.BorrowAccount;

            DialogResult = true;
        }
    }

}
