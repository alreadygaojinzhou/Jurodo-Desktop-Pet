using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TojirouPet
{
    public sealed class CharacterCatalog
    {
        public List<CharacterProfile> Characters { get; set; } = new();
        public string DefaultCharacter { get; set; } = "tojirou";

        public static CharacterCatalog Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "CharacterData", "characters.json");
            try
            {
                if (File.Exists(path))
                {
                    var catalog = JsonSerializer.Deserialize<CharacterCatalog>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (catalog?.Characters.Count > 0)
                        return catalog;
                }
            }
            catch (Exception ex)
            {
                MainWindow.Log("character catalog fail: " + ex.Message);
            }

            return new CharacterCatalog
            {
                Characters =
                {
                    new CharacterProfile
                    {
                        Key = "tojirou",
                        Name = "斗次郎",
                        JapaneseName = "トジロウ",
                        Species = "虎兽人",
                        Role = "熊手组工务店的老师傅",
                        CorePersonality = "粗声粗气、嘴硬心软、毒舌但可靠。",
                        SpeakingStyle = "短句、口语，常用“切”“喂”“哼”。",
                        SafeQuotes = { "……切，人类。", "哼，干得不错。" },
                        Outfits =
                        {
                            new OutfitProfile { Label = "工装", Graph = "dresswork" },
                            new OutfitProfile { Label = "西装", Graph = "dresssuit" },
                            new OutfitProfile { Label = "浴衣", Graph = "dressyukata" },
                            new OutfitProfile { Label = "祭典", Graph = "dressmatsuri" },
                        },
                        HasSit = true,
                        ModDirectory = "characters/tojirou",
                        LpsFile = "character.lps"
                    }
                }
            };
        }

        public CharacterProfile Find(string? key)
        {
            var selected = Characters.FirstOrDefault(x =>
                string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return selected
                ?? Characters.FirstOrDefault(x => x.Key == DefaultCharacter)
                ?? Characters[0];
        }
    }

    public sealed class CharacterProfile
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string JapaneseName { get; set; } = "";
        public string Species { get; set; } = "";
        public string Role { get; set; } = "";
        public string CorePersonality { get; set; } = "";
        public string SpeakingStyle { get; set; } = "";
        public string Likes { get; set; } = "";
        public string Boundaries { get; set; } = "";
        public string StoryPosition { get; set; } = "";
        public int LineCount { get; set; }
        public List<string> SafeQuotes { get; set; } = new();
        public List<OutfitProfile> Outfits { get; set; } = new();
        public bool HasSit { get; set; }
        public string ModDirectory { get; set; } = "";
        public string LpsFile { get; set; } = "character.lps";
    }

    public sealed class OutfitProfile
    {
        public string Label { get; set; } = "";
        public string Graph { get; set; } = "";
    }
}
