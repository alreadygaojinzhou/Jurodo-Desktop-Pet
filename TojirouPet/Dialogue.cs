using System;

namespace TojirouPet
{
    /// <summary>斗次郎的台词库（按场景分类）</summary>
    public static class Dialogue
    {
        private static readonly Random Rnd = new();

        public static string Pick(string[] lines) => lines[Rnd.Next(lines.Length)];

        public static readonly string[] Greeting =
        {
            "……切，又见面了，人类。",
            "喂。今天也别偷懒啊。",
            "……哼，知道开电脑了？",
            "喂，人类。我在这儿。",
        };

        public static readonly string[] Click =
        {
            "干嘛。",
            "……有屁快放。",
            "别戳，忙着呢。",
            "人类，你是不是闲得慌。",
            "……真是，一天到晚没个正形。",
            "哼，找我有事？",
        };

        public static readonly string[] HeadLow =
        {
            "喂，摸哪儿呢！",
            "……切，当我是猫啊。",
            "手拿开，人类。",
            "……别得寸进尺啊。",
        };

        public static readonly string[] HeadHigh =
        {
            "……哼，就这一次。",
            "……随你摸两下。",
            "哦、哦，够了啊。",
            "……真是拿你没办法。",
            "……手感不错吧，哼。",
        };

        public static readonly string[] Body =
        {
            "别乱碰，工具箱会掉。",
            "……真是，工装都被你摸脏了。",
            "喂，那儿不能碰。",
            "……手劲倒是挺大。",
        };

        public static readonly string[] FeedMeat =
        {
            "……哟。肉啊。",
            "哼，懂事。",
            "……不错，这个能吃。",
            "喂，还有没有。",
            "……再来一份。别装听不见。",
        };

        public static readonly string[] FeedRice =
        {
            "……就这？",
            "垫垫肚子吧。",
            "……也行，总比没有强。",
        };

        public static readonly string[] FeedCig =
        {
            "……总算有点眼力见。",
            "呼——",
            "……嗯，就这个味儿。",
        };

        public static readonly string[] FeedBeer =
        {
            "……嗯，干活后来一口。",
            "喂，你倒是挺会来事。",
            "……凉。不错。",
        };

        public static readonly string[] Hungry =
        {
            "喂，人类，我饿了。",
            "……肚子叫了，你听见没。",
            "再不管饭，我就咬你。",
            "……切，工地上还管一顿饭呢。",
        };

        public static readonly string[] LowMood =
        {
            "……真是，提不起劲。",
            "别烦我。",
            "……今天没什么想说的。",
        };

        public static readonly string[] Idle =
        {
            "2米浅蓝，2.5米黑，3米蓝……",
            "……切，这钉子又弯了。",
            "喂，人类，你那活儿干完了吗。",
            "……哼，工地上我可是最快的。",
            "……这烟有点呛。",
            "喂，别老盯着屏幕，眼睛会坏。",
            "……真是，坐太久了。",
            "……你那边进度怎么样。",
        };

        public static readonly string[] OutfitWork =
        {
            "工装才是正经衣服。",
            "……干活，还是这个顺手。",
        };

        public static readonly string[] OutfitSuit =
        {
            "西装？……偶尔穿穿。",
            "……别盯着看。领口有点紧。",
        };

        public static readonly string[] OutfitMatsuri =
        {
            "……祭典啊，人多，麻烦。",
            "偶尔热闹一下……也不是不行。",
        };

        public static readonly string[] OutfitYukata =
        {
            "浴衣……别笑。",
            "……天热，穿这个凉快。",
        };

        public static readonly string[] Sit =
        {
            "……坐会儿。腿酸。",
            "地上凉，不过还行。",
        };

        public static readonly string[] ChatOpen =
        {
            "……说。",
            "有事快讲。",
            "……嗯？",
        };

        public static readonly string[] ChatError =
        {
            "……切，脑子（接口）坏了？",
            "……喂，那边没回话。",
            "……真是，连不上。",
        };

        public static readonly string[] LateNight =
        {
            "几点了还不睡。",
            "……明天还要上工，你不困？",
        };

        public static readonly string[] LevelUp =
        {
            "哼，干得不错。……别得意。",
            "……还行吧，人类。",
        };
    }
}
