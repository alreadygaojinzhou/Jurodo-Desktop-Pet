using System;
using System.Windows;
using System.Windows.Input;

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

            var cfg = AppConfig.Load();
            if (!cfg.IsReady)
            {
                Hint.Visibility = Visibility.Visible;
                Hint.Text = "还没配置 AI 接口：编辑程序目录下的 config.json（ApiBaseUrl / ApiKey / Model），重启后生效。";
            }

            Loaded += (_, _) => Input.Focus();
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }

        public void PlaceNear(Window pet)
        {
            var wa = SystemParameters.WorkArea;
            double w = Width;
            double h = ActualHeight > 0 ? ActualHeight : 220;

            Left = Math.Min(Math.Max(wa.Left + 8, pet.Left - w - 12), wa.Right - w - 8);
            Top = Math.Min(Math.Max(wa.Top + 8, pet.Top + pet.ActualHeight - h), wa.Bottom - h - 8);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
            Input.Clear();
            Reply.Text = "……";
            Hint.Visibility = Visibility.Collapsed;

            var reply = await _owner.ChatAsync(text);
            Reply.Text = reply ?? "……没信号。";
            _busy = false;
            Input.Focus();
        }
    }
}
