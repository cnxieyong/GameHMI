using System.Diagnostics;
using System.Text.Json;

var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Data", "courses"));
var runner = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CodeRunner", "bin", "Debug", "net9.0", "CodeRunner.exe"));

if (!File.Exists(runner)) { Console.WriteLine("CodeRunner not built"); return 1; }

var actDirs = new[] { "act01_apprentice", "act02_debugger", "act03_automation", "act04_architect", "act05_commander", "bonus" };
int pass = 0, fail = 0, skip = 0;

foreach (var act in actDirs)
{
    var dir = Path.Combine(baseDir, act);
    if (!Directory.Exists(dir)) continue;

    foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
    {
        var levelId = Path.GetFileNameWithoutExtension(file);
        var json = File.ReadAllText(file);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hints = root.GetProperty("hints");
        string? solution = null;
        foreach (var h in hints.EnumerateArray())
        {
            if (h.GetProperty("level").GetInt32() == 3)
            { solution = h.GetProperty("text").GetString(); break; }
        }

        if (string.IsNullOrWhiteSpace(solution))
        {
            Console.WriteLine($"  ⬜ {act}/{levelId} — SKIP");
            skip++; continue;
        }

        solution = ExtractCode(solution);

        var wc = root.GetProperty("winCondition");
        var wcType = wc.GetProperty("type").GetString() ?? "";
        var wcValue = wc.GetProperty("value").GetString() ?? "";

        var factory = root.GetProperty("factory");
        var serialResp = factory.TryGetProperty("serialResponse", out var sr) ? sr.GetString() ?? "" : "";
        var plcRegs = factory.TryGetProperty("plcRegisters", out var pr) ? pr : default;
        var plcCoils = factory.TryGetProperty("plcCoils", out var pc) ? pc : default;

        var devices = factory.GetProperty("devices");
        var sensorTemp = 28.5;
        if (devices.GetArrayLength() > 0 && devices[0].TryGetProperty("temp", out var t))
            sensorTemp = t.GetDouble();

        var request = new Dictionary<string, object?>
        {
            ["Code"] = solution,
            ["TimeoutMs"] = 5000,
            ["SensorTemp"] = sensorTemp,
            ["SensorTemps"] = new[] { 28.0, 32.0, 26.0, 34.0, 29.0 },
            ["MotorRunning"] = false,
            ["MotorSpeed"] = 0,
            ["SerialResponse"] = serialResp
        };
        if (plcRegs.ValueKind != JsonValueKind.Undefined)
        {
            var d = new Dictionary<string, ushort>();
            foreach (var kv in plcRegs.EnumerateObject()) d[kv.Name] = (ushort)kv.Value.GetInt32();
            request["PlcRegisters"] = d;
        }
        if (plcCoils.ValueKind != JsonValueKind.Undefined)
        {
            var d = new Dictionary<string, bool>();
            foreach (var kv in plcCoils.EnumerateObject()) d[kv.Name] = kv.Value.GetBoolean();
            request["PlcCoils"] = d;
        }

        var reqJson = JsonSerializer.Serialize(request);
        var psi = new ProcessStartInfo
        {
            FileName = runner, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };

        try
        {
            using var p = new Process { StartInfo = psi };
            p.Start();
            p.StandardInput.Write(reqJson);
            p.StandardInput.Close();

            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            var result = JsonDocument.Parse(stdout).RootElement;
            var success = result.GetProperty("Success").GetBoolean();
            var output = result.GetProperty("Output").GetString() ?? "";

            if (!success)
            {
                var err = result.TryGetProperty("Error", out var e) ? e.GetString() : "unknown";
                Console.WriteLine($"  ❌ {act}/{levelId} — {err}");
                fail++;
            }
            else if (wcType == "console_contains" && !string.IsNullOrEmpty(wcValue) && !output.Contains(wcValue, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  ❌ {act}/{levelId} — missing '{wcValue}'");
                Console.WriteLine($"     Output: {output[..Math.Min(80, output.Length)]}");
                fail++;
            }
            else
            {
                Console.WriteLine($"  ✅ {act}/{levelId}");
                pass++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  💥 {act}/{levelId} — {ex.Message}");
            fail++;
        }
    }
}

Console.WriteLine($"\n=== {pass} pass, {fail} fail, {skip} skip ===");
return fail > 0 ? 1 : 0;

static string ExtractCode(string hint)
{
    var lines = hint.Replace("\\n", "\n").Split('\n');
    var codeLines = new List<string>();
    bool inCode = false;
    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed)) { if (inCode) codeLines.Add(""); continue; }
        if (trimmed.StartsWith("//") || trimmed.StartsWith("class ") || trimmed.StartsWith("void ") ||
            trimmed.StartsWith("if ") || trimmed.StartsWith("else") || trimmed.StartsWith("for ") ||
            trimmed.StartsWith("foreach") || trimmed.StartsWith("while") || trimmed.StartsWith("var ") ||
            trimmed.StartsWith("int ") || trimmed.StartsWith("double ") || trimmed.StartsWith("bool ") ||
            trimmed.StartsWith("string ") || trimmed.StartsWith("ushort ") || trimmed.StartsWith("Serial.") ||
            trimmed.StartsWith("Plc.") || trimmed.StartsWith("Print") || trimmed.StartsWith("Motor") ||
            trimmed.StartsWith("HeaterOn") || trimmed.StartsWith("enum ") || trimmed.StartsWith("interface ") ||
            trimmed.StartsWith("switch") || trimmed.Contains('=') || trimmed.Contains('{') || trimmed.Contains('}') ||
            trimmed.StartsWith("return") || trimmed.StartsWith("break") || trimmed.StartsWith("case ") ||
            inCode)
        {
            codeLines.Add(line);
            inCode = true;
        }
        else if (inCode && (trimmed.EndsWith(';') || trimmed.EndsWith('}')))
            codeLines.Add(line);
    }
    var code = string.Join("\n", codeLines).Trim();
    return code.Length > 0 ? code : hint.Replace("\\n", "\n");
}
