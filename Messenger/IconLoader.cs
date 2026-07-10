using System;
using System.Runtime.InteropServices;

namespace Messenger
{
    /// <summary>
    /// Class for loading icons from files using the Windows API.
    /// </summary>
    public static class IconLoader
    {
        /// <summary>
        /// Loads an icon from a specified file path.
        /// </summary>
        /// <param name="hInst"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="fuLoad"></param>
        /// <returns>Pointer to image</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(
            IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        public static IntPtr LoadHIcon(string path)
            => LoadImage(IntPtr.Zero, path, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

        /// <summary>
        /// Destroys an icon handle to free resources.
        /// </summary>
        /// <param name="hIcon"></param>
        /// <returns>Destroy status for icon</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static void DestroyHIcon(IntPtr hIcon)
        {
            if (hIcon != IntPtr.Zero)
                DestroyIcon(hIcon);
        }
    }
}