using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LinePutScript;
using VPet_Simulator.Core;
using static VPet_Simulator.Core.GraphInfo;
using Forms = System.Windows.Forms;

namespace TojirouPet
{
    public partial class MainWindow : Window
    {
        private GameCore _core = null!;
        private PetController _ctrl = null!;
        private Main _pet = null!;
        private PetState _state = null!;
        private readonly AiChatClient _ai = new();
        private readonly CharacterCatalog _catalog;
        private CharacterProfile _profile = null!;
        private DialogueSet _lines = null!;
        private ChatWindow? _chat;
        private InteractionRulesWindow? _rules;
        private Forms.NotifyIcon? _tray;
        private bool _switchingCharacter;

        private DispatcherTimer? _statTimer;
        private DispatcherTimer? _saveTimer;
        private DispatcherTimer? _idleTimer;

        private DateTime _lastTick = DateTime.Now;
        private DateTime _lastHead = DateTime.MinValue;
        private DateTime _lastBody = DateTime.MinValue;
        private DateTime _lastHungry = DateTime.MinValue;
        private DateTime _lastAutoSay = DateTime.Now;
        private IGameSave.ModeType _lastMode;
        private bool _started;

        private double _petBox = 500 * PetState.DefaultScale;   // 宠物显示区域的边长（px）
        private double _bubbleExtra;          // 说话时窗口向上临时扩展的高度
        private const double DragStartDistance = 10;

        private bool _dragArmed;
        private bool _dragging;
        private DateTime _dragStartTime;
        private DispatcherTimer? _dragAppearanceTimer;
        private Point _dragStartCursor;
        private Point _dragStartWindow;
        private bool _dragHadBubble;
        private string? _deferredSpeech;
        private string? _deferredGraph;

        public double PetZoom => Math.Round(_petBox / 500.0, 3);
        public string PetName => _profile?.Name ?? "桌宠";
        public Rect PetScreenBounds
        {
            get
            {
                double left = Left + (Width - _petBox) / 2;
                double top = Top + _bubbleExtra;
                return new Rect(left, top, _petBox, _petBox);
            }
        }

        public MainWindow()
        {
            _catalog = CharacterCatalog.Load();
            _profile = _catalog.Find(null);
            _lines = DialogueSet.Create(_profile);
            _ai.SetCharacter(_profile);
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _state = PetState.Load();
            _profile = _catalog.Find(_state.SelectedCharacter);
            _lines = DialogueSet.Create(_profile);
            _ai.SetCharacter(_profile);
            Title = _profile.Name;
            ApplyScale(_state.Scale);
            RestorePosition();
            Topmost = _state.TopMost;
            SyncAutoStart();
            InitTray();
            await InitPetAsync();
        }

        private void SyncAutoStart()
        {
            try
            {
                StartupManager.SetEnabled(_state.AutoStart);
                Log($"autostart={_state.AutoStart}, registered={StartupManager.IsEnabled()}");
            }
            catch (Exception ex)
            {
                Log("autostart sync fail: " + ex.Message);
            }
        }

