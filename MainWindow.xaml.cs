using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameHMI.Devices;
using GameHMI.Models;
using GameHMI.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;

namespace GameHMI;

public partial class MainWindow : Window
{
    private readonly VirtualTemperatureSensor _sensor = new()
    {
        Id = "sensor1", Name = "烘干箱温度传感器", Temperature = 25.5, AmbientTemp = 30
    };
    private readonly VirtualMotor _motor = new()
    {
        Id = "motor1", Name = "传送带电机", MaxSpeed = 3000
    };
    private readonly SimulationEngine _sim = new();
    private readonly CourseLoader _loader = new();
    private readonly MissionManager _mission = new();
    private readonly SaveLoadService _save = new();

    private GameSave _gameSave = new();
    private string _currentAct = "act01_apprentice";
    private string _currentLevel = "01";
    private bool _levelCompleted;

    // 动态工厂卡片
    private record DeviceCard(Border Card, TextBlock Icon, TextBlock Value, TextBlock Status, TextBlock Action, string DeviceType);
    private readonly List<DeviceCard> _deviceCards = [];

    // === 补全词汇 ===
    private static readonly (string Text, string Desc)[] _completions =
    [
        ("Print",           "输出文字到控制台 Print(object)"),
        ("Serial",          "虚拟串口对象 Serial.Open()/ReadLine()/WriteLine()/Close()"),
        ("Plc",             "虚拟 PLC Plc.ReadHoldingRegister()/WriteHoldingRegister()/ReadCoil()/WriteCoil()"),
        ("SensorTemp",      "只读 当前烘干箱温度 (double)"),
        ("SensorTemps",     "只读 多温区温度数组 double[]"),
        ("MotorRunning",    "只读 电机是否运行中 (bool)"),
        ("MotorSpeed",      "只读 电机当前转速 (int)"),
        ("MotorOn",         "读写 启动/停止电机 (bool)"),
        ("MotorSpeed_Result","读写 设置电机目标转速 (int)"),
        ("HeaterOn",        "读写 开关加热器 (bool)"),
        ("if",              "if (条件) { ... }"),
        ("else",            "else { ... }"),
        ("while",           "while (条件) { ... }"),
        ("for",             "for (int i = 0; i < n; i++) { ... }"),
        ("foreach",         "foreach (var item in collection) { ... }"),
        ("true",            "布尔值 真"),
        ("false",           "布尔值 假"),
        ("var",             "隐式类型声明 var x = ..."),
        ("new",             "创建对象 new 类型()"),
        ("return",          "返回 return 值;"),
        ("class",           "定义类 class 类名 { }"),
        ("static",          "静态修饰符"),
        ("public",          "公共访问修饰符"),
        ("private",         "私有访问修饰符"),
        ("void",            "空返回类型"),
        ("int",             "整数类型"),
        ("double",          "双精度浮点"),
        ("bool",            "布尔类型"),
        ("string",          "字符串类型"),
        ("List",            "List<T> 动态列表"),
        ("Dictionary",      "Dictionary<K,V> 字典"),
        ("ToString",        ".ToString() 转字符串"),
        ("Parse",           "int.Parse() / double.Parse() 解析字符串"),
        ("TryParse",        "int.TryParse() 安全解析字符串"),
        ("Split",           "string.Split() 分割字符串"),
        ("Contains",        "string.Contains() 判断是否包含"),
        ("Length",          ".Length 获取长度"),
        ("Count",           ".Count() 获取元素个数"),
        ("Add",             ".Add() 添加元素到集合"),
        ("Console",         "Console.WriteLine() 系统控制台"),
    ];

    // === 生命周期 ===
    public MainWindow()
    {
        InitializeComponent();
        SetupCodeEditor();

        _sim.Register(_sensor);
        _sim.Register(_motor);
        _sim.Tick += UpdateFactoryView;
        _sim.Start();

        _mission.MissionChanged += RefreshTaskPanel;
        _gameSave = _save.Load();
        _currentAct = _gameSave.CurrentAct;
        _currentLevel = _gameSave.CurrentLevel;
        LoadLevel(_currentAct, _currentLevel);
    }

