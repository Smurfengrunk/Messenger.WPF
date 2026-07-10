#pragma warning disable CS8602, CS8618

using System;
using System.Runtime.InteropServices;

using Microsoft.Win32;

namespace Messenger
{
    /// <summary>
    /// Static class for managing the system tray icon and its associated functionality,
    /// including updating unread counts, showing context menus, and handling user interactions with the tray icon.
    /// </summary>
    public static class TrayIcon
    {
        private static NOTIFYICONDATA _data;
        private static MainWindow? _window;
        private static Settings _settings = Settings.Load();
        private static IntPtr _defaultIcon;
        private static IntPtr _currentTrayIcon;
        private static IntPtr _currentOverlayIcon;
        private static bool _currentTrayIconIsOwned;
        private static int _lastUnreadCount;
        private static bool? _lastShowBadges;

        private const int WM_TRAY = 0x800;

        /// <summary>
        /// Initializes the tray icon with the specified main window, icon handle, and tooltip text.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="hIcon"></param>
        /// <param name="tooltip"></param>
        public static void Initialize(MainWindow window, IntPtr hIcon, string tooltip)
        {
            _window = window;
            _defaultIcon = hIcon;
            _currentTrayIcon = hIcon;
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;

            _data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAY,
                hIcon = hIcon,
                szTip = tooltip
            };

            Shell_NotifyIcon(NIM_ADD, ref _data);
        }

        /// <summary>
        /// Updates the unread count displayed on the tray icon and taskbar overlay badge, if applicable.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="count"></param>
        /// <param name="showBadges"></param>
        public static void UpdateUnread(MainWindow window, int count, bool showBadges)
        {
            if (_lastUnreadCount == count && _lastShowBadges == showBadges)
                return;

            _lastUnreadCount = count;
            _lastShowBadges = showBadges;

            // Change tray icon
            IntPtr hIcon = count > 0
                ? BadgeIconFactory.CreateTrayIcon(count)
                : _defaultIcon;

            bool hIconIsOwned = count > 0;
            var previousTrayIcon = _currentTrayIcon;
            bool previousTrayIconIsOwned = _currentTrayIconIsOwned;

            _data.hIcon = hIcon;
            Shell_NotifyIcon(NIM_MODIFY, ref _data);

            _currentTrayIcon = hIcon;
            _currentTrayIconIsOwned = hIconIsOwned;

            if (previousTrayIconIsOwned)
                IconLoader.DestroyHIcon(previousTrayIcon);

            // Taskbar overlay badge
            if (showBadges && count > 0)
            {
                var overlayIcon = BadgeIconFactory.CreateOverlayIcon(count);
                TaskbarBadge.SetBadge(window, overlayIcon);
                ReplaceOverlayIcon(overlayIcon);
            }
            else
            {
                TaskbarBadge.Clear(window);
                ReplaceOverlayIcon(IntPtr.Zero);
            }
        }

        /// <summary>
        /// Removes the tray icon from the system tray and clears any associated taskbar badges. Also cleans up any owned icons to prevent resource leaks.
        /// </summary>
        public static void Remove()
        {
            Shell_NotifyIcon(NIM_DELETE, ref _data);
            if (_window != null)
                TaskbarBadge.Clear(_window);

            ClearOwnedIcons();
        }

        /// <summary>
        /// Handles messages sent to the tray icon, such as mouse clicks.
        /// It responds to left-clicks by showing the main window and right-clicks by displaying a context menu with various options.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        public static void HandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg != WM_TRAY) return;

            int eventId = lParam.ToInt32();

            // Left click
            if (eventId == 0x0201 || eventId == 0x0202)
            {
                _window?.ShowWindow();
                return;
            }

