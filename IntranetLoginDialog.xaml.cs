using System.Windows;
using System.Windows.Input;

namespace AgentAssistant
{
    public partial class IntranetLoginDialog : Window
    {
        public string Username { get; private set; } = "";
        public string Password { get; private set; } = "";

        public IntranetLoginDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => UsernameInput.Focus();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Login_Click(sender, e);
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameInput.Text))
            {
                MessageBox.Show("사용자 ID를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
                MessageBox.Show("비밀번호를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Username = UsernameInput.Text.Trim();
            Password = PasswordInput.Password;
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

