using System;
using System.Runtime.InteropServices;

namespace Messenger
{

    /// <summary>
    /// Class for hooking into the window procedure of a window to intercept messages.
    /// </summary>
    public static class HwndHook
    {
        /// <summary>
        /// Static field to hold the new window procedure delegate. This is necessary to prevent the delegate from being garbage collected while it is still in use.
        /// </summary>
        private static WndProc? _newWndProc;

        /// <summary>
        /// IntPtr to hold the old window procedure. This is used to call the original window procedure after processing messages in the new procedure.
        /// </summary>
        private static IntPtr _oldWndProc;

        /// <summary>
        /// Delegate that defines the signature of the window procedure. This delegate is used to create a new window procedure that can intercept messages sent to the window.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Sets a new window procedure for the specified window handle. This function replaces the existing window procedure with a new one, allowing for message interception.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <param name="newProc"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProc newProc);

        /// <summary>
        /// Calls the original window procedure for the specified window handle.
        /// This function is used to forward messages to the original window procedure after processing them in the new procedure.
        /// </summary>
        /// <param name="lpPrevWndFunc"></param>
        /// <param name="hWnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(
            IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;

        /// <summary>
        /// Attaches a new window procedure to the specified window handle. This allows for intercepting messages sent to the window and processing them in the provided callback function.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="callback"></param>
        public static void Attach(IntPtr hwnd, Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> callback)
        {
            _newWndProc = (h, m, w, l) => callback(h, m, w, l);
            _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, _newWndProc);
        }

        /// <summary>
        /// Calls the original window procedure for the specified window handle.
        /// This function is used to forward messages to the original window procedure after processing them in the new procedure.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        public static IntPtr CallOld(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            => CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
    }
}