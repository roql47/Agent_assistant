using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace AgentAssistant
{
    public partial class BoardWindow : FluentWindow
    {
        private List<BoardItem> currentPageItems = new List<BoardItem>();
        private string? boardUrl;
        private string? username;
        private string? password;
        private string? loginUrl;
        
        private int currentPage = 1;
        private int totalPages = 1;
        private string? viewState;
        private string? viewStateGenerator;
        private string? eventValidation;
        private HttpIntranetCrawler? crawler;
        private string eventsFilePath = "calendar_events.json";

        public BoardWindow(BoardPageResult pageResult, string boardUrl, string loginUrl, string username, string password, HttpIntranetCrawler? existingCrawler = null)
        {
            InitializeComponent();
            this.boardUrl = boardUrl;
            this.loginUrl = loginUrl;
            this.username = username;
            this.password = password;
            
            // 기존 crawler를 재사용하거나 새로 생성
            if (existingCrawler != null)
            {
                this.crawler = existingCrawler;
            }
            else
            {
                this.crawler = new HttpIntranetCrawler();
                var baseUrl = new Uri(boardUrl).GetLeftPart(UriPartial.Authority);
                crawler.LoadCookies(baseUrl);
            }
            
            DisplayPageResult(pageResult);
        }

        private void DisplayPageResult(BoardPageResult pageResult)
        {
            // HTML 엔티티 제거 (&nbsp; 등)
            currentPageItems = pageResult.Items.Select(item => new BoardItem
            {
                Number = System.Net.WebUtility.HtmlDecode(item.Number),
                Title = System.Net.WebUtility.HtmlDecode(item.Title),
                Author = System.Net.WebUtility.HtmlDecode(item.Author),
                Date = System.Net.WebUtility.HtmlDecode(item.Date),
                Url = item.Url
            }).ToList();
            
            currentPage = pageResult.CurrentPage;
            
            // 총 페이지 수는 한번 설정되면 유지 (더 큰 값만 업데이트)
            if (pageResult.TotalPages > totalPages)
            {
                totalPages = pageResult.TotalPages;
            }
            
            viewState = pageResult.ViewState;
            viewStateGenerator = pageResult.ViewStateGenerator;
            eventValidation = pageResult.EventValidation;
            
            UpdatePageDisplay();
        }
        
        private void UpdatePageDisplay()
        {
            BoardList.ItemsSource = null;
            BoardList.ItemsSource = currentPageItems;
            
            BoardInfoText.Text = $"총 {totalPages}페이지";
            PageInfoText.Text = $"{currentPage} / {totalPages} 페이지";
            
            // 버튼 활성화/비활성화
            PrevPageButton.IsEnabled = currentPage > 1;
            NextPageButton.IsEnabled = currentPage < totalPages;
        }
        
        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1 && crawler != null && boardUrl != null)
            {
                try
                {
                    PrevPageButton.IsEnabled = false;
                    PrevPageButton.Content = "로딩 중...";
                    
                    var newPage = currentPage - 1;
                    var pageResult = await crawler.NavigateToPageAsync(boardUrl, newPage, viewState ?? "", viewStateGenerator ?? "", eventValidation ?? "");
                    DisplayPageResult(pageResult);
                    
                    PrevPageButton.Content = "◀ 이전";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"페이지 이동 실패:\n\n{ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    PrevPageButton.IsEnabled = true;
                    PrevPageButton.Content = "◀ 이전";
                }
            }
        }
        
        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages && crawler != null && boardUrl != null)
            {
                try
                {
                    NextPageButton.IsEnabled = false;
                    NextPageButton.Content = "로딩 중...";
                    
                    var newPage = currentPage + 1;
                    var pageResult = await crawler.NavigateToPageAsync(boardUrl, newPage, viewState ?? "", viewStateGenerator ?? "", eventValidation ?? "");
                    DisplayPageResult(pageResult);
                    
                    NextPageButton.Content = "다음 ▶";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"페이지 이동 실패:\n\n{ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    NextPageButton.IsEnabled = true;
                    NextPageButton.Content = "다음 ▶";
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(boardUrl) || crawler == null)
            {
                System.Windows.MessageBox.Show("새로고침 정보가 없습니다.", "알림", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "🔄 새로고침 중...";
                }
                
                var pageResult = await crawler.GetBoardItemsAsync(boardUrl, 1);
                DisplayPageResult(pageResult);
                
                System.Windows.MessageBox.Show($"새로고침 완료! (총 {totalPages}페이지)", "새로고침 완료", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);

                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "🔄 새로고침";
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"새로고침 실패:\n\n{ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "🔄 새로고침";
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BoardItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border && border.Tag is BoardItem boardItem)
            {
                System.Diagnostics.Debug.WriteLine($"[게시글 클릭] 번호: {boardItem.Number}, 제목: {boardItem.Title}");
                System.Diagnostics.Debug.WriteLine($"[게시글 클릭] URL: '{boardItem.Url}'");
                System.Diagnostics.Debug.WriteLine($"[게시글 클릭] boardUrl: '{boardUrl}'");
                
                // URL이 없으면 상세 조회 불가
                if (string.IsNullOrEmpty(boardItem.Url))
                {
                    System.Windows.MessageBox.Show(
                        $"게시글 URL을 찾을 수 없습니다.\n\n게시글 정보:\n번호: {boardItem.Number}\n제목: {boardItem.Title}\n작성자: {boardItem.Author}\n날짜: {boardItem.Date}",
                        "알림",
                        System.Windows.MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                try
                {
                    // 로딩 표시
                    this.Cursor = System.Windows.Input.Cursors.Wait;

                    // 게시글 상세 정보 가져오기 (ViewState 정보 전달)
                    var detail = await crawler?.GetBoardDetailAsync(
                        boardItem.Url, 
                        boardUrl ?? "", 
                        viewState ?? "", 
                        viewStateGenerator ?? "", 
                        eventValidation ?? "")!;

                    // 정보가 없으면 기본 정보로 채우기
                    if (string.IsNullOrEmpty(detail.Title))
                        detail.Title = boardItem.Title;
                    if (string.IsNullOrEmpty(detail.Author))
                        detail.Author = boardItem.Author;
                    if (string.IsNullOrEmpty(detail.Date))
                        detail.Date = boardItem.Date;
                    if (string.IsNullOrEmpty(detail.Number))
                        detail.Number = boardItem.Number;

                    // 상세 창 열기
                    var detailWindow = new BoardDetailWindow(detail);
                    detailWindow.Owner = this;
                    detailWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[게시글 클릭 오류] {ex.ToString()}");
                    System.Windows.MessageBox.Show(
                        $"게시글을 불러오는 중 오류가 발생했습니다:\n\n{ex.Message}\n\n게시글 URL:\n{boardItem.Url}",
                        "오류",
                        System.Windows.MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }
        }

        private void AddToCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is BoardItem boardItem)
            {
                // 날짜 선택 다이얼로그 표시
                var datePickerDialog = new DatePickerDialog(boardItem);
                datePickerDialog.Owner = this;
                
                if (datePickerDialog.ShowDialog() == true)
                {
                    var selectedDate = datePickerDialog.SelectedDate;
                    var selectedTime = datePickerDialog.SelectedTime;
                    
                    // 일정 데이터 로드
                    var events = LoadEvents();
                    
                    // 새 일정 추가
                    var newEvent = new CalendarEvent
                    {
                        Title = boardItem.Title,
                        Time = selectedTime,
                        Description = $"작성자: {boardItem.Author}\n날짜: {boardItem.Date}",
                        Url = boardItem.Url
                    };
                    
                    if (!events.ContainsKey(selectedDate))
                    {
                        events[selectedDate] = new List<CalendarEvent>();
                    }
                    
                    events[selectedDate].Add(newEvent);
                    
                    // 일정 저장
                    SaveEvents(events);
                    
                    // 캘린더 창이 열려있으면 실시간 동기화
                    var calendarWindow = CalendarWindow.GetCurrentInstance();
                    if (calendarWindow != null)
                    {
                        calendarWindow.RefreshCalendar();
                    }
                    
                    System.Windows.MessageBox.Show(
                        $"'{boardItem.Title}' 게시글이\n{selectedDate:yyyy년 M월 d일} {selectedTime}에 일정으로 추가되었습니다.",
                        "일정 추가 완료",
                        System.Windows.MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }
        
        private Dictionary<DateTime, List<CalendarEvent>> LoadEvents()
        {
            try
            {
                if (File.Exists(eventsFilePath))
                {
                    var json = File.ReadAllText(eventsFilePath);
                    var savedEvents = JsonSerializer.Deserialize<Dictionary<string, List<CalendarEvent>>>(json);
                    
                    var result = new Dictionary<DateTime, List<CalendarEvent>>();
                    if (savedEvents != null)
                    {
                        foreach (var kvp in savedEvents)
                        {
                            if (DateTime.TryParse(kvp.Key, out DateTime date))
                            {
                                result[date.Date] = kvp.Value;
                            }
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"일정 불러오기 오류: {ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            return new Dictionary<DateTime, List<CalendarEvent>>();
        }
        
        private void SaveEvents(Dictionary<DateTime, List<CalendarEvent>> events)
        {
            try
            {
                var savedEvents = new Dictionary<string, List<CalendarEvent>>();
                foreach (var kvp in events)
                {
                    savedEvents[kvp.Key.ToString("yyyy-MM-dd")] = kvp.Value;
                }
                
                var json = JsonSerializer.Serialize(savedEvents, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(eventsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"일정 저장 오류: {ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}


