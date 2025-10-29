using System;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;
using HtmlAgilityPack;

namespace AgentAssistant
{
    public partial class BoardDetailWindow : FluentWindow
    {
        private BoardDetail? boardDetail;

        public BoardDetailWindow(BoardDetail detail)
        {
            InitializeComponent();
            this.boardDetail = detail;
            Loaded += BoardDetailWindow_Loaded;
        }

        private void BoardDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 기본 정보와 텍스트 뷰 즉시 표시
            DisplayBasicInfo();
        }

        private void DisplayBasicInfo()
        {
            if (boardDetail == null) return;

            PostNumberText.Text = $"게시글 번호: {boardDetail.Number}";
            TitleText.Text = boardDetail.Title;
            AuthorText.Text = boardDetail.Author;
            DateText.Text = boardDetail.Date;
            UrlText.Text = boardDetail.Url;
            
            // HTML을 텍스트로 변환 (원본 서식 유지)
            var doc = new HtmlDocument();
            doc.LoadHtml(boardDetail.Content);
            
            // HTML 태그 제거하고 텍스트만 추출
            var text = doc.DocumentNode.InnerText;
            
            // HTML 엔티티 디코딩
            text = System.Net.WebUtility.HtmlDecode(text);
            
            // 각 줄의 끝 공백만 제거 (줄바꿈과 들여쓰기는 유지)
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var cleanedLines = lines.Select(line => line.TrimEnd()).ToList();
            
            // 모든 줄을 합치기 (빈 줄 포함)
            text = string.Join("\r\n", cleanedLines);
            
            // 앞뒤 불필요한 공백만 제거
            ContentText.Text = text.Trim();
        }


        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            // 원본 URL을 기본 브라우저로 열기
            if (boardDetail != null && !string.IsNullOrEmpty(boardDetail.Url))
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = boardDetail.Url,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    
                    System.Diagnostics.Debug.WriteLine($"[브라우저 열기] URL: {boardDetail.Url}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[브라우저 열기 실패] {ex.Message}");
                    System.Windows.MessageBox.Show(
                        $"브라우저를 열 수 없습니다.\n\nURL: {boardDetail.Url}\n\n오류: {ex.Message}",
                        "오류",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "게시글 URL이 없습니다.",
                    "알림",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void CopyContent_Click(object sender, RoutedEventArgs e)
        {
            if (boardDetail == null) return;

            try
            {
                var copyText = $"제목: {boardDetail.Title}\n";
                copyText += $"작성자: {boardDetail.Author}\n";
                copyText += $"작성일: {boardDetail.Date}\n";
                copyText += $"\n내용:\n{boardDetail.Content}\n";
                copyText += $"\nURL: {boardDetail.Url}";

                System.Windows.Clipboard.SetText(copyText);
                System.Windows.MessageBox.Show(
                    "게시글 내용이 클립보드에 복사되었습니다.",
                    "복사 완료",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"복사 실패:\n{ex.Message}",
                    "오류",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

