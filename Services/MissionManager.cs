using GameHMI.Models;

namespace GameHMI.Services;

public class MissionManager
{
    public CourseData? CurrentCourse { get; private set; }
    public int HintsUsed { get; private set; }
    public int CompileErrors { get; private set; }
    public int Stars => Math.Max(1, 3 - HintsUsed - Math.Min(CompileErrors, 2));

    public event Action? MissionChanged;

    public void Load(CourseData course)
    {
        CurrentCourse = course;
        HintsUsed = 0;
        CompileErrors = 0;
        MissionChanged?.Invoke();
    }

    public string GetHint(int level)
    {
        if (CurrentCourse == null) return "没有加载关卡";
        var hint = CurrentCourse.Hints.FirstOrDefault(h => h.Level == level);
        if (hint == null) return "没有更多提示了";

        if (level > 0) HintsUsed = Math.Max(HintsUsed, level);
        return hint.Text;
    }

    public void RecordCompileError() => CompileErrors++;

    public WinResult Evaluate(string consoleOutput)
    {
        if (CurrentCourse == null)
            return new WinResult { Passed = false, Message = "没有加载关卡" };

        var wc = CurrentCourse.WinCondition;
        var passed = wc.Type switch
        {
            "console_contains" => consoleOutput.Contains(wc.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        if (passed)
        {
            wc.IsMet = true;
            return new WinResult
            {
                Passed = true,
                Stars = Stars,
                Achievement = CurrentCourse.Achievement,
                Message = $"✓ 任务完成！获得 {Stars} 颗星"
            };
        }

        return new WinResult { Passed = false, Message = "代码运行成功，但输出不符合任务要求，再试试？" };
    }
}

public class WinResult
{
    public bool Passed { get; set; }
    public int Stars { get; set; }
    public AchievementInfo? Achievement { get; set; }
    public string Message { get; set; } = "";
}
