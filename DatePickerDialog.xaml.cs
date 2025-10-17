using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace AgentAssistant
{
    public partial class DatePickerDialog : FluentWindow
    {
        public DateTime SelectedDate { get; private set; }
        public string SelectedTime { get; private set; } = "오후 2:00";

        public DatePickerDialog(BoardItem boardItem)
        {
            InitializeComponent();
            
            // 게시글 제목 표시
            BoardTitleText.Text = $"게시글: {boardItem.Title}";
            
            // 기본값 설정
            SelectedDate = DateTime.Today;
            DatePickerControl.SelectedDate = DateTime.Today;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!DatePickerControl.SelectedDate.HasValue)
            {
                System.Windows.MessageBox.Show("날짜를 선택해주세요.", "입력 오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(TimeInput.Text))
            {
                System.Windows.MessageBox.Show("시간을 입력해주세요.", "입력 오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            SelectedDate = DatePickerControl.SelectedDate.Value.Date;
            SelectedTime = TimeInput.Text.Trim();
            
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