        private async Task InitPetAsync(bool runSelfTest = true)
        {
            LoadingBox.Visibility = Visibility.Visible;
            LoadingText.Text = $"叫醒{_profile.Name}中…";
            await Task.Yield();
            try
            {
                _core = new GameCore();
                _ctrl = new PetController(this);
                _core.Controller = _ctrl;

                var save = new GameSave(_profile.Name)
                {
                    StrengthFood = _state.Food,
                    StrengthDrink = 88,
                    Feeling = _state.Mood,
                    Likability = _state.Affection,
                    Health = _state.Health
                };
                _core.Save = save;

                var modDir = Path.Combine(AppContext.BaseDirectory, "PetMod", _profile.ModDirectory);
                var lpsPath = Path.Combine(modDir, _profile.LpsFile);
                if (!File.Exists(lpsPath) && _profile.Key == "tojirou")
                {
                    modDir = Path.Combine(AppContext.BaseDirectory, "PetMod", "tojirou");
                    lpsPath = Path.Combine(modDir, "tojirou.lps");
                }
                if (!File.Exists(lpsPath))
                    throw new FileNotFoundException("找不到动画配置：" + lpsPath);

                var lps = new LpsDocument(File.ReadAllText(lpsPath));
                var loader = new PetLoader(lps, new DirectoryInfo(modDir));
                _core.Graph = loader.Graph(1000, Dispatcher);

                _pet = new Main(_core)
                {
                    Width = 500,
                    Height = 500,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                _pet.DefaultClickAction = OnPetClicked;
                _pet.DefaultPressAction = ShowPetMenu;
                PetHost.Child = _pet;

                _pet.Load_0_BaseConsole();
                ThemeMessageBar();
                _pet.Load_2_TouchEvent();
                await _pet.Load_2_WaitGraph();
                _pet.Load_3_BindingTimer();
                _pet.Load_4_Start();

                var cfg = _core.Graph.GraphConfig;
                _core.TouchEvent.Insert(0, new TouchArea(cfg.TouchHeadLocate, cfg.TouchHeadSize,
                    () => { OnHeadTouch(); return false; }));
                _core.TouchEvent.Insert(0, new TouchArea(cfg.TouchBodyLocate, cfg.TouchBodySize,
                    () => { OnBodyTouch(); return false; }));
                // 提起拖拽：长按身体区域即可抓着桌宠走
                for (int i = 0; i < 4; i++)
                {
                    var m = (IGameSave.ModeType)i;
                    _core.TouchEvent.Add(new TouchArea(cfg.TouchRaisedLocate[i], cfg.TouchRaisedSize[i],
                        () =>
                        {
                            if (_core.Save!.Mode != m) return false;
                            try { _pet.DisplayRaised(); } catch { }
                            return true;
                        }, true));
                }
                Log($"raised graphs: static={_core.Graph!.FindName(GraphType.Raised_Static)}, dynamic={_core.Graph!.FindName(GraphType.Raised_Dynamic)}");

                Log($"graphs loaded={loader.GraphCount}, mode={_core.Save!.Mode}, " +
                    $"head={cfg.TouchHeadLocate} {cfg.TouchHeadSize}, body={cfg.TouchBodyLocate} {cfg.TouchBodySize}");
                _lastMode = _core.Save!.Mode;
                _started = true;
                _lastTick = DateTime.Now;
                ApplyPersistentAppearance();
                LoadingBox.Visibility = Visibility.Collapsed;

                StartTimers();

                if (runSelfTest && Environment.GetEnvironmentVariable("TOJIROU_SELFTEST") == "1")
                {
                    await RunSelfTestAsync();
                    return;
                }

                await Task.Delay(400);
                Say(_lines.Pick(DateTime.Now.Hour >= 23 || DateTime.Now.Hour < 5
                    ? _lines.LateNight
                    : _lines.Greeting), "laugh");
            }
            catch (Exception ex)
            {
                Log("INIT FAIL: " + ex);
                LoadingText.Text = "启动失败：" + ex.Message;
                LoadingText.Foreground = Brushes.OrangeRed;
                LoadingText.TextWrapping = TextWrapping.Wrap;
                LoadingText.MaxWidth = 300;
                Width = Math.Max(Width, 340);
                Height = Math.Max(Height, 150);
            }
        }

        /// <summary>把 Core 的蓝色气泡改成与斗次郎相配的暖色</summary>
        private void ThemeMessageBar()
        {
            try
            {
                if (_pet.MsgBar?.This is not FrameworkElement fe) return;
                var bg = new SolidColorBrush(Color.FromArgb(0xE6, 0x26, 0x20, 0x18));
                var fg = new SolidColorBrush(Color.FromRgb(0xF6, 0xEC, 0xDC));
                var border = new SolidColorBrush(Color.FromRgb(0xC8, 0x73, 0x2E));
                fe.Resources["Primary"] = bg;
                fe.Resources["PrimaryTrans"] = bg;
                fe.Resources["PrimaryText"] = fg;
                fe.Resources["DARKPrimaryDark"] = border;
                fe.Resources["DARKPrimary"] = border;
            }
            catch (Exception ex)
            {
                Log("theme bubble fail: " + ex.Message);
            }
        }

        private async Task RunSelfTestAsync()
        {
            await Task.Delay(1500);
            Log($"selftest: window={Width:F0}x{Height:F0}, petBox={_petBox:F0}, zoom={PetZoom}, pos=({Left:F0},{Top:F0})");
            Log($"selftest core interaction functions={_ctrl.EnableFunction}");
            Snap("01_idle");

            // 实际触发“大小”菜单项，防止只验证属性而漏掉菜单接线问题。
            var initialScale = _state.Scale;
            var menuForScale = BuildMenu();
            foreach (var topItem in menuForScale.Items)
            {
                if (topItem is not MenuItem { Header: "大小" } sizeMenu) continue;
                foreach (var sizeItem in sizeMenu.Items)
                {
                    if (sizeItem is not MenuItem { Header: "大（360px）" } largeItem) continue;
                    largeItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    break;
                }
                break;
            }
            await Task.Delay(150);
            Log($"selftest resize menu: window={Width:F0}x{Height:F0}, petBox={_petBox:F0}, zoom={PetZoom}");
            ApplyScale(initialScale);
            await Task.Delay(150);

            var clickMood = _state.Mood;
            OnPetClicked();
            await Task.Delay(120);
            Log($"selftest click moodDelta={_state.Mood - clickMood:F3}");
            Log($"selftest idle interval minutes={_idleTimer?.Interval.TotalMinutes:F1}");

            var l0 = Left; var t0 = Top;
            MoveBy(80 * PetZoom, -40 * PetZoom);
            await Task.Delay(200);
            Log($"selftest moveBy: dx={Left - l0:F1} dy={Top - t0:F1}");
            MoveBy(-80 * PetZoom, 40 * PetZoom);
            var headMood = _state.Mood;
            OnHeadTouch();
            await Task.Delay(900);
            Log($"selftest head moodDelta={_state.Mood - headMood:F3}");
            Snap("02_head");
            Feed("meat");
            await Task.Delay(1200);
            Snap("03_feed");

            // 像用户一样从右键菜单触发换装，再确认当前动画没有被“说话”覆盖。
            var targetOutfit = _profile.Outfits.FirstOrDefault(x => x.Graph == "dresssuit")
                ?? _profile.Outfits.FirstOrDefault();
            if (targetOutfit != null)
            {
                var dressMenu = BuildMenu();
                foreach (var topItem in dressMenu.Items)
                {
                    if (topItem is not MenuItem { Header: "换装" } dressSubmenu) continue;
                    foreach (var dressItem in dressSubmenu.Items)
                    {
                        if (dressItem is not MenuItem clothing) continue;
                        if (!Equals(clothing.Header, targetOutfit.Label)) continue;
                        clothing.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        break;
                    }
                    break;
                }
                await Task.Delay(500);
                Log($"selftest dress graph={_pet.DisplayType.Name}");
                Snap("04_dress");
                await Task.Delay(8000);
                Log($"selftest dress persistent={_pet.DisplayType.Name}, state={_state.Appearance}");
                ShowDragPose();
                Log($"selftest dress duringDrag={_pet.DisplayType.Name}");
                _dragging = true;
                StartDragAppearanceGuard();
                try { _pet.Display("surprise", AnimatType.Single); } catch { }
                await Task.Delay(400);
                Log($"selftest dress guard={_pet.DisplayType.Name}");
                _dragging = false;
                _dragAppearanceTimer?.Stop();
                RestorePersistentAppearance();
            }

            if (_profile.HasSit)
            {
                var sitMenu = BuildMenu();
                foreach (var topItem in sitMenu.Items)
                {
                    if (topItem is not MenuItem { Header: "换装" } dressSubmenu) continue;
                    foreach (var dressItem in dressSubmenu.Items)
                    {
                        if (dressItem is not MenuItem { Header: "盘腿坐（保持）" } sitItem) continue;
                        sitItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        break;
                    }
                    break;
                }
                await Task.Delay(500);
                Log($"selftest sit graph={_pet.DisplayType.Name}");
                Snap("05_sit");
                await Task.Delay(6500);
                Log($"selftest sit persistent={_pet.DisplayType.Name}, state={_state.Appearance}");
                ShowDragPose();
                Log($"selftest sit duringDrag={_pet.DisplayType.Name}");
            }

            OnBodyTouch();
            await Task.Delay(900);
            Snap("06_body");
            var cm = BuildMenu();
            Log($"selftest menu items={cm.Items.Count}");
            OpenInteractionRules();
            await Task.Delay(400);
            Log($"selftest rules window visible={_rules?.IsVisible}, autostart={StartupManager.IsEnabled()}");
            _rules?.Close();
            OpenChat();
            await Task.Delay(700);
            if (_chat is { } chat)
            {
                var overlap = Rect.Intersect(
                    new Rect(chat.Left, chat.Top, chat.ActualWidth, chat.ActualHeight),
                    PetScreenBounds);
                double overlapArea = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
                Log($"selftest chat window visible={chat.IsVisible}, " +
                    $"placement=({chat.Left:F0},{chat.Top:F0}) " +
                    $"petOverlap={overlapArea:F0}");

                var oldLeft = Left;
                var oldTop = Top;
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - _petBox - 2;
                Top = workArea.Bottom - _petBox - 2;
                chat.PlaceNear(this);
                await Task.Delay(120);
                var edgeOverlap = Rect.Intersect(
                    new Rect(chat.Left, chat.Top, chat.ActualWidth, chat.ActualHeight),
                    PetScreenBounds);
                double edgeArea = edgeOverlap.IsEmpty ? 0 : edgeOverlap.Width * edgeOverlap.Height;
                Log($"selftest chat edge placement=({chat.Left:F0},{chat.Top:F0}) petOverlap={edgeArea:F0}");
                Left = oldLeft;
                Top = oldTop;
            }
            var reply = await ChatAsync("你在工地上干嘛呢？");
            Log("selftest chat: " + (reply ?? "null"));
            await Task.Delay(600);
            Snap("07_chat");
            Log($"selftest state: food={_state.Food:F1} mood={_state.Mood:F1} aff={_state.Affection:F1} mode={_core.Save!.Mode}");

            var originalCharacter = _profile.Key;
            var characterDelay = int.TryParse(
                Environment.GetEnvironmentVariable("TOJIROU_CHARTEST_DELAY_MS"), out var configuredDelay)
                ? Math.Clamp(configuredDelay, 100, 10000)
                : 250;
            foreach (var character in _catalog.Characters)
            {
                if (character.Key == originalCharacter) continue;
                await SwitchCharacterAsync(character.Key);
                await Task.Delay(characterDelay);
                Log($"selftest character {character.Key}: started={_started}, graph={_pet.DisplayType.Name}");
                if (character.Key == "bunji") Snap("08_bunji");
            }
            await SwitchCharacterAsync(originalCharacter);
            await Task.Delay(300);

            PersistState();
            await Task.Delay(300);
            ExitApp();
        }

        private void Snap(string name)
        {
            try
            {
                var w = (int)Math.Max(1, Root.ActualWidth);
                var h = (int)Math.Max(1, Root.ActualHeight);
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(Root);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TojirouPet");
                Directory.CreateDirectory(dir);
                using var fs = File.Create(Path.Combine(dir, $"snap_{name}.png"));
                enc.Save(fs);
                Log($"snap {name} {w}x{h}");
            }
            catch (Exception ex)
            {
                Log("snap fail: " + ex.Message);
            }
        }

        public static void Log(string msg)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TojirouPet");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "log.txt"), $"{DateTime.Now:HH:mm:ss} {msg}\n");
            }
            catch { }
        }

