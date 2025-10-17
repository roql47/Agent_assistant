using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;

namespace AgentAssistant
{
    public partial class EventDialog : FluentWindow
    {
        private DateTime selectedDate;
        public List<CalendarEvent> EventList { get; private set; }

        public EventDialog(DateTime date, List<CalendarEvent> existingEvents)
        {
            InitializeComponent();
            selectedDate = date;
            EventList = new List<CalendarEvent>(existingEvents);
            
            DateText.Text = date.ToString("yyyy년 M월 d일 (ddd)");
            UpdateEventList();
        }

        private void UpdateEventList()
        {
            EventListControl.ItemsSource = null;
            EventListControl.ItemsSource = this.EventList;
            
            NoEventsText.Visibility = this.EventList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleInput.Text))
            {
                System.Windows.MessageBox.Show("제목을 입력해주세요.", "입력 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var newEvent = new CalendarEvent
            {
                Title = TitleInput.Text.Trim(),
                Time = TimeInput.Text.Trim(),
                Description = DescriptionInput.Text.Trim()
            };

            EventList.Add(newEvent);
            UpdateEventList();

            // 입력 필드 초기화
            TitleInput.Text = "";
            TimeInput.Text = "오후 2:00";
            DescriptionInput.Text = "";
            
            TitleInput.Focus();
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is CalendarEvent eventToDelete)
            {
                var result = System.Windows.MessageBox.Show(
                    $"'{eventToDelete.Title}' 일정을 삭제하시겠습니까?",
                    "일정 삭제",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    EventList.Remove(eventToDelete);
                    UpdateEventList();
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

