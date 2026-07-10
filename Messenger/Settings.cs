using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;

namespace Messenger
{

    /// <summary>
    /// Class representing the application settings for Messenger.
    /// This class provides properties to manage user preferences such as showing badges, starting the application at login, minimizing to tray, and showing notifications.
    /// It also includes methods to load and save settings to a JSON file located in the user's local application data folder.
    /// </summary>
    public class Settings
    {
        public bool ShowBadges { get; set; } = true;
        public bool StartAtLogin { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;

        private static readonly string Path =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Messenger",
                "settings.json"
            );

        /// <summary>
        /// Loads the settings from the JSON file located at the specified path.
        /// </summary>
        /// <returns>Settings object</returns>
        public static Settings Load()
        {
            if (!File.Exists(Path))
                return new Settings();

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new Settings();
        }

        /// <summary>
        /// Saves the current settings to the JSON file located at the specified path.
        /// </summary>
        public void Save()
        {
            File.WriteAllText(Path,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}