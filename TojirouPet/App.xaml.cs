using System;
using System.Threading;
using System.Windows;

namespace TojirouPet
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "TojirouPet_SingleInstance", out bool created);
            if (!created)
            {
                MessageBox.Show("桌宠已经在桌面上了。", "桌宠");
                Shutdown();
                return;
            }
            base.OnStartup(e);
            var win = new MainWindow();
            MainWindow = win;
            win.Show();
        }
    }
}
