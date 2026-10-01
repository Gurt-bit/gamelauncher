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
        IReadOnlyList<AccountDefinition>? accountsForGame)
    {
        InitializeComponent();

        _accountsForGame =
            accountsForGame ??
            System.Array.Empty<AccountDefinition>();

        TitleText.Text =
            $"Starta \"{gameTitle}\"";

        if (_accountsForGame.Any())
        {
            var first =
                _accountsForGame.First();

            BorrowAccountDetails.Text =
                $"Låna konto:\n{first.Username}";
        }
        else
        {
            BorrowAccountDetails.Text =
                "Inga lånekonton är konfigurerade.";

            BorrowAccountRadio.IsEnabled = false;
        }

        MyAccountPanel.Visibility =
            Visibility.Visible;
    }

    private void AccountType_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (MyAccountPanel == null)
        {
            return;
        }

        if (UseMyAccountRadio.IsChecked == true)
        {
            MyAccountPanel.Visibility =
                Visibility.Visible;
        }
        else
        {
            MyAccountPanel.Visibility =
                Visibility.Collapsed;
        }
    }

    private void Launch_Click(
        object sender,
        RoutedEventArgs e)
    {
        // =============================================
        // EGET KONTO
        // =============================================

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

        // =============================================
        // LÅNA KONTO
        // =============================================

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
    }
}
