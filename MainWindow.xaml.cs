using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AgentAssistant
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private DispatcherTimer idleTimer;
        private DispatcherTimer eyeTrackingTimer;
        private DispatcherTimer clipboardMonitorTimer;
        private Random random;
        private string lastClipboardText = "";
        private string[] greetings = new[]
        {
            "안녕하세요! 😊",
            "좋은 하루 되세요!",
            "무엇을 도와드릴까요?",
            "오늘도 화이팅! 💪",
            "함께 작업해봐요!",
            "궁금한 게 있으신가요?"
        };
        
        private string[] helpMessages = new[]
        {
            "저를 드래그해서 이동할 수 있어요!",
            "우클릭하면 메뉴가 나타나요!",
            "설정에서 더 많은 옵션을 확인하세요!",
            "언제든 도움이 필요하면 말씀해주세요!"
        };

        public bool EnableRandomMessages { get; set; } = true;

        public MainWindow()
        {
            InitializeComponent();
            random = new Random();
            
            // 타이머 설정 (주기적인 인사말)
            idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            idleTimer.Tick += IdleTimer_Tick;
            idleTimer.Start();

            // 눈동자 추적 타이머
            eyeTrackingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 초당 20회 업데이트
            };
            eyeTrackingTimer.Tick += EyeTrackingTimer_Tick;
            eyeTrackingTimer.Start();
            
            // 클립보드 모니터링 타이머 (쿠키 자동 감지)
            clipboardMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // 1초마다 확인
            };
            clipboardMonitorTimer.Tick += ClipboardMonitor_Tick;
            clipboardMonitorTimer.Start();
            
            // 시작 위치 설정 (화면 우측 하단)
            Left = SystemParameters.PrimaryScreenWidth - Width - 50;
            Top = SystemParameters.PrimaryScreenHeight - Height - 100;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Idle 애니메이션 시작
            var storyboard = (Storyboard)FindResource("IdleAnimation");
            storyboard.Begin();
            
            // 시작 인사
            ShowMessage("안녕하세요! 저는 여러분의 비서예요! 😊");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 윈도우 드래그
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // 윈도우 위에 마우스가 있을 때도 업데이트
        }

        private void EyeTrackingTimer_Tick(object? sender, EventArgs e)
        {
            UpdatePupilPosition();
        }

        private void UpdatePupilPosition()
        {
            try
            {
                if (LeftPupilTransform == null || RightPupilTransform == null || CharacterContainer == null)
                    return;

                // 화면 상의 마우스 절대 위치 가져오기
                POINT cursorPos;
                if (!GetCursorPos(out cursorPos))
                    return;
                
                // 윈도우 위치
                var windowPos = PointToScreen(new Point(0, 0));
                
                // 캐릭터 컨테이너의 중심점 (화면 좌표)
                var containerRect = CharacterContainer.TransformToAncestor(this)
                    .TransformBounds(new Rect(0, 0, CharacterContainer.ActualWidth, CharacterContainer.ActualHeight));
                var centerX = windowPos.X + containerRect.Left + containerRect.Width / 2;
                var centerY = windowPos.Y + containerRect.Top + containerRect.Height / 2;

                // 마우스와 캐릭터 중심 간의 거리 계산
                var deltaX = cursorPos.X - centerX;
                var deltaY = cursorPos.Y - centerY;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // 거리에 따른 움직임 계산
                var maxMove = 3.5;
                var moveRatio = Math.Min(1.0, distance / 300.0); // 300픽셀 거리에서 최대값
                
                var angle = Math.Atan2(deltaY, deltaX);
                var moveX = Math.Cos(angle) * maxMove * moveRatio;
                var moveY = Math.Sin(angle) * maxMove * moveRatio;

                // 부드러운 애니메이션 적용
                var duration = TimeSpan.FromMilliseconds(150);
                var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                
                var leftAnimX = new DoubleAnimation(moveX, duration) { EasingFunction = easing };
                var leftAnimY = new DoubleAnimation(moveY, duration) { EasingFunction = easing };
                var rightAnimX = new DoubleAnimation(moveX, duration) { EasingFunction = easing };
                var rightAnimY = new DoubleAnimation(moveY, duration) { EasingFunction = easing };

                LeftPupilTransform.BeginAnimation(TranslateTransform.XProperty, leftAnimX);
                LeftPupilTransform.BeginAnimation(TranslateTransform.YProperty, leftAnimY);
                RightPupilTransform.BeginAnimation(TranslateTransform.XProperty, rightAnimX);
                RightPupilTransform.BeginAnimation(TranslateTransform.YProperty, rightAnimY);
            }
            catch
            {
                // 예외 무시
            }
        }

        private void ClipboardMonitor_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var clipboardText = Clipboard.GetText();
                    
                    // 같은 내용이면 무시
                    if (clipboardText == lastClipboardText)
                        return;
                    
                    lastClipboardText = clipboardText;
                    
                    // 쿠키 형식인지 확인
                    if (IsCookieFormat(clipboardText))
                    {
                        // 자동으로 쿠키 저장
                        if (SaveCookieFromClipboard(clipboardText))
                        {
                            ShowMessage("🍪 쿠키를 자동으로 감지하고 저장했어요! (클립보드에서)", 5);
                        }
                    }
                }
            }
            catch
            {
                // 클립보드 접근 오류 무시
            }
        }

        private bool IsCookieFormat(string text)
        {
            // 쿠키 헤더 형식 확인
            text = text.ToLower();
            return (text.Contains("asp.net_sessionid") || text.Contains("smart2application")) &&
                   (text.Contains("=") && (text.Contains(";") || text.Contains("cookie:")));
        }

        private bool SaveCookieFromClipboard(string cookieText)
        {
            try
            {
                var cookies = new Dictionary<string, string>();
                
                // "cookie:" 헤더 제거
                if (cookieText.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
                {
                    cookieText = cookieText.Substring(7).Trim();
                }
                
                // 여러 줄 처리
                var lines = cookieText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
                    {
                        cookieText = trimmedLine.Substring(7).Trim();
                        break;
                    }
                }
                
                // 세미콜론으로 구분된 쿠키 파싱
                var cookieParts = cookieText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in cookieParts)
                {
                    var kvp = part.Split(new[] { '=' }, 2);
                    if (kvp.Length == 2)
                    {
                        var name = kvp[0].Trim();
                        var value = kvp[1].Trim();
                        
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                        {
                            cookies[name] = value;
                        }
                    }
                }
                
                if (cookies.Count > 0)
                {
                    // 쿠키 저장
                    var json = System.Text.Json.JsonSerializer.Serialize(cookies, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText("manual_cookies.json", json);
                    return true;
                }
            }
            catch
            {
            }
            
            return false;
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            // 30초마다 랜덤 메시지 표시
            if (EnableRandomMessages && random.Next(0, 3) == 0) // 33% 확률
            {
                ShowRandomGreeting();
            }
        }

        public void UpdateCharacterColor(string colorHex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var brush = new SolidColorBrush(color);
                
                // CharacterContainer에서 Ellipse 찾기
                var ellipse = FindVisualChild<Ellipse>(CharacterContainer);
                if (ellipse != null)
                {
                    ellipse.Fill = brush;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"색상 변경 오류: {ex.Message}", 3);
            }
        }

        public void UpdateMessageInterval(int seconds)
        {
            idleTimer.Interval = TimeSpan.FromSeconds(seconds);
            idleTimer.Stop();
            idleTimer.Start();
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void ShowMessage(string message, int durationSeconds = 5)
        {
            SpeechText.Text = message;
            SpeechBubble.Visibility = Visibility.Visible;
            
            // 애니메이션 효과
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            SpeechBubble.BeginAnimation(OpacityProperty, fadeIn);
            
            // Wave 애니메이션
            var waveStoryboard = (Storyboard)FindResource("WaveAnimation");
            waveStoryboard.Begin();
            
            // 일정 시간 후 숨김
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            hideTimer.Tick += (s, e) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (ss, ee) =>
                {
                    SpeechBubble.Visibility = Visibility.Collapsed;
                };
                SpeechBubble.BeginAnimation(OpacityProperty, fadeOut);
                hideTimer.Stop();
            };
            hideTimer.Start();
        }

        private void ShowRandomGreeting()
        {
            var greeting = greetings[random.Next(greetings.Length)];
            ShowMessage(greeting, 4);
        }

        private void Greet_Click(object sender, RoutedEventArgs e)
        {
            ShowRandomGreeting();
        }

        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            var help = helpMessages[random.Next(helpMessages.Length)];
            ShowMessage(help, 6);
        }

        private void HideBubble_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, ee) =>
            {
                SpeechBubble.Visibility = Visibility.Collapsed;
            };
            SpeechBubble.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                
                var result = settingsWindow.ShowDialog();
                if (result == true)
                {
                    ShowMessage("설정이 적용되었어요! ✨", 3);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"설정 창을 여는 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenCalendar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var calendarWindow = new CalendarWindow();
                calendarWindow.Show();
                ShowMessage("캘린더를 열었어요! 📅", 3);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"캘린더 열기 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CheckBoard_Notice_Click(object sender, RoutedEventArgs e)
        {
            string boardUrl = "https://ngw.cauhs.or.kr/WebSite/Basic/Board/BoardList.aspx?system=Board&fdid=5565";
            CheckBoardAsync("공지사항", boardUrl);
        }

        private void CheckBoard_WorkRules_Click(object sender, RoutedEventArgs e)
        {
            string boardUrl = "https://ngw.cauhs.or.kr/WebSite/Basic/Board/BoardList.aspx?system=Board&fdid=5997";
            CheckBoardAsync("업무규정", boardUrl);
        }

        private async void CheckMail_Inbox_Click(object sender, RoutedEventArgs e)
        {
            await CheckMailFolderAsync("받은 편지함", false);
        }

        private async void CheckMail_Sent_Click(object sender, RoutedEventArgs e)
        {
            await CheckMailFolderAsync("보낸 편지함", true);
        }

        private async System.Threading.Tasks.Task CheckMailFolderAsync(string folderName, bool isSentItems)
        {
            try
            {
                ShowMessage($"{folderName}을 찾고 있어요... 📧", 3);

                var crawler = new HttpIntranetCrawler();
                string baseUrl = "https://ngw.cauhs.or.kr";
                
                // 쿠키 로드
                bool cookieLoginSuccess = crawler.LoadCookies(baseUrl);
                
                if (!cookieLoginSuccess)
                {
                    MessageBox.Show(
                        "쿠키를 찾을 수 없습니다.\n\n먼저 로그인하거나 쿠키를 입력해주세요.",
                        "쿠키 필요",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // 메일 폴더 목록 가져오기
                var folders = await crawler.GetMailFoldersAsync();
                
                // 원하는 폴더 찾기
                var targetFolder = folders.FirstOrDefault(f => f.IsSentItems == isSentItems);
                
                if (targetFolder == null || string.IsNullOrEmpty(targetFolder.Fid))
                {
                    MessageBox.Show(
                        $"{folderName}을 찾을 수 없습니다.\n\n발견된 폴더: {string.Join(", ", folders.Select(f => f.Name))}",
                        "알림",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // 폴더 URL 생성
                string mailUrl = $"https://ngw.cauhs.or.kr/WebSite/Mail/MailList.aspx?system=Mail&fid={Uri.EscapeDataString(targetFolder.Fid)}&issentitems={(isSentItems ? "Y" : "N")}";
                
                CheckMailAsync(folderName, mailUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"메일 폴더 조회 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void CheckMailAsync(string mailName, string mailUrl)
        {
            try
            {
                ShowMessage($"{mailName}을 확인하고 있어요... 📧", 3);

                var crawler = new HttpIntranetCrawler();
                string baseUrl = "https://ngw.cauhs.or.kr";
                
                // 쿠키 로드
                bool cookieLoginSuccess = crawler.LoadCookies(baseUrl);
                
                if (!cookieLoginSuccess)
                {
                    var debugMessage = crawler.GetLastCookieDebugInfo();
                    MessageBox.Show(
                        $"쿠키를 찾을 수 없습니다.\n\n" +
                        $"먼저 로그인하거나 쿠키를 입력해주세요.\n\n" +
                        "🔐 로그인 (ID/PW) 또는 🔑 쿠키 입력을 사용하세요.",
                        "쿠키 필요",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                ShowMessage($"{mailName}을 불러오고 있어요... 📧", 3);
                
                var pageResult = await crawler.GetMailPageAsync(mailUrl, 1);
                
                if (pageResult.Items.Any())
                {
                    ShowMessage($"{mailName} {pageResult.Items.Count}개를 찾았어요! (총 {pageResult.TotalPages}페이지) 📧", 3);
                    
                    // 메일 목록 창 표시 (crawler 전달)
                    var mailWindow = new MailWindow(pageResult, mailName, mailUrl, crawler);
                    mailWindow.Show();
                }
                else
                {
                    // 응답 HTML 다시 가져와서 디버그
                    var response = await new System.Net.Http.HttpClient().GetStringAsync(mailUrl);
                    var debugResult = MailCrawler.ParseMailListHtmlWithDebug(response);
                    
                    // 복사 가능한 디버그 창
                    var debugWindow = new Window
                    {
                        Title = "메일 파싱 디버그",
                        Width = 700,
                        Height = 500,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Owner = this
                    };
                    
                    var grid = new Grid { Margin = new Thickness(20) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    var title = new TextBlock
                    {
                        Text = "메일을 찾을 수 없습니다",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    Grid.SetRow(title, 0);
                    grid.Children.Add(title);
                    
                    var debugTextBox = new TextBox
                    {
                        Text = $"디버그 정보:\n\n{debugResult.debugInfo}\n\n아래 정보를 복사해서 확인할 수 있습니다.",
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Padding = new Thickness(10)
                    };
                    Grid.SetRow(debugTextBox, 1);
                    grid.Children.Add(debugTextBox);
                    
                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    
                    var copyButton = new System.Windows.Controls.Button
                    {
                        Content = "📋 복사",
                        Width = 100,
                        Height = 35,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    copyButton.Click += (s, ev) =>
                    {
                        Clipboard.SetText(debugResult.debugInfo);
                        MessageBox.Show("디버그 정보가 복사되었습니다!", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    };
                    buttonPanel.Children.Add(copyButton);
                    
                    var closeButton = new System.Windows.Controls.Button
                    {
                        Content = "닫기",
                        Width = 100,
                        Height = 35
                    };
                    closeButton.Click += (s, ev) => debugWindow.Close();
                    buttonPanel.Children.Add(closeButton);
                    
                    Grid.SetRow(buttonPanel, 2);
                    grid.Children.Add(buttonPanel);
                    
                    debugWindow.Content = grid;
                    debugWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"메일 조회 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CheckIntranet_Click(object sender, RoutedEventArgs e)
        {
            // 이전 버전 호환성을 위해 공지사항으로 연결
            CheckBoard_Notice_Click(sender, e);
        }

        private async void CheckBoardAsync(string boardName, string boardUrl)
        {
            try
            {
                ShowMessage($"{boardName}을 확인하고 있어요... 🔍", 3);

                var crawler = new HttpIntranetCrawler();
                string loginUrl = "https://ngw.cauhs.or.kr/WebSite/Login.aspx?isMobile=0";
                string baseUrl = "https://ngw.cauhs.or.kr";
                
                // LoadCookies가 자동으로 Chrome/Edge에서 최신 쿠키를 로드함
                bool cookieLoginSuccess = false;
                string debugMessage = "";
                
                try
                {
                    cookieLoginSuccess = crawler.LoadCookies(baseUrl);
                    debugMessage = crawler.GetLastCookieDebugInfo(); // 디버그 정보 가져오기
                }
                catch (Exception ex)
                {
                    debugMessage = $"쿠키 로드 예외: {ex.Message}";
                }
                
                if (!cookieLoginSuccess)
                {
                    // 복사 가능한 디버그 정보 창 생성
                    var debugWindow = new Window
                    {
                        Title = "쿠키 디버그 정보",
                        Width = 600,
                        Height = 400,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Owner = this
                    };
                    
                    var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    var title = new System.Windows.Controls.TextBlock
                    {
                        Text = "쿠키를 찾을 수 없습니다",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    System.Windows.Controls.Grid.SetRow(title, 0);
                    grid.Children.Add(title);
                    
                    var debugTextBox = new System.Windows.Controls.TextBox
                    {
                        Text = $"디버그 정보:\n{debugMessage}\n\nChrome 또는 Edge에서 https://ngw.cauhs.or.kr 에 로그인되어 있나요?\n\n아래 정보를 복사해서 개발자에게 전달할 수 있습니다.",
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    System.Windows.Controls.Grid.SetRow(debugTextBox, 1);
                    grid.Children.Add(debugTextBox);
                    
                    var buttonPanel = new System.Windows.Controls.StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    
                    var copyButton = new System.Windows.Controls.Button
                    {
                        Content = "📋 복사",
                        Width = 100,
                        Height = 35,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    copyButton.Click += (s, e) =>
                    {
                        Clipboard.SetText(debugMessage);
                        MessageBox.Show("디버그 정보가 클립보드에 복사되었습니다!", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    };
                    buttonPanel.Children.Add(copyButton);
                    
                    var manualInputButton = new System.Windows.Controls.Button
                    {
                        Content = "수동 입력",
                        Width = 100,
                        Height = 35,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    manualInputButton.Click += (s, e) =>
                    {
                        debugWindow.DialogResult = true;
                        debugWindow.Close();
                    };
                    buttonPanel.Children.Add(manualInputButton);
                    
                    var closeButton = new System.Windows.Controls.Button
                    {
                        Content = "닫기",
                        Width = 100,
                        Height = 35
                    };
                    closeButton.Click += (s, e) => debugWindow.Close();
                    buttonPanel.Children.Add(closeButton);
                    
                    System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
                    grid.Children.Add(buttonPanel);
                    
                    debugWindow.Content = grid;
                    
                    if (debugWindow.ShowDialog() == true)
                    {
                        var cookieDialog = new CookieInputDialog { Owner = this };
                        if (cookieDialog.ShowDialog() == true)
                        {
                            cookieLoginSuccess = crawler.LoadCookies(baseUrl);
                        }
                    }
                    
                    if (!cookieLoginSuccess)
                    {
                        return;
                    }
                }
                
                ShowMessage($"{boardName}을 불러오고 있어요... 📋", 3);
                
                var pageResult = await crawler.GetBoardItemsAsync(boardUrl, 1);
                
                if (pageResult.Items.Any())
                {
                    ShowMessage($"{boardName} {pageResult.Items.Count}개를 찾았어요! (총 {pageResult.TotalPages}페이지) 📋", 3);
                    
                    // crawler를 전달하여 세션 유지
                    var boardWindow = new BoardWindow(
                        pageResult,
                        boardUrl,
                        loginUrl,
                        "",
                        "",
                        crawler);  // crawler 전달
                    boardWindow.Title = $"{boardName} 목록";
                    boardWindow.Show();
                }
                else
                {
                    MessageBox.Show(
                        "게시글을 찾을 수 없습니다.\n\n게시판이 비어있거나 페이지 구조가 변경되었을 수 있습니다.",
                        "알림",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"게시판 조회 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InputCookie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cookieDialog = new CookieInputDialog { Owner = this };
                if (cookieDialog.ShowDialog() == true)
                {
                    ShowMessage("쿠키가 저장되었어요! 🍪", 3);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"쿠키 입력 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void IntranetLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var loginDialog = new IntranetLoginDialog { Owner = this };
                if (loginDialog.ShowDialog() == true)
                {
                    ShowMessage("Selenium으로 자동 로그인 중... 🤖", 3);
                    
                    string loginUrl = "https://ngw.cauhs.or.kr/WebSite/Login.aspx?isMobile=0";
                    
                    var loginResult = SeleniumCookieExtractor.AutoLogin(loginDialog.Username, loginDialog.Password, loginUrl);
                    
                    if (loginResult.success && loginResult.cookies.Count > 0)
                    {
                        // 쿠키 저장
                        var json = System.Text.Json.JsonSerializer.Serialize(loginResult.cookies, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText("manual_cookies.json", json);
                        
                        ShowMessage($"로그인 성공! {loginResult.cookies.Count}개 쿠키 저장했어요! 🎉", 4);
                        
                        // 로그인 정보를 파일로 저장 (자동 로그인용)
                        var loginInfo = new { username = loginDialog.Username, password = loginDialog.Password, loginUrl = loginUrl };
                        var loginJson = System.Text.Json.JsonSerializer.Serialize(loginInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText("login_info.json", loginJson);
                    }
                    else
                    {
                        // 디버그 정보를 보여주는 창
                        var debugWindow = new Window
                        {
                            Title = "로그인 실패 - 디버그 정보",
                            Width = 600,
                            Height = 400,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            Owner = this
                        };
                        
                        var grid = new Grid { Margin = new Thickness(20) };
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        
                        var title = new TextBlock
                        {
                            Text = "로그인 실패",
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.Red,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        Grid.SetRow(title, 0);
                        grid.Children.Add(title);
                        
                        var debugTextBox = new TextBox
                        {
                            Text = $"아이디 또는 비밀번호를 확인해주세요.\n\n디버그 정보:\n{loginResult.debugInfo}",
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 11,
                            Padding = new Thickness(10)
                        };
                        Grid.SetRow(debugTextBox, 1);
                        grid.Children.Add(debugTextBox);
                        
                        var buttonPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        
                        var copyButton = new System.Windows.Controls.Button
                        {
                            Content = "📋 복사",
                            Width = 100,
                            Height = 35,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        copyButton.Click += (s, ev) =>
                        {
                            Clipboard.SetText(loginResult.debugInfo);
                            MessageBox.Show("디버그 정보가 복사되었습니다!", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                        };
                        buttonPanel.Children.Add(copyButton);
                        
                        var closeButton = new System.Windows.Controls.Button
                        {
                            Content = "닫기",
                            Width = 100,
                            Height = 35
                        };
                        closeButton.Click += (s, ev) => debugWindow.Close();
                        buttonPanel.Children.Add(closeButton);
                        
                        Grid.SetRow(buttonPanel, 2);
                        grid.Children.Add(buttonPanel);
                        
                        debugWindow.Content = grid;
                        debugWindow.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"로그인 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DiagnoseCookies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowMessage("쿠키를 진단하고 있어요... 🔍", 3);
                var diagnosis = TestCookieReader.GetAllCookieDomains();
                
                var diagWindow = new Window
                {
                    Title = "쿠키 진단 결과",
                    Width = 700,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Owner = this
                };
                
                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var title = new TextBlock
                {
                    Text = "브라우저 쿠키 진단 결과",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(title, 0);
                grid.Children.Add(title);
                
                var diagTextBox = new TextBox
                {
                    Text = diagnosis,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Padding = new Thickness(10)
                };
                Grid.SetRow(diagTextBox, 1);
                grid.Children.Add(diagTextBox);
                
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                
                var copyButton = new System.Windows.Controls.Button
                {
                    Content = "📋 복사",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                copyButton.Click += (s, ev) =>
                {
                    Clipboard.SetText(diagnosis);
                    MessageBox.Show("진단 정보가 클립보드에 복사되었습니다!", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                };
                buttonPanel.Children.Add(copyButton);
                
                var closeButton = new System.Windows.Controls.Button
                {
                    Content = "닫기",
                    Width = 100,
                    Height = 35
                };
                closeButton.Click += (s, ev) => diagWindow.Close();
                buttonPanel.Children.Add(closeButton);
                
                Grid.SetRow(buttonPanel, 2);
                grid.Children.Add(buttonPanel);
                
                diagWindow.Content = grid;
                diagWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"쿠키 진단 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "정말 종료하시겠어요? 😢", 
                "에이전트 비서", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}

