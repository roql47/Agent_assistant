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
        private Dictionary<string, string> allFormFields = new Dictionary<string, string>();

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
            this.allFormFields = new Dictionary<string, string>(pageResult.AllFormFields);
            
            System.Diagnostics.Debug.WriteLine($"[DisplayPageResult] 페이지 결과 업데이트:");
            System.Diagnostics.Debug.WriteLine($"  현재 페이지: {this.currentPage}");
            System.Diagnostics.Debug.WriteLine($"  총 페이지: {this.totalPages}");
            System.Diagnostics.Debug.WriteLine($"  메일 개수: {this.currentPageItems.Count}");
            System.Diagnostics.Debug.WriteLine($"  Form 필드 개수: {this.allFormFields.Count}");
            System.Diagnostics.Debug.WriteLine($"  ViewState 길이: {this.viewState?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  EventValidation 처음 50자: {(this.eventValidation?.Length > 50 ? this.eventValidation.Substring(0, 50) : this.eventValidation)}");
            
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
                
                System.Diagnostics.Debug.WriteLine($"[LoadPageAsync] 요청 페이지: {currentPage}");
                System.Diagnostics.Debug.WriteLine($"[LoadPageAsync] 사용할 Form 필드 개수: {allFormFields.Count}");
                System.Diagnostics.Debug.WriteLine($"[LoadPageAsync] 사용할 ViewState 길이: {viewState?.Length ?? 0}");
                System.Diagnostics.Debug.WriteLine($"[LoadPageAsync] 사용할 EventValidation 처음 50자: {(eventValidation?.Length > 50 ? eventValidation.Substring(0, 50) : eventValidation)}");
                
                // ASP.NET 포스트백으로 페이지 이동
                var pageResult = await crawler.NavigateToMailPageAsync(baseMailUrl, currentPage, allFormFields, viewState ?? "", viewStateGenerator, eventValidation ?? "");
                
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

