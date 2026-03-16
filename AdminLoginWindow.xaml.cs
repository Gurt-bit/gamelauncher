using System.Windows;

namespace GameLauncher;

public partial class AdminLoginWindow : Window
{
    // Change this value to update the admin password.
    private const string AdminPassword = "admin123";

    public bool IsAuthorized { get; private set; }

    public AdminLoginWindow()
    {
        InitializeComponent();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password == AdminPassword)
        {
            IsAuthorized = true;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(this,
                "Incorrect admin password.",
                "Access denied",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

