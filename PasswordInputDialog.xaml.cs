using System.Windows;
using System.Windows.Input;

namespace AgentAssistant
{
    public partial class PasswordInputDialog : Window
    {
        public string EnteredPassword { get; private set; } = "";
        public bool IsAuthenticated { get; private set; } = false;

        public PasswordInputDialog(string departmentName)
        {
            InitializeComponent();
            DepartmentNameText.Text = $"부서: {departmentName}";
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PasswordBox.Password;
            IsAuthenticated = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}


