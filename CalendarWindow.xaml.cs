using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace AgentAssistant
{
    public partial class CalendarWindow : Window
    {
        private DateTime currentMonth;
        private Dictionary<DateTime, List<CalendarEvent>> events;
        private string eventsFilePath = "calendar_events.json";
        private static CalendarWindow? currentInstance;
        private double currentOpacity = 1.0;

        public CalendarWindow()
        {
            InitializeComponent();
            currentMonth = DateTime.Now;
            events = LoadEvents();
            UpdateCalendar();
            currentInstance = this;
            
            // 창 상태 변경 이벤트 처리
            this.StateChanged += CalendarWindow_StateChanged;
        }
        
        public static CalendarWindow? GetCurrentInstance()
        {
            return currentInstance;
        }
        
        // 외부에서 캘린더를 새로고침할 수 있도록 public 메서드 추가
        public void RefreshCalendar()
        {
            events = LoadEvents();
            UpdateCalendar();
        }
        
        private void CalendarWindow_StateChanged(object? sender, EventArgs e)
        {
            // 최대화/복원 버튼 아이콘 변경
            if (this.WindowState == WindowState.Maximized)
            {
                MaximizeButton.Content = "❐"; // 복원 아이콘
            }
            else
            {
                MaximizeButton.Content = "☐"; // 최대화 아이콘
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
            catch (Exception ex)
            {
                // DragMove 오류 무시
                System.Diagnostics.Debug.WriteLine($"DragMove 오류: {ex.Message}");
            }
        }

        private void UpdateCalendar()
        {
            CurrentMonthText.Text = currentMonth.ToString("yyyy년 M월");
            CalendarGrid.Items.Clear();

            var firstDayOfMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);
            var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            // 이전 달 빈 칸
            for (int i = 0; i < startDayOfWeek; i++)
            {
                CalendarGrid.Items.Add(CreateEmptyDayCell());
            }

            // 현재 달 날짜
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(currentMonth.Year, currentMonth.Month, day);
                CalendarGrid.Items.Add(CreateDayCell(date));
            }

            // 나머지 빈 칸
            var totalCells = startDayOfWeek + daysInMonth;
            var remainingCells = 42 - totalCells;
            for (int i = 0; i < remainingCells; i++)
            {
                CalendarGrid.Items.Add(CreateEmptyDayCell());
            }
        }

        private Border CreateEmptyDayCell()
        {
            return new Border
            {
                Background = Brushes.Transparent,
                Margin = new Thickness(2)
            };
        }

        private Border CreateDayCell(DateTime date)
        {
            var isToday = date.Date == DateTime.Today;
            var hasEvents = events.ContainsKey(date.Date) && events[date.Date].Count > 0;
            var isSunday = date.DayOfWeek == DayOfWeek.Sunday;
            var isSaturday = date.DayOfWeek == DayOfWeek.Saturday;

            // 투명도 적용
            var backgroundColor = isToday 
                ? new SolidColorBrush(Color.FromArgb(100, 108, 92, 231)) 
                : new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 255, 255, 255));

            var border = new Border
            {
                Background = backgroundColor,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                BorderBrush = isToday ? new SolidColorBrush(Color.FromRgb(108, 92, 231)) : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(isToday ? 2 : 1),
                Padding = new Thickness(5)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 날짜 텍스트 색상에 투명도 적용
            var dayTextColor = isSunday 
                ? Color.FromArgb((byte)(currentOpacity * 255), 255, 0, 0)  // 빨강
                : (isSaturday 
                    ? Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 255)  // 파랑
                    : Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0)); // 검정

            var dayText = new System.Windows.Controls.TextBlock
            {
                Text = date.Day.ToString(),
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(dayTextColor),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 2, 2, 5),
                FontSize = 16
            };
            Grid.SetRow(dayText, 0);
            grid.Children.Add(dayText);

            // 일정 목록 표시
            if (hasEvents)
            {
                var eventsPanel = new StackPanel
                {
                    Margin = new Thickness(2, 0, 2, 2),
                    IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                };
                Grid.SetRow(eventsPanel, 1);
                
                var eventList = events[date.Date];
                var maxEventsToShow = 3; // 최대 3개까지 표시
                
                for (int i = 0; i < Math.Min(eventList.Count, maxEventsToShow); i++)
                {
                    var eventItem = eventList[i];
                    
                    var eventBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 200), 108, 92, 231)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 2, 4, 2),
                        Margin = new Thickness(0, 0, 0, 2),
                        IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                    };
                    
                    var eventStack = new StackPanel
                    {
                        IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                    };
                    
                    var titleText = new System.Windows.Controls.TextBlock
                    {
                        Text = eventItem.Title,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 255, 255, 255)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                    };
                    eventStack.Children.Add(titleText);
                    
                    if (!string.IsNullOrWhiteSpace(eventItem.Time))
                    {
                        var timeText = new System.Windows.Controls.TextBlock
                        {
                            Text = eventItem.Time,
                            FontSize = 8,
                            Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 230, 230, 230)),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                        };
                        eventStack.Children.Add(timeText);
                    }
                    
                    eventBorder.Child = eventStack;
                    eventsPanel.Children.Add(eventBorder);
                }
                
                // 더 많은 일정이 있으면 표시
                if (eventList.Count > maxEventsToShow)
                {
                    var moreText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"+{eventList.Count - maxEventsToShow}개 더",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 108, 92, 231)),
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(2, 2, 0, 0),
                        IsHitTestVisible = false  // 클릭 이벤트가 부모로 전파되도록 설정
                    };
                    eventsPanel.Children.Add(moreText);
                }
                
                grid.Children.Add(eventsPanel);
            }

            border.Child = grid;
            border.MouseLeftButtonDown += (s, e) => DayCell_Click(date);
            border.MouseEnter += (s, e) =>
            {
                if (!isToday)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(50, 108, 92, 231));
                }
            };
            border.MouseLeave += (s, e) =>
            {
                if (!isToday)
                {
                    // 투명도 적용
                    border.Background = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 255, 255, 255));
                }
            };

            return border;
        }

        private void DayCell_Click(DateTime date)
        {
            var eventDialog = new EventDialog(date, events.ContainsKey(date) ? events[date] : new List<CalendarEvent>());
            eventDialog.Owner = this;  // 모달 창이 캘린더 위에 표시되도록 Owner 설정
            eventDialog.Topmost = true;  // Topmost 설정으로 최상위 표시
            if (eventDialog.ShowDialog() == true)
            {
                if (eventDialog.EventList.Count > 0)
                {
                    events[date] = eventDialog.EventList;
                }
                else
                {
                    events.Remove(date);
                }
                SaveEvents();
                UpdateCalendar();
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(-1);
            UpdateCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(1);
            UpdateCalendar();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            currentInstance = null;
            Close();
        }
        
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }
        
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityText != null && MainBorder != null)
            {
                // 투명도 값 저장
                currentOpacity = e.NewValue;
                
                // MainBorder의 배경 투명도 조절 (흰색)
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(currentOpacity * 255), 
                    255, 255, 255));
                
                // HeaderBorder의 배경 투명도 조절 (보라색)
                if (HeaderBorder != null)
                {
                    HeaderBorder.Background = new SolidColorBrush(Color.FromArgb(
                        (byte)(currentOpacity * 255), 
                        108, 92, 231)); // #6C5CE7
                }
                
                // NavigationBorder의 배경 투명도 조절 (회색)
                if (NavigationBorder != null)
                {
                    NavigationBorder.Background = new SolidColorBrush(Color.FromArgb(
                        (byte)(currentOpacity * 255), 
                        245, 245, 245)); // #F5F5F5
                }
                
                // 요일 헤더 텍스트 투명도 조절
                if (SundayText != null) SundayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 231, 76, 60)); // 빨강
                if (MondayText != null) MondayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                if (TuesdayText != null) TuesdayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                if (WednesdayText != null) WednesdayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                if (ThursdayText != null) ThursdayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                if (FridayText != null) FridayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                if (SaturdayText != null) SaturdayText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 52, 152, 219)); // 파랑
                
                // 월/년 텍스트 투명도 조절
                if (CurrentMonthText != null) CurrentMonthText.Foreground = new SolidColorBrush(Color.FromArgb((byte)(currentOpacity * 255), 0, 0, 0));
                
                OpacityText.Text = $"{(int)(currentOpacity * 100)}%";
                
                // 캘린더 다시 그리기 (날짜 셀 투명도 업데이트)
                UpdateCalendar();
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

        private void SaveEvents()
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

    public class CalendarEvent
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Time { get; set; } = "";
    }
}

