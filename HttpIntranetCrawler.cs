using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace AgentAssistant
{
    public class HttpIntranetCrawler
    {
        private readonly HttpClient httpClient;
        private readonly CookieContainer cookieContainer;
        private const string CookieFilePath = "http_session_cookies.json";
        private string lastCookieDebugInfo = "";

        public HttpIntranetCrawler()
        {
            cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = true
            };
            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<(bool success, string debugInfo)> LoginAsyncWithDebug(string username, string password, string loginUrl)
        {
            var debugInfo = "";
            try
            {
                // 1. 로그인 페이지 방문 (ViewState 가져오기)
                debugInfo += "1. 로그인 페이지 방문 중...\n";
                var loginPageResponse = await httpClient.GetAsync(loginUrl);
                var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
                debugInfo += $"   응답 상태: {loginPageResponse.StatusCode}\n";

                var doc = new HtmlDocument();
                doc.LoadHtml(loginPageHtml);

                // ASP.NET ViewState 추출
                var viewState = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATE']")?.GetAttributeValue("value", "");
                var viewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", "");
                var eventValidation = doc.DocumentNode.SelectSingleNode("//input[@id='__EVENTVALIDATION']")?.GetAttributeValue("value", "");
                debugInfo += $"   ViewState: {(viewState != null ? "찾음" : "없음")}\n";

                // 2. 로그인 폼 데이터 구성
                debugInfo += "\n2. 로그인 요청 전송 중...\n";
                var formData = new Dictionary<string, string>
                {
                    ["__VIEWSTATE"] = viewState ?? "",
                    ["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "",
                    ["__EVENTVALIDATION"] = eventValidation ?? "",
                    ["txtPC_LoginID"] = username,
                    ["txtPC_LoginPW"] = password,
                    ["btnPC_Login"] = "LOGIN"
                };

                var content = new FormUrlEncodedContent(formData);

                // 3. 로그인 요청
                var loginResponse = await httpClient.PostAsync(loginUrl, content);
                var responseUrl = loginResponse.RequestMessage?.RequestUri?.ToString() ?? "";
                var responseHtml = await loginResponse.Content.ReadAsStringAsync();
                
                debugInfo += $"   응답 URL: {responseUrl}\n";
                debugInfo += $"   응답 상태: {loginResponse.StatusCode}\n";

                // 4. 로그인 성공 확인
                debugInfo += "\n3. 로그인 결과 확인 중...\n";
                
                // 여러 조건으로 성공 판단
                bool loginSuccess = false;
                
                // 조건 1: URL이 Login.aspx가 아님
                if (!responseUrl.Contains("Login.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    debugInfo += "   ✓ URL이 로그인 페이지가 아님\n";
                    loginSuccess = true;
                }
                
                // 조건 2: 쿠키에 Smart2Application이 있음
                var cookies = cookieContainer.GetCookies(new Uri(loginUrl));
                if (cookies["Smart2Application"] != null)
                {
                    debugInfo += "   ✓ Smart2Application 쿠키 발견\n";
                    loginSuccess = true;
                }
                
                // 조건 3: 응답에 로그인 실패 메시지가 없음
                if (!responseHtml.Contains("로그인") && !responseHtml.Contains("LOGIN"))
                {
                    debugInfo += "   ✓ 로그인 페이지 아님 (HTML 확인)\n";
                    loginSuccess = true;
                }
                
                if (loginSuccess)
                {
                    debugInfo += "\n✓ 로그인 성공!\n";
                    debugInfo += $"   쿠키 개수: {cookies.Count}\n";
                    SaveCookies();
                    debugInfo += "   쿠키 저장 완료\n";
                    return (true, debugInfo);
                }
                else
                {
                    debugInfo += "\n✗ 로그인 실패\n";
                    debugInfo += $"   응답 HTML 길이: {responseHtml.Length}\n";
                    return (false, debugInfo);
                }
            }
            catch (Exception ex)
            {
                debugInfo += $"\n✗ 예외 발생: {ex.Message}\n";
                return (false, debugInfo);
            }
        }

        public async Task<bool> LoginAsync(string username, string password, string loginUrl)
        {
            var result = await LoginAsyncWithDebug(username, password, loginUrl);
            return result.success;
        }

        public async Task<BoardPageResult> GetBoardItemsAsync(string boardUrl, int pageNumber = 1)
        {
            try
            {
                var response = await httpClient.GetAsync(boardUrl);
                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var boardItems = new List<BoardItem>();

                // 테이블에서 게시글 파싱
                var rows = doc.DocumentNode.SelectNodes("//table//tr");
                if (rows != null)
                {
                    foreach (var row in rows.Skip(1)) // 헤더 제외
                    {
                        // 페이지네이션 div를 포함하는 행은 건너뛰기
                        if (row.SelectSingleNode(".//div[@class='paginate']") != null)
                        {
                            continue;
                        }
                        
                        var cells = row.SelectNodes(".//td");
                        if (cells != null && cells.Count >= 3)  // 체크박스, 번호, 제목 최소 3개 필요
                        {
                            var item = new BoardItem();

                            // cells[0]: 체크박스
                            // cells[1]: 번호 또는 공지 아이콘
                            // cells[2]: 제목
                            if (cells.Count > 1)
                            {
                                // 번호 또는 공지 아이콘 처리
                                var numberCell = cells[1];  // 두번째 셀이 번호/공지
                                var imgNode = numberCell.SelectSingleNode(".//img");
                                if (imgNode != null && imgNode.GetAttributeValue("src", "").Contains("notice.gif"))
                                {
                                    item.Number = "공지"; // 공지 아이콘인 경우
                                }
                                else
                                {
                                    item.Number = numberCell.InnerText.Trim();
                                }
                            }

                            if (cells.Count > 2)  // 제목은 세번째 셀
                            {
                                // a 태그 찾기 (aBoardSubject로 시작하는 id를 가진 것)
                                var titleCell = cells[2];
                                var links = titleCell.SelectNodes(".//a");
                                HtmlNode? titleLink = null;
                                
                                if (links != null && links.Count > 0)
                                {
                                    // aBoardSubject_로 시작하는 id를 가진 링크 찾기
                                    foreach (var link in links)
                                    {
                                        var id = link.GetAttributeValue("id", "");
                                        if (id.StartsWith("aBoardSubject_"))
                                        {
                                            titleLink = link;
                                            break;
                                        }
                                    }
                                    
                                    // 못 찾으면 preview가 아닌 링크 찾기 (텍스트가 있는 것)
                                    if (titleLink == null)
                                    {
                                        foreach (var link in links)
                                        {
                                            if (!string.IsNullOrWhiteSpace(link.InnerText))
                                            {
                                                titleLink = link;
                                                break;
                                            }
                                        }
                                    }
                                }
                                
                                if (titleLink != null)
                                {
                                    // a 태그의 텍스트를 제목으로 사용 (HTML 엔티티 포함)
                                    item.Title = System.Net.WebUtility.HtmlDecode(titleLink.InnerText.Trim());
                                    
                                    var href = titleLink.GetAttributeValue("href", "");
                                    if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                                    {
                                        var baseUri = new Uri(boardUrl);
                                        item.Url = new Uri(baseUri, href).ToString();
                                    }
                                    else
                                    {
                                        item.Url = href;
                                    }
                                }
                                else
                                {
                                    // 링크가 없으면 전체 텍스트 사용
                                    item.Title = System.Net.WebUtility.HtmlDecode(titleCell.InnerText.Trim());
                                }
                            }

                            if (cells.Count > 4)
                                item.Author = cells[4].InnerText.Trim();  // 5번째 셀: 등록자

                            if (cells.Count > 6)
                                item.Date = cells[6].InnerText.Trim();  // 7번째 셀: 등록일

                            // 제목이 있고, 페이지 정보가 아니면 게시글로 추가
                            bool isPaginationInfo = item.Title.Contains("/ 총") && item.Title.Contains("페이지") && string.IsNullOrEmpty(item.Url);
                            
                            if (!string.IsNullOrWhiteSpace(item.Title) && !isPaginationInfo)
                            {
                                boardItems.Add(item);
                            }
                        }
                    }
                }

                // 총 페이지 수 파싱 (paginate div에서 모든 페이지 링크 찾기)
                int totalPages = 1;
                var paginateDiv = doc.DocumentNode.SelectSingleNode("//div[@class='paginate']");
                if (paginateDiv != null)
                {
                    var allPageNumbers = new List<int>();
                    
                    // 모든 페이지 링크에서 번호 추출
                    var pageLinks = paginateDiv.SelectNodes(".//a[contains(@href, '__doPostBack')]");
                    if (pageLinks != null)
                    {
                        foreach (var link in pageLinks)
                        {
                            var href = link.GetAttributeValue("href", "");
                            // javascript:__doPostBack('...','297') 형태에서 번호 추출
                            var match = System.Text.RegularExpressions.Regex.Match(href, @"'(\d+)'\s*\)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                            {
                                allPageNumbers.Add(pageNum);
                            }
                        }
                    }
                    
                    // 현재 페이지 번호도 추가 (strong 태그)
                    var currentPageStrong = paginateDiv.SelectSingleNode(".//strong");
                    if (currentPageStrong != null && int.TryParse(currentPageStrong.InnerText.Trim(), out int currentPageNum))
                    {
                        allPageNumbers.Add(currentPageNum);
                    }
                    
                    // 가장 큰 번호를 총 페이지 수로 설정
                    if (allPageNumbers.Any())
                    {
                        totalPages = allPageNumbers.Max();
                    }
                }

                return new BoardPageResult
                {
                    Items = boardItems,
                    CurrentPage = pageNumber,
                    TotalPages = totalPages,
                    ViewState = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATE']")?.GetAttributeValue("value", ""),
                    ViewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", ""),
                    EventValidation = doc.DocumentNode.SelectSingleNode("//input[@id='__EVENTVALIDATION']")?.GetAttributeValue("value", "")
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"게시판 조회 실패: {ex.Message}", ex);
            }
        }

        public async Task<BoardPageResult> NavigateToPageAsync(string boardUrl, int pageNumber, string viewState, string viewStateGenerator, string eventValidation)
        {
            try
            {
                // ASP.NET 페이지네이션을 위한 POST 요청
                var formData = new Dictionary<string, string>
                {
                    ["__VIEWSTATE"] = viewState ?? "",
                    ["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "",
                    ["__EVENTVALIDATION"] = eventValidation ?? "",
                    ["__EVENTTARGET"] = "ctl00$ctl00$cphContent$cphContent$pager",
                    ["__EVENTARGUMENT"] = pageNumber.ToString(),
                    // 추가 필수 필드들
                    ["ctl00$ctl00$cphContent$cphContent$hidPageSize"] = "15",
                    ["ctl00$ctl00$cphContent$cphContent$hidSearchGubun"] = "All",
                    ["ctl00$ctl00$cphContent$cphContent$hidSelectedDay"] = "All",  // 전체 보기!
                    ["ctl00$ctl00$cphContent$cphContent$hidDateBarYN"] = "Y",
                    ["ctl00$ctl00$cphContent$cphContent$hidSortColum"] = "MessageID",
                    ["ctl00$ctl00$cphContent$cphContent$hidSortDirection"] = "DESC",
                    ["ctl00$ctl00$cphContent$cphContent$hidReadSearchType"] = "NONE"
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(boardUrl, content);
                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var boardItems = new List<BoardItem>();

                // 테이블에서 게시글 파싱
                var rows = doc.DocumentNode.SelectNodes("//table//tr");
                if (rows != null)
                {
                    foreach (var row in rows.Skip(1))
                    {
                        var cells = row.SelectNodes(".//td");
                        if (cells != null && cells.Count >= 3)  // 체크박스, 번호, 제목 최소 3개 필요
                        {
                            var item = new BoardItem();

                            // cells[0]: 체크박스
                            // cells[1]: 번호 또는 공지 아이콘
                            // cells[2]: 제목
                            if (cells.Count > 1)
                            {
                                // 번호 또는 공지 아이콘 처리
                                var numberCell = cells[1];  // 두번째 셀이 번호/공지
                                var imgNode = numberCell.SelectSingleNode(".//img");
                                if (imgNode != null && imgNode.GetAttributeValue("src", "").Contains("notice.gif"))
                                {
                                    item.Number = "공지"; // 공지 아이콘인 경우
                                }
                                else
                                {
                                    item.Number = numberCell.InnerText.Trim();
                                }
                            }

                            if (cells.Count > 2)  // 제목은 세번째 셀
                            {
                                // a 태그 찾기 (aBoardSubject로 시작하는 id를 가진 것)
                                var titleCell = cells[2];
                                var links = titleCell.SelectNodes(".//a");
                                HtmlNode? titleLink = null;
                                
                                if (links != null && links.Count > 0)
                                {
                                    // aBoardSubject_로 시작하는 id를 가진 링크 찾기
                                    foreach (var link in links)
                                    {
                                        var id = link.GetAttributeValue("id", "");
                                        if (id.StartsWith("aBoardSubject_"))
                                        {
                                            titleLink = link;
                                            break;
                                        }
                                    }
                                    
                                    // 못 찾으면 preview가 아닌 링크 찾기 (텍스트가 있는 것)
                                    if (titleLink == null)
                                    {
                                        foreach (var link in links)
                                        {
                                            if (!string.IsNullOrWhiteSpace(link.InnerText))
                                            {
                                                titleLink = link;
                                                break;
                                            }
                                        }
                                    }
                                }
                                
                                if (titleLink != null)
                                {
                                    // a 태그의 텍스트를 제목으로 사용 (HTML 엔티티 포함)
                                    item.Title = System.Net.WebUtility.HtmlDecode(titleLink.InnerText.Trim());
                                    
                                    var href = titleLink.GetAttributeValue("href", "");
                                    if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                                    {
                                        var baseUri = new Uri(boardUrl);
                                        item.Url = new Uri(baseUri, href).ToString();
                                    }
                                    else
                                    {
                                        item.Url = href;
                                    }
                                }
                                else
                                {
                                    // 링크가 없으면 전체 텍스트 사용
                                    item.Title = System.Net.WebUtility.HtmlDecode(titleCell.InnerText.Trim());
                                }
                            }

                            if (cells.Count > 4)
                                item.Author = cells[4].InnerText.Trim();  // 5번째 셀: 등록자

                            if (cells.Count > 6)
                                item.Date = cells[6].InnerText.Trim();  // 7번째 셀: 등록일

                            if (!string.IsNullOrWhiteSpace(item.Title))
                            {
                                boardItems.Add(item);
                            }
                        }
                    }
                }

                // 총 페이지 수 파싱 (paginate div에서 모든 페이지 링크 찾기)
                int totalPages = 1;
                var paginateDiv = doc.DocumentNode.SelectSingleNode("//div[@class='paginate']");
                if (paginateDiv != null)
                {
                    var allPageNumbers = new List<int>();
                    
                    // 모든 페이지 링크에서 번호 추출
                    var pageLinks = paginateDiv.SelectNodes(".//a[contains(@href, '__doPostBack')]");
                    if (pageLinks != null)
                    {
                        foreach (var link in pageLinks)
                        {
                            var href = link.GetAttributeValue("href", "");
                            // javascript:__doPostBack('...','297') 형태에서 번호 추출
                            var match = System.Text.RegularExpressions.Regex.Match(href, @"'(\d+)'\s*\)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                            {
                                allPageNumbers.Add(pageNum);
                            }
                        }
                    }
                    
                    // 현재 페이지 번호도 추가 (strong 태그)
                    var currentPageStrong = paginateDiv.SelectSingleNode(".//strong");
                    if (currentPageStrong != null && int.TryParse(currentPageStrong.InnerText.Trim(), out int currentPageNum))
                    {
                        allPageNumbers.Add(currentPageNum);
                    }
                    
                    // 가장 큰 번호를 총 페이지 수로 설정
                    if (allPageNumbers.Any())
                    {
                        totalPages = allPageNumbers.Max();
                    }
                }

                return new BoardPageResult
                {
                    Items = boardItems,
                    CurrentPage = pageNumber,
                    TotalPages = totalPages,
                    ViewState = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATE']")?.GetAttributeValue("value", ""),
                    ViewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", ""),
                    EventValidation = doc.DocumentNode.SelectSingleNode("//input[@id='__EVENTVALIDATION']")?.GetAttributeValue("value", "")
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"페이지 이동 실패: {ex.Message}", ex);
            }
        }

        public void SaveCookies()
        {
            try
            {
                var cookies = cookieContainer.GetAllCookies();
                var cookieList = new List<object>();

                foreach (Cookie cookie in cookies)
                {
                    cookieList.Add(new
                    {
                        cookie.Name,
                        cookie.Value,
                        cookie.Domain,
                        cookie.Path,
                        cookie.Expires
                    });
                }

                var json = JsonSerializer.Serialize(cookieList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CookieFilePath, json);
            }
            catch { }
        }

        public string GetLastCookieDebugInfo()
        {
            return lastCookieDebugInfo;
        }

        public bool LoadCookies(string baseUrl)
        {
            try
            {
                var uri = new Uri(baseUrl);
                lastCookieDebugInfo = "";

                // 1단계: Chrome/Edge 브라우저에서 최신 쿠키 읽기 시도 (우선순위 1)
                try
                {
                    lastCookieDebugInfo += $"Chrome/Edge 쿠키 읽기 시도...\n";
                    lastCookieDebugInfo += $"대상 도메인: {uri.Host}\n\n";
                    
                    // 여러 도메인 패턴으로 시도
                    var domainPatterns = new[] { uri.Host, $".{uri.Host}", uri.Host.Replace("www.", "") };
                    List<Cookie>? chromeCookies = null;
                    string detailedDebug = "";
                    
                    foreach (var pattern in domainPatterns)
                    {
                        try
                        {
                            var result = ChromeCookieReader.ReadCookiesWithDebug(pattern);
                            chromeCookies = result.cookies;
                            detailedDebug = result.debugInfo;
                            
                            if (chromeCookies != null && chromeCookies.Count > 0)
                            {
                                lastCookieDebugInfo += $"✓ 패턴 '{pattern}'으로 쿠키 찾음\n";
                                lastCookieDebugInfo += detailedDebug;
                                break;
                            }
                        }
                        catch { }
                    }
                    
                    // 쿠키를 못 찾았으면 상세 디버그 정보 추가
                    if (chromeCookies == null || chromeCookies.Count == 0)
                    {
                        lastCookieDebugInfo += "\n=== 상세 디버그 정보 ===\n";
                        lastCookieDebugInfo += detailedDebug;
                    }
                    
                    if (chromeCookies != null && chromeCookies.Count > 0)
                    {
                        lastCookieDebugInfo += $"✓ Chrome/Edge에서 {chromeCookies.Count}개의 쿠키 찾음\n";
                        System.Diagnostics.Debug.WriteLine($"Chrome/Edge에서 {chromeCookies.Count}개의 쿠키를 찾았습니다.");
                        
                        foreach (var cookie in chromeCookies)
                        {
                            cookieContainer.Add(uri, cookie);
                            lastCookieDebugInfo += $"  - {cookie.Name} = {cookie.Value.Substring(0, Math.Min(30, cookie.Value.Length))}...\n";
                            System.Diagnostics.Debug.WriteLine($"쿠키 추가: {cookie.Name} = {cookie.Value.Substring(0, Math.Min(20, cookie.Value.Length))}...");
                        }
                        
                        // Chrome/Edge에서 가져온 쿠키를 파일로 저장 (백업)
                        SaveCookies();
                        lastCookieDebugInfo += "✓ 쿠키를 http_session_cookies.json에 저장함\n";
                        
                        return true;
                    }
                    else
                    {
                        lastCookieDebugInfo += "✗ Chrome/Edge에서 쿠키를 찾지 못함\n";
                        lastCookieDebugInfo += $"  시도한 패턴: {string.Join(", ", domainPatterns)}\n";
                    }
                }
                catch (Exception ex)
                {
                    lastCookieDebugInfo += $"✗ Chrome 쿠키 읽기 오류: {ex.Message}\n";
                    System.Diagnostics.Debug.WriteLine($"Chrome 쿠키 읽기 실패: {ex.Message}");
                    // Chrome 쿠키 읽기 실패해도 계속 진행
                }

                // 2단계: 수동으로 입력한 쿠키 읽기 시도 (백업)
                lastCookieDebugInfo += "\nmanual_cookies.json 확인...\n";
                if (File.Exists("manual_cookies.json"))
                {
                    try
                    {
                        var manualJson = File.ReadAllText("manual_cookies.json");
                        var manualCookies = JsonSerializer.Deserialize<Dictionary<string, string>>(manualJson);
                        
                        if (manualCookies != null && manualCookies.Count > 0)
                        {
                            lastCookieDebugInfo += $"✓ manual_cookies.json에서 {manualCookies.Count}개 찾음\n";
                            System.Diagnostics.Debug.WriteLine($"manual_cookies.json에서 {manualCookies.Count}개의 쿠키를 찾았습니다.");
                            foreach (var kvp in manualCookies)
                            {
                                var cookie = new Cookie(kvp.Key, kvp.Value, "/", uri.Host);
                                cookieContainer.Add(uri, cookie);
                            }
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastCookieDebugInfo += $"✗ 읽기 오류: {ex.Message}\n";
                        System.Diagnostics.Debug.WriteLine($"manual_cookies.json 읽기 실패: {ex.Message}");
                    }
                }
                else
                {
                    lastCookieDebugInfo += "✗ 파일 없음\n";
                }

                // 3단계: 저장된 쿠키 파일에서 읽기 (마지막 백업)
                if (File.Exists(CookieFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(CookieFilePath);
                        using var document = JsonDocument.Parse(json);

                        foreach (var element in document.RootElement.EnumerateArray())
                        {
                            try
                            {
                                var cookie = new Cookie
                                {
                                    Name = element.GetProperty("Name").GetString() ?? "",
                                    Value = element.GetProperty("Value").GetString() ?? "",
                                    Domain = element.GetProperty("Domain").GetString() ?? "",
                                    Path = element.GetProperty("Path").GetString() ?? "/"
                                };

                                cookieContainer.Add(uri, cookie);
                            }
                            catch { }
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"http_session_cookies.json 읽기 실패: {ex.Message}");
                    }
                }

                lastCookieDebugInfo += "\n모든 쿠키 소스에서 쿠키를 찾지 못했습니다.";
                System.Diagnostics.Debug.WriteLine("모든 쿠키 소스에서 쿠키를 찾지 못했습니다.");
                return false;
            }
            catch (Exception ex)
            {
                lastCookieDebugInfo += $"\n전체 오류: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"LoadCookies 전체 오류: {ex.Message}");
                return false;
            }
        }

        public async Task<MailPageResult> GetMailPageAsync(string mailUrl, int pageNumber = 1)
        {
            try
            {
                var response = await httpClient.GetAsync(mailUrl);
                var html = await response.Content.ReadAsStringAsync();
                
                lastMailPageHtml = html; // 다음 페이지 이동을 위해 저장
                
                var result = MailCrawler.ParseMailPageResult(html);
                
                // 첫 페이지가 아닌 경우, 페이지 번호가 안 맞으면 페이지네이션 필요
                if (pageNumber > 1 && result.CurrentPage != pageNumber)
                {
                    result = await NavigateToMailPageAsync(mailUrl, pageNumber, result.ViewState, result.ViewStateGenerator, result.EventValidation);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"메일 페이지 조회 실패: {ex.Message}", ex);
            }
        }

        private string lastMailPageHtml = "";

        public async Task<MailPageResult> NavigateToMailPageAsync(string mailUrl, int pageNumber, string viewState, string viewStateGenerator, string eventValidation)
        {
            try
            {
                // 이전 페이지의 모든 필드 사용
                var previousResult = MailCrawler.ParseMailPageResult(lastMailPageHtml);
                
                // 이전 페이지의 모든 필드를 가져옴
                var formData = new Dictionary<string, string>(previousResult.AllFormFields);
                
                // 페이지네이션에 필요한 필드만 덮어쓰기
                formData["__VIEWSTATE"] = viewState;
                formData["__VIEWSTATEGENERATOR"] = viewStateGenerator;
                formData["__EVENTVALIDATION"] = eventValidation;
                formData["__EVENTTARGET"] = "ctl00$ctl00$cphContent$cphContent$pagerList";
                formData["__EVENTARGUMENT"] = pageNumber.ToString();

                System.Diagnostics.Debug.WriteLine($"페이지 이동: {pageNumber}");
                System.Diagnostics.Debug.WriteLine($"POST 필드 개수: {formData.Count}");

                var content = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(mailUrl, content);
                var html = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"응답 HTML 길이: {html.Length}");
                
                // 디버깅용: 응답 HTML 저장
                File.WriteAllText($"mail_page_{pageNumber}_response.html", html);
                System.Diagnostics.Debug.WriteLine($"응답 HTML 저장됨: mail_page_{pageNumber}_response.html");

                lastMailPageHtml = html;
                return MailCrawler.ParseMailPageResult(html);
            }
            catch (Exception ex)
            {
                throw new Exception($"메일 페이지 이동 실패: {ex.Message}", ex);
            }
        }

        public async Task<List<MailItem>> GetMailListAsync(string mailUrl)
        {
            var pageResult = await GetMailPageAsync(mailUrl, 1);
            return pageResult.Items;
        }

        public async Task<List<MailFolder>> GetMailFoldersAsync(string baseUrl = "https://ngw.cauhs.or.kr/WebSite/Mail/Main.aspx")
        {
            try
            {
                var response = await httpClient.GetAsync(baseUrl);
                var html = await response.Content.ReadAsStringAsync();
                
                return MailCrawler.ParseMailFolders(html);
            }
            catch (Exception ex)
            {
                throw new Exception($"메일 폴더 목록 조회 실패: {ex.Message}", ex);
            }
        }

        public void ClearSavedCookies()
        {
            try
            {
                if (File.Exists(CookieFilePath))
                    File.Delete(CookieFilePath);
            }
            catch { }
        }
    }

    public class BoardItem
    {
        public string Number { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Date { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class BoardPageResult
    {
        public List<BoardItem> Items { get; set; } = new List<BoardItem>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? ViewState { get; set; }
        public string? ViewStateGenerator { get; set; }
        public string? EventValidation { get; set; }
    }
}

