using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TojirouPet
{
    public sealed record ChatMessage(string Role, string Content);

    public static class ChatMemoryStore
    {
        private const int MaxSavedMessages = 32;

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TojirouPet", "chat");

        public static List<ChatMessage> Load(string characterKey)
        {
            try
            {
                var path = Path.Combine(Dir, SafeName(characterKey) + ".json");
                if (!File.Exists(path)) return new List<ChatMessage>();
                var messages = JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(path));
                return messages?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Role) && !string.IsNullOrWhiteSpace(x.Content))
                    .TakeLast(MaxSavedMessages)
                    .ToList() ?? new List<ChatMessage>();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }

        public static void Save(string characterKey, IReadOnlyList<ChatMessage> messages)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var path = Path.Combine(Dir, SafeName(characterKey) + ".json");
                var recent = messages.TakeLast(MaxSavedMessages).ToList();
                File.WriteAllText(
                    path,
                    JsonSerializer.Serialize(recent, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }));
            }
            catch
            {
            }
        }

        public static void Clear(string characterKey)
        {
            try
            {
                var path = Path.Combine(Dir, SafeName(characterKey) + ".json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static string SafeName(string key) =>
            string.Concat(key.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
    }
}