    // === 编辑器 ===
    private void SetupCodeEditor()
    {
        CodeBox.SyntaxHighlighting = CreateDarkHighlighting();
        CodeBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#010409"));
        CodeBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c9d1d9"));
        CodeBox.Options.ConvertTabsToSpaces = true;
        CodeBox.Options.IndentationSize = 4;
        CodeBox.Options.EnableHyperlinks = false;
        CodeBox.Options.HighlightCurrentLine = false;
        CodeBox.ShowLineNumbers = true;

        var margins = CodeBox.TextArea.LeftMargins;
        for (int i = margins.Count - 1; i >= 0; i--)
            if (margins[i] is not LineNumberMargin)
                margins.RemoveAt(i);

        CodeBox.TextArea.TextEntered += OnCodeTextEntered;
    }

    private static IHighlightingDefinition? CreateDarkHighlighting()
    {
        var h = HighlightingManager.Instance.GetDefinition("C#");
        if (h == null) return null;

        // 遍历命名颜色，无条件提亮
        foreach (var nc in h.NamedHighlightingColors)
        {
            if (nc.Foreground == null) continue;
            var col = nc.Foreground.GetColor(null);
            if (col == null) continue;
            var c = col.Value;
            // 无条件向白色偏移
            nc.Foreground = new SimpleHighlightingBrush(Color.FromRgb(
                (byte)Math.Min(255, c.R + 80),
                (byte)Math.Min(255, c.G + 80),
                (byte)Math.Min(255, c.B + 80)));
        }
        return h;
    }

    private CompletionWindow? _completionWindow;
    private void OnCodeTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (_completionWindow != null) return;

        var ch = e.Text.Length > 0 ? e.Text[0] : '\0';
        if (!char.IsLetter(ch) && ch != '.') return;

        var prefix = GetCurrentWord(CodeBox);
        var matches = _completions
            .Where(k => k.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (string.IsNullOrEmpty(prefix)) matches = _completions.ToList();
        if (matches.Count == 0) return;

        _completionWindow = new CompletionWindow(CodeBox.TextArea) { CloseWhenCaretAtBeginning = true, CloseAutomatically = true };
        foreach (var (text, desc) in matches)
            _completionWindow.CompletionList.CompletionData.Add(new CompletionData(text, desc));
        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private static string GetCurrentWord(TextEditor editor)
    {
        var doc = editor.Document; var offset = editor.CaretOffset; var start = offset;
        while (start > 0) { var c = doc.GetCharAt(start - 1); if (!char.IsLetterOrDigit(c) && c != '_') break; start--; }
        return doc.GetText(start, offset - start);
    }

    private class CompletionData : ICompletionData
    {
        private readonly string _desc;
        public CompletionData(string text, string desc) { Text = text; _desc = desc; }
        public System.Windows.Media.ImageSource? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description => _desc;
        public double Priority => 0;
        public void Complete(TextArea area, ISegment segment, EventArgs args)
        {
            var doc = area.Document; var start = segment.Offset;
            while (start > 0 && (char.IsLetterOrDigit(doc.GetCharAt(start - 1)) || doc.GetCharAt(start - 1) == '_')) start--;
            area.Document.Replace(start, (segment.Offset + segment.Length) - start, Text);
        }
    }

    // === 关卡导航 ===
    private void LoadLevel(string act, string id, int retry = 0)
    {
        var course = _loader.Load(act, id);
        if (course == null)
        {
            if (retry < 5)
            {
                var next = (int.Parse(id) + 1).ToString("D2");
                if (_loader.Load(act, next) != null) { LoadLevel(act, next, retry + 1); return; }
                var prev = (int.Parse(id) - 1).ToString("D2");
                if (int.Parse(prev) >= 1 && _loader.Load(act, prev) != null) { LoadLevel(act, prev, retry + 1); return; }
            }
            NarrativeText.Text = $"关卡 {id} 不存在";
            return;
        }

        _currentAct = act; _currentLevel = id;
        _levelCompleted = _gameSave.CompletedLevels.Contains($"{act}/{id}");

        // 按关卡初始化设备状态
        var firstDevice = course.Factory.Devices.FirstOrDefault();
        if (firstDevice != null)
        {
            _sensor.Temperature = firstDevice.Temp > 0 ? firstDevice.Temp : 25.5;
            _sensor.HeaterOn = false;
            _motor.Stop();
        }

        _mission.Load(course);
        CodeBox.Text = course.Template;
        ConsoleOut.Text = _levelCompleted ? ">>> ✓ 已通关，按 ▶ 可重玩" : "按 ▶ 运行 执行代码";

        BuildFactoryView(course);

        Title = $"智造工厂 — 关 {id} · {course.Title}";
        HeaderTitle.Text = act switch
        {
            "act01_apprentice" => "  第一幕 · 产线学徒",
            "act02_debugger" => "  第二幕 · 设备调试员",
            "act03_automation" => "  第三幕 · 自动化工程师",
            "act04_architect" => "  第四幕 · 系统架构师",
            "act05_commander" => "  第五幕 · 产线指挥官",
            "bonus" => "  🏆 Bonus 挑战",
            _ => act
        } + $" · 关 {id}";

        UpdateNavButtons(); SaveProgress();
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Bonus 关特殊处理
            if (_currentAct == "bonus")
            {
                var bnext = _currentLevel switch { "b1" => "b2", "b2" => "b3", "b3" => "b4", _ => null };
                if (bnext != null) LoadLevel("bonus", bnext);
                return;
            }

            var next = int.Parse(_currentLevel) + 1;
            var act = _currentAct;

            if (_currentAct == "act01_apprentice" && next > 20) { act = "act02_debugger"; next = 21; }
            else if (_currentAct == "act02_debugger" && next > 35) { act = "act03_automation"; next = 36; }
            else if (_currentAct == "act03_automation" && next > 50) { act = "act04_architect"; next = 51; }
            else if (_currentAct == "act04_architect" && next > 60) { act = "act05_commander"; next = 61; }
            else if (_currentAct == "act05_commander" && next > 65) { LoadLevel("bonus", "b1"); return; }
            if (next > 99) return;
            LoadLevel(act, next.ToString("D2"));
        }
        catch (Exception ex) { MessageBox.Show($"{ex.GetType().Name}:\n{ex.Message}\n\n{ex.StackTrace}", "切关失败"); }
    }