        private void StartTimers()
        {
            _statTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statTimer.Tick += OnStatTick;
            _statTimer.Start();

            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _saveTimer.Tick += (_, _) => PersistState();
            _saveTimer.Start();

            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
            _idleTimer.Tick += OnIdleTick;
            _idleTimer.Start();
        }

        private void StopTimers()
        {
            _statTimer?.Stop();
            _saveTimer?.Stop();
            _idleTimer?.Stop();
            _statTimer = null;
            _saveTimer = null;
            _idleTimer = null;
        }

        // ---------- 状态系统 ----------

        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

        private void OnStatTick(object? sender, EventArgs e)
        {
            if (!_started) return;
            var now = DateTime.Now;
            double dt = Math.Min((now - _lastTick).TotalSeconds, 300);
            _lastTick = now;

            // 饱食约 14 小时见底，心情约 10 小时见底
            _state.Food = Clamp(_state.Food - dt * 100.0 / (14 * 3600), 0, 100);
            _state.Mood = Clamp(_state.Mood - dt * 100.0 / (10 * 3600), 0, 100);
            _state.Health = _state.Food <= 0
                ? Clamp(_state.Health - dt * 100.0 / (6 * 3600), 0, 100)
                : Clamp(_state.Health + dt * 100.0 / (10 * 3600), 0, 100);

            var mode = CalcMode();
            if (mode != _lastMode)
            {
                _lastMode = mode;
                RestorePersistentAppearance();
            }
            PushStats();
            UpdateTrayTip();

            if (_state.Food < 25 && (now - _lastHungry).TotalMinutes > 12)
            {
                _lastHungry = now;
                Say(_lines.Pick(_lines.Hungry), "surprise");
                _state.Mood = Clamp(_state.Mood - 2, 0, 100);
                PushStats();
            }
        }

