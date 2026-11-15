using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TojirouPet
{
    /// <summary>OpenAI 兼容接口的极简客户端</summary>
    public class AiChatClient
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };
        private static List<StoryLine>? _story;
        private readonly List<(string role, string content)> _history = new();
        private CharacterProfile? _profile;

        public void SetCharacter(CharacterProfile profile)
        {
            _profile = profile;
            _history.Clear();
        }

        public async Task<string> AskAsync(string userText, double food, double mood, double affection)
        {
            var profile = _profile ?? throw new InvalidOperationException("角色尚未初始化");
            var cfg = AppConfig.Load();
            if (!cfg.IsReady)
                throw new InvalidOperationException("API 未配置");

            var url = cfg.ApiBaseUrl.TrimEnd('/') + "/chat/completions";
            var memory = BuildStoryContext(profile, userText);
            var messages = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = Persona.SystemPrompt(profile, food, mood, affection, memory)
                }
            };
            foreach (var (role, content) in _history)
                messages.Add(new JsonObject { ["role"] = role, ["content"] = content });
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = userText });

            var payload = new JsonObject
            {
                ["model"] = cfg.Model,
                ["messages"] = messages,
                ["temperature"] = cfg.Temperature,
                ["max_tokens"] = 200,
                ["stream"] = false
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + cfg.ApiKey);

            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}");

            var node = JsonNode.Parse(body);
            var text = node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("返回内容为空");

            _history.Add(("user", userText));
            _history.Add(("assistant", text));
            while (_history.Count > 8)
                _history.RemoveRange(0, 2);

            return text;
        }

        private static string BuildStoryContext(CharacterProfile profile, string query)
        {
            var terms = QueryTerms(query).Take(36).ToArray();
            if (terms.Length == 0)
                return "没有检索到直接相关的剧情片段。";

            var story = LoadStory();
            if (story.Count == 0)
                return "剧情索引不可用；请只依据角色资料回应。";

            var matches = story
                .Select(line =>
                {
                    int score = 0;
                    foreach (var term in terms)
                    {
                        if (line.Text.Contains(term, StringComparison.Ordinal))
                            score += Math.Min(6, term.Length * 2);
                    }
                    if (string.Equals(line.Speaker, profile.Name, StringComparison.Ordinal))
                        score += 5;
                    return (line, score);
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.line.Chapter)
                .Take(7)
                .Select(x => x.line)
                .ToList();

            if (matches.Count == 0)
                return "没有检索到直接相关的剧情片段。";

            return string.Join("\n", matches.Select(x =>
                $"- [{x.Speaker}｜{x.Chapter}] {Truncate(x.Text, 140)}"));
        }

        private static IEnumerable<string> QueryTerms(string text)
        {
            var runs = Regex.Matches(text, @"[\p{L}\p{N}]+")
                .Select(x => x.Value)
                .Where(x => x.Length >= 2)
                .Distinct();
            foreach (var run in runs)
            {
                if (run.Length <= 8)
                    yield return run;
                for (var i = 0; i < run.Length - 1; i++)
                    yield return run.Substring(i, 2);
            }
        }

        private static List<StoryLine> LoadStory()
        {
            if (_story != null) return _story;
            _story = new List<StoryLine>();
            var path = Path.Combine(AppContext.BaseDirectory, "CharacterData", "story.jsonl");
            if (!File.Exists(path)) return _story;

            foreach (var raw in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                try
                {
                    using var document = JsonDocument.Parse(raw);
                    var root = document.RootElement;
                    var text = root.TryGetProperty("zh", out var zh) ? zh.GetString()?.Trim() ?? "" : "";
                    if (text.Length == 0) continue;
                    _story.Add(new StoryLine
                    {
                        Speaker = root.TryGetProperty("speaker_zh", out var speaker)
                            ? speaker.GetString()?.Trim() ?? ""
                            : "",
                        Chapter = root.TryGetProperty("chapter", out var chapter)
                            ? chapter.GetString()?.Trim() ?? ""
                            : "",
                        Text = text
                    });
                }
                catch (JsonException)
                {
                }
            }
            return _story;
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];

        private sealed class StoryLine
        {
            public string Speaker { get; init; } = "";
            public string Chapter { get; init; } = "";
            public string Text { get; init; } = "";
        }
    }
}
