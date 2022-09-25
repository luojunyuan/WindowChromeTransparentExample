using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Vanara.PInvoke;

namespace TestDragWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static nint _hwnd;
        public MainWindow()
        {
            InitializeComponent();
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            Loaded += PerformanceDesktopTransparentWindow_Loaded;
            Loaded += (_, _) => Install();
            Closed += (_, _) => _hookId?.Close();
        }

        private void PerformanceDesktopTransparentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Add WS_EX_LAYERED style
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(
                (nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) =>
                {
                    if (msg == (int)User32.WindowMessage.WM_STYLECHANGING &&
                        wParam == (long)User32.WindowLongFlags.GWL_EXSTYLE)
                    {
                        var styleStruct = (STYLESTRUCT)Marshal.PtrToStructure(lParam, typeof(STYLESTRUCT));
                        styleStruct.styleNew |= User32.WindowStylesEx.WS_EX_LAYERED;
                        Marshal.StructureToPtr(styleStruct, lParam, false);
                        handled = true;
                    }
                    return IntPtr.Zero;
                });
        }

        private static nint CurrentExStyle => User32.GetWindowLongPtr(_hwnd, User32.WindowLongFlags.GWL_EXSTYLE);

        public static void SetTransparentHitThrough() => 
            User32.SetWindowLong(_hwnd, User32.WindowLongFlags.GWL_EXSTYLE, CurrentExStyle | (nint)User32.WindowStylesEx.WS_EX_TRANSPARENT);

        public static void SetTransparentNotHitThrough() => 
            User32.SetWindowLong(_hwnd, User32.WindowLongFlags.GWL_EXSTYLE, CurrentExStyle & ~(nint)User32.WindowStylesEx.WS_EX_TRANSPARENT);

        private static User32.SafeHHOOK? _hookId;
        public static void Install()
        {
            var moduleHandle = Kernel32.GetModuleHandle();

            _hookId = User32.SetWindowsHookEx(User32.HookType.WH_MOUSE_LL, Hook, moduleHandle, 0);
            if (_hookId == nint.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        private static bool CurrentTransparent = false;

        private static double CurrentDpi => WpfScreenHelper.Screen.FromWindow(Application.Current.MainWindow).ScaleFactor;
        private static IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return User32.CallNextHookEx(_hookId, nCode, wParam, lParam);

            var obj = Marshal.PtrToStructure(lParam, typeof(User32.MSLLHOOKSTRUCT));
            if (obj is not User32.MSLLHOOKSTRUCT info)
                return User32.CallNextHookEx(_hookId, nCode, wParam, lParam);

            // Check position
            var win = Application.Current.MainWindow;
            var relativePoint = new Point(info.pt.X / CurrentDpi - win.Left, info.pt.Y / CurrentDpi - win.Top);
            if (VisualTreeHelper.HitTest((Grid)win.Content, relativePoint) != null)
            {
                if (CurrentTransparent == true)
                {
                    SetTransparentNotHitThrough();
                }
                CurrentTransparent = false;
            }
            else
            {
                if (CurrentTransparent == false)
                {
                    SetTransparentHitThrough();
                }
                CurrentTransparent = true;
            }


            return User32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }

    public struct STYLESTRUCT
    {
        public User32.WindowStylesEx styleOld;
        public User32.WindowStylesEx styleNew;
    }
}
