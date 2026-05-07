using System.IO;
using System.Text.Json;
using GameHMI.Models;

namespace GameHMI.Services;

public class CourseLoader
{
    private readonly string _basePath;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CourseLoader(string? basePath = null)
    {
        // 生产环境：Data/courses 和 EXE 同目录
        var prod = Path.Combine(AppContext.BaseDirectory, "Data", "courses");
        if (Directory.Exists(prod)) { _basePath = prod; return; }

        // 开发环境：Data/courses 在项目根目录
        var dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "courses"));
        _basePath = basePath ?? dev;
    }

    public CourseData? Load(string act, string levelId)
    {
        var path = Path.Combine(_basePath, act, $"{levelId}.json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CourseData>(json, _jsonOpts);
    }

    public List<CourseData> LoadAct(string act)
    {
        var dir = Path.Combine(_basePath, act);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.json")
            .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : 9999)
            .Select(f =>
            {
                var json = File.ReadAllText(f);
                return JsonSerializer.Deserialize<CourseData>(json, _jsonOpts);
            })
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
    }
}
