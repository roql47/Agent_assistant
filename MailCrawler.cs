using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace AgentAssistant
{
    public class MailItem
    {
        public string Subject { get; set; } = "";
        public string Sender { get; set; } = "";
        public string Receiver { get; set; } = "";
        public string Date { get; set; } = "";
        public string Size { get; set; } = "";
        public bool IsRead { get; set; }
        public bool HasAttachment { get; set; }
    }

    public class MailFolder
    {
        public string Name { get; set; } = "";
        public string Fid { get; set; } = "";
        public bool IsSentItems { get; set; }
    }

    public class MailPageResult
    {
        public List<MailItem> Items { get; set; } = new List<MailItem>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string ViewState { get; set; } = "";
        public string ViewStateGenerator { get; set; } = "";
        public string EventValidation { get; set; } = "";
        public string MailFID { get; set; } = "";
        public string IsSentItems { get; set; } = "N";
        public string MailItemInfo { get; set; } = "";
        public Dictionary<string, string> AllFormFields { get; set; } = new Dictionary<string, string>();
    }

    public class MailCrawler
    {
        public static List<MailFolder> ParseMailFolders(string html)
        {
            var folders = new List<MailFolder>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 왼쪽 메뉴의 메일 폴더 찾기
            var folderNodes = doc.DocumentNode.SelectNodes("//li[@id and contains(@id, 'leftMail_') and @fid]");
            
            if (folderNodes != null)
            {
                foreach (var node in folderNodes)
                {
                    var folder = new MailFolder
                    {
                        Fid = node.GetAttributeValue("fid", ""),
                        IsSentItems = node.GetAttributeValue("sentitemsyn", "").Equals("Y", StringComparison.OrdinalIgnoreCase)
                    };
                    
                    // 폴더 이름 추출
                    var nameNode = node.SelectSingleNode(".//span[contains(@id, 'spanMailMenuText_')]");
                    if (nameNode != null)
                    {
                        folder.Name = nameNode.InnerText.Trim();
                        // "(숫자)" 제거
                        var idx = folder.Name.IndexOf('(');
                        if (idx > 0)
                        {
                            folder.Name = folder.Name.Substring(0, idx).Trim();
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(folder.Fid))
                    {
                        folders.Add(folder);
                        System.Diagnostics.Debug.WriteLine($"폴더 발견: {folder.Name} (SentItems={folder.IsSentItems})");
                    }
                }
            }
            
            return folders;
        }

        public static (List<MailItem> items, string debugInfo) ParseMailListHtmlWithDebug(string html)
        {
            var mailItems = new List<MailItem>();
            var debugInfo = "";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            debugInfo += $"HTML 길이: {html.Length}\n";
            debugInfo += $"HTML 시작: {html.Substring(0, Math.Min(200, html.Length))}...\n\n";

            // tbody의 모든 tr 찾기
            var rows = doc.DocumentNode.SelectNodes("//tbody/tr[@id and contains(@id, 'trMailItem')]");
            
            debugInfo += $"찾은 행 수: {rows?.Count ?? 0}\n";
            
            if (rows == null)
            {
                debugInfo += "⚠️ trMailItem 요소를 찾을 수 없음\n";
                return (mailItems, debugInfo);
            }

            return (ParseMailListHtml(html), debugInfo);
        }

        public static List<MailItem> ParseMailListHtml(string html)
        {
            var mailItems = new List<MailItem>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // tbody의 모든 tr 찾기
            var rows = doc.DocumentNode.SelectNodes("//tbody/tr[@id and contains(@id, 'trMailItem')]");
            
            if (rows == null)
            {
                System.Diagnostics.Debug.WriteLine("메일 행을 찾을 수 없음");
                return mailItems;
            }
            
            System.Diagnostics.Debug.WriteLine($"파싱할 메일 개수: {rows.Count}");

            foreach (var row in rows)
            {
                try
                {
                    var mailItem = new MailItem();

                    // subject, sender, receivedate 속성에서 추출
                    var checkbox = row.SelectSingleNode(".//input[@name='chkSelect']");
                    if (checkbox != null)
                    {
                        mailItem.Subject = checkbox.GetAttributeValue("subject", "");
                        mailItem.Sender = checkbox.GetAttributeValue("sender", "");
                        mailItem.Receiver = checkbox.GetAttributeValue("receiver", "");
                        mailItem.Date = checkbox.GetAttributeValue("receivedate", "");
                        mailItem.Size = checkbox.GetAttributeValue("size", "");
                        
                        // 날짜 포맷 정리 (2025-10-17 1800 -> 2025-10-17 18:00)
                        if (mailItem.Date.Length >= 13)
                        {
                            var datePart = mailItem.Date.Substring(0, 11); // "2025-10-17 "
                            var timePart = mailItem.Date.Substring(11); // "1800"
                            if (timePart.Length == 4)
                            {
                                mailItem.Date = $"{datePart}{timePart.Substring(0, 2)}:{timePart.Substring(2, 2)}";
                            }
                        }
                        
                        // 크기 포맷 정리 (바이트 -> KB/MB)
                        if (long.TryParse(mailItem.Size, out long sizeBytes))
                        {
                            if (sizeBytes >= 1024 * 1024)
                            {
                                mailItem.Size = $"{sizeBytes / (1024.0 * 1024.0):F1} MB";
                            }
                            else if (sizeBytes >= 1024)
                            {
                                mailItem.Size = $"{sizeBytes / 1024.0:F1} KB";
                            }
                            else
                            {
                                mailItem.Size = $"{sizeBytes} B";
                            }
                        }
                    }

                    // 읽음 상태 확인
                    var imgState = row.SelectSingleNode(".//img[contains(@id, 'imgState_')]");
                    if (imgState != null)
                    {
                        var src = imgState.GetAttributeValue("src", "");
                        mailItem.IsRead = src.Contains("read.gif");
                    }

                    // 첨부파일 확인
                    var tdAttachment = row.SelectSingleNode(".//td[@name='tdAttachment']");
                    if (tdAttachment != null)
                    {
                        mailItem.HasAttachment = tdAttachment.InnerHtml.Contains("ico_webmailaddfile");
                    }

                    if (!string.IsNullOrEmpty(mailItem.Subject))
                    {
                        mailItems.Add(mailItem);
                        System.Diagnostics.Debug.WriteLine($"메일 파싱 성공: {mailItem.Subject}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"메일 파싱 오류: {ex.Message}");
                    // 파싱 오류 무시하고 계속
                }
            }

            System.Diagnostics.Debug.WriteLine($"총 {mailItems.Count}개 메일 파싱 완료");
            return mailItems;
        }

        public static MailPageResult ParseMailPageResult(string html)
        {
            var result = new MailPageResult();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 메일 목록 파싱
            result.Items = ParseMailListHtml(html);

            // ViewState 추출
            result.ViewState = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATE']")?.GetAttributeValue("value", "") ?? "";
            result.ViewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@id='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", "") ?? "";
            result.EventValidation = doc.DocumentNode.SelectSingleNode("//input[@id='__EVENTVALIDATION']")?.GetAttributeValue("value", "") ?? "";
            
            // 메일함 필수 정보 추출
            result.MailFID = doc.DocumentNode.SelectSingleNode("//input[@id='cphContent_cphContent_hidMailFID']")?.GetAttributeValue("value", "") ?? "";
            result.IsSentItems = doc.DocumentNode.SelectSingleNode("//input[@id='cphContent_cphContent_hidIsSentItems']")?.GetAttributeValue("value", "N") ?? "N";
            result.MailItemInfo = doc.DocumentNode.SelectSingleNode("//input[@id='cphContent_cphContent_hidMailItemInfo']")?.GetAttributeValue("value", "") ?? "";
            
            // 모든 form 필드 추출
            result.AllFormFields = ExtractAllFormFields(html);

            // 현재 페이지 추출
            var hidPageNode = doc.DocumentNode.SelectSingleNode("//input[@id='cphContent_cphContent_hidPage']");
            if (hidPageNode != null && int.TryParse(hidPageNode.GetAttributeValue("value", "1"), out int currentPage))
            {
                result.CurrentPage = currentPage;
            }
            else
            {
                result.CurrentPage = 1;
            }

            // 총 페이지 수 추출 (페이징 정보에서)
            var pagingText = doc.DocumentNode.SelectSingleNode("//td[@class='page_go']")?.InnerText ?? "";
            var match = System.Text.RegularExpressions.Regex.Match(pagingText, @"총 (\d+)페이지");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int totalPages))
            {
                result.TotalPages = totalPages;
            }
            else
            {
                result.TotalPages = 1;
            }

            System.Diagnostics.Debug.WriteLine($"페이지 파싱: {result.CurrentPage}/{result.TotalPages}, 메일 {result.Items.Count}개");
            System.Diagnostics.Debug.WriteLine($"HTML 길이: {html.Length}");

            return result;
        }

        public static Dictionary<string, string> ExtractAllFormFields(string html)
        {
            var formData = new Dictionary<string, string>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. 모든 input 요소 추출 (hidden, text, checkbox 등 모든 타입)
            var inputs = doc.DocumentNode.SelectNodes("//input[@name]");
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    var name = input.GetAttributeValue("name", "");
                    var type = input.GetAttributeValue("type", "text").ToLower();
                    var value = input.GetAttributeValue("value", "");
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        // checkbox나 radio는 checked 상태일 때만 포함
                        if (type == "checkbox" || type == "radio")
                        {
                            var isChecked = input.GetAttributeValue("checked", null) != null;
                            if (isChecked)
                            {
                                formData[name] = value;
                            }
                        }
                        else if (type != "submit" && type != "button" && type != "image")
                        {
                            // submit, button, image 타입은 제외
                            formData[name] = value;
                        }
                    }
                }
            }

            // 2. 모든 select 요소 추출 (selected option 값)
            var selects = doc.DocumentNode.SelectNodes("//select[@name]");
            if (selects != null)
            {
                foreach (var select in selects)
                {
                    var name = select.GetAttributeValue("name", "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        // selected option 찾기
                        var selectedOption = select.SelectSingleNode(".//option[@selected]");
                        if (selectedOption != null)
                        {
                            formData[name] = selectedOption.GetAttributeValue("value", "");
                        }
                        else
                        {
                            // selected가 없으면 첫 번째 option
                            var firstOption = select.SelectSingleNode(".//option");
                            if (firstOption != null)
                            {
                                formData[name] = firstOption.GetAttributeValue("value", "");
                            }
                        }
                    }
                }
            }

            // 3. 모든 textarea 요소 추출
            var textareas = doc.DocumentNode.SelectNodes("//textarea[@name]");
            if (textareas != null)
            {
                foreach (var textarea in textareas)
                {
                    var name = textarea.GetAttributeValue("name", "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        formData[name] = textarea.InnerText;
                    }
                }
            }

            return formData;
        }
    }
}


