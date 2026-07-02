using Microsoft.Win32;

using System;

namespace Messenger
{
    /// <summary>
    /// Handles registration of the application for automatic startup on Windows by manipulating the registry.
    /// </summary>
    internal static class AutoStartHelper
    {
        // ── Konstanter ─────────────────────────────────────────────

        /// <summary>
        /// Application name used as the registry value name for autostart. This should be unique to avoid conflicts with other applications.
        /// </summary>
        private const string AppName = "Messenger";

        /// <summary>
        /// Registry key path for user-specific autostart entries. This is where Windows looks for applications to start automatically when the user logs in.
        /// </summary>
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // ── Publika metoder ────────────────────────────────────────

        /// <summary>
        /// Checks if the application is currently registered for autostart by looking for its entry in the registry. Returns true if the entry exists, false otherwise.
        /// </summary>
        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            return key?.GetValue(AppName) != null;
        }

        /// <summary>
        /// Sets the autostart status of the application by adding or removing its entry in the registry.
        /// When enabled, it adds an entry with the application's executable path, ensuring that it starts automatically when the user logs in.
        /// When disabled, it removes the entry from the registry.
        /// <see cref="Environment.ProcessPath"/>.
        /// </summary>
        /// <param name="enable">True för att aktivera autostart, false för att inaktivera.</param>
        public static void Set(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
            if (key == null) return;

            if (enable)
                key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