            // Right click
            if (eventId == 0x0205)
            {
                ShowContextMenu();
            }
        }

        /// <summary>
        /// Shows the context menu for the tray icon, allowing the user to open the application, toggle settings, or exit the application.
        /// The menu items reflect the current state of the settings (e.g., whether "Minimize to tray" is enabled).
        /// </summary>
        private static void ShowContextMenu()
        {
            IntPtr menu = CreatePopupMenu();

            AppendMenu(menu, 0, 1, "Öppna");
            AppendMenu(menu, 0x0800, 0, "");
            AppendMenu(menu, 0, 10, _settings.MinimizeToTray ? "✓ Minimize to tray" : "Minimize to tray");
            AppendMenu(menu, 0, 11, _settings.ShowBadges ? "✓ Visa badges" : "Visa badges");
            AppendMenu(menu, 0, 12, _settings.ShowNotifications ? "✓ Notiser" : "Notiser");
            AppendMenu(menu, 0, 13, _settings.StartAtLogin ? "✓ Starta vid inloggning" : "Starta vid inloggning");
            AppendMenu(menu, 0x0800, 0, "");
            AppendMenu(menu, 0, 2, "Avsluta");

            GetCursorPos(out POINT pt);

            // Necessary to make the menu disappear when clicking outside of it
            SetForegroundWindow(_data.hWnd);

            int cmd = TrackPopupMenu(menu, TPM_LEFTALIGN | TPM_RETURNCMD, pt.X, pt.Y, 0, _data.hWnd, IntPtr.Zero);

            switch (cmd)
            {
                case 1:
                    _window?.ShowWindow();
                    break;
                case 10:
                    _settings.MinimizeToTray = !_settings.MinimizeToTray;
                    _settings.Save();
                    break;
                case 11:
                    _settings.ShowBadges = !_settings.ShowBadges;
                    _settings.Save();
                    if (_window != null)
                        UpdateUnread(_window, _lastUnreadCount, _settings.ShowBadges);
                    break;
                case 12:
                    _settings.ShowNotifications = !_settings.ShowNotifications;
                    _settings.Save();
                    SetNotification(_settings.ShowNotifications);
                    break;
                case 13:
                    _settings.StartAtLogin = !_settings.StartAtLogin;
                    _settings.Save();
                    SetStartup(_settings.StartAtLogin);
                    break;
                case 2:
                    _window?.CloseForReal();
                    break;
            }
        }

        internal static void SetNotification(bool enable)
        {
            if (enable) _data.uFlags |= NIF_TIP;
            else _data.uFlags &= ~NIF_TIP;
            Shell_NotifyIcon(NIF_TIP, ref _data); // Update tooltip to reflect notification setting
        }


        internal static void SetStartup(bool enable)
        {
            string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            string appName = "MessengerApp";
            string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey, true))
            {
                if (key == null) return;
                if (enable)
                    key.SetValue(appName, $"\"{appPath}\"");
                else
                    key.DeleteValue(appName, false);
            }
        }

        /// <summary>
        /// Replaces the current overlay icon with a new one, destroying the previous icon if it was owned by the application.
        /// This helps manage resources and ensures that only the current overlay icon is displayed on the taskbar.
        /// </summary>
        /// <param name="hIcon"></param>
        private static void ReplaceOverlayIcon(IntPtr hIcon)
        {
            if (_currentOverlayIcon != IntPtr.Zero)
                IconLoader.DestroyHIcon(_currentOverlayIcon);

            _currentOverlayIcon = hIcon;
        }

        /// <summary>
        /// Clears any owned tray and overlay icons, destroying them to free resources.
        /// This is important to prevent memory leaks when the application is closing or when the icons are no longer needed.
        /// </summary>
        private static void ClearOwnedIcons()
        {
            if (_currentTrayIconIsOwned)
                IconLoader.DestroyHIcon(_currentTrayIcon);

            if (_currentOverlayIcon != IntPtr.Zero)
                IconLoader.DestroyHIcon(_currentOverlayIcon);

            _currentTrayIcon = IntPtr.Zero;
            _currentOverlayIcon = IntPtr.Zero;
            _currentTrayIconIsOwned = false;
        }

        // ── Win32 interop ──────────────────────────────────────────

        /// <summary>
        /// Struct representing the NOTIFYICONDATA structure used in the Windows Shell API for managing tray icons.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        /// <summary>
        /// Constants for the NOTIFYICONDATA structure and Shell_NotifyIcon function, defining flags for icon, tooltip, and message handling.
        /// </summary>
        private const int NIF_MESSAGE = 0x0001;
        private const int NIF_ICON = 0x0002;
        private const int NIF_TIP = 0x0004;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        /// <summary>
        /// Sends a message to the system tray to add, modify, or delete a tray icon, using the specified NOTIFYICONDATA structure.
        /// </summary>
        /// <param name="dwMessage"></param>
        /// <param name="lpdata"></param>
        /// <returns></returns>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

        /// <summary>
        /// Creates a popup menu that can be displayed in response to user interactions with the tray icon, such as right-clicking.
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        /// <summary>
        /// Gets the current position of the cursor in screen coordinates, which is useful for positioning context menus relative to the mouse pointer.
        /// </summary>
        /// <param name="lpPoint"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Tracks a popup menu, displaying it at the specified screen coordinates and returning the command ID of the selected menu item.
        /// </summary>
        /// <param name="hMenu"></param>
        /// <param name="uFlags"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="nReserved"></param>
        /// <param name="hWnd"></param>
        /// <param name="prcRect"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        /// <summary>
        /// Sets the specified window to the foreground, bringing it to the front of the Z-order and giving it focus.
        /// This is used when the user interacts with the tray icon to ensure the main application window is visible.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;

        /// <summary>
        /// Struct representing a point in 2D space, used for cursor position and menu placement in the Windows API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }
    }
}

#pragma warning restore CS8602, CS8618
