using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;

namespace Messenger;

/// <summary>
/// Class to store and manage the state of the application window, including its position and size.
/// It provides methods to load and save the window state to a JSON file in the local application data folder.
/// </summary>
public class WindowStateStore
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;


    /// <summary>
    /// Static readonly string Path to the JSON file where the window state is stored. The file is located in the LocalApplicationData folder under "Messenger/windowstate.json".
    /// </summary>
    private static readonly string Path =
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Messenger",
            "windowstate.json"
        );

    /// <summary>
    /// Constructor for the WindowStateStore class. It ensures that the directory for the window state JSON file exists by creating it if it does not.
    /// </summary>
    static WindowStateStore()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
    }

    /// <summary>
    /// Loads the window state from the JSON file. If the file does not exist, it returns a new instance of WindowStateStore with default values.
    /// </summary>
    /// <returns></returns>
    public static WindowStateStore Load()
    {
        if (!File.Exists(Path))
            return new WindowStateStore();

        return JsonSerializer.Deserialize<WindowStateStore>(File.ReadAllText(Path))
               ?? new WindowStateStore();
    }

    /// <summary>
    /// Saves the current window state to the JSON file.
    /// It serializes the current instance of WindowStateStore to a JSON string and writes it to the file, formatting it with indentation for readability.
    /// </summary>
    public void Save()
    {
        File.WriteAllText(Path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}