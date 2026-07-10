using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Messenger
{

    /// <summary>
    /// Class to help ensure that only a single instance of the application is running.
    /// </summary>
    public static class SingleInstanceHelper
    {
        /// <summary>
        /// Sends a message to all top-level windows to bring the existing instance of the application to the front.
        /// </summary>
        /// <param name="lpString"></param>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        /// <summary>
        /// Posts a message to the message queue of the specified window. In this case, it is used to send a message to all top-level windows.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="Msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static readonly uint WM_SHOW_MESSENGER = RegisterWindowMessage("WM_SHOW_MESSENGER_WPF_APP");
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        /// <summary>
        /// Brings the existing instance of the application to the front by broadcasting a message to all top-level windows.
        /// </summary>
        public static void BringExistingToFront()
        {
            PostMessage(HWND_BROADCAST, WM_SHOW_MESSENGER, IntPtr.Zero, IntPtr.Zero);
        }
    }
}