        private IGameSave.ModeType CalcMode()
        {
            if (_state.Health < 40) return IGameSave.ModeType.Ill;
            if (_state.Food < 25 || _state.Mood < 25) return IGameSave.ModeType.PoorCondition;
            if (_state.Mood > 72 && _state.Food > 40) return IGameSave.ModeType.Happy;
            return IGameSave.ModeType.Nomal;
        }

        private void PushStats()
        {
            var save = _core.Save!;
            save.StrengthFood = _state.Food;
            save.Feeling = _state.Mood;
            save.Likability = _state.Affection;
            save.Health = _state.Health;
            save.Mode = _lastMode = CalcMode();
        }

        // ---------- 互动 ----------

        private void OnPetClicked()
        {
            if (!_started) return;
            _lastAutoSay = DateTime.Now;
            Say(_lines.Pick(_state.Mood < 25 ? _lines.LowMood : _lines.Click));
        }

        private void OnHeadTouch()
        {
            if (!_started) return;
            _lastHead = DateTime.Now;

            _state.Affection = Clamp(_state.Affection + 1.2, 0, 100);
            PushStats();

            bool close = _state.Affection >= 35;
            Say(_lines.Pick(close ? _lines.HeadHigh : _lines.HeadLow), close ? "laugh" : "angryface");
            PersistState();
        }

        private void OnBodyTouch()
        {
            if (!_started) return;
            _lastBody = DateTime.Now;

            _state.Affection = Clamp(_state.Affection + 0.5, 0, 100);
            PushStats();
            Say(_lines.Pick(_lines.Body), "angryface");
        }

