using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace AgentAssistant
{
    public partial class MailWindow : FluentWindow
    {
        private List<MailItem> currentPageItems = new List<MailItem>();
        private string mailboxName = "";
        private string baseMailUrl = "";
        private HttpIntranetCrawler crawler = null!;
        private int currentPage = 1;
        private int totalPages = 1;
        private string viewState = "";
        private string viewStateGenerator = "";
        private string eventValidation = "";

        public MailWindow(MailPageResult pageResult, string mailboxName, string mailUrl, HttpIntranetCrawler crawler)
        {
            InitializeComponent();
            
            this.mailboxName = mailboxName;
            this.baseMailUrl = mailUrl;
            this.crawler = crawler;
            
            TitleText.Text = $"📧 {mailboxName}";
            DisplayPageResult(pageResult);
        }

        private void DisplayPageResult(MailPageResult pageResult)
        {
            this.currentPageItems = pageResult.Items;
            this.currentPage = pageResult.CurrentPage;
            this.totalPages = pageResult.TotalPages;
            this.viewState = pageResult.ViewState;
            this.viewStateGenerator = pageResult.ViewStateGenerator;
            this.eventValidation = pageResult.EventValidation;
            
            UpdateMailDisplay();
        }

        private void UpdateMailDisplay()
        {
            MailList.ItemsSource = null;
            MailList.ItemsSource = currentPageItems;
            
            MailInfoText.Text = $"총 {totalPages}페이지";
            PageInfoText.Text = $"{currentPage} / {totalPages} 페이지";
            
            PrevPageButton.IsEnabled = currentPage > 1;
            NextPageButton.IsEnabled = currentPage < totalPages;
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                await LoadPageAsync();
            }
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage++;
            await LoadPageAsync();
        }

        private async System.Threading.Tasks.Task LoadPageAsync()
        {
            try
            {
                PrevPageButton.IsEnabled = false;
                NextPageButton.IsEnabled = false;
                PrevPageButton.Content = "로딩 중...";
                NextPageButton.Content = "로딩 중...";
                
                // ASP.NET 포스트백으로 페이지 이동
                var pageResult = await crawler.NavigateToMailPageAsync(baseMailUrl, currentPage, viewState, viewStateGenerator, eventValidation);
                
                // 복사 가능한 디버그 창
                var debugInfo = $"페이지 이동 결과:\n\n" +
                    $"요청 페이지: {currentPage}\n" +
                    $"응답 페이지: {pageResult.CurrentPage}\n" +
                    $"총 페이지: {pageResult.TotalPages}\n" +
                    $"메일 개수: {pageResult.Items.Count}\n" +
                    $"첫 메일: {(pageResult.Items.Count > 0 ? pageResult.Items[0].Subject : "없음")}";
                
                var debugWin = new Window
                {
                    Title = "페이지 이동 디버그",
                    Width = 500,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                
                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = debugInfo,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(10)
                };
                Grid.SetRow(textBox, 0);
                grid.Children.Add(textBox);
                
                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                
                var copyBtn = new System.Windows.Controls.Button
                {
                    Content = "📋 복사",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                copyBtn.Click += (s, ev) => { Clipboard.SetText(debugInfo); System.Windows.MessageBox.Show("복사됨!", "알림", System.Windows.MessageBoxButton.OK); };
                btnPanel.Children.Add(copyBtn);
                
                var closeBtn = new System.Windows.Controls.Button
                {
                    Content = "닫기",
                    Width = 80,
                    Height = 30
                };
                closeBtn.Click += (s, ev) => debugWin.Close();
                btnPanel.Children.Add(closeBtn);
                
                Grid.SetRow(btnPanel, 1);
                grid.Children.Add(btnPanel);
                
                debugWin.Content = grid;
                debugWin.ShowDialog();
                
                DisplayPageResult(pageResult);
                
                PrevPageButton.Content = "◀ 이전";
                NextPageButton.Content = "다음 ▶";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"페이지 이동 실패:\n\n{ex.Message}", "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                PrevPageButton.Content = "◀ 이전";
                NextPageButton.Content = "다음 ▶";
                PrevPageButton.IsEnabled = currentPage > 1;
                NextPageButton.IsEnabled = currentPage < totalPages;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // 읽음 상태 변환기
    public class ReadStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return isRead ? "✓" : "NEW";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 읽음 상태 색상 변환기
    public class ReadColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return isRead ? Brushes.Green : new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 첨부파일 변환기
    public class AttachmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasAttachment)
            {
                return hasAttachment ? "📎" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

