using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AgentAssistant
{
    /// <summary>
    /// Windows DPAPI(Data Protection API)를 사용한 쿠키 암호화/복호화
    /// - 키 관리를 Windows OS가 자동으로 처리
    /// - 현재 로그인한 사용자만 복호화 가능
    /// - 다른 사용자나 다른 컴퓨터에서는 복호화 불가능
    /// - AES-256보다 더 안전하고 간단함
    /// </summary>
    public static class CookieEncryption
    {
        private static readonly byte[] s_additionalEntropy = Encoding.UTF8.GetBytes("AgentAssistant_Cookie_v1.0");

        /// <summary>
        /// 평문을 DPAPI로 암호화
        /// </summary>
        /// <param name="plainText">암호화할 평문</param>
        /// <returns>암호화된 바이트 배열</returns>
        public static byte[] Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return Array.Empty<byte>();

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                
                // DPAPI로 암호화 (CurrentUser 스코프 = 현재 사용자만 복호화 가능)
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    s_additionalEntropy,  // 추가 엔트로피 (Salt 역할)
                    DataProtectionScope.CurrentUser
                );

                System.Diagnostics.Debug.WriteLine($"[보안] DPAPI 암호화 완료 (원본: {plainBytes.Length} bytes → 암호화: {encryptedBytes.Length} bytes)");
                
                return encryptedBytes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 암호화 실패: {ex.Message}");
                throw new CryptographicException("쿠키 암호화에 실패했습니다.", ex);
            }
        }

        /// <summary>
        /// DPAPI로 복호화
        /// </summary>
        /// <param name="encryptedData">암호화된 바이트 배열</param>
        /// <returns>복호화된 평문</returns>
        public static string Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return string.Empty;

            try
            {
                // DPAPI로 복호화
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedData,
                    s_additionalEntropy,
                    DataProtectionScope.CurrentUser
                );

                string plainText = Encoding.UTF8.GetString(decryptedBytes);
                System.Diagnostics.Debug.WriteLine($"[보안] DPAPI 복호화 완료 (암호화: {encryptedData.Length} bytes → 원본: {decryptedBytes.Length} bytes)");
                
                return plainText;
            }
            catch (CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 복호화 실패: {ex.Message}");
                throw new CryptographicException("쿠키 복호화에 실패했습니다. 다른 사용자가 암호화한 파일이거나 손상된 파일입니다.", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 복호화 중 예외: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 평문을 암호화하여 파일로 저장
        /// </summary>
        /// <param name="plainText">암호화할 평문</param>
        /// <param name="filePath">저장할 파일 경로</param>
        public static void EncryptToFile(string plainText, string filePath)
        {
            try
            {
                byte[] encrypted = Encrypt(plainText);
                File.WriteAllBytes(filePath, encrypted);
                System.Diagnostics.Debug.WriteLine($"[보안] 암호화된 파일 저장: {filePath} ({encrypted.Length} bytes)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 파일 저장 실패: {ex.Message}");
                throw new IOException($"암호화된 쿠키 파일 저장에 실패했습니다: {filePath}", ex);
            }
        }

        /// <summary>
        /// 암호화된 파일을 읽어서 복호화
        /// </summary>
        /// <param name="filePath">읽을 파일 경로</param>
        /// <returns>복호화된 평문</returns>
        public static string DecryptFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[보안] 암호화 파일 없음: {filePath}");
                return string.Empty;
            }

            try
            {
                byte[] encrypted = File.ReadAllBytes(filePath);
                System.Diagnostics.Debug.WriteLine($"[보안] 암호화 파일 읽기: {filePath} ({encrypted.Length} bytes)");
                return Decrypt(encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 파일 읽기 실패: {ex.Message}");
                throw new IOException($"암호화된 쿠키 파일 읽기에 실패했습니다: {filePath}", ex);
            }
        }

        /// <summary>
        /// 암호화 정보 확인 (디버깅용)
        /// </summary>
        public static string GetEncryptionInfo()
        {
            return "암호화 방식: Windows DPAPI (Data Protection API)\n" +
                   "스코프: CurrentUser (현재 사용자만 복호화 가능)\n" +
                   "키 관리: Windows OS 자동 관리\n" +
                   "보안 수준: 매우 높음 (타 사용자/컴퓨터에서 복호화 불가)";
        }

        /// <summary>
        /// JSON 쿠키를 암호화하여 저장 (편의 함수)
        /// </summary>
        public static void SaveEncryptedCookies(string cookiesJson, string filePath = "manual_cookies.dat")
        {
            if (string.IsNullOrWhiteSpace(cookiesJson))
            {
                throw new ArgumentException("쿠키 JSON이 비어있습니다.");
            }

            EncryptToFile(cookiesJson, filePath);
            System.Diagnostics.Debug.WriteLine($"[보안] 쿠키 암호화 저장 완료: {filePath}");
        }

        /// <summary>
        /// 암호화된 쿠키 파일을 읽어서 JSON 반환 (편의 함수)
        /// </summary>
        public static string LoadEncryptedCookies(string filePath = "manual_cookies.dat")
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[보안] 암호화된 쿠키 파일 없음: {filePath}");
                return string.Empty;
            }

            string json = DecryptFromFile(filePath);
            System.Diagnostics.Debug.WriteLine($"[보안] 쿠키 복호화 완료: {filePath}");
            return json;
        }

        /// <summary>
        /// 평문 JSON 파일을 암호화 파일로 마이그레이션
        /// </summary>
        public static bool MigratePlainToEncrypted(string plainFilePath, string encryptedFilePath)
        {
            try
            {
                if (!File.Exists(plainFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[보안] 마이그레이션할 파일 없음: {plainFilePath}");
                    return false;
                }

                // 평문 읽기
                string plainJson = File.ReadAllText(plainFilePath);
                
                if (string.IsNullOrWhiteSpace(plainJson))
                {
                    System.Diagnostics.Debug.WriteLine($"[보안] 빈 파일 무시: {plainFilePath}");
                    return false;
                }

                // 암호화하여 저장
                EncryptToFile(plainJson, encryptedFilePath);
                
                System.Diagnostics.Debug.WriteLine($"[보안] 마이그레이션 성공: {plainFilePath} → {encryptedFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[보안 오류] 마이그레이션 실패: {ex.Message}");
                return false;
            }
        }
    }
}


