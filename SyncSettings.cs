using System;
using System.IO;
using System.Text.Json;

namespace AgentAssistant
{
    /// <summary>
    /// 동기화 설정 저장 및 로드
    /// </summary>
    public class SyncSettings
    {
        public bool EnableSync { get; set; } = true;
        public string ServerUrl { get; set; } = "https://agent-assistant-backend.your-domain.com/";
        public int SelectedDepartmentId { get; set; } = 0;
        public string SelectedDepartmentName { get; set; } = "";

        private static readonly string SettingsFilePath = "sync_settings.json";

        /// <summary>
        /// 설정 저장
        /// </summary>
        public static void Save(SyncSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 저장 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 설정 로드
        /// </summary>
        public static SyncSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<SyncSettings>(json);
                    return settings ?? new SyncSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 로드 오류: {ex.Message}");
            }

            return new SyncSettings();
        }

        /// <summary>
        /// 설정 초기화
        /// </summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 초기화 오류: {ex.Message}");
            }
        }
    }
}

