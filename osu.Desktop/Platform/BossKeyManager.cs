using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;

namespace osu.Desktop.Platform
{
    internal partial class BossKeyManager : Component, IKeyBindingHandler<GlobalAction>, IHandleGlobalKeyboardInput
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private VolumeOverlay volumeOverlay { get; set; } = null!;

        //WinApi imports
        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("shell32.dll")]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);


        //Delegates
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

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
        private enum PeekMessageArguments
        {
            WM_MOUSEFIRST = 0x00000200,
            PM_REMOVE = 0x00000001,
        }

        [Flags]
        private enum WindowEvent
        {
            CLOSE = 0x00000010,
            TRAYICON = 0x00008001,
            LBUTTONDOWN = 0x00000201,
            RBUTTONDOWN = 0x00000204,
        }

        //Structs
        struct MSG
        {
            internal IntPtr hWnd;
            internal uint message;
            internal IntPtr wParam;
            internal IntPtr lParam;
            internal uint time;
            internal System.Drawing.Point pt;
        }

        private struct NOTIFYICONDATA
        {
            internal int cbSize;
            internal IntPtr hWnd;
            internal int uID;
            internal int uFlags;
            internal int uCallbackMessage;
            internal IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szTip;
        }

        private NOTIFYICONDATA notifyIconData;

        private bool previousAudioMuteState = false;
        private bool isBossKeyState = false;

        [BackgroundDependencyLoader]
        private void load()
        {
            var appLocation = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if(appLocation is null)
            {
                //If for some reason we can't fetch the icon from the executable, show no icon.
                //Ideally we'd find an alternative to directly load the .ico, but afaik there's no good way to directly load it.
                //Perhaps a placeholder icon could be loaded, but as I've yet to see GetCurrentProcess failing, it's probably not worth the effort.
                notifyIconData = CreateNotificationIconData(IntPtr.Zero);

                return;
            }

            var appIcon = ExtractIcon(IntPtr.Zero, appLocation, 0);

            notifyIconData = CreateNotificationIconData(appIcon);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.BossKey && !e.Repeat)
            {
                host.Window.Hide();

                previousAudioMuteState = volumeOverlay.IsMuted.Value;
                volumeOverlay.IsMuted.Value = true;
                isBossKeyState = true;

                Task.Factory.StartNew(CreateTaskbarIcon,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                return true;
            }

            return false;
        }

        private static WndProcDelegate? wndProcDelegate;

        private void CreateTaskbarIcon()
        {
            IntPtr hWnd = CreateWindowEx(0, "STATIC", "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            wndProcDelegate = new WndProcDelegate(HandleNotificationItemEvents);
            IntPtr wndProcPtr = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
            SetWindowLongPtr(hWnd, -4, wndProcPtr);

            notifyIconData.hWnd = hWnd;

            Shell_NotifyIcon((int)NotifyIconAction.ADD, ref notifyIconData);

            MSG msg;


            while (isBossKeyState)
            {
                if (PeekMessage(out msg, hWnd, (uint)PeekMessageArguments.WM_MOUSEFIRST, (uint)PeekMessageArguments.WM_MOUSEFIRST, (uint)PeekMessageArguments.PM_REMOVE))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                //When hidden by the boss key, this is looping to handle inpts. This reduces the number of polls, and thus CPU usage.
                //CPU usage should be kept to an absolute minimum, given you can't access the game while on boss mode.
                Thread.Sleep(50);
            }

            return;
        }

        private NOTIFYICONDATA CreateNotificationIconData(nint icon)
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                uID = (int)NotificationIconParameters.UID,
                uFlags = (int)(NotificationIconParameters.MESSAGE | NotificationIconParameters.ICON),
                uCallbackMessage = (int)WindowEvent.TRAYICON,
                hIcon = icon,
                szTip = "osu!",
            };
        }

        private IntPtr HandleNotificationItemEvents(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == (int)WindowEvent.TRAYICON)
            {
                var mouseEvent = lParam.ToInt32();
                if (mouseEvent == (int)WindowEvent.LBUTTONDOWN || mouseEvent == (int)WindowEvent.RBUTTONDOWN)
                {
                    Shell_NotifyIcon((int)NotifyIconAction.DELETE, ref notifyIconData);

                    //Audio actions have to be called through the scheduler because this handler isn't running on the main thread.
                    Scheduler.Add(new ScheduledDelegate(() => volumeOverlay.IsMuted.Value = previousAudioMuteState));

                    //Immediately hides the volume overlay to avoid it showing for a split second after showing the window.
                    Scheduler.Add(new ScheduledDelegate(() => volumeOverlay.Hide()));

                    host.Window.Show();
                    host.Window.Raise();

                    isBossKeyState = false;

                    DestroyWindow(hWnd);
                }

                return IntPtr.Zero;
            }
            else if (uMsg == (int)WindowEvent.CLOSE)
            {
                return IntPtr.Zero;
            }
            else
            {
                return DefWindowProc(hWnd, uMsg, wParam, lParam);
            }
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e) { }
    }
}
