using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TojirouPet
{
    public sealed class UserProfile
    {
        public string Name { get; set; } = "";
        public string PreferredAddress { get; set; } = "";
        public string Pronouns { get; set; } = "";
        public string Description { get; set; } = "";
        public string Boundaries { get; set; } = "";
        public bool AllowBriefActions { get; set; } = true;

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TojirouPet");

        private static string FilePath => Path.Combine(Dir, "user-profile.json");

        public static UserProfile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(FilePath)) ?? new UserProfile();
            }
            catch
            {
            }

            var profile = new UserProfile();
            try { profile.Save(); } catch { }
            return profile;
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }),
                Encoding.UTF8);
        }

        public string ToPrompt()
        {
            var builder = new StringBuilder();
            builder.AppendLine("现实中的用户设定：");
            builder.AppendLine($"- 用户姓名：{(string.IsNullOrWhiteSpace(Name) ? "未提供，不要擅自猜测" : Name)}");
            builder.AppendLine($"- 希望被称呼：{(string.IsNullOrWhiteSpace(PreferredAddress) ? "未指定，使用自然的第二人称" : PreferredAddress)}");
            if (!string.IsNullOrWhiteSpace(Pronouns))
                builder.AppendLine($"- 人称/身份表达：{Pronouns}");
            if (!string.IsNullOrWhiteSpace(Description))
                builder.AppendLine($"- 用户提供的自我介绍：{Description}");
            builder.AppendLine($"- 用户边界：{(string.IsNullOrWhiteSpace(Boundaries) ? "尚未额外填写；仍需以用户当前明确态度为准" : Boundaries)}");
            builder.AppendLine($"- 动作描写：{(AllowBriefActions ? "允许偶尔使用一处很短的括号动作" : "禁止动作描写")}");
            builder.AppendLine("- 用户是现实中与你聊天的人，不是游戏主人公。游戏剧情中的关系、选择和称呼不属于该用户。");
            return builder.ToString();
        }
    }
}
