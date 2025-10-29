using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Newtonsoft.Json;
using System.Linq;

namespace AgentAssistant
{
    public partial class AdminWindow : Window
    {
        private const string ServerUrl = "http://127.0.0.1:5000";
        private HttpClient httpClient = new HttpClient();
        
        // 페이지네이션 관련
        private List<DepartmentItem> allDepartments = new List<DepartmentItem>();
        private int currentPage = 1;
        private const int itemsPerPage = 10;
        private int totalPages = 1;

        public AdminWindow()
        {
            InitializeComponent();
            LoadDepartments();
        }

        /// <summary>
        /// 부서 목록 로드
        /// </summary>
        private async void LoadDepartments()
        {
            try
            {
                var response = await httpClient.GetAsync($"{ServerUrl}/api/departments");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(json);
                    
                    if (result?["departments"] != null)
                    {
                        // JArray를 List로 변환
                        var departmentsArray = result["departments"] as Newtonsoft.Json.Linq.JArray;
                        if (departmentsArray != null)
                        {
                            allDepartments = new List<DepartmentItem>();
                            foreach (var dept in departmentsArray)
                            {
                                allDepartments.Add(new DepartmentItem
                                {
                                    id = dept["id"]?.ToString() ?? "",
                                    name = dept["name"]?.ToString() ?? "Unknown",
                                    description = dept["description"]?.ToString() ?? "",
                                    HasPassword = !string.IsNullOrEmpty(dept["description"]?.ToString())
                                });
                            }
                            
                            // 페이지네이션 초기화
                            currentPage = 1;
                            totalPages = (int)Math.Ceiling((double)allDepartments.Count / itemsPerPage);
                            UpdatePagination();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("부서 목록을 불러올 수 없습니다.", "오류");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}", "오류");
            }
        }

        /// <summary>
        /// 페이지네이션 업데이트
        /// </summary>
        private void UpdatePagination()
        {
            // 현재 페이지의 부서 목록 가져오기
            var startIndex = (currentPage - 1) * itemsPerPage;
            var currentPageDepartments = allDepartments
                .Skip(startIndex)
                .Take(itemsPerPage)
                .Select((dept, index) => new DepartmentItem
                {
                    RowNumber = startIndex + index + 1,
                    id = dept.id,
                    name = dept.name,
                    description = dept.description,
                    HasPassword = dept.HasPassword
                })
                .ToList();
            
            // DataGrid 업데이트
            DepartmentDataGrid.ItemsSource = currentPageDepartments;
            
            // 페이지 정보 업데이트
            PageInfoText.Text = $"{currentPage} / {Math.Max(1, totalPages)} 페이지";
            TotalCountText.Text = $"총 {allDepartments.Count}개";
            
            // 버튼 활성화/비활성화
            PrevPageButton.IsEnabled = currentPage > 1;
            NextPageButton.IsEnabled = currentPage < totalPages;
        }

        /// <summary>
        /// 이전 페이지 버튼
        /// </summary>
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                UpdatePagination();
            }
        }

        /// <summary>
        /// 다음 페이지 버튼
        /// </summary>
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                UpdatePagination();
            }
        }

        /// <summary>
        /// 부서 추가 버튼 클릭
        /// </summary>
        private async void AddDepartmentButton_Click(object sender, RoutedEventArgs e)
        {
            var departmentName = DepartmentNameTextBox.Text.Trim();
            var departmentPassword = DepartmentPasswordBox.Password;

            // 유효성 검사
            if (string.IsNullOrEmpty(departmentName))
            {
                MessageBox.Show("부서명을 입력해주세요.", "알림");
                DepartmentNameTextBox.Focus();
                return;
            }

            try
            {
                // API 호출 - description에 비밀번호 저장
                var department = new
                {
                    name = departmentName,
                    description = departmentPassword  // 비밀번호를 description 필드에 저장
                };

                var json = JsonConvert.SerializeObject(department);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{ServerUrl}/api/departments", content);

                if (response.IsSuccessStatusCode)
                {
                    var message = string.IsNullOrEmpty(departmentPassword) 
                        ? $"✅ 부서 '{departmentName}'이 추가되었습니다!" 
                        : $"✅ 비밀번호가 설정된 부서 '{departmentName}'이 추가되었습니다! 🔒";
                    MessageBox.Show(message, "성공");
                    
                    // 입력 필드 초기화
                    DepartmentNameTextBox.Clear();
                    DepartmentPasswordBox.Clear();
                    
                    // 부서 목록 새로고침
                    LoadDepartments();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    MessageBox.Show($"⚠️ 이미 존재하는 부서 이름입니다.", "알림");
                }
                else
                {
                    MessageBox.Show($"부서 추가 실패: {response.StatusCode}", "오류");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ 오류: {ex.Message}", "오류");
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDepartments();
            MessageBox.Show("✅ 부서 목록이 새로고침되었습니다.", "성공");
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 부서 삭제 버튼 클릭
        /// </summary>
        private async void DeleteDepartmentButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.Tag == null) return;

            var departmentId = button.Tag.ToString();
            
            // 부서 이름 및 비밀번호 찾기
            var department = allDepartments.FirstOrDefault(d => d.id == departmentId);
            if (department == null) return;
            
            var departmentName = department.name;
            
            // 비밀번호가 설정된 부서인 경우 비밀번호 확인
            if (department.HasPassword && !string.IsNullOrEmpty(department.description))
            {
                var passwordDialog = new PasswordInputDialog(departmentName);
                var passwordResult = passwordDialog.ShowDialog();
                
                if (passwordResult != true || passwordDialog.EnteredPassword != department.description)
                {
                    MessageBox.Show("❌ 비밀번호가 일치하지 않습니다. 부서를 삭제할 수 없습니다.", "인증 실패", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            // 삭제 확인 대화상자
            var result = MessageBox.Show(
                $"정말로 '{departmentName}'을(를) 삭제하시겠습니까?\n\n⚠️ 해당 부서의 모든 일정도 함께 삭제됩니다!",
                "부서 삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var response = await httpClient.DeleteAsync($"{ServerUrl}/api/departments/{departmentId}");

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("✅ 부서가 삭제되었습니다.", "성공");
                    LoadDepartments(); // 부서 목록 새로고침
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"❌ 부서 삭제 실패: {response.StatusCode}\n{errorContent}", "오류");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ 오류: {ex.Message}", "오류");
            }
        }
    }

    /// <summary>
    /// 부서 아이템 클래스
    /// </summary>
    public class DepartmentItem
    {
        public int RowNumber { get; set; }
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public bool HasPassword { get; set; }
    }

    /// <summary>
    /// 비밀번호 아이콘 변환기
    /// </summary>
    public class PasswordIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasPassword)
            {
                return hasPassword ? "🔒" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