        public void Feed(string kind)
        {
            if (!_started) return;
            switch (kind)
            {
                case "meat":
                    _state.Food = Clamp(_state.Food + 26, 0, 100);
                    _state.Mood = Clamp(_state.Mood + 6, 0, 100);
                    _state.Affection = Clamp(_state.Affection + 1.5, 0, 100);
                    Play("laugh");
                    Say(_lines.Pick(_lines.FeedMeat), "laugh");
                    break;
                case "rice":
                    _state.Food = Clamp(_state.Food + 14, 0, 100);
                    _state.Mood = Clamp(_state.Mood + 3, 0, 100);
                    _state.Affection = Clamp(_state.Affection + 0.5, 0, 100);
                    Play("laugh");
                    Say(_lines.Pick(_lines.FeedRice));
                    break;
                case "smoke":
                    _state.Mood = Clamp(_state.Mood + 16, 0, 100);
                    _state.Food = Clamp(_state.Food - 2, 0, 100);
                    _state.Affection = Clamp(_state.Affection + 1, 0, 100);
                    Play("smoke");
                    Say(_lines.Pick(_lines.FeedCig), "smoke");
                    break;
                case "beer":
                    _state.Food = Clamp(_state.Food + 8, 0, 100);
                    _state.Mood = Clamp(_state.Mood + 11, 0, 100);
                    _state.Affection = Clamp(_state.Affection + 1, 0, 100);
                    Play("laugh");
                    Say(_lines.Pick(_lines.FeedBeer));
                    break;
            }
            PushStats();
            PersistState();
            UpdateTrayTip();
        }

        // ---------- 显示辅助 ----------

        private DispatcherTimer? _bubbleTimer;

