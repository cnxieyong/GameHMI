using System.Text.Json.Serialization;

namespace GameHMI.Models;

public class CourseData
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public SceneInfo Scene { get; set; } = new();
    public string[] Concepts { get; set; } = [];
    public FactoryLayout Factory { get; set; } = new();
    public string Template { get; set; } = "";
    public int MaxRuntimeMs { get; set; } = 5000;
    public HintEntry[] Hints { get; set; } = [];
    public WinCondition WinCondition { get; set; } = new();
    public AchievementInfo? Achievement { get; set; }
}

public class SceneInfo
{
    public string Narrative { get; set; } = "";
    public string Task { get; set; } = "";
}

public class FactoryLayout
{
    public string Layout { get; set; } = "single_device";
    public DeviceTemplate[] Devices { get; set; } = [];
    public string? SerialResponse { get; set; }
    public Dictionary<string, ushort>? PlcRegisters { get; set; }
    public Dictionary<string, bool>? PlcCoils { get; set; }
}

public class DeviceTemplate
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double Temp { get; set; }
}

public class HintEntry
{
    public int Level { get; set; }
    public string Text { get; set; } = "";
}

public class WinCondition
{
    public string Type { get; set; } = "console_contains";
    public string Value { get; set; } = "";
    public string? Device { get; set; }
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public double? TargetValue { get; set; }

    [JsonIgnore]
    public bool IsMet { get; set; }
}

public class AchievementInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
