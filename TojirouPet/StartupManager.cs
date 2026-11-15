using System;
using System.IO;
using Microsoft.Win32;

namespace TojirouPet
{
    /// <summary>管理当前用户的 Windows 开机启动项，不需要管理员权限。</summary>
    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TojirouPet";

        private static string ExecutablePath =>
            Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TojirouPet.exe");

        private static string ExpectedCommand => $"\"{ExecutablePath}\"";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(ValueName) as string;
            return string.Equals(Normalize(value), Normalize(ExpectedCommand), StringComparison.OrdinalIgnoreCase);
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
                ?? throw new InvalidOperationException("无法打开当前用户的启动项注册表。");

            if (enabled)
                key.SetValue(ValueName, ExpectedCommand, RegistryValueKind.String);
            else
                key.DeleteValue(ValueName, false);
        }

        private static string Normalize(string? command) =>
            (command ?? string.Empty).Trim().Trim('"');
    }
}
