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
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ko,en;q=0.9,en-US;q=0.8");
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
                
                // 디버깅용으로 HTML 저장
                var savePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "board_list_page.html");
                System.IO.File.WriteAllText(savePath, html);
                System.Diagnostics.Debug.WriteLine($"[게시판 목록] HTML 저장됨: {savePath}");

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
                                    var onclick = titleLink.GetAttributeValue("onclick", "");
                                    var linkId = titleLink.GetAttributeValue("id", "");
                                    
                                    System.Diagnostics.Debug.WriteLine($"[GetBoardItems] 제목: {item.Title}");
                                    System.Diagnostics.Debug.WriteLine($"[GetBoardItems] href: '{href}'");
                                    System.Diagnostics.Debug.WriteLine($"[GetBoardItems] onclick: '{onclick}'");
                                    System.Diagnostics.Debug.WriteLine($"[GetBoardItems] id: '{linkId}'");
                                    
                                    // 링크 ID에서 MsgId 추출 시도 (예: aBoardSubject_73362)
                                    string? msgId = null;
                                    if (!string.IsNullOrEmpty(linkId) && linkId.Contains("_"))
                                    {
                                        var parts = linkId.Split('_');
                                        if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
                                        {
                                            msgId = parts[parts.Length - 1];
                                            System.Diagnostics.Debug.WriteLine($"[GetBoardItems] ID에서 MsgId 추출: {msgId}");
                                        }
                                    }
                                    
                                    // onclick에서 MsgId 추출 시도
                                    if (msgId == null && !string.IsNullOrEmpty(onclick))
                                    {
                                        var match = System.Text.RegularExpressions.Regex.Match(onclick, @"GotoBoardView\((\d+)\)");
                                        if (match.Success)
                                        {
                                            msgId = match.Groups[1].Value;
                                            System.Diagnostics.Debug.WriteLine($"[GetBoardItems] onclick에서 MsgId 추출: {msgId}");
                                        }
                                    }
                                    
                                    // MsgId를 찾았으면 BoardView.aspx URL 생성
                                    if (!string.IsNullOrEmpty(msgId))
                                    {
                                        // 게시판 URL에서 fdid 추출
                                        var boardUri = new Uri(boardUrl);
                                        var fdid = "";
                                        
                                        // 쿼리 스트링에서 fdid 추출
                                        var query = boardUri.Query.TrimStart('?');
                                        foreach (var param in query.Split('&'))
                                        {
                                            var parts = param.Split('=');
                                            if (parts.Length == 2 && parts[0].Equals("fdid", StringComparison.OrdinalIgnoreCase))
                                            {
                                                fdid = parts[1];
                                                break;
                                            }
                                        }
                                        
                                        System.Diagnostics.Debug.WriteLine($"[GetBoardItems] 추출된 fdid: {fdid}");
                                        
                                        var baseUri = new Uri(boardUrl);
                                        var boardViewUrl = new Uri(baseUri, $"/WebSite/Basic/Board/BoardView.aspx?system=Board&BoardType=Normal&FromOuterYN=N&fdid={fdid}&MsgId={msgId}&DateBarYN=Y&BoardGubun=Normal&PageSize=15&PageCurrent=1&SortField=MessageID&SortDirection=DESC&Cate=0&CateGubunYN=N&SearchGubun=All&SelectedDay=All");
                                        item.Url = boardViewUrl.ToString();
                                        System.Diagnostics.Debug.WriteLine($"[GetBoardItems] 생성된 URL: {item.Url}");
                                    }
                                    // onclick에 __doPostBack이 있으면 그것을 사용
                                    else if (!string.IsNullOrEmpty(onclick) && onclick.Contains("__doPostBack"))
                                    {
                                        item.Url = "javascript:" + onclick;
                                        System.Diagnostics.Debug.WriteLine($"[GetBoardItems] PostBack 사용: {item.Url}");
                                    }
                                    else if (!string.IsNullOrEmpty(href))
                                    {
                                        // javascript: 링크는 그대로 저장
                                        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            item.Url = href;
                                            System.Diagnostics.Debug.WriteLine($"[GetBoardItems] JavaScript 링크: {href}");
                                        }
                                        // http로 시작하는 절대 URL도 그대로 저장
                                        else if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                        {
                                            item.Url = href;
                                        }
                                        // 상대 URL은 절대 URL로 변환
                                        else
                                        {
                                            var baseUri = new Uri(boardUrl);
                                            item.Url = new Uri(baseUri, href).ToString();
                                        }
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
                                    var onclick = titleLink.GetAttributeValue("onclick", "");
                                    var linkId = titleLink.GetAttributeValue("id", "");
                                    
                                    // 링크 ID에서 MsgId 추출 시도
                                    string? msgId = null;
                                    if (!string.IsNullOrEmpty(linkId) && linkId.Contains("_"))
                                    {
                                        var parts = linkId.Split('_');
                                        if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
                                        {
                                            msgId = parts[parts.Length - 1];
                                        }
                                    }
                                    
                                    // onclick에서 MsgId 추출 시도
                                    if (msgId == null && !string.IsNullOrEmpty(onclick))
                                    {
                                        var match = System.Text.RegularExpressions.Regex.Match(onclick, @"GotoBoardView\((\d+)\)");
                                        if (match.Success)
                                        {
                                            msgId = match.Groups[1].Value;
                                        }
                                    }
                                    
                                    // MsgId를 찾았으면 BoardView.aspx URL 생성
                                    if (!string.IsNullOrEmpty(msgId))
                                    {
                                        // 게시판 URL에서 fdid 추출
                                        var boardUri = new Uri(boardUrl);
                                        var fdid = "";
                                        
                                        // 쿼리 스트링에서 fdid 추출
                                        var query = boardUri.Query.TrimStart('?');
                                        foreach (var param in query.Split('&'))
                                        {
                                            var parts = param.Split('=');
                                            if (parts.Length == 2 && parts[0].Equals("fdid", StringComparison.OrdinalIgnoreCase))
                                            {
                                                fdid = parts[1];
                                                break;
                                            }
                                        }
                                        
                                        var baseUri = new Uri(boardUrl);
                                        var boardViewUrl = new Uri(baseUri, $"/WebSite/Basic/Board/BoardView.aspx?system=Board&BoardType=Normal&FromOuterYN=N&fdid={fdid}&MsgId={msgId}&DateBarYN=Y&BoardGubun=Normal&PageSize=15&PageCurrent=1&SortField=MessageID&SortDirection=DESC&Cate=0&CateGubunYN=N&SearchGubun=All&SelectedDay=All");
                                        item.Url = boardViewUrl.ToString();
                                    }
                                    // onclick에 __doPostBack이 있으면 그것을 사용
                                    else if (!string.IsNullOrEmpty(onclick) && onclick.Contains("__doPostBack"))
                                    {
                                        item.Url = "javascript:" + onclick;
                                    }
                                    else if (!string.IsNullOrEmpty(href))
                                    {
                                        // javascript: 링크는 그대로 저장
                                        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            item.Url = href;
                                        }
                                        // http로 시작하는 절대 URL도 그대로 저장
                                        else if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                        {
                                            item.Url = href;
                                        }
                                        // 상대 URL은 절대 URL로 변환
                                        else
                                        {
                                            var baseUri = new Uri(boardUrl);
                                            item.Url = new Uri(baseUri, href).ToString();
                                        }
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

                // 2단계: 수동으로 입력한 쿠키 읽기 시도 (백업) - 암호화된 파일
                lastCookieDebugInfo += "\nmanual_cookies.dat (암호화) 확인...\n";
                if (File.Exists("manual_cookies.dat"))
                {
                    try
                    {
                        var manualJson = CookieEncryption.LoadEncryptedCookies("manual_cookies.dat");
                        
                        if (!string.IsNullOrEmpty(manualJson))
                        {
                            var manualCookies = JsonSerializer.Deserialize<Dictionary<string, string>>(manualJson);
                            
                            if (manualCookies != null && manualCookies.Count > 0)
                            {
                                lastCookieDebugInfo += $"✓ manual_cookies.dat에서 {manualCookies.Count}개 찾음 (DPAPI 복호화)\n";
                                System.Diagnostics.Debug.WriteLine($"manual_cookies.dat에서 {manualCookies.Count}개의 쿠키를 복호화했습니다.");
                                foreach (var kvp in manualCookies)
                                {
                                    var cookie = new Cookie(kvp.Key, kvp.Value, "/", uri.Host);
                                    cookieContainer.Add(uri, cookie);
                                }
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastCookieDebugInfo += $"✗ 복호화 오류: {ex.Message}\n";
                        System.Diagnostics.Debug.WriteLine($"manual_cookies.dat 복호화 실패: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"===== 메일 페이지 GET 요청 =====");
                System.Diagnostics.Debug.WriteLine($"URL: {mailUrl}");
                System.Diagnostics.Debug.WriteLine($"요청 페이지: {pageNumber}");
                
                var response = await httpClient.GetAsync(mailUrl);
                var html = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"응답 상태: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"응답 HTML 길이: {html.Length}");
                
                // 디버깅용: 첫 GET 요청 HTML 저장
                File.WriteAllText($"mail_initial_get.html", html);
                System.Diagnostics.Debug.WriteLine($"첫 GET 요청 HTML 저장됨: mail_initial_get.html");
                
                lastMailPageHtml = html; // 다음 페이지 이동을 위해 저장
                
                var result = MailCrawler.ParseMailPageResult(html);
                
                System.Diagnostics.Debug.WriteLine($"파싱 결과 - 현재 페이지: {result.CurrentPage}, 총 페이지: {result.TotalPages}, 메일 개수: {result.Items.Count}");
                System.Diagnostics.Debug.WriteLine($"추출된 Form 필드 개수: {result.AllFormFields.Count}");
                
                // 첫 페이지가 아닌 경우, 페이지 번호가 안 맞으면 페이지네이션 필요
                if (pageNumber > 1 && result.CurrentPage != pageNumber)
                {
                    result = await NavigateToMailPageAsync(mailUrl, pageNumber, result.AllFormFields, result.ViewState, result.ViewStateGenerator, result.EventValidation);
                }
                
                System.Diagnostics.Debug.WriteLine($"================================");
                
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"메일 페이지 조회 실패: {ex.Message}", ex);
            }
        }

        private string lastMailPageHtml = "";

        public async Task<MailPageResult> NavigateToMailPageAsync(string mailUrl, int pageNumber, Dictionary<string, string> previousFormFields, string viewState, string viewStateGenerator, string eventValidation)
        {
            try
            {
                // 이전 페이지의 모든 필드를 복사 (브라우저처럼)
                var formData = new Dictionary<string, string>(previousFormFields);
                
                System.Diagnostics.Debug.WriteLine($"[NavigateToMailPageAsync] 전달받은 Form 필드 개수: {previousFormFields.Count}");
                
                // 체크박스 관련 필드 제거 (페이지네이션에는 불필요)
                var keysToRemove = formData.Keys.Where(k => 
                    k.Contains("chkSelect") || 
                    k == "chkSelectAll").ToList();
                foreach (var key in keysToRemove)
                {
                    formData.Remove(key);
                }
                System.Diagnostics.Debug.WriteLine($"[NavigateToMailPageAsync] 체크박스 {keysToRemove.Count}개 제거");
                
                // select 드롭다운 필드 추가 (필수!)
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$ddlFilterType"))
                    formData["ctl00$ctl00$cphContent$cphContent$ddlFilterType"] = formData.GetValueOrDefault("ctl00$ctl00$cphContent$cphContent$hidFilterType", "ALL");
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$ddlSearchType"))
                    formData["ctl00$ctl00$cphContent$cphContent$ddlSearchType"] = formData.GetValueOrDefault("ctl00$ctl00$cphContent$cphContent$hidSearchType", "ALL");
                
                // complete 필드는 현재 페이지 (이동하기 전 페이지!)
                var currentPageInForm = formData.GetValueOrDefault("ctl00$ctl00$cphContent$cphContent$hidPage", "1");
                formData["complete"] = currentPageInForm;
                
                // 텍스트 검색 필드들 (빈 값이라도 필요)
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$txtSearchText"))
                    formData["ctl00$ctl00$cphContent$cphContent$txtSearchText"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$txtStartDate"))
                    formData["ctl00$ctl00$cphContent$cphContent$txtStartDate"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$txtEndDate"))
                    formData["ctl00$ctl00$cphContent$cphContent$txtEndDate"] = "";
                
                // 상세 검색 필드들
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailTitle"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailTitle"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailContent"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailContent"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailSender"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailSender"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailReceiver"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailReceiver"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailStartDate"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailStartDate"] = "";
                if (!formData.ContainsKey("ctl00$ctl00$cphContent$cphContent$DetailEndDate"))
                    formData["ctl00$ctl00$cphContent$cphContent$DetailEndDate"] = "";
                
                // hidPeriodChecked는 N으로 설정
                formData["ctl00$ctl00$cphContent$cphContent$hidPeriodChecked"] = "N";
                
                System.Diagnostics.Debug.WriteLine($"[NavigateToMailPageAsync] 추가 필드 설정 완료: complete={pageNumber}");
                
                // 페이지네이션 필드만 덮어쓰기
                formData["__VIEWSTATE"] = viewState ?? "";
                formData["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "";
                formData["__EVENTVALIDATION"] = eventValidation ?? "";
                formData["__EVENTTARGET"] = "ctl00$ctl00$cphContent$cphContent$pagerList";
                formData["__EVENTARGUMENT"] = pageNumber.ToString();
                formData["__LASTFOCUS"] = "";
                
                // hidPage는 업데이트하지 않음! 현재 페이지를 유지해야 함
                
                // 상위 레벨 필드들 추가 (브라우저에서 확인)
                var mailFID = formData.GetValueOrDefault("ctl00$ctl00$cphContent$cphContent$hidMailFID", "");
                formData["ctl00$ctl00$cphContent$hidSystem"] = "Mail";
                formData["ctl00$ctl00$cphContent$hidFID"] = mailFID;
                formData["ctl00$ctl00$cphContent$hidLeftMenuPopupOption"] = "WINDOW";
                formData["ctl00$ctl00$cphContent$hidMailWritable"] = "Y";
                formData["ctl00$ctl00$cphContent$hidUseMailTreeAsync"] = "N";
                formData["ctl00$ctl00$cphContent$hidMailFolderPath"] = "";
                formData["ctl00$ctl00$hidLoginSelected"] = "Default";
                formData["ctl00$ctl00$hidDevHelperInfo"] = "";
                
                // gadget 필드들
                formData["ctl00$ctl00$gadgetLeft$hidQuickMenuConf"] = "";
                formData["ctl00$ctl00$gadgetType05$hidQuickMenuConf"] = "";
                
                System.Diagnostics.Debug.WriteLine($"===== 메일 페이지 이동 (전체 필드 복사) =====");
                System.Diagnostics.Debug.WriteLine($"요청 페이지: {pageNumber}");
                System.Diagnostics.Debug.WriteLine($"POST 필드 개수: {formData.Count}");
                System.Diagnostics.Debug.WriteLine($"__EVENTTARGET: {formData.GetValueOrDefault("__EVENTTARGET")}");
                System.Diagnostics.Debug.WriteLine($"__EVENTARGUMENT: {formData.GetValueOrDefault("__EVENTARGUMENT")}");
                System.Diagnostics.Debug.WriteLine($"complete: {formData.GetValueOrDefault("complete")}");
                System.Diagnostics.Debug.WriteLine($"hidPage: {formData.GetValueOrDefault("ctl00$ctl00$cphContent$cphContent$hidPage")}");
                
                // POST 페이로드를 파일로 저장
                var payload = string.Join("\n", formData.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key} = {kvp.Value}"));
                File.WriteAllText($"mail_post_payload_page_{pageNumber}.txt", payload);
                System.Diagnostics.Debug.WriteLine($"POST 페이로드 저장됨: mail_post_payload_page_{pageNumber}.txt");
                System.Diagnostics.Debug.WriteLine($"==========================================");

                var content = new FormUrlEncodedContent(formData);
                
                System.Diagnostics.Debug.WriteLine($"POST 요청 URL: {mailUrl}");
                
                // Referer 헤더 추가 (브라우저처럼)
                var request = new HttpRequestMessage(HttpMethod.Post, mailUrl);
                request.Content = content;
                request.Headers.Referrer = new Uri(mailUrl);
                var originUri = new Uri(mailUrl);
                request.Headers.Add("Origin", $"{originUri.Scheme}://{originUri.Host}");
                
                System.Diagnostics.Debug.WriteLine($"Referer: {request.Headers.Referrer}");
                System.Diagnostics.Debug.WriteLine($"Origin: {originUri.Scheme}://{originUri.Host}");
                
                var response = await httpClient.SendAsync(request);
                var html = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"응답 HTML 길이: {html.Length}");
                
                // 디버깅용: 응답 HTML 저장
                File.WriteAllText($"mail_page_{pageNumber}_response.html", html);
                System.Diagnostics.Debug.WriteLine($"응답 HTML 저장됨: mail_page_{pageNumber}_response.html");

                lastMailPageHtml = html;
                var result = MailCrawler.ParseMailPageResult(html);
                
                System.Diagnostics.Debug.WriteLine($"파싱 결과 - 페이지: {result.CurrentPage}, 메일 개수: {result.Items.Count}");
                
                return result;
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

        public async Task<BoardDetail> GetBoardDetailAsync(string detailUrl, string boardPageUrl = "", string viewState = "", string viewStateGenerator = "", string eventValidation = "")
        {
            try
            {
                string html;
                
                System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] URL: {detailUrl}");
                System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] boardPageUrl: {boardPageUrl}");
                
                // javascript: 링크인 경우 PostBack으로 처리
                if (detailUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    // javascript:__doPostBack('ctl00$ctl00$...','') 형태에서 파라미터 추출
                    var match = System.Text.RegularExpressions.Regex.Match(detailUrl, @"__doPostBack\('([^']+)','([^']*)'\)");
                    
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] Regex 매칭 성공: {match.Success}");
                    
                    if (!match.Success)
                    {
                        throw new Exception($"지원하지 않는 JavaScript 링크입니다.\n\n링크: {detailUrl}\n\n이 게시판은 JavaScript 함수를 사용하므로 상세 내용을 볼 수 없습니다.");
                    }
                    
                    if (string.IsNullOrEmpty(boardPageUrl))
                    {
                        throw new Exception("게시판 페이지 URL이 필요합니다.");
                    }

                    var eventTarget = match.Groups[1].Value;
                    var eventArgument = match.Groups[2].Value;
                    
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] EventTarget: {eventTarget}");
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] EventArgument: {eventArgument}");

                    // POST 요청으로 게시글 내용 가져오기
                    var formData = new Dictionary<string, string>
                    {
                        ["__VIEWSTATE"] = viewState ?? "",
                        ["__VIEWSTATEGENERATOR"] = viewStateGenerator ?? "",
                        ["__EVENTVALIDATION"] = eventValidation ?? "",
                        ["__EVENTTARGET"] = eventTarget,
                        ["__EVENTARGUMENT"] = eventArgument
                    };

                    var content = new FormUrlEncodedContent(formData);
                    var response = await httpClient.PostAsync(boardPageUrl, content);
                    html = await response.Content.ReadAsStringAsync();
                    
                    // 디버깅용으로 응답 저장
                    var savePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "board_detail_response.html");
                    System.IO.File.WriteAllText(savePath, html);
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 응답 HTML 저장됨: {savePath}");
                }
                else
                {
                    // 일반 URL인 경우 GET 요청
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 일반 URL로 GET 요청");
                    var response = await httpClient.GetAsync(detailUrl);
                    html = await response.Content.ReadAsStringAsync();
                    
                    // 디버깅용으로 응답 저장
                    var savePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "board_detail_response.html");
                    System.IO.File.WriteAllText(savePath, html);
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 응답 HTML 저장됨: {savePath}");
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var detail = new BoardDetail();
                detail.Url = detailUrl;

                // 제목 찾기 - 여러 패턴 시도
                var titleNode = doc.DocumentNode.SelectSingleNode("//span[@class='txt_bb14_view']")
                             ?? doc.DocumentNode.SelectSingleNode("//span[@id='lblSubject']") 
                             ?? doc.DocumentNode.SelectSingleNode("//td[contains(@class,'subject')]")
                             ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'subject')]")
                             ?? doc.DocumentNode.SelectSingleNode("//h4/span[1]");
                
                if (titleNode != null)
                {
                    detail.Title = System.Net.WebUtility.HtmlDecode(titleNode.InnerText.Trim());
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 제목 찾음: {detail.Title}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 제목 노드를 찾지 못함");
                }

                // 번호 찾기
                var numberNode = doc.DocumentNode.SelectSingleNode("//span[@id='lblMessageID']");
                if (numberNode != null)
                {
                    detail.Number = System.Net.WebUtility.HtmlDecode(numberNode.InnerText.Trim());
                }

                // 작성자 찾기
                var authorNode = doc.DocumentNode.SelectSingleNode("//span[@id='lblCreateUser']")
                              ?? doc.DocumentNode.SelectSingleNode("//span[contains(@id,'CreateUser')]")
                              ?? doc.DocumentNode.SelectSingleNode("//td[contains(@class,'writer')]//span");
                if (authorNode != null)
                {
                    detail.Author = System.Net.WebUtility.HtmlDecode(authorNode.InnerText.Trim());
                }

                // 작성일 찾기
                var dateNode = doc.DocumentNode.SelectSingleNode("//span[@id='lblCreateDate']")
                            ?? doc.DocumentNode.SelectSingleNode("//span[contains(@id,'CreateDate')]")
                            ?? doc.DocumentNode.SelectSingleNode("//td[contains(@class,'date')]");
                if (dateNode != null)
                {
                    detail.Date = System.Net.WebUtility.HtmlDecode(dateNode.InnerText.Trim());
                }

                // 본문 내용 찾기 - 여러 패턴 시도
                var contentNode = doc.DocumentNode.SelectSingleNode("//span[@id='dext_body']")
                               ?? doc.DocumentNode.SelectSingleNode("//div[@id='divContent']")
                               ?? doc.DocumentNode.SelectSingleNode("//div[@id='divMessageContent']")
                               ?? doc.DocumentNode.SelectSingleNode("//td[contains(@class,'content')]")
                               ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content')]")
                               ?? doc.DocumentNode.SelectSingleNode("//div[@class='message-content']");

                if (contentNode != null)
                {
                    // HTML 태그를 유지한 채로 가져오기 (표, 이미지 등 보존)
                    var content = contentNode.InnerHtml;
                    
                    // HTML 엔티티는 디코딩하지 않음 (브라우저가 처리)
                    content = content.Trim();
                    
                    detail.Content = content;
                    
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 본문 HTML 길이: {content.Length}자");
                }
                else
                {
                    // 본문을 찾지 못한 경우
                    detail.Content = "본문 내용을 찾을 수 없습니다.";
                    System.Diagnostics.Debug.WriteLine($"[게시글 상세조회] 본문 노드를 찾지 못함");
                }

                return detail;
            }
            catch (Exception ex)
            {
                throw new Exception($"게시글 상세 조회 실패: {ex.Message}", ex);
            }
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

    public class BoardDetail
    {
        public string Number { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Date { get; set; } = "";
        public string Content { get; set; } = "";
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

