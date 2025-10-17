using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AgentAssistant
{
    public class ChromeCookieReader
    {
        private static List<string> GetAllChromeCookiePaths()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var allPaths = new List<string>();
            
            // Chrome과 Edge의 모든 프로필 스캔
            var basePaths = new[]
            {
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
            };
            
            foreach (var basePath in basePaths)
            {
                if (Directory.Exists(basePath))
                {
                    // Default 프로필
                    var defaultPaths = new[]
                    {
                        Path.Combine(basePath, @"Default\Network\Cookies"),
                        Path.Combine(basePath, @"Default\Cookies"),
                    };
                    allPaths.AddRange(defaultPaths.Where(File.Exists));
                    
                    // Profile 1, 2, 3... 등 모든 프로필
                    for (int i = 1; i <= 10; i++)
                    {
                        var profilePaths = new[]
                        {
                            Path.Combine(basePath, $@"Profile {i}\Network\Cookies"),
                            Path.Combine(basePath, $@"Profile {i}\Cookies"),
                        };
                        allPaths.AddRange(profilePaths.Where(File.Exists));
                    }
                }
            }
            
            return allPaths;
        }

        private static string GetChromeCookiePath()
        {
            var paths = GetAllChromeCookiePaths();
            return paths.FirstOrDefault() ?? "";
        }

        public static (List<Cookie> cookies, string debugInfo) ReadCookiesWithDebug(string domain)
        {
            var allCookies = new List<Cookie>();
            var debugInfo = "";
            var cookiePaths = GetAllChromeCookiePaths();

            debugInfo += $"스캔할 쿠키 파일 개수: {cookiePaths.Count}\n\n";
            
            foreach (var cookiePath in cookiePaths)
            {
                debugInfo += $"파일: {cookiePath}\n";
                debugInfo += $"존재: {File.Exists(cookiePath)}\n";
                
                try
                {
                    var cookies = ReadCookiesFromFileWithDebug(cookiePath, domain, out string fileDebug);
                    debugInfo += fileDebug;
                    if (cookies.Count > 0)
                    {
                        debugInfo += $"✓ {cookies.Count}개 쿠키 발견!\n\n";
                        allCookies.AddRange(cookies);
                    }
                    else
                    {
                        debugInfo += "✗ 해당 도메인 쿠키 없음\n\n";
                    }
                }
                catch (Exception ex)
                {
                    debugInfo += $"✗ 오류: {ex.Message}\n\n";
                }
            }
            
            return (allCookies, debugInfo);
        }
        
        private static List<Cookie> ReadCookiesFromFileWithDebug(string cookiePath, string domain, out string debugInfo)
        {
            var cookies = new List<Cookie>();
            debugInfo = "";
            
            if (!File.Exists(cookiePath))
            {
                debugInfo += "파일 없음\n";
                return cookies;
            }

            var tempPath = Path.GetTempFileName();
            try
            {
                // FileShare.ReadWrite로 복사
                using (var sourceStream = new FileStream(cookiePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var destStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(destStream);
                }
                debugInfo += "파일 복사 성공\n";

                using (var conn = new SQLiteConnection($"Data Source={tempPath};Version=3;"))
                {
                    conn.Open();

                    // 전체 쿠키 개수
                    var countCmd = conn.CreateCommand();
                    countCmd.CommandText = "SELECT COUNT(*) FROM cookies";
                    var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    debugInfo += $"전체 쿠키 개수: {totalCount}\n";

                    // ngw로 시작하는 도메인들
                    var ngwCmd = conn.CreateCommand();
                    ngwCmd.CommandText = "SELECT DISTINCT host_key FROM cookies WHERE host_key LIKE '%ngw%'";
                    using (var reader = ngwCmd.ExecuteReader())
                    {
                        debugInfo += "ngw 포함 도메인:\n";
                        while (reader.Read())
                        {
                            debugInfo += $"  - {reader["host_key"]}\n";
                        }
                    }
                    
                    // cauhs 포함 도메인들
                    var cauhsCmd = conn.CreateCommand();
                    cauhsCmd.CommandText = "SELECT DISTINCT host_key FROM cookies WHERE host_key LIKE '%cauhs%'";
                    using (var reader = cauhsCmd.ExecuteReader())
                    {
                        debugInfo += "cauhs 포함 도메인:\n";
                        while (reader.Read())
                        {
                            debugInfo += $"  - {reader["host_key"]}\n";
                        }
                    }

                    // 실제 쿠키 검색
                    var command = conn.CreateCommand();
                    command.CommandText = @"
                        SELECT name, encrypted_value, host_key, path, expires_utc, is_secure, is_httponly 
                        FROM cookies 
                        WHERE host_key LIKE @domain OR host_key LIKE @domainDot";
                    command.Parameters.AddWithValue("@domain", $"%{domain}%");
                    command.Parameters.AddWithValue("@domainDot", $"%.{domain}%");

                    using (var reader = command.ExecuteReader())
                    {
                        int count = 0;
                        while (reader.Read())
                        {
                            count++;
                            try
                            {
                                var name = reader["name"].ToString();
                                var encryptedValue = (byte[])reader["encrypted_value"];
                                var hostKey = reader["host_key"].ToString();
                                var path = reader["path"].ToString();

                                string value = DecryptChromeValue(encryptedValue);

                                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                                {
                                    var cookie = new Cookie(name, value, path, hostKey);
                                    cookies.Add(cookie);
                                }
                            }
                            catch { }
                        }
                        debugInfo += $"검색된 쿠키: {count}개\n";
                    }
                }
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
            
            return cookies;
        }

        public static List<Cookie> ReadCookies(string domain)
        {
            var allCookies = new List<Cookie>();
            var cookiePaths = GetAllChromeCookiePaths();

            System.Diagnostics.Debug.WriteLine($"스캔할 쿠키 파일 개수: {cookiePaths.Count}");

            if (cookiePaths.Count == 0)
            {
                throw new FileNotFoundException("Chrome/Edge 쿠키 파일을 찾을 수 없습니다.");
            }
            
            // 모든 프로필에서 쿠키 검색
            foreach (var cookiePath in cookiePaths)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"쿠키 파일 확인 중: {cookiePath}");
                    var cookies = ReadCookiesFromFile(cookiePath, domain);
                    if (cookies.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  → {cookies.Count}개 쿠키 발견!");
                        allCookies.AddRange(cookies);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  → 오류: {ex.Message}");
                    // 한 프로필 실패해도 다음 프로필 계속 시도
                }
            }
            
            return allCookies;
        }

        private static List<Cookie> ReadCookiesFromFile(string cookiePath, string domain)
        {
            var cookies = new List<Cookie>();
            
            if (!File.Exists(cookiePath))
            {
                return cookies;
            }

            // Chrome이 실행 중이면 파일이 잠겨있을 수 있으므로 임시 복사 (강력한 재시도 로직)
            var tempPath = Path.GetTempFileName();
            try
            {
                // 파일이 잠겨있을 수 있으므로 여러 번 재시도 (최대 20회, 총 10초)
                bool copySuccess = false;
                int retryCount = 20;
                
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        // FileShare.ReadWrite로 읽기 전용 공유 모드로 복사 시도
                        using (var sourceStream = new FileStream(cookiePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var destStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            sourceStream.CopyTo(destStream);
                        }
                        copySuccess = true;
                        System.Diagnostics.Debug.WriteLine($"쿠키 파일 복사 성공 ({i + 1}번째 시도)");
                        break;
                    }
                    catch (IOException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"쿠키 복사 시도 {i + 1}/{retryCount} 실패: {ex.Message}");
                        if (i < retryCount - 1)
                        {
                            System.Threading.Thread.Sleep(500); // 500ms 대기 후 재시도
                        }
                        else
                        {
                            throw new IOException($"쿠키 파일 복사 실패 ({retryCount}번 시도). Chrome/Edge를 잠시 닫아주세요.\n\n상세: {ex.Message}");
                        }
                    }
                }
                
                if (!copySuccess)
                {
                    throw new IOException("쿠키 파일을 복사할 수 없습니다. 브라우저를 잠시 닫아주세요.");
                }

                using (var conn = new SQLiteConnection($"Data Source={tempPath};Version=3;"))
                {
                    conn.Open();

                    // 먼저 DB에 있는 모든 쿠키 확인 (디버깅용)
                    var allCookiesCmd = conn.CreateCommand();
                    allCookiesCmd.CommandText = "SELECT host_key, name, COUNT(*) as cnt FROM cookies GROUP BY host_key, name LIMIT 100";
                    using (var allReader = allCookiesCmd.ExecuteReader())
                    {
                        System.Diagnostics.Debug.WriteLine($"=== 쿠키 DB 전체 내용 (파일: {cookiePath}) ===");
                        while (allReader.Read())
                        {
                            var hostKey = allReader["host_key"].ToString();
                            var name = allReader["name"].ToString();
                            System.Diagnostics.Debug.WriteLine($"  - 도메인: [{hostKey}], 이름: [{name}]");
                        }
                    }

                    var command = conn.CreateCommand();
                    command.CommandText = @"
                        SELECT name, encrypted_value, host_key, path, expires_utc, is_secure, is_httponly 
                        FROM cookies 
                        WHERE host_key LIKE @domain OR host_key LIKE @domainDot";
                    command.Parameters.AddWithValue("@domain", $"%{domain}%");
                    command.Parameters.AddWithValue("@domainDot", $"%.{domain}%");
                    
                    System.Diagnostics.Debug.WriteLine($"쿠키 검색 도메인: {domain}");
                    System.Diagnostics.Debug.WriteLine($"검색 쿼리: WHERE host_key LIKE '%{domain}%' OR host_key LIKE '%.{domain}%'");

                    using (var reader = command.ExecuteReader())
                    {
                        int totalCount = 0;
                        int successCount = 0;
                        
                        while (reader.Read())
                        {
                            totalCount++;
                            try
                            {
                                var name = reader["name"].ToString();
                                var encryptedValue = (byte[])reader["encrypted_value"];
                                var hostKey = reader["host_key"].ToString();
                                var path = reader["path"].ToString();
                                var expiresUtc = reader["expires_utc"] != DBNull.Value 
                                    ? Convert.ToInt64(reader["expires_utc"]) 
                                    : 0;
                                var isSecure = Convert.ToBoolean(reader["is_secure"]);
                                var isHttpOnly = Convert.ToBoolean(reader["is_httponly"]);

                                // 복호화
                                string value = DecryptChromeValue(encryptedValue);

                                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                                {
                                    var cookie = new Cookie(name, value, path, hostKey)
                                    {
                                        Secure = isSecure,
                                        HttpOnly = isHttpOnly
                                    };

                                    // Chrome의 expires_utc를 DateTime으로 변환
                                    if (expiresUtc > 0)
                                    {
                                        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                                        var expiry = epoch.AddMicroseconds(expiresUtc);
                                        if (expiry > DateTime.UtcNow)
                                        {
                                            cookie.Expires = expiry;
                                        }
                                    }

                                    cookies.Add(cookie);
                                    successCount++;
                                    System.Diagnostics.Debug.WriteLine($"쿠키 읽기 성공: {name} = {value.Substring(0, Math.Min(20, value.Length))}...");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"쿠키 읽기 오류: {ex.Message}");
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"총 {totalCount}개 중 {successCount}개 쿠키 읽기 성공");
                    }
                }
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            return cookies;
        }

        private static string DecryptChromeValue(byte[] encryptedData)
        {
            try
            {
                // Chrome v80+ uses "v10" prefix
                if (encryptedData.Length > 3 && 
                    encryptedData[0] == 'v' && 
                    encryptedData[1] == '1' && 
                    encryptedData[2] == '0')
                {
                    // AES-256-GCM 복호화 (Chrome v80+)
                    // 여기서는 간단하게 DPAPI만 시도
                    var data = encryptedData.Skip(3).ToArray();
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
                }
                else
                {
                    // 이전 버전의 Chrome: DPAPI로 직접 복호화
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser));
                }
            }
            catch
            {
                // 복호화 실패 시 빈 문자열 반환
                return string.Empty;
            }
        }
    }
}

