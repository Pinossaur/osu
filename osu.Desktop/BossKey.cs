// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using DiscordRPC;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Online.Multiplayer;

namespace osu.Desktop
{
    internal partial class BossKey : Component
    {
        //WinApi dependencies
        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);
        [DllImport("shell32.dll")]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        //Delegates
        private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        //public event EventHandler OnClickNotifyIconEvent;

        //Enums
        [Flags]
        private enum NotifyIconAction
        {
            ADD = 0x00000000,
            DELETE = 0x00000002,
        }

        [Flags]
        private enum NotificationIconParameters
        {
            MESSAGE = 0x00000001,
            ICON = 0x00000002,
            UID = 727,
        }

        [Flags]
        private enum WindowEvent
        {
            CLOSE = 0x10,
            TRAYICON = 0x8000 + 0x1,
            LBUTTONDOWN = 0x0201,
            RBUTTONDOWN = 0x0204,
        }

        //Structs
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
        }

        private static WndProcDelegate? wndProcDelegate;

        private NOTIFYICONDATA notifyIconData;

        [BackgroundDependencyLoader]
        private void load()
        {
            var appLocation = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (appLocation is null)
            {
                return;
            }

            var appIcon = ExtractIcon(IntPtr.Zero, appLocation, 0);

            notifyIconData = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                uID = (int)NotificationIconParameters.UID,
                uFlags = (int)(NotificationIconParameters.MESSAGE | NotificationIconParameters.ICON),
                uCallbackMessage = (int)WindowEvent.TRAYICON,
                //hWnd = process.MainWindowHandle,
                hIcon = appIcon,
            };

            
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            CreateTaskbarIcon();
        }

        private bool CreateTaskbarIcon()
        {
            IntPtr hWnd = CreateWindowEx(0, "STATIC", "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            wndProcDelegate = new WndProcDelegate(HiddenWndProc);
            IntPtr wndProcPtr = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
            SetWindowLongPtr(hWnd, -4, wndProcPtr);

            notifyIconData.hWnd = hWnd;

            return Shell_NotifyIcon((int)NotifyIconAction.ADD, ref notifyIconData);
        }

        private IntPtr HiddenWndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == (int)WindowEvent.TRAYICON)
            {
                var mouseEvent = lParam.ToInt32();
                if (mouseEvent == (int)WindowEvent.LBUTTONDOWN || mouseEvent == (int)WindowEvent.RBUTTONDOWN)
                {
                    Shell_NotifyIcon((int)NotifyIconAction.DELETE, ref notifyIconData);

                    DestroyWindow(hWnd);
                    return IntPtr.Zero;
                }

                return DefWindowProc(hWnd, uMsg, wParam, lParam);
            }
            else if (uMsg == (int)WindowEvent.CLOSE)
            {
                DestroyWindow(hWnd);
                return IntPtr.Zero;
            }
            else
            {
                return DefWindowProc(hWnd, uMsg, wParam, lParam);
            }
        }
    }
}
