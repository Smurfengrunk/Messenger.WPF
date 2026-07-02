using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Messenger
{
    public static class TaskbarBadge
    {
        // ── COM: ITaskbarList3 ──────────────────────────────────────
        /// <summary>
        /// TaskbarList3 COM interface for Windows 7+ taskbar features, including overlay icons.
        /// </summary>
        [ComImport]
        [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]   // rätt GUID
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, bool fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, int state);
            void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
            void UnregisterTab(IntPtr hwndTab);
            void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
            void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, int flags);
            void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
            void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, string? description);
            void SetThumbnailTooltip(IntPtr hwnd, string? tooltip);
            void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
        }

        /// <summary>
        /// TaskbarList COM class for creating an instance of ITaskbarList3.
        /// </summary>
        [ComImport]
        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        private class TaskbarList { }

        // ── State ───────────────────────────────────────────────────
        private static ITaskbarList3? _taskbar;
        private static IntPtr _hwnd;

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Initializes the TaskbarBadge for a specific WPF window. This method must be called before using SetBadge or Clear.
        /// </summary>
        /// <param name="window"></param>
        public static void InitializeFor(Window window)
        {
            _hwnd = new WindowInteropHelper(window).Handle;
            try
            {
                var tb = (ITaskbarList3)new TaskbarList();
                tb.HrInit();
                _taskbar = tb;
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"TaskbarBadge init failed: 0x{ex.HResult:X8} {ex.Message}");
            }
        }

        /// <summary>
        /// Sets an overlay icon on the taskbar button of the specified window.
        /// The icon is represented by a handle to an icon (hIcon). If hIcon is IntPtr.Zero, it will clear the overlay icon.
        /// </summary>
        public static void SetBadge(Window window, IntPtr hIcon)
        {
            EnsureHwnd(window);
            try { _taskbar?.SetOverlayIcon(_hwnd, hIcon, "Olästa meddelanden"); }
            catch { }
        }

        /// <summary>
        /// Clears the overlay icon on the taskbar button of the specified window.
        /// </summary>
        /// <param name="window"></param>
        public static void Clear(Window window)
        {
            EnsureHwnd(window);
            try { _taskbar?.SetOverlayIcon(_hwnd, IntPtr.Zero, null); }
            catch { }
        }

        // ── Helpers ─────────────────────────────────────────────────
        /// <summary>
        /// Ensures that the window handle (_hwnd) is set for the specified window. If _hwnd is IntPtr.Zero, it retrieves the handle using WindowInteropHelper.
        /// </summary>
        /// <param name="window"></param>
        private static void EnsureHwnd(Window window)
        {
            if (_hwnd == IntPtr.Zero)
                _hwnd = new WindowInteropHelper(window).Handle;
        }
    }
}
