using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

const int canvasSize = 500;
const int characterHeight = 492;
const int bottom = 494;

var project = FindParentWithChild(AppContext.BaseDirectory, "TojirouPet.csproj")
    ?? throw new DirectoryNotFoundException("找不到 TojirouPet 工程目录。");
var workspace = project.Parent
    ?? throw new DirectoryNotFoundException("找不到项目上级目录。");
var source = Path.Combine(workspace.FullName, "素材");
var charactersSource = Path.Combine(source, "角色");
var packsOut = Path.Combine(project.FullName, "PetMod", "characters");
var dataOut = Path.Combine(project.FullName, "CharacterData");

var order = new[]
{
    "文治", "斗次郎", "熊手", "雪之丞", "武智", "狮子堂", "穴守", "北之纹",
    "笹吉", "宇野", "狸丸", "母亲", "鸢高", "野兽人", "基维", "黑市商人", "梶丸"
};

var safeBlacklist = new[]
{
    "射", "精液", "阴茎", "性器", "做爱", "插入", "发情", "裸", "乳", "屁股",
    "欲望", "色情", "高潮", "内裤", "舔", "咬住", "身体", "接吻"
};

var modeSpecs = new Dictionary<string, Dictionary<string, string[][]>>
{
    ["Nomal"] = new()
    {
        ["default"] = Pair(("站立", "普通"), ("站立", "工装")),
        ["say"] = Pair(("站立", "眨眼"), ("站立", "普通")),
        ["idel"] = Pair(("站立", "抽烟"), ("站立", "普通")),
        ["sleep"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["head"] = Pair(("站立", "笑"), ("站立", "普通")),
        ["body"] = Pair(("站立", "囧"), ("站立", "丧")),
        ["raised"] = Pair(("站立", "惊"), ("站立", "普通")),
        ["move"] = Pair(("站立", "普通"), ("站立", "工装")),
        ["work"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["switch"] = Pair(("站立", "呆"), ("站立", "普通")),
    },
    ["Happy"] = new()
    {
        ["default"] = Pair(("站立", "笑"), ("站立", "普通")),
        ["say"] = Pair(("站立", "眨眼"), ("站立", "笑")),
        ["idel"] = Pair(("站立", "抽烟"), ("站立", "笑")),
        ["sleep"] = Pair(("盘腿", "笑"), ("盘腿", "普通")),
        ["head"] = Pair(("站立", "笑"), ("站立", "普通")),
        ["body"] = Pair(("盘腿", "笑"), ("站立", "笑")),
        ["raised"] = Pair(("站立", "笑"), ("站立", "普通")),
        ["move"] = Pair(("站立", "笑"), ("站立", "普通")),
        ["work"] = Pair(("盘腿", "笑"), ("盘腿", "普通")),
        ["switch"] = Pair(("站立", "普通"), ("站立", "笑")),
    },
    ["PoorCondition"] = new()
    {
        ["default"] = Pair(("站立", "呆"), ("站立", "丧")),
        ["say"] = Pair(("站立", "眨眼"), ("站立", "普通")),
        ["idel"] = Pair(("站立", "抽烟"), ("站立", "普通")),
        ["sleep"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["head"] = Pair(("站立", "囧"), ("站立", "普通")),
        ["body"] = Pair(("站立", "丧"), ("站立", "囧")),
        ["raised"] = Pair(("站立", "丧"), ("站立", "普通")),
        ["move"] = Pair(("站立", "呆"), ("站立", "普通")),
        ["work"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["switch"] = Pair(("站立", "丧"), ("站立", "普通")),
    },
    ["Ill"] = new()
    {
        ["default"] = Pair(("站立", "丧"), ("站立", "呆")),
        ["say"] = Pair(("站立", "眨眼"), ("站立", "普通")),
        ["idel"] = Pair(("站立", "丧"), ("站立", "普通")),
        ["sleep"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["head"] = Pair(("站立", "囧"), ("站立", "普通")),
        ["body"] = Pair(("站立", "丧"), ("站立", "囧")),
        ["raised"] = Pair(("站立", "丧"), ("站立", "普通")),
        ["move"] = Pair(("站立", "丧"), ("站立", "普通")),
        ["work"] = Pair(("盘腿", "普通"), ("站立", "普通")),
        ["switch"] = Pair(("站立", "丧"), ("站立", "普通")),
    },
};

var animations = new (string Graph, string Key, bool Breathing)[]
{
    ("Default", "default", true), ("Say", "say", false), ("Idel", "idel", false),
    ("Sleep", "sleep", false), ("Touch_Head", "head", false), ("Touch_Body", "body", false),
    ("Raised_Static", "raised", false), ("Raised_Dynamic", "raised", false),
    ("Move", "move", false), ("Work", "work", false), ("Switch_Up", "switch", false),
    ("Switch_Down", "switch", false), ("Switch_Hunger", "switch", false),
    ("Switch_Thirsty", "switch", false), ("StartUP", "head", false), ("Shutdown", "sleep", false)
};

var outfits = new (string Label, string Graph, string Pose, string Expression)[]
{
    ("工装", "dresswork", "站立", "工装"),
    ("西装", "dresssuit", "站立", "西装"),
    ("浴衣", "dressyukata", "站立", "浴衣"),
    ("祭典", "dressmatsuri", "站立", "祭典"),
};

if (Directory.Exists(packsOut)) Directory.Delete(packsOut, true);
if (Directory.Exists(dataOut)) Directory.Delete(dataOut, true);
Directory.CreateDirectory(packsOut);
Directory.CreateDirectory(dataOut);

var directories = Directory.EnumerateDirectories(charactersSource)
    .ToDictionary(Path.GetFileName, path => path, StringComparer.Ordinal);
var orderedNames = order.Concat(directories.Keys.Except(order).OrderBy(x => x, StringComparer.Ordinal));
var catalog = new List<CharacterProfile>();
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

foreach (var name in orderedNames)
{
    if (!directories.TryGetValue(name, out var characterDir)) continue;
    var metadataPath = Path.Combine(characterDir, "角色资料.json");
    if (!File.Exists(metadataPath)) continue;
    var metadata = JsonSerializer.Deserialize<RoleMetadata>(File.ReadAllText(metadataPath), jsonOptions);
    if (metadata is null || !metadata.HasSprites) continue;

    Console.WriteLine($"building {metadata.Name} ...");
    catalog.Add(MakePack(metadata, characterDir));
}

var manifest = new CharacterManifest
{
    Characters = catalog,
    DefaultCharacter = catalog.Any(x => x.Key == "tojirou") ? "tojirou" : catalog[0].Key
};
File.WriteAllText(
    Path.Combine(dataOut, "characters.json"),
    JsonSerializer.Serialize(manifest, new JsonSerializerOptions(jsonOptions) { WriteIndented = true }) + "\n",
    Encoding.UTF8);

var storySource = Path.Combine(source, "全游戏对话", "全游戏对话.jsonl");
if (File.Exists(storySource))
    File.Copy(storySource, Path.Combine(dataOut, "story.jsonl"), true);

var files = Directory.EnumerateFiles(packsOut, "*", SearchOption.AllDirectories).ToList();
Console.WriteLine(
    $"done: {catalog.Count} characters, {files.Count} files, " +
    $"{files.Sum(x => new FileInfo(x).Length) / 1024d / 1024d:F1} MiB");

CharacterProfile MakePack(RoleMetadata metadata, string characterDir)
{
    var images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
    var spriteDir = Path.Combine(characterDir, "立绘");
    foreach (var path in Directory.EnumerateFiles(spriteDir, "*.png"))
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        var prefix = metadata.Name + "_";
        if (!stem.StartsWith(prefix, StringComparison.Ordinal)) continue;
        var parts = stem[prefix.Length..].Split('_', 2);
        if (parts.Length != 2) continue;
        images[Key(parts[0], parts[1])] = LoadBitmap(path);
    }
    if (!images.ContainsKey(Key("站立", "普通")))
        throw new InvalidOperationException($"{metadata.Name}: missing 站立_普通");

    var packDir = Path.Combine(packsOut, metadata.Key);
    var petDir = Path.Combine(packDir, "pet");
    var normal = Compose(images[Key("站立", "普通")], 1.0);
    foreach (var (mode, spec) in modeSpecs)
    {
        foreach (var (graph, key, breathing) in animations)
        {
            var sourceImage = Choose(images, spec[key]);
            var folder = Path.Combine(petDir, mode, graph);
            if (breathing)
                WriteBreathing(folder, sourceImage);
            else
                SavePng(Compose(sourceImage, 1.0), Path.Combine(folder, "_000_1500.png"));
        }
    }

    var common = new Dictionary<string, Bitmap>
    {
        ["dresswork"] = Choose(images, Pair(("站立", "工装"), ("站立", "普通"))),
        ["dresssuit"] = Choose(images, Pair(("站立", "西装"), ("站立", "普通"))),
        ["dressyukata"] = Choose(images, Pair(("站立", "浴衣"), ("站立", "普通"))),
        ["dressmatsuri"] = Choose(images, Pair(("站立", "祭典"), ("站立", "普通"))),
        ["smoke"] = Choose(images, Pair(("站立", "抽烟"), ("站立", "普通"))),
        ["sit"] = Choose(images, Pair(("盘腿", "普通"), ("站立", "普通"))),
        ["sitlaugh"] = Choose(images, Pair(("盘腿", "笑"), ("盘腿", "普通"))),
        ["sitangry"] = Choose(images, Pair(("盘腿", "怒"), ("盘腿", "普通"))),
        ["surprise"] = Choose(images, Pair(("站立", "惊"), ("站立", "普通"))),
        ["angryface"] = Choose(images, Pair(("站立", "怒"), ("站立", "普通"))),
        ["laugh"] = Choose(images, Pair(("站立", "笑"), ("站立", "普通"))),
        ["blink"] = Choose(images, Pair(("站立", "眨眼"), ("站立", "普通"))),
    };
    foreach (var mode in modeSpecs.Keys)
    {
        foreach (var (name, sourceImage) in common)
            SavePng(Compose(sourceImage, 1.0), Path.Combine(petDir, mode, $"Common_{name}", "_000_1500.png"));
    }

    var bounds = AlphaBounds(normal);
    var headHeight = Math.Max(80, (int)Math.Round((bounds.Bottom - bounds.Top) * 0.30));
    var headWidth = Math.Max(80, (int)Math.Round((bounds.Right - bounds.Left) * 0.62));
    var headX = (bounds.Left + bounds.Right) / 2 - headWidth / 2;
    var bodyX = bounds.Left + (int)Math.Round((bounds.Right - bounds.Left) * 0.12);
    var bodyY = bounds.Top + headHeight + 10;
    var bodyWidth = Math.Max(80, (int)Math.Round((bounds.Right - bounds.Left) * 0.76));
    var bodyHeight = Math.Max(100, bounds.Bottom - bounds.Top - headHeight - 20);
    var intro = $"{metadata.Name}，{metadata.Species}。{metadata.Role}";
    var lps = new StringBuilder()
        .AppendLine($"pet#{metadata.Key}:|intor#{intro}:|path#pet:|petname#{metadata.Name}:|tag#all:|")
        .AppendLine($"touchhead:|px#{headX}:|py#{bounds.Top + 4}:|sw#{headWidth}:|sh#{headHeight}:|")
        .AppendLine($"touchbody:|px#{bodyX}:|py#{bodyY}:|sw#{bodyWidth}:|sh#{bodyHeight}:|")
        .AppendLine("touchraised:|happy_px#0:|happy_py#40:|happy_sw#500:|happy_sh#260:|nomal_px#0:|nomal_py#40:|nomal_sw#500:|nomal_sh#260:|poorcondition_px#0:|poorcondition_py#40:|poorcondition_sw#500:|poorcondition_sh#260:|ill_px#0:|ill_py#60:|ill_sw#500:|ill_sh#300:|")
        .AppendLine("raisepoint:|happy_x#250:|happy_y#120:|nomal_x#250:|nomal_y#120:|poorcondition_x#250:|poorcondition_y#120:|ill_x#250:|ill_y#140:|")
        .AppendLine("duration:|dresswork#8:|dresssuit#8:|dressyukata#8:|dressmatsuri#8:|smoke#4:|sit#6:|sitlaugh#6:|sitangry#6:|surprise#2:|angryface#3:|laugh#3:|blink#1:|");
    File.WriteAllText(Path.Combine(packDir, "character.lps"), lps.ToString(), Encoding.UTF8);

    var markdown = File.ReadAllText(Path.Combine(characterDir, "人设卡.md"));
    var quotes = SafeQuotes(Path.Combine(characterDir, "对话记录", "我的台词.jsonl"));
    return new CharacterProfile
    {
        Key = metadata.Key,
        Name = metadata.Name,
        JapaneseName = metadata.JapaneseName,
        Species = metadata.Species,
        Role = metadata.Role,
        CorePersonality = Section(markdown, "核心性格"),
        SpeakingStyle = Section(markdown, "说话方式"),
        Likes = Section(markdown, "喜欢与在意的内容"),
        Boundaries = Section(markdown, "雷点、冲突与边界"),
        StoryPosition = Section(markdown, "剧情中的人物位置"),
        LineCount = metadata.LineCount,
        SafeQuotes = quotes,
        Outfits = outfits
            .Where(x => images.ContainsKey(Key(x.Pose, x.Expression)))
            .Select(x => new OutfitProfile { Label = x.Label, Graph = x.Graph })
            .ToList(),
        HasSit = images.ContainsKey(Key("盘腿", "普通")),
        ModDirectory = $"characters/{metadata.Key}",
        LpsFile = "character.lps"
    };
}

Bitmap Choose(Dictionary<string, Bitmap> images, string[][] candidates)
{
    foreach (var pair in candidates)
    {
        if (images.TryGetValue(Key(pair[0], pair[1]), out var result))
            return result;
    }
    return images[Key("站立", "普通")];
}

Bitmap LoadBitmap(string path)
{
    using var sourceImage = Image.FromFile(path);
    var bitmap = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Transparent);
    graphics.DrawImage(sourceImage, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
    return bitmap;
}

Bitmap Compose(Bitmap image, double scaleY)
{
    var height = (int)Math.Round(characterHeight * scaleY);
    var width = Math.Max(1, (int)Math.Round(image.Width * (double)height / image.Height));
    var result = new Bitmap(canvasSize, canvasSize, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(result);
    graphics.Clear(Color.Transparent);
    graphics.CompositingMode = CompositingMode.SourceOver;
    graphics.CompositingQuality = CompositingQuality.HighQuality;
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    graphics.DrawImage(
        image,
        new Rectangle((canvasSize - width) / 2, bottom - height, width, height),
        new Rectangle(0, 0, image.Width, image.Height),
        GraphicsUnit.Pixel);
    return result;
}

void WriteBreathing(string folder, Bitmap image)
{
    double[] scales = [1.0, 1.0035, 1.0, 0.9965];
    for (var i = 0; i < scales.Length; i++)
    {
        using var frame = Compose(image, scales[i]);
        SavePng(frame, Path.Combine(folder, $"_{i:000}_1100.png"));
    }
}

void SavePng(Bitmap image, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    image.Save(path, ImageFormat.Png);
}

Rectangle AlphaBounds(Bitmap image)
{
    var minX = image.Width;
    var minY = image.Height;
    var maxX = -1;
    var maxY = -1;
    for (var y = 0; y < image.Height; y++)
    for (var x = 0; x < image.Width; x++)
    {
        if (image.GetPixel(x, y).A <= 8) continue;
        minX = Math.Min(minX, x);
        minY = Math.Min(minY, y);
        maxX = Math.Max(maxX, x);
        maxY = Math.Max(maxY, y);
    }
    return maxX < minX ? new Rectangle(0, 0, image.Width, image.Height)
        : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
}

List<string> SafeQuotes(string path)
{
    var result = new List<string>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    if (!File.Exists(path)) return result;
    foreach (var line in File.ReadLines(path))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            using var document = JsonDocument.Parse(line);
            var text = document.RootElement.TryGetProperty("zh", out var node)
                ? node.GetString()?.Trim() ?? ""
                : "";
            if (text.Length is < 3 or > 48 || !seen.Add(text)) continue;
            if (safeBlacklist.Any(text.Contains)) continue;
            if (text.StartsWith('\\') || text.Contains("Message_")) continue;
            result.Add(text);
        }
        catch (JsonException)
        {
        }
    }
    if (result.Count <= 24) return result;
    return Enumerable.Range(0, 24)
        .Select(i => result[(int)Math.Round(i * (result.Count - 1) / 23d)])
        .ToList();
}

static string Section(string markdown, string title)
{
    var match = Regex.Match(
        markdown,
        $@"^## {Regex.Escape(title)}\s*\n(.*?)(?=^## |\z)",
        RegexOptions.Multiline | RegexOptions.Singleline);
    return match.Success ? Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim() : "";
}

static string[][] Pair(params (string Pose, string Expression)[] values) =>
    values.Select(x => new[] { x.Pose, x.Expression }).ToArray();

static string Key(string pose, string expression) => $"{pose}|{expression}";

static DirectoryInfo? FindParentWithChild(string start, string child)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, child))) return directory;
        directory = directory.Parent;
    }
    return null;
}

internal sealed class RoleMetadata
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("zh")] public string Name { get; set; } = "";
    [JsonPropertyName("ja")] public string JapaneseName { get; set; } = "";
    [JsonPropertyName("species")] public string Species { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("line_count")] public int LineCount { get; set; }
    [JsonPropertyName("has_sprites")] public bool HasSprites { get; set; }
}

internal sealed class CharacterManifest
{
    public List<CharacterProfile> Characters { get; set; } = new();
    public string DefaultCharacter { get; set; } = "tojirou";
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
