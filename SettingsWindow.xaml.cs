using System;
using System.Windows;
using System.Windows.Controls;

namespace AgentAssistant
{
    public partial class SettingsWindow : Window
    {
        public bool StartWithWindows { get; private set; }
        public bool AlwaysOnTop { get; private set; }
        public bool ShowInTaskbarEnabled { get; private set; }
        public string CharacterColor { get; private set; } = "#6C5CE7";
        public double WindowOpacity { get; private set; } = 1.0;
        public bool EnableRandomMessages { get; private set; } = true;
        public int MessageInterval { get; private set; } = 30;

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // MainWindow에서 현재 설정 로드
            try
            {
                if (TopmostCheckBox != null && ShowInTaskbarCheckBox != null && OpacitySlider != null)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        TopmostCheckBox.IsChecked = mainWindow.Topmost;
                        ShowInTaskbarCheckBox.IsChecked = mainWindow.ShowInTaskbar;
                        OpacitySlider.Value = mainWindow.Opacity;
                    }
                }
            }
            catch (Exception ex)
            {
                // 기본값 사용
                System.Diagnostics.Debug.WriteLine($"설정 로드 오류: {ex.Message}");
            }
        }

        private void TopmostCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (TopmostCheckBox != null)
            {
                AlwaysOnTop = TopmostCheckBox.IsChecked ?? true;
            }
        }

        private void ShowInTaskbarCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowInTaskbarCheckBox != null)
            {
                ShowInTaskbarEnabled = ShowInTaskbarCheckBox.IsChecked ?? true;
            }
        }

        private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorComboBox != null && ColorComboBox.SelectedItem is ComboBoxItem item && item.Tag is string color)
            {
                CharacterColor = color;
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacitySlider != null)
            {
                WindowOpacity = OpacitySlider.Value;
            }
        }

        private void RandomMessagesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (RandomMessagesCheckBox != null)
            {
                EnableRandomMessages = RandomMessagesCheckBox.IsChecked ?? true;
                if (MessageIntervalSlider != null)
                {
                    MessageIntervalSlider.IsEnabled = EnableRandomMessages;
                }
            }
        }

        private void MessageIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MessageIntervalSlider != null)
            {
                MessageInterval = (int)MessageIntervalSlider.Value;
                if (MessageIntervalText != null)
                {
                    MessageIntervalText.Text = $"{MessageInterval}초";
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 설정 적용
            StartWithWindows = StartupCheckBox.IsChecked ?? false;
            AlwaysOnTop = TopmostCheckBox.IsChecked ?? true;
            ShowInTaskbarEnabled = ShowInTaskbarCheckBox.IsChecked ?? true;
            
            ApplySettings();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplySettings()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Topmost = AlwaysOnTop;
                mainWindow.ShowInTaskbar = ShowInTaskbarEnabled;
                mainWindow.Opacity = WindowOpacity;
                mainWindow.UpdateCharacterColor(CharacterColor);
                mainWindow.UpdateMessageInterval(MessageInterval);
                mainWindow.EnableRandomMessages = EnableRandomMessages;
            }
        }
    }
}


