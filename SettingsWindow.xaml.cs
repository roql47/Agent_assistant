using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        
        // 동기화 설정
        public bool EnableSync { get; private set; } = false;
        public string ServerUrl { get; private set; } = "";
        public int SelectedDepartmentId { get; private set; } = 0;
        
        private SyncService syncService;

        public SettingsWindow()
        {
            InitializeComponent();
            syncService = new SyncService();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
            LoadSyncSettings(); // LoadSyncSettings에서 이미 부서 목록을 로드함
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

        private void EnableSyncCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (EnableSyncCheckBox != null)
            {
                bool isEnabled = EnableSyncCheckBox.IsChecked ?? false;
                if (ServerUrlTextBox != null) ServerUrlTextBox.IsEnabled = isEnabled;
                if (RefreshDepartmentsButton != null) RefreshDepartmentsButton.IsEnabled = isEnabled;
                
                // 부서 선택은 항상 활성화 (로컬 모드에서도 사용)
                if (DepartmentComboBox != null) DepartmentComboBox.IsEnabled = true;
                
                // 동기화가 비활성화되어도 서버 URL이 있으면 부서 목록 로드
                if (!isEnabled && ServerUrlTextBox != null && !string.IsNullOrWhiteSpace(ServerUrlTextBox.Text))
                {
                    _ = LoadDepartmentsFromServerAsync();
                }
            }
        }
        
        /// <summary>
        /// 서버 URL 텍스트 변경 이벤트 - 자동으로 부서 목록 로드
        /// </summary>
        private async void ServerUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 텍스트 변경이 완료된 후에 실행되도록 지연
            await Task.Delay(500); // 0.5초 지연
            
            if (ServerUrlTextBox != null && !string.IsNullOrWhiteSpace(ServerUrlTextBox.Text))
            {
                // URL이 유효한 형태인지 간단히 확인
                var url = ServerUrlTextBox.Text.Trim();
                if (url.StartsWith("http://") || url.StartsWith("https://"))
                {
                    System.Diagnostics.Debug.WriteLine($"서버 URL 변경 감지: {url}");
                    await LoadDepartmentsFromServerAsync();
                }
            }
        }

        private async void RefreshDepartments_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerUrlTextBox?.Text))
            {
                MessageBox.Show("서버 URL을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                RefreshDepartmentsButton.IsEnabled = false;
                RefreshDepartmentsButton.Content = "⏳";
                
                // 임시 연결로 부서 목록 가져오기
                var tempSync = new SyncService();
                await tempSync.ConnectAsync(ServerUrlTextBox.Text.Trim(), 0);
                var departments = await tempSync.GetDepartmentsAsync();
                await tempSync.DisconnectAsync();
                
                if (departments.Count > 0)
                {
                    DepartmentComboBox.ItemsSource = departments;
                    
                    // 이전에 선택한 부서가 있으면 복원
                    if (SelectedDepartmentId > 0)
                    {
                        DepartmentComboBox.SelectedValue = SelectedDepartmentId;
                    }
                    else if (DepartmentComboBox.Items.Count > 0)
                    {
                        DepartmentComboBox.SelectedIndex = 0;
                    }
                    
                    SyncStatusText.Text = $"{departments.Count}개 부서 로드됨";
                }
                else
                {
                    MessageBox.Show("부서 목록을 가져올 수 없습니다.\n서버 URL을 확인해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SyncStatusText.Text = "부서 로드 실패";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"부서 목록을 가져오는 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                SyncStatusText.Text = "연결 실패";
            }
            finally
            {
                RefreshDepartmentsButton.IsEnabled = true;
                RefreshDepartmentsButton.Content = "🔄";
            }
        }
        
        /// <summary>
        /// 서버에서 부서 목록 로드
        /// </summary>
        private async Task LoadDepartmentsFromServerAsync()
        {
            try
            {
                // 현재 입력된 서버 URL 사용 (텍스트박스에서 직접 가져오기)
                string serverUrl = "";
                if (ServerUrlTextBox != null)
                {
                    serverUrl = ServerUrlTextBox.Text?.Trim() ?? "";
                }
                
                // 텍스트박스에 URL이 없으면 저장된 설정에서 가져오기
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    var syncSettings = SyncSettings.Load();
                    serverUrl = syncSettings.ServerUrl;
                }
                
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    System.Diagnostics.Debug.WriteLine("서버 URL이 설정되지 않았습니다. 기본 부서 목록을 사용합니다.");
                    LoadDefaultDepartments();
                    return;
                }
                
                // 임시 연결로 부서 목록 가져오기
                var tempSync = new SyncService();
                await tempSync.ConnectAsync(serverUrl, 0);
                var departments = await tempSync.GetDepartmentsAsync();
                await tempSync.DisconnectAsync();
                
                if (departments != null && departments.Count > 0)
                {
                    if (DepartmentComboBox != null)
                    {
                        DepartmentComboBox.ItemsSource = departments;
                        
                        // 저장된 부서 ID가 있으면 해당 부서 선택
                        if (SelectedDepartmentId > 0)
                        {
                            DepartmentComboBox.SelectedValue = SelectedDepartmentId;
                            // SelectedValue가 작동하지 않으면 SelectedItem으로 시도
                            if (DepartmentComboBox.SelectedValue == null)
                            {
                                var dept = departments.FirstOrDefault(d => d.Id == SelectedDepartmentId);
                                if (dept != null)
                                {
                                    DepartmentComboBox.SelectedItem = dept;
                                }
                                else
                                {
                                    // 저장된 부서가 목록에 없으면 첫 번째 부서 선택
                                    DepartmentComboBox.SelectedIndex = 0;
                                }
                            }
                        }
                        else if (DepartmentComboBox.Items.Count > 0)
                        {
                            DepartmentComboBox.SelectedIndex = 0;
                        }
                        
                        if (SyncStatusText != null)
                        {
                            var selectedDept = departments.FirstOrDefault(d => d.Id == SelectedDepartmentId);
                            if (selectedDept != null)
                            {
                                SyncStatusText.Text = $"{departments.Count}개 부서 로드됨 - '{selectedDept.Name}' 선택됨";
                            }
                            else
                            {
                                SyncStatusText.Text = $"{departments.Count}개 부서 로드됨";
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("서버에서 부서 목록을 가져올 수 없습니다. 기본 부서 목록을 사용합니다.");
                    LoadDefaultDepartments();
                }
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
                // 기본 부서 목록 생성 (서버 연결 실패 시에만 사용)
                var defaultDepartments = new List<Department>
                {
                    new Department { Id = 1, Name = "기본 부서", Description = "기본 부서" }
                };
                
                if (DepartmentComboBox != null)
                {
                    DepartmentComboBox.ItemsSource = defaultDepartments;
                    DepartmentComboBox.SelectedIndex = 0;
                    
                    if (SyncStatusText != null)
                    {
                        SyncStatusText.Text = "기본 부서 사용 중";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"기본 부서 목록 로드 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 저장된 동기화 설정 로드
        /// </summary>
        private async void LoadSyncSettings()
        {
            var settings = SyncSettings.Load();
            
            if (EnableSyncCheckBox != null)
            {
                EnableSyncCheckBox.IsChecked = settings.EnableSync;
            }
            
            if (ServerUrlTextBox != null)
            {
                ServerUrlTextBox.Text = settings.ServerUrl;
            }
            
            // 저장된 부서 ID 설정
            SelectedDepartmentId = settings.SelectedDepartmentId;
            
            // 서버 URL이 있으면 동기화 활성화 여부와 관계없이 부서 목록 로드
            if (!string.IsNullOrWhiteSpace(settings.ServerUrl))
            {
                await LoadDepartmentsFromServerAsync();
            }
        }
        
        /// <summary>
        /// 부서 목록 자동 새로고침 (설정 로드 시)
        /// </summary>
        private async System.Threading.Tasks.Task RefreshDepartmentsAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerUrlTextBox?.Text))
                return;

            try
            {
                var tempSync = new SyncService();
                await tempSync.ConnectAsync(ServerUrlTextBox.Text.Trim(), 0);
                var departments = await tempSync.GetDepartmentsAsync();
                await tempSync.DisconnectAsync();
                
                if (departments.Count > 0 && DepartmentComboBox != null)
                {
                    DepartmentComboBox.ItemsSource = departments;
                    
                    // 약간의 지연 후 선택 (UI 렌더링 대기)
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    // 이전에 선택한 부서 복원
                    if (SelectedDepartmentId > 0)
                    {
                        DepartmentComboBox.SelectedValue = SelectedDepartmentId;
                        
                        // 선택이 제대로 되었는지 확인
                        if (DepartmentComboBox.SelectedValue == null || (int)DepartmentComboBox.SelectedValue != SelectedDepartmentId)
                        {
                            // SelectedValue가 작동하지 않으면 인덱스로 찾기
                            var dept = departments.FirstOrDefault(d => d.Id == SelectedDepartmentId);
                            if (dept != null)
                            {
                                DepartmentComboBox.SelectedItem = dept;
                            }
                        }
                        
                        if (SyncStatusText != null)
                        {
                            var selectedDept = departments.FirstOrDefault(d => d.Id == SelectedDepartmentId);
                            if (selectedDept != null)
                            {
                                SyncStatusText.Text = $"{departments.Count}개 부서 로드됨 - '{selectedDept.Name}' 선택됨";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"자동 부서 로드 오류: {ex.Message}");
                if (SyncStatusText != null)
                {
                    SyncStatusText.Text = "부서 로드 실패";
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 설정 적용
            StartWithWindows = StartupCheckBox.IsChecked ?? false;
            AlwaysOnTop = TopmostCheckBox.IsChecked ?? true;
            ShowInTaskbarEnabled = ShowInTaskbarCheckBox.IsChecked ?? true;
            
            // 동기화 설정
            EnableSync = EnableSyncCheckBox.IsChecked ?? false;
            ServerUrl = ServerUrlTextBox.Text?.Trim() ?? "";
            
            // 부서 선택 정보 저장 (동기화 활성화 여부와 관계없이)
            if (DepartmentComboBox.SelectedValue != null)
            {
                SelectedDepartmentId = (int)DepartmentComboBox.SelectedValue;
            }
            else if (DepartmentComboBox.SelectedItem is Department selectedDept)
            {
                SelectedDepartmentId = selectedDept.Id;
            }
            
            // 동기화 설정 저장 (부서 정보는 항상 저장)
            var syncSettings = new SyncSettings
            {
                EnableSync = EnableSync,
                ServerUrl = ServerUrl,
                SelectedDepartmentId = SelectedDepartmentId,
                SelectedDepartmentName = DepartmentComboBox.SelectedItem is Department dept ? dept.Name : ""
            };
            SyncSettings.Save(syncSettings);
            
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


