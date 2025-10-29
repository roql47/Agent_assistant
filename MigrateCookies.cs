using System;
using System.IO;
using System.Windows;

namespace AgentAssistant
{
    /// <summary>
    /// 평문 쿠키 파일을 암호화된 파일로 마이그레이션하는 유틸리티
    /// </summary>
    public static class CookieMigrationHelper
    {
        /// <summary>
        /// 프로그램 시작 시 자동으로 평문 쿠키 파일을 암호화 파일로 마이그레이션
        /// </summary>
        public static void AutoMigrate()
        {
            try
            {
                // manual_cookies.json → manual_cookies.dat 마이그레이션
                if (File.Exists("manual_cookies.json") && !File.Exists("manual_cookies.dat"))
                {
                    bool success = CookieEncryption.MigratePlainToEncrypted("manual_cookies.json", "manual_cookies.dat");
                    
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine("[보안] manual_cookies.json → manual_cookies.dat 마이그레이션 완료");
                        
                        // 평문 파일 백업 후 삭제
                        try
                        {
                            File.Move("manual_cookies.json", "manual_cookies.json.backup");
                            System.Diagnostics.Debug.WriteLine("[보안] 평문 파일 백업 완료: manual_cookies.json.backup");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[보안] 평문 파일 백업 실패: {ex.Message}");
                        }
                    }
                }
                
                // manual_cookies2.json도 마이그레이션
                if (File.Exists("manual_cookies2.json"))
                {
                    bool success = CookieEncryption.MigratePlainToEncrypted("manual_cookies2.json", "manual_cookies2.dat");
                    
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine("[보안] manual_cookies2.json → manual_cookies2.dat 마이그레이션 완료");
                        
                        try
                        {
                            File.Move("manual_cookies2.json", "manual_cookies2.json.backup");
                            System.Diagnostics.Debug.WriteLine("[보안] 평문 파일 백업 완료: manual_cookies2.json.backup");
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안] 자동 마이그레이션 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 수동으로 마이그레이션 실행 (메뉴나 설정에서 호출)
        /// </summary>
        public static void ManualMigrate()
        {
            try
            {
                int migratedCount = 0;
                string message = "";

                // manual_cookies.json 마이그레이션
                if (File.Exists("manual_cookies.json"))
                {
                    bool success = CookieEncryption.MigratePlainToEncrypted("manual_cookies.json", "manual_cookies.dat");
                    if (success)
                    {
                        migratedCount++;
                        message += "✓ manual_cookies.json → manual_cookies.dat\n";
                        
                        // 백업
                        try
                        {
                            File.Move("manual_cookies.json", "manual_cookies.json.backup", true);
                        }
                        catch { }
                    }
                }

                // manual_cookies2.json 마이그레이션
                if (File.Exists("manual_cookies2.json"))
                {
                    bool success = CookieEncryption.MigratePlainToEncrypted("manual_cookies2.json", "manual_cookies2.dat");
                    if (success)
                    {
                        migratedCount++;
                        message += "✓ manual_cookies2.json → manual_cookies2.dat\n";
                        
                        try
                        {
                            File.Move("manual_cookies2.json", "manual_cookies2.json.backup", true);
                        }
                        catch { }
                    }
                }

                if (migratedCount > 0)
                {
                    MessageBox.Show(
                        $"🔒 {migratedCount}개의 쿠키 파일을 암호화했습니다!\n\n" +
                        message +
                        "\n원본 파일은 .backup으로 백업되었습니다.\n" +
                        "암호화 방식: Windows DPAPI (현재 사용자만 복호화 가능)",
                        "쿠키 암호화 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "마이그레이션할 평문 쿠키 파일이 없습니다.\n\n" +
                        "이미 암호화가 완료되었거나 쿠키 파일이 존재하지 않습니다.",
                        "알림",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"쿠키 마이그레이션 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 암호화 상태 확인
        /// </summary>
        public static string GetEncryptionStatus()
        {
            string status = "=== 쿠키 파일 암호화 상태 ===\n\n";
            
            // 암호화된 파일
            if (File.Exists("manual_cookies.dat"))
            {
                status += "✅ manual_cookies.dat (암호화됨)\n";
            }
            
            if (File.Exists("manual_cookies2.dat"))
            {
                status += "✅ manual_cookies2.dat (암호화됨)\n";
            }
            
            // 평문 파일 (취약)
            if (File.Exists("manual_cookies.json"))
            {
                status += "⚠️ manual_cookies.json (평문, 취약)\n";
            }
            
            if (File.Exists("manual_cookies2.json"))
            {
                status += "⚠️ manual_cookies2.json (평문, 취약)\n";
            }
            
            // 백업 파일
            if (File.Exists("manual_cookies.json.backup"))
            {
                status += "📦 manual_cookies.json.backup (백업)\n";
            }
            
            status += "\n" + CookieEncryption.GetEncryptionInfo();
            
            return status;
        }
    }
}

