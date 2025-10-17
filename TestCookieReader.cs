using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace AgentAssistant
{
    public class TestCookieReader
    {
        public static string GetAllCookieDomains()
        {
            var result = "";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            var paths = new[]
            {
                Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Network\Cookies"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cookies"),
                Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Network\Cookies"),
            };
            
            foreach (var cookiePath in paths)
            {
                result += $"\n경로: {cookiePath}\n";
                result += $"존재: {File.Exists(cookiePath)}\n";
                
                if (!File.Exists(cookiePath))
                    continue;
                
                var tempPath = Path.GetTempFileName();
                try
                {
                    // FileShare.ReadWrite로 읽기 - Chrome 켜진 상태에서도 작동
                    bool copySuccess = false;
                    for (int i = 0; i < 20; i++)
                    {
                        try
                        {
                            using (var sourceStream = new FileStream(cookiePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var destStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                sourceStream.CopyTo(destStream);
                            }
                            copySuccess = true;
                            result += $"  쿠키 파일 복사 성공 ({i + 1}번째 시도)\n\n";
                            break;
                        }
                        catch (IOException ex)
                        {
                            if (i < 19)
                            {
                                System.Threading.Thread.Sleep(500);
                            }
                            else
                            {
                                result += $"  복사 실패 (20번 시도 후): {ex.Message}\n\n";
                                throw;
                            }
                        }
                    }
                    
                    if (!copySuccess)
                    {
                        result += "  쿠키 파일 복사 실패\n\n";
                        continue;
                    }
                    
                    using (var conn = new SQLiteConnection($"Data Source={tempPath};Version=3;"))
                    {
                        conn.Open();
                        
                        // cauhs가 포함된 모든 쿠키 찾기
                        var command = conn.CreateCommand();
                        command.CommandText = "SELECT DISTINCT host_key FROM cookies WHERE host_key LIKE '%cauhs%'";
                        
                        result += "\n[cauhs 관련 쿠키 도메인]\n";
                        using (var reader = command.ExecuteReader())
                        {
                            int count = 0;
                            while (reader.Read())
                            {
                                var hostKey = reader["host_key"].ToString();
                                result += $"  {++count}. {hostKey}\n";
                            }
                            
                            if (count == 0)
                            {
                                result += "  (없음)\n";
                            }
                        }
                        
                        // 각 도메인의 쿠키 개수
                        command.CommandText = "SELECT host_key, COUNT(*) as cnt FROM cookies WHERE host_key LIKE '%cauhs%' GROUP BY host_key";
                        result += "\n[쿠키 개수]\n";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result += $"  {reader["host_key"]}: {reader["cnt"]}개\n";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result += $"오류: {ex.Message}\n";
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
            
            return result;
        }
    }
}

