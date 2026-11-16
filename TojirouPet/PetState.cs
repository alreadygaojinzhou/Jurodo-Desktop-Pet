using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TojirouPet
{
    /// <summary>持久化的桌宠状态（饱食/心情/好感等）</summary>
    public class PetState
    {
        public const double DefaultScale = 0.54;
        private const int CurrentScaleVersion = 3;

        public double Food { get; set; } = 85;
        public double Mood { get; set; } = 70;
        public double Affection { get; set; } = 10;
        public double Health { get; set; } = 100;
        public double Scale { get; set; } = DefaultScale;
        public int ScaleVersion { get; set; } // 0 = 旧存档；保存时写入当前版本
        public bool TopMost { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public string Appearance { get; set; } = "";
        public string SelectedCharacter { get; set; } = "tojirou";
        public Dictionary<string, PetCharacterState> CharacterStates { get; set; } = new();
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public DateTime LastSeen { get; set; } = DateTime.Now;

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TojirouPet");

        private static string FilePath => Path.Combine(Dir, "state.json");

        public static PetState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var st = JsonSerializer.Deserialize<PetState>(File.ReadAllText(FilePath)) ?? new PetState();
                    // 尺寸默认值升级时只迁移一次，之后仍允许用户手动选回其他档位。
                    if (st.ScaleVersion < CurrentScaleVersion)
                    {
                        st.Scale = DefaultScale;
                        st.ScaleVersion = CurrentScaleVersion;
                    }
                    st.NormalizeCharacterStates();
                    st.LoadCharacter(st.SelectedCharacter);
                    return st;
                }
            }
            catch
            {
                // 存档损坏就重来，不要打扰用户
            }
            return new PetState { ScaleVersion = CurrentScaleVersion };
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                ScaleVersion = CurrentScaleVersion;
                StoreActiveCharacter(DateTime.Now);
                LastSeen = DateTime.Now;
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }

        public void SwitchCharacter(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (string.Equals(SelectedCharacter, key, StringComparison.OrdinalIgnoreCase))
                return;

            var now = DateTime.Now;
            StoreActiveCharacter(now);
            SelectedCharacter = key;
            LoadCharacter(key, now);
        }

        private void NormalizeCharacterStates()
        {
            if (string.IsNullOrWhiteSpace(SelectedCharacter))
                SelectedCharacter = "tojirou";
            CharacterStates ??= new Dictionary<string, PetCharacterState>();
            if (!CharacterStates.ContainsKey(SelectedCharacter))
            {
                CharacterStates[SelectedCharacter] = new PetCharacterState
                {
                    Food = Food,
                    Mood = Mood,
                    Affection = Affection,
                    Health = Health,
                    Appearance = Appearance,
                    LastSeen = LastSeen
                };
            }
        }

        private void StoreActiveCharacter(DateTime now)
        {
            if (string.IsNullOrWhiteSpace(SelectedCharacter))
                SelectedCharacter = "tojirou";
            CharacterStates ??= new Dictionary<string, PetCharacterState>();
            CharacterStates[SelectedCharacter] = new PetCharacterState
            {
                Food = Food,
                Mood = Mood,
                Affection = Affection,
                Health = Health,
                Appearance = Appearance,
                LastSeen = now
            };
        }

        private void LoadCharacter(string key, DateTime? now = null)
        {
            CharacterStates ??= new Dictionary<string, PetCharacterState>();
            if (!CharacterStates.TryGetValue(key, out var character))
            {
                character = new PetCharacterState();
                CharacterStates[key] = character;
            }
            character.ApplyDecay(now ?? DateTime.Now);
            Food = character.Food;
            Mood = character.Mood;
            Affection = character.Affection;
            Health = character.Health;
            Appearance = character.Appearance;
            LastSeen = character.LastSeen;
        }

        public static void Reset()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch
            {
            }
        }
    }

    public sealed class PetCharacterState
    {
        public double Food { get; set; } = 85;
        public double Mood { get; set; } = 70;
        public double Affection { get; set; } = 10;
        public double Health { get; set; } = 100;
        public string Appearance { get; set; } = "";
        public DateTime LastSeen { get; set; } = DateTime.Now;

        public void ApplyDecay(DateTime now)
        {
            var away = now - LastSeen;
            if (away <= TimeSpan.FromMinutes(5)) return;
            var hours = Math.Min(away.TotalHours, 12);
            Food = Math.Max(0, Food - hours * 100.0 / 14.0);
            Mood = Math.Max(0, Mood - hours * 100.0 / 10.0);
            Health = Math.Max(20, Health - (Food < 15 ? hours * 3 : 0));
            LastSeen = now;
        }
    }
}
