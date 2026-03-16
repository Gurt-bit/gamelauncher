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

    public LaunchChoice Choice { get; private set; } = LaunchChoice.None;
    public AccountDefinition? SelectedBorrowAccount { get; private set; }

    public AccountDialog(string gameTitle, IReadOnlyList<AccountDefinition> accountsForGame)
    {
        InitializeComponent();
        _accountsForGame = accountsForGame;

        TitleText.Text = $"Starta \"{gameTitle}\"";

        if (_accountsForGame.Any())
        {
            var first = _accountsForGame.First();
            BorrowAccountDetails.Text = $"Låna konto:\n{first.Username}";
        }
        else
        {
            BorrowAccountDetails.Text = "No borrowable accounts configured for this game.";
            BorrowAccountRadio.IsEnabled = false;

            // Dölj eller disable lokalt konto
            UseMyAccountRadio.Visibility = Visibility.Collapsed;
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (UseMyAccountRadio.IsChecked == true)
        {
            Choice = LaunchChoice.UseMyAccount;
            DialogResult = true;
            return;
        }

        if (BorrowAccountRadio.IsChecked == true)
        {
            if (!_accountsForGame.Any())
            {
                MessageBox.Show(this,
                    "No borrowable accounts are configured for this game (see accounts.json).",
                    "No accounts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Choice = LaunchChoice.BorrowAccount;
            SelectedBorrowAccount = _accountsForGame.First();
            DialogResult = true;
        }
    }
}