    private void PrevBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Bonus 关特殊处理
            if (_currentAct == "bonus")
            {
                var bprev = _currentLevel switch { "b2" => "b1", "b3" => "b2", "b4" => "b3", "b1" => null, _ => null };
                if (bprev != null) LoadLevel("bonus", bprev);
                else if (_currentLevel == "b1") LoadLevel("act05_commander", "65");
                return;
            }

            var prev = int.Parse(_currentLevel) - 1;
            var act = _currentAct;

            if (_currentAct == "act02_debugger" && prev < 21) { act = "act01_apprentice"; prev = 20; }
            else if (_currentAct == "act03_automation" && prev < 36) { act = "act02_debugger"; prev = 35; }
            else if (_currentAct == "act04_architect" && prev < 51) { act = "act03_automation"; prev = 50; }
            else if (_currentAct == "act05_commander" && prev < 61) { act = "act04_architect"; prev = 60; }
            if (prev >= 1) LoadLevel(act, prev.ToString("D2"));
        }
        catch (Exception ex) { ConsoleOut.Text = $">>> 切换关卡失败: {ex.GetType().Name}: {ex.Message}"; }
    }

    private void UpdateNavButtons()
    {
        PrevBtn.Visibility = Visibility.Visible;
        NextBtn.IsEnabled = true;
        NextBtn.Content = "下一关 ▶";
        NextBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1f6feb"));
        NextBtn.Foreground = new SolidColorBrush(Colors.White);

        if (_currentAct == "bonus")
        {
            PrevBtn.Visibility = Visibility.Visible;  // B1 可以回 65
            NextBtn.IsEnabled = _currentLevel != "b4";
        }
        else if (int.TryParse(_currentLevel, out var cur))
        {
            PrevBtn.Visibility = cur > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // === 动态工厂视图 ===
    private void BuildFactoryView(CourseData course)
    {
        FactoryPanel.Children.Clear();
        _deviceCards.Clear();

        var layout = course.Factory.Layout;
        var devices = course.Factory.Devices;

        // Act 3+: PLC 关卡 — 只显示 PLC 和产线，不显示温度/电机/加热器
        if (_currentAct == "act03_automation" || _currentAct == "act04_architect" || _currentAct == "act05_commander")
        {
            AddDeviceCard("🖥", "PLC Modbus", "plc", "plc1");
            AddDeviceCard("📊", "保持寄存器", "plc_reg", "reg1");
            AddDeviceCard("🔘", "线圈状态", "plc_coil", "coil1");
            AddDeviceCard("🔌", "串口 COM3", "serial", "serial1");

            // 多设备布局时显示设备
            if (layout == "multi_device" || layout == "multi_zone")
            {
                foreach (var d in devices.Take(3))
                {
                    var icon = d.Type switch { "DryerBox" => "🏭", _ => "📦" };
                    AddDeviceCard(icon, d.Label, d.Type, d.Id);
                }
            }
            return;
        }

        // Act 2: 串口关卡 — 保留传感器+电机+加热器+串口
        if (_currentAct == "act02_debugger")
        {
            var label = devices.Length > 0 ? devices[0].Label : "烘干箱 #1";
            AddDeviceCard("🌡", label, "DryerBox", "sensor1");
            AddDeviceCard("⚙", "传送带电机", "motor", "motor1");
            AddDeviceCard("🔥", "加热器", "heater", "heater1");
            AddDeviceCard("🔌", "串口 COM3", "serial", "serial1");
            return;
        }

        // Act 1 和 Bonus: 传感器+电机+加热器
        if (layout == "multi_zone")
        {
            for (int i = 0; i < Math.Min(devices.Length > 0 ? 5 : 3, 5); i++)
            {
                var zoneLabel = devices.Length > 0 ? $"{devices[0].Label} 温区{i + 1}" : $"烘干箱 温区{i + 1}";
                AddDeviceCard("🌡", zoneLabel, "zone", $"zone_{i}");
            }
            AddDeviceCard("⚙", "传送带电机", "motor", "motor1");
        }
        else if (layout == "multi_device")
        {
            foreach (var d in devices)
            {
                var icon = d.Type switch { "DryerBox" => "🌡", _ => "📦" };
                AddDeviceCard(icon, d.Label, d.Type, d.Id);
            }
            AddDeviceCard("⚙", "传送带电机", "motor", "motor1");
        }
        else
        {
            var label = devices.Length > 0 ? devices[0].Label : "烘干箱 #1";
            AddDeviceCard("🌡", label, "DryerBox", "sensor1");
            AddDeviceCard("⚙", "传送带电机", "motor", "motor1");
            AddDeviceCard("🔥", "加热器", "heater", "heater1");
        }
    }

    private void AddDeviceCard(string icon, string label, string type, string id)
    {
        var iconTb = new TextBlock { Text = icon, FontSize = 36, HorizontalAlignment = HorizontalAlignment.Center };
        var labelTb = new TextBlock { Text = label, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e")), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        var valueTb = new TextBlock { Text = "—", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00cec9")), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        var statusTb = new TextBlock { Text = "", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e")), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
        var actionTb = new TextBlock { Text = "", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(iconTb); stack.Children.Add(labelTb); stack.Children.Add(valueTb); stack.Children.Add(statusTb); stack.Children.Add(actionTb);

        var card = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0d1117")),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21262d")),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 10)
        };
        card.Child = stack;

        FactoryPanel.Children.Add(card);
        _deviceCards.Add(new DeviceCard(card, iconTb, valueTb, statusTb, actionTb, type));
    }

    private void UpdateFactoryView()
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var dc in _deviceCards)
            {
                switch (dc.DeviceType)
                {
                    case "DryerBox":
                    case "zone":
                        dc.Icon.Text = _sensor.Temperature > 35 ? "🌡️⚠" : _sensor.Temperature > 30 ? "🌡️" : "🌡";
                        dc.Value.Text = $"{_sensor.Temperature:F1}°C";
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_sensor.Color));
                        dc.Status.Text = _sensor.Status;
                        dc.Status.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_sensor.Color));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_sensor.Color));
                        break;

                    case "motor":
                        dc.Icon.Text = _motor.IsRunning ? "⚙️💨" : "⚙";
                        dc.Value.Text = _motor.Status;
                        dc.Value.FontSize = _motor.IsRunning ? 18 : 18;
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_motor.Color));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_motor.Color));
                        break;

                    case "heater":
                        dc.Icon.Text = _sensor.HeaterOn ? "🔥" : "🧯";
                        dc.Value.Text = _sensor.HeaterOn ? "加热中" : "已关闭";
                        dc.Value.FontSize = 18;
                        var hc = _sensor.HeaterOn ? "#fdcb6e" : "#484f58";
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hc));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hc));
                        break;

                    case "serial":
                        dc.Icon.Text = "🔌";
                        dc.Value.Text = "就绪";
                        dc.Value.FontSize = 16;
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e"));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21262d"));
                        break;

                    case "plc":
                        dc.Icon.Text = "🖥";
                        dc.Value.Text = "运行中";
                        dc.Value.FontSize = 16;
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3fb950"));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3fb950"));
                        break;

                    case "plc_reg":
                        dc.Icon.Text = "📊";
                        dc.Value.Text = "寄存器";
                        dc.Value.FontSize = 16;
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#79c0ff"));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#79c0ff"));
                        break;

                    case "plc_coil":
                        dc.Icon.Text = "🔘";
                        dc.Value.Text = "线圈";
                        dc.Value.FontSize = 16;
                        dc.Value.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d2a8ff"));
                        dc.Card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d2a8ff"));
                        break;

                    default:
                        dc.Value.Text = dc.DeviceType;
                        break;
                }
            }
        });
    }

    // === 存档 ===
    private void ShowAchievementPopup(int stars, AchievementInfo? achieve)
    {
        // 填星星
        StarsPanel.Children.Clear();
        for (int i = 0; i < 3; i++)
        {
            var filled = i < stars;
            StarsPanel.Children.Add(new TextBlock
            {
                Text = filled ? "★" : "☆",
                FontSize = 32,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(filled ? "#fdcb6e" : "#484f58")),
                Margin = new Thickness(4, 0, 4, 0)
            });
        }

        AchieveStars.Text = $"获得 {stars} 颗星";
        AchieveName.Text = achieve?.Name ?? "";
        if (achieve != null)
            AchieveTitle.Text = "🏆 成就解锁！";
        else
            AchieveTitle.Text = "🎉 任务完成！";

        AchievementOverlay.Visibility = Visibility.Visible;
    }

    private void DismissAchievement(object sender, MouseButtonEventArgs e)
    {
        AchievementOverlay.Visibility = Visibility.Collapsed;
    }

    private async void FlashFactoryCards()
    {
        var green = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3fb950"));
        var originals = _deviceCards.Select(dc => dc.Card.BorderBrush).ToList();

        // 闪绿
        foreach (var dc in _deviceCards) dc.Card.BorderBrush = green;
        await Task.Delay(400);
        // 恢复原色
        for (int i = 0; i < _deviceCards.Count; i++)
            _deviceCards[i].Card.BorderBrush = originals[i];
        await Task.Delay(200);
        // 再闪一次
        foreach (var dc in _deviceCards) dc.Card.BorderBrush = green;
        await Task.Delay(400);
        for (int i = 0; i < _deviceCards.Count; i++)
            _deviceCards[i].Card.BorderBrush = originals[i];
    }

    private void SaveProgress()
    {
        _gameSave.CurrentAct = _currentAct; _gameSave.CurrentLevel = _currentLevel;
        _save.Save(_gameSave);
    }

    // === 右侧任务面板 ===
    private void RefreshTaskPanel()
    {
        var c = _mission.CurrentCourse;
        if (c == null) return;

        LevelLabel.Text = $"  关 {c.Id}";
        NarrativeText.Text = c.Scene.Narrative;
        TaskText.Text = c.Scene.Task;
        ConceptsText.Text = "已掌握: " + string.Join(", ", c.Concepts);

        HintText.Text = "需要帮助时点下方按钮";
        HintText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c9d1d9"));

        ResetHintBtn(Hint1Btn); ResetHintBtn(Hint2Btn); ResetHintBtn(Hint3Btn);
        Hint1Btn.Opacity = c.Hints.Any(h => h.Level == 1) ? 1 : 0.4;
        Hint2Btn.Opacity = c.Hints.Any(h => h.Level == 2) ? 1 : 0.4;
        Hint3Btn.Opacity = c.Hints.Any(h => h.Level == 3) ? 1 : 0.4;
    }

    private void HintBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag == null) return;
        if (!int.TryParse(border.Tag.ToString(), out var level)) return;

        var hint = _mission.GetHint(level);
        HintText.Text = $"💡 {level}级提示: {hint}";
        HintText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fdcb6e"));

        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3fb950"));
        if (border.Child is TextBlock tb) tb.Foreground = new SolidColorBrush(Colors.White);
        ConsoleOut.Text = $">>> 💡 {level}级提示已显示";
    }

    private static void ResetHintBtn(Border btn)
    {
        btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21262d"));
        if (btn.Child is TextBlock tb) tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e"));
    }

    // === 运行按钮 ===
    private async void RunBtn_Click(object sender, RoutedEventArgs e)
    {
        RunBtn.IsEnabled = false;
        ConsoleOut.Text = ">>> 编译执行中...\n";

        var runner = FindCodeRunner();
        if (runner == null) { ConsoleOut.Text += ">>> ❌ 找不到 CodeRunner.exe"; RunBtn.IsEnabled = true; return; }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = runner, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            _ = process.StandardError.ReadToEndAsync();

            var request = JsonSerializer.Serialize(new
            {
                Code = CodeBox.Text, TimeoutMs = 5000,
                SensorTemp = _sensor.Temperature,
                SensorTemps = new[] { _sensor.Temperature, _sensor.Temperature + 2.1, _sensor.Temperature - 1.3, _sensor.Temperature + 4, _sensor.Temperature - 0.5 },
                MotorRunning = _motor.IsRunning, MotorSpeed = _motor.CurrentSpeed,
                SerialResponse = _mission.CurrentCourse?.Factory.SerialResponse ?? "",
                PlcRegisters = _mission.CurrentCourse?.Factory.PlcRegisters,
                PlcCoils = _mission.CurrentCourse?.Factory.PlcCoils
            });

            process.StandardInput.Write(request);
            process.StandardInput.Flush();
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            if (await Task.WhenAny(stdoutTask, Task.Delay(8000)) != stdoutTask)
            { process.Kill(true); ConsoleOut.Text += ">>> ⏱ 超时！"; RunBtn.IsEnabled = true; return; }

            var json = await stdoutTask;
            var result = JsonSerializer.Deserialize<RunResult>(json);

            if (result?.Success == true)
            {
                var output = result.Output ?? "";

                // 清空操作提示
                foreach (var dc in _deviceCards) dc.Action.Text = "";

                if (result.MotorSet)
                {
                    if (result.MotorOn) { _motor.Start(); _actionFor("motor").Text = "🟢 代码启动了电机"; }
                    else { _motor.Stop(); _actionFor("motor").Text = "🔴 代码停止了电机"; }
                }
                if (result.SpeedSet) { _motor.SetSpeed(result.MotorSpeedOut); _actionFor("motor").Text = $"⚡ 转速 → {result.MotorSpeedOut} RPM"; }
                if (result.HeaterSet)
                {
                    _sensor.HeaterOn = result.HeaterOn;
                    _actionFor("heater").Text = result.HeaterOn ? "🔥 代码打开了加热器" : "🧯 代码关闭了加热器";
                }

                // 串口/PLC 操作反馈
                var serialCard = _actionFor("serial");
                if (serialCard != null) serialCard.Text = "📡 代码已执行";
                var plcCard = _actionFor("plc");
                if (plcCard != null) plcCard.Text = "🖥 代码已执行";
                var regCard = _actionFor("plc_reg");
                if (regCard != null) regCard.Text = "📊 已读取";
                var coilCard = _actionFor("plc_coil");
                if (coilCard != null) coilCard.Text = "🔘 已操作";

                bool anyAction = _deviceCards.Any(dc => !string.IsNullOrEmpty(dc.Action.Text));
                if (!anyAction) _actionFor("DryerBox").Text = "📖 代码只读取了状态";

                var win = _mission.Evaluate(output);
                if (win.Passed)
                {
                    ConsoleOut.Text = $">>> 输出:\n{output}\n>>> {win.Message}";
                    if (win.Achievement != null) ConsoleOut.Text += $"\n>>> 🏆 成就: {win.Achievement.Name}";
                    var key = $"{_currentAct}/{_currentLevel}";
                    var isNew = !_gameSave.CompletedLevels.Contains(key);
                    if (isNew)
                    {
                        _gameSave.CompletedLevels.Add(key);
                        if (win.Achievement != null && !_gameSave.Achievements.Contains(win.Achievement.Id))
                            _gameSave.Achievements.Add(win.Achievement.Id);
                        SaveProgress();
                    }
                    _levelCompleted = true; UpdateNavButtons();

                    // 成就弹窗（每次都显示）
                    ShowAchievementPopup(win.Stars, win.Achievement);

                    // 通关动画：设备卡片闪绿
                    FlashFactoryCards();
                }
                else ConsoleOut.Text = $">>> 输出:\n{output}\n>>> {win.Message}";
            }
            else
            {
                var err = result?.Error ?? "未知错误";
                if (err?.StartsWith("COMPILE:") == true)
                {
                    _mission.RecordCompileError();
                    var raw = err[8..];
                    ConsoleOut.Text = $">>> ❌ 编译错误:\n{string.Join("\n", raw.Split('\n').Select(ErrorTranslator.Translate))}";
                }
                else if (err == "TIMEOUT") ConsoleOut.Text = ">>> ⏱ 超时！";
                else ConsoleOut.Text = $">>> ❌ {err}";
            }
        }
        catch (Exception ex) { ConsoleOut.Text += $">>> 错误: {ex.Message}"; }
        RunBtn.IsEnabled = true;
    }

    private TextBlock _actionFor(string type)
    {
        var dc = _deviceCards.FirstOrDefault(d => d.DeviceType == type);
        return dc?.Action ?? _deviceCards.FirstOrDefault()?.Action ?? new TextBlock();
    }

    private void MapBtn_Click(object sender, RoutedEventArgs e)
    {
        AchievementOverlay.Visibility = Visibility.Collapsed;
        var map = new CourseMapWindow(_gameSave, _currentAct, _currentLevel, (a, i) => LoadLevel(a, i));
        map.Owner = this;
        map.ShowDialog();
    }

    private void CheatBtn_Click(object sender, RoutedEventArgs e)
    {
        var course = _mission.CurrentCourse;
        if (course == null) return;

        // 模拟通关：0 提示，0 编译错误 = 3 星
        var stars = 3;
        var achieve = course.Achievement;

        var key = $"{_currentAct}/{_currentLevel}";
        if (!_gameSave.CompletedLevels.Contains(key))
        {
            _gameSave.CompletedLevels.Add(key);
            if (achieve != null && !_gameSave.Achievements.Contains(achieve.Id))
                _gameSave.Achievements.Add(achieve.Id);
            SaveProgress();
        }
        _levelCompleted = true; UpdateNavButtons();

        // 过关特效
        ConsoleOut.Text = $">>> ⚡ 开发模式过关！\n>>> ✓ 任务完成！获得 {stars} 颗星";
        if (achieve != null) ConsoleOut.Text += $"\n>>> 🏆 成就: {achieve.Name}";

        FlashFactoryCards();
        ShowAchievementPopup(stars, achieve);
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _sensor.Temperature = 25.5; _sensor.HeaterOn = false; _motor.Stop();
        CodeBox.Text = _mission.CurrentCourse?.Template ?? CodeBox.Text;
        ConsoleOut.Text = "按 ▶ 运行 执行代码";
        foreach (var dc in _deviceCards) dc.Action.Text = "";
    }

    private static string? FindCodeRunner()
    {
        // 生产环境：CodeRunner.exe 和 GameHMI.exe 同目录
        var prod = Path.Combine(AppContext.BaseDirectory, "CodeRunner.exe");
        if (File.Exists(prod)) return prod;

        // 开发环境：CodeRunner 在子项目 bin 目录
        var dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "CodeRunner", "bin", "Debug", "net9.0", "CodeRunner.exe"));
        return File.Exists(dev) ? dev : null;
    }

    private class RunResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public bool MotorSet { get; set; }
        public bool MotorOn { get; set; }
        public bool SpeedSet { get; set; }
        public int MotorSpeedOut { get; set; }
        public bool HeaterSet { get; set; }
        public bool HeaterOn { get; set; }
    }
}
