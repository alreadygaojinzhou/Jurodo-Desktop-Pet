using System;
using System.Windows;
using VPet_Simulator.Core;

namespace TojirouPet
{
    /// <summary>把 VPet Core 需要的窗体控制接口接到我们的主窗口上</summary>
    public class PetController : IController
    {
        private readonly MainWindow _win;

        public PetController(MainWindow win) => _win = win;

        public void MoveWindows(double X, double Y) => _win.MoveBy(X * ZoomRatio, Y * ZoomRatio);

        public double GetWindowsDistanceLeft() => _win.Left - SystemParameters.WorkArea.Left;

        public double GetWindowsDistanceRight() =>
            SystemParameters.WorkArea.Right - (_win.Left + _win.ActualWidth);

        public double GetWindowsDistanceUp() => _win.Top - SystemParameters.WorkArea.Top;

        public double GetWindowsDistanceDown() =>
            SystemParameters.WorkArea.Bottom - (_win.Top + _win.ActualHeight);

        public double ZoomRatio => _win.PetZoom;

        public int PressLength => 800;

        public void ShowPanel() => _win.ShowPetMenu();

        public void ResetPosition()
        {
        }

        public bool CheckPosition() => false;

        public bool EnableFunction => true;

        public int InteractionCycle => 200;

        public bool RePositionActive { get; set; }
    }
}
