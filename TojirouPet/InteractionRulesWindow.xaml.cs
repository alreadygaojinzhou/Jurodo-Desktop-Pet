using System;
using System.Windows;
using System.Windows.Input;

namespace TojirouPet
{
    public partial class InteractionRulesWindow : Window
    {
        public InteractionRulesWindow()
        {
            InitializeComponent();
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }

        public void PlaceNear(Window pet)
        {
            var wa = SystemParameters.WorkArea;
            Left = Math.Min(Math.Max(wa.Left + 8, pet.Left - Width - 12), wa.Right - Width - 8);
            Top = Math.Min(Math.Max(wa.Top + 8, pet.Top + pet.ActualHeight - Height), wa.Bottom - Height - 8);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