        private void Say(string text, string? graph = null)
        {
            if (!_started || string.IsNullOrWhiteSpace(text)) return;
            if (_dragging)
            {
                _deferredSpeech = text;
                _deferredGraph = graph;
                return;
            }
            _lastAutoSay = DateTime.Now;

            // 有持续服装/姿势时不播放临时表情，避免一句话就破坏当前外观。
            if (string.IsNullOrEmpty(_state.Appearance))
                try { _pet.Display(graph ?? "blink", AnimatType.Single, RestorePersistentAppearance); } catch { }

            var bw = Math.Clamp(_petBox * 1.8 + 60, 170, 320);
            Bubble.MaxWidth = bw;
            BubbleText.Text = text;
            // Collapsed elements report a DesiredSize of zero; show before measuring
            // so the window expands by the real bubble height instead of overlapping the pet.
            Bubble.Visibility = Visibility.Visible;
            Bubble.Measure(new Size(bw, double.PositiveInfinity));
            ExpandForBubble(bw, Bubble.DesiredSize.Height);

            if (_bubbleTimer == null)
            {
                _bubbleTimer = new DispatcherTimer();
                _bubbleTimer.Tick += (_, _) =>
                {
                    _bubbleTimer!.Stop();
                    Bubble.Visibility = Visibility.Collapsed;
                    CollapseBubble();
                };
            }
            _bubbleTimer.Stop();
            _bubbleTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(2.5 + text.Length * 0.12, 3, 12));
            _bubbleTimer.Start();
        }

        private void Play(string graph)
        {
            if (!_started) return;
            try { _pet.Display(graph, AnimatType.Single, RestorePersistentAppearance); } catch { }
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            t.Tick += (_, _) => { t.Stop(); RestorePersistentAppearance(); };
            t.Start();
        }

        private void ShowOutfit(string graph, string[] lines)
        {
            if (!_started) return;
            _state.Appearance = graph;
            PersistState();
            ApplyPersistentAppearance();
            Log($"outfit persistent={graph}");
            Say(_lines.Pick(lines));
        }

        private void SitDown()
        {
            if (!_started) return;
            _state.Appearance = "sit";
            PersistState();
            ApplyPersistentAppearance();
            Log("sit persistent");
            Say(_lines.Pick(_lines.Sit));
        }

        private void StandUp()
        {
            if (!_started) return;
            _state.Appearance = "";
            PersistState();
            RestorePersistentAppearance();
            Log("appearance default");
            Say("……站直了。");
        }

        private void ApplyPersistentAppearance()
        {
            if (!_started) return;
            RestorePersistentAppearance();
            Log($"appearance={(_state.Appearance.Length == 0 ? "default" : _state.Appearance)}");
        }

        private void RestorePersistentAppearance()
        {
            if (!_started) return;
            var appearance = _state.Appearance;
            if (!string.IsNullOrEmpty(appearance))
            {
                bool supported = appearance == "sit"
                    ? _profile.HasSit
                    : _profile.Outfits.Any(x => x.Graph == appearance);
                if (supported)
                {
                    try
                    {
                        var graph = _core.Graph?.FindGraph(
                            appearance, AnimatType.Single, _core.Save!.Mode);
                        if (graph != null)
                        {
                            graph.IsLoop = true;
                            _pet.Display(graph);
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
                _state.Appearance = "";
            }
            try { _pet.DisplayToNomal(); } catch { }
        }

        // ---------- 自动说话 ----------

        private void OnIdleTick(object? sender, EventArgs e)
        {
            if (!_started) return;

            if (DateTime.Now.Hour >= 0 && DateTime.Now.Hour < 5 && Random.Shared.NextDouble() < 0.5)
                Say(_lines.Pick(_lines.LateNight));
            else if (_state.Mood < 25)
                Say(_lines.Pick(_lines.LowMood));
            else
                Say(_lines.Pick(_lines.Idle));
        }

        // ---------- AI 对话 ----------

        public async Task<string?> ChatAsync(string text)
        {
            _state.Mood = Clamp(_state.Mood + 1.5, 0, 100);
            _state.Affection = Clamp(_state.Affection + 0.4, 0, 100);
            PushStats();
            try
            {
                var reply = await _ai.AskAsync(text, _state.Food, _state.Mood, _state.Affection);
                Say(reply);
                return reply;
            }
            catch (Exception)
            {
                Say(_lines.Pick(_lines.ChatError));
                return null;
            }
        }

        public IReadOnlyList<ChatMessage> GetChatHistory() => _ai.History;

        public void ClearChatHistory()
        {
            _ai.ClearHistory();
        }

        public void OpenChat()
        {
            if (_chat is { IsLoaded: true })
            {
                _chat.Activate();
                return;
            }
            _chat = new ChatWindow(this);
            _chat.Closed += (_, _) => _chat = null;
            _chat.Show();
            _chat.PlaceNear(this);
            if (_started)
                Say(_lines.Pick(_lines.ChatOpen));
        }

        // ---------- 菜单 ----------

        public void ShowPetMenu()
        {
            if (!_started) return;
            var cm = BuildMenu();
            cm.PlacementTarget = this;
            cm.IsOpen = true;
        }

        private static MenuItem Item(string header, Action action)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => action();
            return mi;
        }

        private ContextMenu BuildMenu()
        {
            var cm = new ContextMenu();

            cm.Items.Add(new MenuItem
            {
                Header = $"{PetName}　饱食 {_state.Food:F0} · 心情 {_state.Mood:F0} · 好感 {_state.Affection:F0}",
                IsEnabled = false
            });
            cm.Items.Add(new Separator());

            var chooseCharacter = new MenuItem { Header = "选择角色" };
            foreach (var character in _catalog.Characters)
            {
                var item = new MenuItem
                {
                    Header = $"{character.Name}　{character.Species}",
                    IsCheckable = true,
                    IsChecked = character.Key == _profile.Key
                };
                item.Click += (_, _) => _ = SwitchCharacterAsync(character.Key);
                chooseCharacter.Items.Add(item);
            }
            cm.Items.Add(chooseCharacter);

            var feed = new MenuItem { Header = "投喂" };
            feed.Items.Add(Item("烤肉　饱食+26 心情+6", () => Feed("meat")));
            feed.Items.Add(Item("饭团　饱食+14", () => Feed("rice")));
            feed.Items.Add(Item("递根烟　心情+16", () => Feed("smoke")));
            feed.Items.Add(Item("啤酒　心情+11", () => Feed("beer")));
            cm.Items.Add(feed);

            cm.Items.Add(Item("摸摸头", () => { OnHeadTouch(); }));
            cm.Items.Add(Item($"和{PetName}说话…", OpenChat));
            cm.Items.Add(Item("互动规则", OpenInteractionRules));

            var dress = new MenuItem { Header = "换装" };
            dress.Items.Add(Item("默认站姿", StandUp));
            if (_profile.Outfits.Count > 0)
                dress.Items.Add(new Separator());
            foreach (var outfit in _profile.Outfits)
                dress.Items.Add(Item(outfit.Label, () => ShowOutfit(outfit.Graph, _lines.Outfit(outfit.Graph))));
            if (_profile.HasSit)
            {
                dress.Items.Add(new Separator());
                dress.Items.Add(Item("盘腿坐（保持）", SitDown));
            }
            cm.Items.Add(dress);

            cm.Items.Add(new Separator());

            var zoomMenu = new MenuItem { Header = "大小" };
            zoomMenu.Items.Add(Item("小（180px）", () => ApplyScale(0.36)));
            zoomMenu.Items.Add(Item("中（默认 · 270px / 3/4）", () => ApplyScale(0.54)));
            zoomMenu.Items.Add(Item("大（360px）", () => ApplyScale(0.72)));
            zoomMenu.Items.Add(Item("特大（450px）", () => ApplyScale(0.9)));
            cm.Items.Add(zoomMenu);

            var autoStart = new MenuItem
            {
                Header = "开机自启动",
                IsCheckable = true,
                IsChecked = _state.AutoStart
            };
            autoStart.Click += (_, _) =>
            {
                bool enabled = autoStart.IsChecked;
                try
                {
                    StartupManager.SetEnabled(enabled);
                    _state.AutoStart = enabled;
                    PersistState();
                    Log($"autostart changed to {enabled}");
                }
                catch (Exception ex)
                {
                    autoStart.IsChecked = !enabled;
                    Log("autostart change fail: " + ex.Message);
                    MessageBox.Show("开机自启动设置失败：" + ex.Message, PetName,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            cm.Items.Add(autoStart);

            var top = new MenuItem { Header = "总在最前", IsCheckable = true, IsChecked = Topmost };
            top.Click += (_, _) =>
            {
                Topmost = !Topmost;
                _state.TopMost = Topmost;
                PersistState();
            };
            cm.Items.Add(top);

            cm.Items.Add(Item("躲到托盘", HideToTray));
            cm.Items.Add(Item("退出", ExitApp));
            return cm;
        }

        public void OpenInteractionRules()
        {
            if (_rules is { IsLoaded: true })
            {
                _rules.Activate();
                return;
            }

            _rules = new InteractionRulesWindow();
            _rules.Closed += (_, _) => _rules = null;
            _rules.Show();
            _rules.PlaceNear(this);
        }

        public async Task SwitchCharacterAsync(string key)
        {
            if (_switchingCharacter || !_started) return;
            var next = _catalog.Find(key);
            if (next.Key == _profile.Key) return;

            _switchingCharacter = true;
            try
            {
                PersistState();
                _started = false;
                StopTimers();
                _state.SwitchCharacter(next.Key);

                _profile = next;
                _lines = DialogueSet.Create(next);
                _ai.SetCharacter(next);
                Title = next.Name;
                _chat?.Close();
                _rules?.Close();
                UpdateTrayTip();

                try { _pet.Dispose(); } catch { }
                PetHost.Child = null;
                Log($"switch character -> {next.Key}");
                await InitPetAsync(false);
            }
            finally
            {
                _switchingCharacter = false;
            }
        }

        // ---------- 窗口 / 托盘 ----------

        public void MoveBy(double x, double y)
        {
            Left += x;
            Top += y;
        }

        private void ApplyScale(double scale)
        {
            scale = Math.Clamp(scale, 0.12, 1.2);
            var newBox = 500 * scale;
            var oldScale = _petBox / 500.0;
            if (_state != null)
                _state.Scale = scale;
            if (IsLoaded && Math.Abs(newBox - _petBox) > 0.5)
            {
                // 保持脚底位置不变：窗口变小则下移，水平居中
                Top += _petBox - newBox;
                Left += (_petBox - newBox) / 2;
            }
            _petBox = newBox;
            PetHost.Width = _petBox;
            PetHost.Height = _petBox;
            Height = _petBox + _bubbleExtra;
            Width = _petBox;

            if (_started && Math.Abs(newBox - oldScale * 500) > 0.5)
            {
                UpdateLayout();
                Log($"scale {oldScale:F2}->{scale:F2}, window={Width:F0}x{Height:F0}");
                PersistState();
            }
        }

        /// <summary>说话时把窗口向上撑开一块放气泡（保持宠物位置不动）</summary>
        private void ExpandForBubble(double needW, double needH)
        {
            if (_dragging) return;
            var extra = Math.Ceiling(needH) + 8;
            if (extra <= _bubbleExtra)
            {
                Height = _petBox + _bubbleExtra;
                return;
            }
            var dh = extra - _bubbleExtra;
            var dw = Math.Max(0, Math.Ceiling(needW) + 12 - Width);
            Top -= dh;
            Left -= dw / 2;
            _bubbleExtra = extra;
            Height = _petBox + _bubbleExtra;
            Width = _petBox + dw;
        }

        /// <summary>气泡消失后窗口缩回宠物本体大小</summary>
        private void CollapseBubble()
        {
            if (_dragging) return;
            if (_bubbleExtra <= 0) return;
            Top += _bubbleExtra;
            Left += (Width - _petBox) / 2;
            _bubbleExtra = 0;
            Height = _petBox;
            Width = _petBox;
        }

        // ---------- 跟手拖拽 ----------

        private Point GetCursorScreenDip(MouseEventArgs e)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var screen = PointToScreen(e.GetPosition(this));
            return new Point(screen.X / dpi.DpiScaleX, screen.Y / dpi.DpiScaleY);
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_started || e.ChangedButton != MouseButton.Left) return;

            _dragArmed = true;
            _dragStartTime = DateTime.Now;
            _dragStartCursor = GetCursorScreenDip(e);
            _dragStartWindow = new Point(Left, Top);
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed)
                return;

            var cursor = GetCursorScreenDip(e);
            if (!_dragging)
            {
                var dx = cursor.X - _dragStartCursor.X;
                var dy = cursor.Y - _dragStartCursor.Y;
                if (dx * dx + dy * dy < DragStartDistance * DragStartDistance)
                    return;

                _dragging = true;
                _dragHadBubble = Bubble.Visibility == Visibility.Visible;
                _bubbleTimer?.Stop();
                _pet.isPress = false; // 取消 Core 的延迟点击/长按动作
                Mouse.Capture(this, CaptureMode.SubTree);
                ShowDragPose();
                StartDragAppearanceGuard();
                Log("drag begin");
            }

            Left = _dragStartWindow.X + cursor.X - _dragStartCursor.X;
            Top = _dragStartWindow.Y + cursor.Y - _dragStartCursor.Y;
            EnsureDragAppearance();
            e.Handled = true;
        }

        private void ShowDragPose()
        {
            if (string.IsNullOrEmpty(_state.Appearance))
                try { _pet.Display("surprise", AnimatType.Single); } catch { }
        }

        private void StartDragAppearanceGuard()
        {
            if (string.IsNullOrEmpty(_state.Appearance)) return;
            if (_dragAppearanceTimer == null)
            {
                _dragAppearanceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _dragAppearanceTimer.Tick += (_, _) => EnsureDragAppearance();
            }
            _dragAppearanceTimer.Start();
        }

        private void EnsureDragAppearance()
        {
            if (!_dragging || string.IsNullOrEmpty(_state.Appearance)) return;
            var expected = _state.Appearance;
            if (string.Equals(_pet.DisplayType.Name, expected, StringComparison.Ordinal))
                return;
            Log($"drag appearance restore: {_pet.DisplayType.Name} -> {expected}");
            RestorePersistentAppearance();
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragArmed) return;

            _dragArmed = false;
            if (_dragging)
            {
                FinishDrag();
                e.Handled = true;
                return;
            }

            _pet.isPress = false;
            HandleDirectClick(e);
            e.Handled = true;
        }

        private void HandleDirectClick(MouseButtonEventArgs e)
        {
            if (DateTime.Now - _dragStartTime > TimeSpan.FromMilliseconds(750))
            {
                Log("click ignored: long press");
                return;
            }

            var point = e.GetPosition(_pet.MainGrid);
            var config = _core.Graph!.GraphConfig;
            var foodBefore = _state.Food;
            var moodBefore = _state.Mood;
            if (Contains(point, config.TouchHeadLocate, config.TouchHeadSize))
            {
                Log("direct click: head");
                OnHeadTouch();
            }
            else if (Contains(point, config.TouchBodyLocate, config.TouchBodySize))
            {
                Log("direct click: body");
                OnBodyTouch();
            }
            else
            {
                Log("direct click: generic");
                OnPetClicked();
            }
            Log($"direct click status foodDelta={_state.Food - foodBefore:F3} " +
                $"moodDelta={_state.Mood - moodBefore:F3}");
        }

        private static bool Contains(Point point, Point locate, Size size) =>
            point.X >= locate.X && point.Y >= locate.Y &&
            point.X <= locate.X + size.Width && point.Y <= locate.Y + size.Height;

        private void Window_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragArmed = false;
            FinishDrag();
        }

        private void FinishDrag()
        {
            _dragging = false;
            _dragAppearanceTimer?.Stop();
            if (Mouse.Captured == this)
                Mouse.Capture(null);

            RestorePersistentAppearance();

            if (_dragHadBubble)
            {
                Bubble.Visibility = Visibility.Collapsed;
                CollapseBubble();
            }
            _dragHadBubble = false;

            PersistState();
            Log($"drag end pos=({Left:F0},{Top:F0})");

            if (_deferredSpeech != null)
            {
                var text = _deferredSpeech;
                var graph = _deferredGraph;
                _deferredSpeech = null;
                _deferredGraph = null;
                Say(text, graph);
            }
        }

        private void RestorePosition()
        {
            var wa = SystemParameters.WorkArea;
            if (_state.WindowLeft < 0 || _state.WindowTop < 0)
            {
                Left = wa.Right - _petBox - 40;
                Top = wa.Bottom - _petBox;
            }
            else
            {
                Left = _state.WindowLeft;
                Top = _state.WindowTop;
            }
        }

        private void InitTray()
        {
            _tray = new Forms.NotifyIcon
            {
                Icon = LoadAppIcon(),
                Visible = true,
                Text = PetName
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示 / 隐藏", null, (_, _) => ToggleVisible());
            menu.Items.Add("说句话", null, (_, _) => Say(_lines.Pick(_lines.Idle)));
            menu.Items.Add("对话…", null, (_, _) => OpenChat());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApp());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, _) => ToggleVisible();
            UpdateTrayTip();
        }

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                var path = Environment.ProcessPath;
                if (path != null)
                {
                    var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (ico != null) return ico;
                }
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private void UpdateTrayTip()
        {
            if (_tray == null) return;
            _tray.Text = $"{PetName}　饱食 {_state.Food:F0} · 心情 {_state.Mood:F0} · 好感 {_state.Affection:F0}";
        }

        private void ToggleVisible()
        {
            if (IsVisible) Hide();
            else
            {
                Show();
                Activate();
            }
        }

        private void HideToTray()
        {
            Hide();
            if (_tray != null)
                _tray.ShowBalloonTip(2000, PetName, "躲到托盘了，双击图标召回。", Forms.ToolTipIcon.None);
        }

        public void ExitApp()
        {
            PersistState();
            try { _pet?.Dispose(); } catch { }
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            Application.Current.Shutdown();
        }

        private void PersistState()
        {
            if (_state == null) return;
            _state.WindowLeft = Left + (Width - _petBox) / 2;   // 存无气泡时的基准位置
            _state.WindowTop = Top + _bubbleExtra;
            _state.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PersistState();
            try { _pet?.Dispose(); } catch { }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
        }

        // 右键：用隧道事件拦截，避免 Core 自带的 ToolBar 弹出
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);
            ShowPetMenu();
            e.Handled = true;
        }
    }
}
