using System;
using System.Runtime.InteropServices;

namespace Messenger
{
    /// <summary>
    /// Class containing Win32 API functions and structures for interacting with Windows features, such as setting the AppUserModelID and sending messages to windows.
    /// </summary>
    public static class Win32
    {
        /// <summary>
        /// Struct representing a property key, which is used to identify properties in the Windows Property System.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;

            public PROPERTYKEY(Guid fmtid, uint pid)
            {
                this.fmtid = fmtid;
                this.pid   = pid;
            }
        }

        /// <summary>
        /// COM interface for accessing the property store of a window, allowing you to get and set properties such as the AppUserModelID.
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        public interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            int SetValue(ref PROPERTYKEY key, PROPVARIANT pv);
            int Commit();
        }

        /// <summary>
        /// Struct representing a PROPVARIANT, which is used to store property values in the Windows Property System.
        /// This class implements IDisposable to ensure that unmanaged memory is properly released.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public sealed class PROPVARIANT : IDisposable
        {
            ushort vt;
            ushort wReserved1;
            ushort wReserved2;
            ushort wReserved3;
            IntPtr p;

            /// <summary>
            /// Property to get the value of the PROPVARIANT as a string. It uses Marshal.PtrToStringUni to convert the unmanaged string pointer to a managed string.
            /// </summary>
            /// <param name="value"></param>
            public PROPVARIANT(string value)
            {
                vt = 31; // VT_LPWSTR
                p  = Marshal.StringToCoTaskMemUni(value);
            }

            /// <summary>
            /// Disposes of the PROPVARIANT, freeing any unmanaged memory allocated for the string value. This is important to prevent memory leaks when working with unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                if (p != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(p);
                    p = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Sets the AppUserModelID for a specified window handle (hwnd).
        /// This is used to associate a window with a specific application identity, which can affect how the window is grouped in the taskbar and how notifications are handled.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="iid"></param>
        /// <param name="propertyStore"></param>
        /// <returns></returns>
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetPropertyStoreForWindow(
            IntPtr hwnd, ref Guid iid, out IPropertyStore propertyStore);

        /// <summary>
        /// Sets the AppUserModelID for a specified window handle (hwnd) using the Windows Property System.
        /// This method retrieves the property store for the window and sets the AppUserModelID property to the specified value.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="appId"></param>
        public static void SetWindowAppId(IntPtr hwnd, string appId)
        {
            Guid iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) != 0) return;

            using var pv  = new PROPVARIANT(appId);
            var       key = PKEY_AppUserModel_ID;
            store.SetValue(ref key, pv);
            store.Commit();
        }

        /// <summary>
        /// Property key for the AppUserModelID, which is used to identify the property in the Windows Property System.
        /// This key is used when setting the AppUserModelID for a window.
        /// </summary>
        public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY(
            new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

        /// <summary>
        /// Constants for Windows messages and icon types used in the Win32 API.
        /// </summary>
        public const uint WM_SETICON  = 0x0080;
        public const int  ICON_SMALL  = 0;
        public const int  ICON_BIG    = 1;
        public const uint IMAGE_ICON  = 1;
        public const uint LR_LOADFROMFILE = 0x00000010;

        /// <summary>
        /// Loads an image (icon) from a specified file and returns a handle to the image.
        /// This function is used to load icons for use in Windows applications, such as setting the icon for a window.
        /// </summary>
        /// <param name="hInst"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadImage(
            IntPtr hInst, string name, uint type, int cx, int cy, uint flags);

        /// <summary>
        /// Sends a message to a specified window or windows. This function is used to communicate with other windows, such as sending commands or notifications.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="Msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
