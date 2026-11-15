using System;
using System.IO;
using System.Text.Json;

namespace TojirouPet
{
    /// <summary>config.json：AI 接口配置</summary>
    public class AppConfig
    {
        public string ApiBaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public double Temperature { get; set; } = 0.9;

        public bool IsReady =>
            !string.IsNullOrWhiteSpace(ApiBaseUrl) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Model);

        private static string FilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
            }
            catch
            {
            }
            var cfg = new AppConfig();
            try { cfg.Save(); } catch { }
            return cfg;
        }

        public void Save()
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
    }
}
