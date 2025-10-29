using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
        
        // 부서별 일정 관리
        private Dictionary<string, Dictionary<DateTime, List<CalendarEvent>>> departmentEvents = 
            new Dictionary<string, Dictionary<DateTime, List<CalendarEvent>>>();
        private string currentDepartmentId = "";  // 현재 선택된 부서 UUID
        
        // 동기화 관련
        private SyncService? syncService;
        private bool syncEnabled = false;
        private string serverUrl = "";
        private int departmentId = 0;
        private List<Department>? availableDepartments;
        private HashSet<int> favoriteDepartments = new HashSet<int>();
        private string favoritesFilePath = "favorite_departments.json";

        public CalendarWindow(int? presetDepartmentId = null)
        {
            InitializeComponent();
            currentMonth = DateTime.Now;
            events = LoadEvents();
            LoadFavorites();
            
            // 기본 부서 ID를 "내PC" (로컬 전용)으로 설정
            currentDepartmentId = "local-pc";
            
            // 설정에서 전달받은 부서 ID가 있으면 사용
            if (presetDepartmentId.HasValue)
            {
                departmentId = presetDepartmentId.Value;
            }
            
            // "내PC" 부서에 대한 기본 Dictionary 초기화
            if (!departmentEvents.ContainsKey(currentDepartmentId))
            {
                departmentEvents[currentDepartmentId] = new Dictionary<DateTime, List<CalendarEvent>>();
            }
            
            // 서버에서 부서 목록 로드 (내PC 포함)
            _ = LoadDepartmentsFromServerAsync();
            UpdateCalendar();
            currentInstance = this;
            
            // 창 상태 변경 이벤트 처리
            this.StateChanged += CalendarWindow_StateChanged;
            
            // 창이 닫힐 때 동기화 연결 해제
            this.Closing += async (s, e) =>
            {
                if (syncService != null)
                {
                    await syncService.DisconnectAsync();
                }
            };
        }
        
        /// <summary>
        /// 동기화 활성화 (설정에서 호출)
        /// </summary>
        public async Task EnableSyncAsync(string url, int deptId)
        {
            try
            {
                serverUrl = url;
                departmentId = deptId;
                syncEnabled = true;
                
                if (syncService == null)
                {
                    syncService = new SyncService();
                    
                    // 이벤트 핸들러 등록
                    syncService.Connected += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateSyncStatus("🟢 연결됨");
                        });
                    };
                    
                    syncService.Disconnected += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateSyncStatus("🔴 끊김");
                        });
                    };
                    
                    syncService.EventCreated += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            HandleServerEventCreated(e.ServerEvent);
                        });
                    };
                    
                    syncService.EventUpdated += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            HandleServerEventUpdated(e.ServerEvent);
                        });
                    };
                    
                    syncService.EventDeleted += (s, eventId) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            HandleServerEventDeleted(eventId);
                        });
                    };
                    
                    syncService.SyncReceived += (s, serverEvents) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            HandleServerSync(serverEvents);
                        });
                    };
                    
                    syncService.ConnectionStatusChanged += (s, status) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"동기화 상태: {status}");
                        });
                    };
                }
                
                UpdateSyncStatus("🟡 연결 중...");
                var connected = await syncService.ConnectAsync(serverUrl, departmentId);
                
                if (connected)
                {
                    // 부서 목록 로드
                    await LoadDepartmentsAsync();
                    
                    // 서버에서 이벤트 동기화
                    await syncService.RequestSyncAsync();
                }
                else
                {
                    UpdateSyncStatus("🔴 연결 실패");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"동기화 활성화 오류: {ex.Message}");
                UpdateSyncStatus("🔴 오류");
            }
        }
        
        /// <summary>
        /// 동기화 비활성화
        /// </summary>
        public async Task DisableSyncAsync()
        {
            syncEnabled = false;
            if (syncService != null)
            {
                await syncService.DisconnectAsync();
            }
            UpdateSyncStatus("");
            
            // 부서 선택 UI 숨기기
            if (DepartmentSelectorBorder != null)
            {
                DepartmentSelectorBorder.Visibility = Visibility.Collapsed;
            }
        }
        
        private void UpdateSyncStatus(string status)
        {
            if (SyncStatusIndicator != null)
            {
                SyncStatusIndicator.Text = status;
            }
        }
        
        /// <summary>
        /// 서버에서 부서 목록 로드
        /// </summary>
        private async Task LoadDepartmentsFromServerAsync()
        {
            try
            {
                var departments = new List<DynamicDepartment>();
                
                // 기본 "내PC" 부서 항상 추가
                departments.Add(new DynamicDepartment
                {
                    Id = "local-pc",
                    Name = "내PC",
                    Description = ""
                });
                
                // 저장된 동기화 설정에서 서버 URL 가져오기
                var syncSettings = SyncSettings.Load();
                
                if (!string.IsNullOrWhiteSpace(syncSettings.ServerUrl))
                {
                    // HTTP API에서 직접 부서 목록 가져오기
                    try
                    {
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            var serverUrl = syncSettings.ServerUrl.TrimEnd('/');
                            var response = await client.GetAsync($"{serverUrl}/api/departments");
                            
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();
                                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                                
                                if (result?["departments"] != null)
                                {
                                    var departmentsArray = result["departments"] as Newtonsoft.Json.Linq.JArray;
                                    if (departmentsArray != null)
                                    {
                                        foreach (var item in departmentsArray)
                                        {
                                            departments.Add(new DynamicDepartment
                                            {
                                                Id = item["id"]?.ToString() ?? "",
                                                Name = item["name"]?.ToString() ?? "Unknown",
                                                Description = item["description"]?.ToString() ?? ""
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"HTTP API에서 부서 목록 로드 오류: {ex.Message}");
                    }
                }
                
                Dispatcher.Invoke(() =>
                {
                    if (DepartmentSelectorComboBox != null)
                    {
                        DepartmentSelectorComboBox.ItemsSource = departments;
                        DepartmentSelectorComboBox.DisplayMemberPath = "Name";
                        DepartmentSelectorComboBox.SelectedValuePath = "Id";
                        
                        // '내PC'를 기본 선택
                        DepartmentSelectorComboBox.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"서버에서 부서 목록 로드 오류: {ex.Message}. 기본 부서 목록을 사용합니다.");
                LoadDefaultDepartments();
            }
        }
        
        /// <summary>
        /// 기본 부서 목록 로드 (서버 연결 실패 시)
        /// </summary>
        private void LoadDefaultDepartments()
        {
            try
            {
                // "내PC" 부서 항상 포함
                var departments = new List<DynamicDepartment>
                {
                    new DynamicDepartment { Id = "local-pc", Name = "내PC", Description = "" }
                };
                
                Dispatcher.Invoke(() =>
                {
                    if (DepartmentSelectorComboBox != null)
                    {
                        DepartmentSelectorComboBox.ItemsSource = departments;
                        DepartmentSelectorComboBox.DisplayMemberPath = "Name";
                        DepartmentSelectorComboBox.SelectedValuePath = "Id";
                        DepartmentSelectorComboBox.SelectedIndex = 0;
                        
                        // 즐겨찾기 버튼 업데이트
                        UpdateFavoriteButton();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"기본 부서 목록 로드 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 부서 목록 로드 및 드롭다운 채우기
        /// </summary>
        private async Task LoadDepartmentsAsync()
        {
            if (syncService == null) return;
            
            try
            {
                availableDepartments = await syncService.GetDepartmentsAsync();
                
                Dispatcher.Invoke(() =>
                {
                    if (DepartmentSelectorComboBox != null && availableDepartments != null)
                    {
                        // 즐겨찾기 부서를 상단에 배치
                        var sortedDepartments = availableDepartments
                            .OrderByDescending(d => favoriteDepartments.Contains(d.Id))
                            .ThenBy(d => d.Name)
                            .ToList();
                        
                        DepartmentSelectorComboBox.ItemsSource = sortedDepartments;
                        DepartmentSelectorComboBox.SelectedValue = departmentId;
                        
                        // 부서 선택 UI 표시
                        if (DepartmentSelectorBorder != null)
                        {
                            DepartmentSelectorBorder.Visibility = Visibility.Visible;
                        }
                        
                        // 즐겨찾기 버튼 업데이트
                        UpdateFavoriteButton();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 목록 로드 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 부서 선택 변경 이벤트
        /// </summary>
        private async void DepartmentSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentSelectorComboBox.SelectedValue == null)
                return;
            
            // string 기반 부서 ID 처리
            string newDepartmentIdStr = DepartmentSelectorComboBox.SelectedValue?.ToString() ?? "";
            
            if (string.IsNullOrEmpty(newDepartmentIdStr))
                return;
            
            // 같은 부서면 무시
            if (newDepartmentIdStr == currentDepartmentId)
                return;
            
            try
            {
                // 선택된 부서 정보 가져오기
                var selectedDept = DepartmentSelectorComboBox.SelectedItem as DynamicDepartment;
                if (selectedDept == null)
                    return;
                
                // "내PC"가 아니고 비밀번호가 설정되어 있는 경우 확인
                if (newDepartmentIdStr != "local-pc" && !string.IsNullOrEmpty(selectedDept.Description))
                {
                    var passwordDialog = new PasswordInputDialog(selectedDept.Name);
                    var result = passwordDialog.ShowDialog();
                    
                    if (result != true || passwordDialog.EnteredPassword != selectedDept.Description)
                    {
                        System.Windows.MessageBox.Show("❌ 비밀번호가 일치하지 않습니다.", "인증 실패", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        
                        // 이전 부서로 되돌리기
                        if (!string.IsNullOrEmpty(currentDepartmentId))
                        {
                            var currentDept = ((System.Collections.IEnumerable)DepartmentSelectorComboBox.ItemsSource)
                                .Cast<DynamicDepartment>()
                                .FirstOrDefault(d => d.Id == currentDepartmentId);
                            if (currentDept != null)
                            {
                                DepartmentSelectorComboBox.SelectedItem = currentDept;
                            }
                        }
                        return;
                    }
                }
                
                // 현재 부서 ID 업데이트
                currentDepartmentId = newDepartmentIdStr;
                System.Diagnostics.Debug.WriteLine($"부서 선택 변경: {selectedDept.Name} (ID: {currentDepartmentId})");
                
                // 해당 부서의 일정이 없으면 빈 Dictionary 생성
                if (!departmentEvents.ContainsKey(currentDepartmentId))
                {
                    departmentEvents[currentDepartmentId] = new Dictionary<DateTime, List<CalendarEvent>>();
                }
                
                // 현재 부서의 일정을 events에 설정
                events = departmentEvents[currentDepartmentId];
                
                // "내PC"인 경우 무조건 로컬 모드
                if (currentDepartmentId == "local-pc")
                {
                    UpdateSyncStatus("💻 내PC (로컬 전용)");
                }
                else if (syncEnabled && syncService != null)
                {
                    // 동기화가 활성화된 경우 - 새 부서의 일정 동기화
                    UpdateSyncStatus("🟡 부서 변경 중...");
                    
                    // 기존 부서 그룹에서 나가기
                    await syncService.DisconnectAsync();
                    
                    // TODO: String 기반 부서 ID로 재연결하려면 SyncService 수정 필요
                    // 현재는 로컬 모드로 전환
                    UpdateSyncStatus("📅 로컬 모드 (부서별 일정)");
                }
                else
                {
                    // 동기화가 비활성화된 경우 - 로컬 이벤트만 필터링
                    UpdateSyncStatus("📅 로컬 모드");
                }
                
                // 즐겨찾기 버튼 업데이트
                UpdateFavoriteButton();
                
                // 캘린더 새로고침 (선택된 부서의 일정 표시)
                UpdateCalendar();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 선택 변경 오류: {ex.Message}");
                UpdateSyncStatus("🔴 오류 발생");
            }
        }
        
        /// <summary>
        /// 즐겨찾기 버튼 클릭
        /// </summary>
        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (departmentId == 0) return;
            
            if (favoriteDepartments.Contains(departmentId))
            {
                // 즐겨찾기 제거
                favoriteDepartments.Remove(departmentId);
            }
            else
            {
                // 즐겨찾기 추가
                favoriteDepartments.Add(departmentId);
            }
            
            SaveFavorites();
            UpdateFavoriteButton();
            
            // 드롭다운 목록 다시 정렬
            if (availableDepartments != null && DepartmentSelectorComboBox != null)
            {
                var sortedDepartments = availableDepartments
                    .OrderByDescending(d => favoriteDepartments.Contains(d.Id))
                    .ThenBy(d => d.Name)
                    .ToList();
                
                var currentSelected = DepartmentSelectorComboBox.SelectedValue;
                DepartmentSelectorComboBox.ItemsSource = sortedDepartments;
                DepartmentSelectorComboBox.SelectedValue = currentSelected;
            }
        }
        
        /// <summary>
        /// 즐겨찾기 버튼 UI 업데이트
        /// </summary>
        private void UpdateFavoriteButton()
        {
            if (FavoriteButton == null || FavoriteIcon == null) return;
            
            if (favoriteDepartments.Contains(departmentId))
            {
                FavoriteIcon.Text = "★"; // 채워진 별
                FavoriteIcon.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // 금색
                FavoriteButton.ToolTip = "즐겨찾기 제거";
            }
            else
            {
                FavoriteIcon.Text = "☆"; // 빈 별
                FavoriteIcon.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // 회색
                FavoriteButton.ToolTip = "즐겨찾기 추가";
            }
        }
        
        /// <summary>
        /// 검색 토글 버튼 클릭
        /// </summary>
        private void SearchToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (DepartmentSearchBox == null || DepartmentSelectorComboBox == null) return;
            
            if (DepartmentSearchBox.Visibility == Visibility.Collapsed)
            {
                // 검색창 표시
                DepartmentSearchBox.Visibility = Visibility.Visible;
                DepartmentSelectorComboBox.Visibility = Visibility.Collapsed;
                DepartmentSearchBox.Focus();
            }
            else
            {
                // 검색창 숨김
                DepartmentSearchBox.Visibility = Visibility.Collapsed;
                DepartmentSelectorComboBox.Visibility = Visibility.Visible;
                DepartmentSearchBox.Clear();
            }
        }
        
        /// <summary>
        /// 부서 검색 텍스트 변경
        /// </summary>
        private void DepartmentSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (DepartmentSearchBox == null || DepartmentSelectorComboBox == null) return;
            
            string searchText = DepartmentSearchBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // 검색어가 없으면 전체 목록 표시
                DepartmentSelectorComboBox.ItemsSource = availableDepartments;
                return;
            }
            
            // 검색어로 필터링
            var filteredDepartments = availableDepartments?
                .Where(d => d.Name.ToLower().Contains(searchText))
                .ToList();
            
            DepartmentSelectorComboBox.ItemsSource = filteredDepartments;
            
            // 검색 결과가 1개면 자동 선택
            if (filteredDepartments != null && filteredDepartments.Count == 1)
            {
                DepartmentSelectorComboBox.SelectedItem = filteredDepartments[0];
            }
        }
        
        /// <summary>
        /// 즐겨찾기 목록 로드
        /// </summary>
        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(favoritesFilePath))
                {
                    var json = File.ReadAllText(favoritesFilePath);
                    var favorites = JsonSerializer.Deserialize<List<int>>(json);
                    if (favorites != null)
                    {
                        favoriteDepartments = new HashSet<int>(favorites);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"즐겨찾기 로드 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 즐겨찾기 목록 저장
        /// </summary>
        private void SaveFavorites()
        {
            try
            {
                var json = JsonSerializer.Serialize(favoriteDepartments.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(favoritesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"즐겨찾기 저장 오류: {ex.Message}");
            }
        }
        
        // 서버로부터 이벤트 생성 알림 처리
        private void HandleServerEventCreated(ServerEvent serverEvent)
        {
            var date = DateTime.Parse(serverEvent.Event_date);
            var calendarEvent = new CalendarEvent
            {
                Id = serverEvent.Id,
                DepartmentId = serverEvent.Department_id,
                Title = serverEvent.Title,
                Description = serverEvent.Description,
                Time = serverEvent.Time,
                Url = serverEvent.Url,
                LastModified = DateTime.Parse(serverEvent.Last_modified)
            };
            
            if (!departmentEvents.ContainsKey(currentDepartmentId))
            {
                departmentEvents[currentDepartmentId] = new Dictionary<DateTime, List<CalendarEvent>>();
            }
            
            if (!departmentEvents[currentDepartmentId].ContainsKey(date))
            {
                departmentEvents[currentDepartmentId][date] = new List<CalendarEvent>();
            }
            
            // 중복 확인
            if (!departmentEvents[currentDepartmentId][date].Any(e => e.Id == calendarEvent.Id))
            {
                departmentEvents[currentDepartmentId][date].Add(calendarEvent);
                SaveEvents(); // 로컬 저장
                UpdateCalendar();
            }
        }
        
        // 서버로부터 이벤트 수정 알림 처리
        private void HandleServerEventUpdated(ServerEvent serverEvent)
        {
            var date = DateTime.Parse(serverEvent.Event_date);
            
            // 모든 부서와 날짜에서 해당 이벤트 찾기
            foreach (var deptKvp in departmentEvents.ToList())
            {
                foreach (var dateKvp in deptKvp.Value.ToList())
                {
                    var evt = dateKvp.Value.FirstOrDefault(e => e.Id == serverEvent.Id);
                    if (evt != null)
                    {
                        // 이전 위치에서 제거
                        dateKvp.Value.Remove(evt);
                        if (dateKvp.Value.Count == 0)
                        {
                            deptKvp.Value.Remove(dateKvp.Key);
                        }
                        
                        // 새 날짜에 추가
                        var updatedEvent = new CalendarEvent
                        {
                            Id = serverEvent.Id,
                            DepartmentId = serverEvent.Department_id,
                            Title = serverEvent.Title,
                            Description = serverEvent.Description,
                            Time = serverEvent.Time,
                            Url = serverEvent.Url,
                            LastModified = DateTime.Parse(serverEvent.Last_modified)
                        };
                        
                        if (!deptKvp.Value.ContainsKey(date))
                        {
                            deptKvp.Value[date] = new List<CalendarEvent>();
                        }
                        deptKvp.Value[date].Add(updatedEvent);
                        
                        SaveEvents();
                        UpdateCalendar();
                        return;
                    }
                }
            }
        }
        
        // 서버로부터 이벤트 삭제 알림 처리
        private void HandleServerEventDeleted(int eventId)
        {
            foreach (var deptKvp in departmentEvents.ToList())
            {
                foreach (var dateKvp in deptKvp.Value.ToList())
                {
                    var evt = dateKvp.Value.FirstOrDefault(e => e.Id == eventId);
                    if (evt != null)
                    {
                        dateKvp.Value.Remove(evt);
                        if (dateKvp.Value.Count == 0)
                        {
                            deptKvp.Value.Remove(dateKvp.Key);
                        }
                        if (deptKvp.Value.Count == 0)
                        {
                            departmentEvents.Remove(deptKvp.Key);
                        }
                        SaveEvents();
                        UpdateCalendar();
                        return;
                    }
                }
            }
        }
        
        // 서버로부터 전체 동기화 처리
        private void HandleServerSync(List<ServerEvent> serverEvents)
        {
            // 서버 우선으로 동기화
            foreach (var kvp in departmentEvents.ToList())
            {
                kvp.Value.Clear();
            }
            
            foreach (var serverEvent in serverEvents)
            {
                var date = DateTime.Parse(serverEvent.Event_date);
                var calendarEvent = new CalendarEvent
                {
                    Id = serverEvent.Id,
                    DepartmentId = serverEvent.Department_id,
                    Title = serverEvent.Title,
                    Description = serverEvent.Description,
                    Time = serverEvent.Time,
                    Url = serverEvent.Url,
                    LastModified = DateTime.Parse(serverEvent.Last_modified)
                };
                
                if (!departmentEvents.ContainsKey(currentDepartmentId))
                {
                    departmentEvents[currentDepartmentId] = new Dictionary<DateTime, List<CalendarEvent>>();
                }
                
                if (!departmentEvents[currentDepartmentId].ContainsKey(date))
                {
                    departmentEvents[currentDepartmentId][date] = new List<CalendarEvent>();
                }
                departmentEvents[currentDepartmentId][date].Add(calendarEvent);
            }
            
            SaveEvents(); // 로컬 저장
            UpdateCalendar();
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
            
            // 현재 선택된 부서의 이벤트만 필터링
            List<CalendarEvent> dayEvents = new List<CalendarEvent>();
            if (departmentEvents.ContainsKey(currentDepartmentId) && 
                departmentEvents[currentDepartmentId].ContainsKey(date.Date))
            {
                dayEvents = departmentEvents[currentDepartmentId][date.Date];
            }
            var filteredEvents = dayEvents.Where(e => e.DepartmentId == 0 || e.DepartmentId == departmentId).ToList();
            var hasEvents = filteredEvents.Count > 0;
            
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
                
                var eventList = filteredEvents; // 필터링된 이벤트 사용
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

        private async void DayCell_Click(DateTime date)
        {
            // 현재 선택된 부서의 이벤트만 필터링
            List<CalendarEvent> dayEvents = new List<CalendarEvent>();
            if (departmentEvents.ContainsKey(currentDepartmentId) && 
                departmentEvents[currentDepartmentId].ContainsKey(date.Date))
            {
                dayEvents = departmentEvents[currentDepartmentId][date.Date];
            }
            var filteredEvents = dayEvents.Where(e => e.DepartmentId == 0 || e.DepartmentId == departmentId).ToList();
            
            var eventDialog = new EventDialog(date, filteredEvents);
            eventDialog.Owner = this;  // 모달 창이 캘린더 위에 표시되도록 Owner 설정
            eventDialog.Topmost = true;  // Topmost 설정으로 최상위 표시
            
            var oldEventList = new List<CalendarEvent>(filteredEvents);
            
            if (eventDialog.ShowDialog() == true)
            {
                if (eventDialog.EventList.Count > 0)
                {
                    // 새로 생성된 이벤트에 현재 부서 ID 설정
                    foreach (var evt in eventDialog.EventList)
                    {
                        if (evt.DepartmentId == 0) // 새로 생성된 이벤트
                        {
                            evt.DepartmentId = departmentId;
                        }
                    }
                    departmentEvents[currentDepartmentId][date] = eventDialog.EventList;
                }
                else
                {
                    departmentEvents[currentDepartmentId].Remove(date);
                    if (departmentEvents[currentDepartmentId].Count == 0)
                    {
                        departmentEvents.Remove(currentDepartmentId);
                    }
                }
                SaveEvents(); // 로컬 저장
                UpdateCalendar();
                
                // 동기화 활성화 시 서버로 전송
                if (syncEnabled && syncService != null && departmentId > 0)
                {
                    await SyncEventsToServer(date, oldEventList, eventDialog.EventList);
                }
            }
        }
        
        /// <summary>
        /// 변경된 이벤트를 서버로 동기화
        /// </summary>
        private async Task SyncEventsToServer(DateTime date, List<CalendarEvent> oldEvents, List<CalendarEvent> newEvents)
        {
            try
            {
                // 삭제된 이벤트 찾기
                var deletedEvents = oldEvents.Where(o => !newEvents.Any(n => n.Id == o.Id && o.Id > 0)).ToList();
                foreach (var evt in deletedEvents)
                {
                    if (evt.Id > 0)
                    {
                        await syncService!.DeleteEventAsync(evt.Id);
                    }
                }
                
                // 추가되거나 수정된 이벤트 찾기
                foreach (var newEvt in newEvents)
                {
                    if (newEvt.Id == 0)
                    {
                        // 새 이벤트 생성
                        var serverEvent = await syncService!.CreateEventAsync(
                            departmentId,
                            date.ToString("yyyy-MM-dd"),
                            newEvt.Title,
                            newEvt.Description,
                            newEvt.Time,
                            newEvt.Url
                        );
                        
                        if (serverEvent != null)
                        {
                            // 로컬 이벤트에 서버 ID 할당
                            newEvt.Id = serverEvent.Id;
                            newEvt.DepartmentId = serverEvent.Department_id;
                            newEvt.LastModified = DateTime.Parse(serverEvent.Last_modified);
                        }
                    }
                    else
                    {
                        // 이벤트 수정 확인
                        var oldEvt = oldEvents.FirstOrDefault(o => o.Id == newEvt.Id);
                        if (oldEvt != null && HasEventChanged(oldEvt, newEvt))
                        {
                            await syncService!.UpdateEventAsync(
                                newEvt.Id,
                                newEvt.Title,
                                newEvt.Description,
                                newEvt.Time,
                                newEvt.Url,
                                date.ToString("yyyy-MM-dd")
                            );
                        }
                    }
                }
                
                // 변경사항 저장
                SaveEvents();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"서버 동기화 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 이벤트가 변경되었는지 확인
        /// </summary>
        private bool HasEventChanged(CalendarEvent old, CalendarEvent newEvt)
        {
            return old.Title != newEvt.Title ||
                   old.Description != newEvt.Description ||
                   old.Time != newEvt.Time ||
                   old.Url != newEvt.Url;
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
                System.Windows.MessageBox.Show($"일정 불러오기 오류: {ex.Message}", "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            
            return new Dictionary<DateTime, List<CalendarEvent>>();
        }

        private void SaveEvents()
        {
            try
            {
                var savedEvents = new Dictionary<string, List<CalendarEvent>>();
                foreach (var kvp in departmentEvents)
                {
                    foreach (var dateKvp in kvp.Value)
                    {
                        savedEvents[dateKvp.Key.ToString("yyyy-MM-dd")] = dateKvp.Value;
                    }
                }
                
                var json = JsonSerializer.Serialize(savedEvents, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(eventsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"일정 저장 오류: {ex.Message}", "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    /// <summary>
    /// UUID 기반 부서 정보 (Flask API에서 받은 부서)
    /// </summary>
    public class DynamicDepartment
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class CalendarEvent
    {
        public int Id { get; set; } = 0;  // 서버 동기화용 ID (0이면 로컬 전용)
        public int DepartmentId { get; set; } = 0;  // 부서 ID
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Time { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime LastModified { get; set; } = DateTime.Now;  // 충돌 해결용
    }
}

