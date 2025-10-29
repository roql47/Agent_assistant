using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace AgentAssistant
{
    public partial class CookieInputDialog : Window
    {
        public Dictionary<string, string> Cookies { get; private set; } = new Dictionary<string, string>();

        public CookieInputDialog()
        {
            InitializeComponent();
            LoadSavedCookies();
        }

        private void LoadSavedCookies()
        {
            try
            {
                // 암호화된 쿠키 파일 읽기
                string json = CookieEncryption.LoadEncryptedCookies("manual_cookies.dat");
                
                if (!string.IsNullOrEmpty(json))
                {
                    var savedCookies = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (savedCookies != null && savedCookies.Count > 0)
                    {
                        // 쿠키를 문자열로 변환하여 표시
                        var cookieString = string.Join("; ", savedCookies.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                        CookieStringInput.Text = cookieString;
                    }
                }
            }
            catch { }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Cookies.Clear();

            if (string.IsNullOrWhiteSpace(CookieStringInput.Text))
            {
                MessageBox.Show("쿠키 문자열을 입력해주세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 쿠키 문자열 파싱
            var cookieString = CookieStringInput.Text.Trim();
            
            // "cookie:" 헤더가 포함된 경우 제거
            if (cookieString.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
            {
                cookieString = cookieString.Substring(7).Trim();
            }
            
            // 여러 줄로 붙여넣은 경우 (예: Network 탭에서 복사)
            // "cookie: ASP.NET_SessionId=..." 같은 형태를 파싱
            var lines = cookieString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
                {
                    cookieString = trimmedLine.Substring(7).Trim();
                    break;
                }
            }
            
            // 세미콜론으로 구분된 쿠키 파싱
            var cookieParts = cookieString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in cookieParts)
            {
                var kvp = part.Split(new[] { '=' }, 2);
                if (kvp.Length == 2)
                {
                    var name = kvp[0].Trim();
                    var value = kvp[1].Trim();
                    
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                    {
                        Cookies[name] = value;
                    }
                }
            }

            if (Cookies.Count == 0)
            {
                MessageBox.Show(
                    "유효한 쿠키를 찾을 수 없습니다.\n\n" +
                    "형식 예시:\n" +
                    "ASP.NET_SessionId=xxx; Smart2Application=yyy\n\n" +
                    "또는 Network 탭의 cookie 헤더 전체를 붙여넣으세요.",
                    "입력 오류", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            // 쿠키 암호화하여 저장
            try
            {
                var json = JsonSerializer.Serialize(Cookies, new JsonSerializerOptions { WriteIndented = true });
                CookieEncryption.SaveEncryptedCookies(json, "manual_cookies.dat");
                
                MessageBox.Show(
                    $"{Cookies.Count}개의 쿠키를 암호화하여 저장했습니다:\n\n" + 
                    string.Join("\n", Cookies.Keys) + 
                    "\n\n🔒 Windows DPAPI로 암호화됨 (현재 사용자만 복호화 가능)",
                    "저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"쿠키 암호화 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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

