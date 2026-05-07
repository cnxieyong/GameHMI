using System.IO;
using System.Text.Json;

namespace GameHMI.Services;

public class SaveLoadService
{
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameHMI", "save.json");

    public GameSave Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonSerializer.Deserialize<GameSave>(File.ReadAllText(SavePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save(GameSave save)
    {
        var dir = Path.GetDirectoryName(SavePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(SavePath, JsonSerializer.Serialize(save));
    }
}

public class GameSave
{
    public string CurrentAct { get; set; } = "act01_apprentice";
    public string CurrentLevel { get; set; } = "01";
    public List<string> CompletedLevels { get; set; } = [];
    public List<string> Achievements { get; set; } = [];
}
