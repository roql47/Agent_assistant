using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace AgentAssistant
{
    public class SeleniumCookieExtractor
    {
        public static (bool success, Dictionary<string, string> cookies, string debugInfo) AutoLogin(string username, string password, string loginUrl)
        {
            var cookies = new Dictionary<string, string>();
            var debugInfo = "";
            IWebDriver? driver = null;
            
            try
            {
                debugInfo += "1. Chrome 드라이버 초기화 중...\n";
                
                // Chrome 옵션 설정
                var options = new ChromeOptions();
                options.AddArgument("--headless"); // 백그라운드 실행
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--window-size=1920,1080");
                
                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true; // 콘솔 창 숨기기
                
                driver = new ChromeDriver(service, options);
                debugInfo += "   ✓ Chrome 드라이버 시작 성공\n";
                
                debugInfo += "\n2. 로그인 페이지 로딩 중...\n";
                driver.Navigate().GoToUrl(loginUrl);
                debugInfo += $"   ✓ 페이지 로드 완료: {driver.Title}\n";
                
                debugInfo += "\n3. 로그인 폼 작성 중...\n";
                
                // 로그인 폼 요소 찾기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.Id("txtPC_LoginID")));
                
                debugInfo += "   ✓ 로그인 폼 요소 찾음\n";
                
                // JavaScript로 직접 값 설정 (숨겨진 필드 문제 해결)
                var js = (IJavaScriptExecutor)driver;
                
                // ID 입력
                js.ExecuteScript($"document.getElementById('txtPC_LoginID').value = '{username}';");
                debugInfo += "   ✓ ID 입력 완료\n";
                
                // 비밀번호 필드 표시 및 입력
                js.ExecuteScript($@"
                    document.getElementById('txtPC_LoginPWTemp').className = 'login_input_hidden';
                    document.getElementById('txtPC_LoginPW').className = 'login_input';
                    document.getElementById('txtPC_LoginPW').value = '{password}';
                ");
                debugInfo += "   ✓ PW 입력 완료\n";
                
                debugInfo += "\n4. 로그인 버튼 클릭...\n";
                
                // JavaScript로 로그인 버튼 클릭 (btnPC_Login_Click 함수 호출)
                js.ExecuteScript("btnPC_Login_Click();");
                
                // 페이지 로딩 대기 (최대 10초)
                System.Threading.Thread.Sleep(3000);
                
                var currentUrl = driver.Url;
                debugInfo += $"   현재 URL: {currentUrl}\n";
                
                debugInfo += "\n5. 로그인 결과 확인 중...\n";
                
                // 로그인 성공 확인
                if (!currentUrl.Contains("Login.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    debugInfo += "   ✓ URL 변경됨 - 로그인 성공!\n";
                }
                else
                {
                    // 페이지에 오류 메시지가 있는지 확인
                    var pageSource = driver.PageSource;
                    if (pageSource.Contains("로그인") || pageSource.Contains("실패") || pageSource.Contains("확인"))
                    {
                        debugInfo += "   ✗ 로그인 실패 (ID/PW 오류 가능)\n";
                        return (false, cookies, debugInfo);
                    }
                }
                
                debugInfo += "\n6. 쿠키 추출 중...\n";
                
                // Selenium에서 쿠키 가져오기
                var seleniumCookies = driver.Manage().Cookies.AllCookies;
                
                foreach (var cookie in seleniumCookies)
                {
                    cookies[cookie.Name] = cookie.Value;
                    debugInfo += $"   - {cookie.Name} = {cookie.Value.Substring(0, Math.Min(30, cookie.Value.Length))}...\n";
                }
                
                debugInfo += $"\n✓ 총 {cookies.Count}개의 쿠키 추출 완료!\n";
                
                return (true, cookies, debugInfo);
            }
            catch (Exception ex)
            {
                debugInfo += $"\n✗ 오류 발생: {ex.Message}\n";
                debugInfo += $"   상세: {ex.StackTrace}\n";
                return (false, cookies, debugInfo);
            }
            finally
            {
                // Chrome 닫기
                try
                {
                    driver?.Quit();
                    debugInfo += "\n7. Chrome 드라이버 종료\n";
                }
                catch { }
            }
        }
    }
}

