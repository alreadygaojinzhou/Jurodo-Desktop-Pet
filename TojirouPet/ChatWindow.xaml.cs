using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TojirouPet
{
    public partial class ChatWindow : Window
    {
        private readonly MainWindow _owner;
        private bool _busy;

        public ChatWindow(MainWindow owner)
        {
            _owner = owner;
            InitializeComponent();
            TitleText.Text = owner.PetName;
            var history = owner.GetChatHistory();
            if (history.Count == 0)
                AddMessage("assistant", "……说。");
            else
                foreach (var message in history)
                    AddMessage(message.Role, message.Content);

            var cfg = AppConfig.Load();
            if (!cfg.IsReady)
            {
                Hint.Visibility = Visibility.Visible;
                Hint.Text = "还没配置 AI 接口：编辑程序目录下的 config.json（ApiBaseUrl / ApiKey / Model），重启后生效。";
            }

            Loaded += (_, _) => Input.Focus();
            Loaded += (_, _) => PlaceNear(_owner);
            SizeChanged += (_, _) =>
            {
                if (IsLoaded) PlaceNear(_owner);
            };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }

        public void PlaceNear(Window pet)
        {
            var wa = SystemParameters.WorkArea;
            UpdateLayout();
            double w = Math.Min(Math.Max(Width, 280), Math.Max(280, wa.Width - 16));
            double h = Math.Max(ActualHeight, 160);
            var petBounds = _owner.PetScreenBounds;
            const double gap = 12;
            const double edge = 8;

            var candidates = new[]
            {
                new Point(petBounds.Right + gap, petBounds.Bottom - h),
                new Point(petBounds.Left - w - gap, petBounds.Bottom - h),
                new Point(petBounds.Left + (petBounds.Width - w) / 2, petBounds.Top - h - gap),
                new Point(petBounds.Left + (petBounds.Width - w) / 2, petBounds.Bottom + gap)
            };

            Rect? best = null;
            double bestOverlap = double.MaxValue;
            foreach (var candidate in candidates)
            {
                var rect = new Rect(
                    Math.Clamp(candidate.X, wa.Left + edge, wa.Right - w - edge),
                    Math.Clamp(candidate.Y, wa.Top + edge, wa.Bottom - h - edge),
                    w,
                    h);
                double overlap = OverlapArea(rect, petBounds);
                if (overlap >= bestOverlap) continue;
                bestOverlap = overlap;
                best = rect;
                if (overlap == 0) break;
            }

            if (best is not Rect placement) return;
            Left = placement.Left;
            Top = placement.Top;
        }

        private static double OverlapArea(Rect a, Rect b)
        {
            var overlap = Rect.Intersect(a, b);
            return overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("清空与当前角色的聊天记忆？", TitleText.Text,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _owner.ClearChatHistory();
            Transcript.Children.Clear();
            AddMessage("assistant", "……重新说吧。");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                _ = SendAsync();
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e) => _ = SendAsync();

        private async System.Threading.Tasks.Task SendAsync()
        {
            if (_busy) return;
            var text = Input.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            var cfg = AppConfig.Load();
            if (!cfg.IsReady)
            {
                Hint.Visibility = Visibility.Visible;
                Hint.Text = "缺少配置：请在程序目录的 config.json 里填写 ApiBaseUrl、ApiKey、Model。";
                return;
            }

            _busy = true;
            AddMessage("user", text);
            Input.Clear();
            Hint.Visibility = Visibility.Collapsed;
            var thinking = AddMessage("assistant", "……");

            var reply = await _owner.ChatAsync(text);
            thinking.Text = reply ?? "……没信号。";
            _busy = false;
            TranscriptScroll.ScrollToEnd();
            Input.Focus();
        }

        private TextBlock AddMessage(string role, string text)
        {
            bool user = role == "user";
            var content = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(user
                    ? Color.FromRgb(0xFF, 0xFF, 0xFF)
                    : Color.FromRgb(0xF1, 0xE7, 0xD8)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };
            var bubble = new Border
            {
                Child = content,
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(user ? 54 : 0, 3, user ? 0 : 54, 3),
                CornerRadius = new CornerRadius(9),
                HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 285,
                Background = new SolidColorBrush(user
                    ? Color.FromArgb(0xD9, 0xC8, 0x73, 0x2E)
                    : Color.FromArgb(0xFF, 0x28, 0x22, 0x1A))
            };
            Transcript.Children.Add(bubble);
            Dispatcher.BeginInvoke(new Action(() => TranscriptScroll.ScrollToEnd()));
            return content;
        }
    }
}
