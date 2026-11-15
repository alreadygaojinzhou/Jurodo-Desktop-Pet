using System;
using System.Collections.Generic;
using System.Linq;

namespace TojirouPet
{
    public sealed class DialogueSet
    {
        private static readonly Random Rnd = new();

        public string[] Greeting { get; init; } = Array.Empty<string>();
        public string[] Click { get; init; } = Array.Empty<string>();
        public string[] HeadLow { get; init; } = Array.Empty<string>();
        public string[] HeadHigh { get; init; } = Array.Empty<string>();
        public string[] Body { get; init; } = Array.Empty<string>();
        public string[] FeedMeat { get; init; } = Array.Empty<string>();
        public string[] FeedRice { get; init; } = Array.Empty<string>();
        public string[] FeedCig { get; init; } = Array.Empty<string>();
        public string[] FeedBeer { get; init; } = Array.Empty<string>();
        public string[] Hungry { get; init; } = Array.Empty<string>();
        public string[] LowMood { get; init; } = Array.Empty<string>();
        public string[] Idle { get; init; } = Array.Empty<string>();
        public string[] Sit { get; init; } = Array.Empty<string>();
        public string[] ChatOpen { get; init; } = Array.Empty<string>();
        public string[] ChatError { get; init; } = Array.Empty<string>();
        public string[] LateNight { get; init; } = Array.Empty<string>();

        public string Pick(string[] lines) => lines.Length == 0 ? "……" : lines[Rnd.Next(lines.Length)];

        public string[] Outfit(string graph)
        {
            return graph switch
            {
                "dresswork" => new[] { "工装。干活方便。" },
                "dresssuit" => new[] { "西装……偶尔穿一次。" },
                "dressyukata" => new[] { "浴衣。……别笑。" },
                "dressmatsuri" => new[] { "祭典啊。热闹一会儿也无妨。" },
                _ => new[] { "……换好了。" }
            };
        }

        public static DialogueSet Create(CharacterProfile profile)
        {
            if (profile.Key == "tojirou")
            {
                return new DialogueSet
                {
                    Greeting = Dialogue.Greeting,
                    Click = Dialogue.Click,
                    HeadLow = Dialogue.HeadLow,
                    HeadHigh = Dialogue.HeadHigh,
                    Body = Dialogue.Body,
                    FeedMeat = Dialogue.FeedMeat,
                    FeedRice = Dialogue.FeedRice,
                    FeedCig = Dialogue.FeedCig,
                    FeedBeer = Dialogue.FeedBeer,
                    Hungry = Dialogue.Hungry,
                    LowMood = Dialogue.LowMood,
                    Idle = Dialogue.Idle,
                    Sit = Dialogue.Sit,
                    ChatOpen = Dialogue.ChatOpen,
                    ChatError = Dialogue.ChatError,
                    LateNight = Dialogue.LateNight
                };
            }

            var quotes = profile.SafeQuotes.Count > 0
                ? profile.SafeQuotes.ToArray()
                : new[] { "……", "怎么了？" };
            return new DialogueSet
            {
                Greeting = new[] { "……在呢。", "来了？", "嗯，我在。" },
                Click = quotes,
                HeadLow = new[] { "……喂。", "行了，别乱摸。", "……还真是上手。" },
                HeadHigh = new[] { "……随你。", "就一会儿。", "……嗯。" },
                Body = new[] { "……别碰那儿。", "喂，手脚轻点。", "……行了。" },
                FeedMeat = new[] { "……肉？谢了。", "这个不错。", "……我开动了。" },
                FeedRice = new[] { "饭团啊。够了。", "……垫垫肚子。" },
                FeedCig = new[] { "……烟。", "呼……", "总算有点眼力见。" },
                FeedBeer = new[] { "……凉。", "干活之后来一口，还行。" },
                Hungry = new[] { "……差不多该吃点东西了。", "你听见肚子叫了吗？" },
                LowMood = new[] { "……让我安静会儿。", "现在没什么精神。" },
                Idle = quotes,
                Sit = new[] { "坐会儿。", "……腿酸。" },
                ChatOpen = new[] { "……说吧。", "我在听。" },
                ChatError = new[] { "……没回话。", "……连不上。" },
                LateNight = new[] { "还不睡？", "时间不早了。" }
            };
        }
    }
}
