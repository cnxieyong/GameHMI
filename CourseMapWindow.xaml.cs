using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameHMI.Services;

namespace GameHMI;

public partial class CourseMapWindow : Window
{
    private readonly CourseLoader _loader = new();
    private readonly GameSave _save;
    private readonly string _currentAct;
    private readonly string _currentLevel;
    private readonly Action<string, string> _onJump;

    public CourseMapWindow(GameSave save, string act, string cur, Action<string, string> onJump)
    {
        InitializeComponent();
        _save = save; _currentAct = act; _currentLevel = cur; _onJump = onJump;

        MouseDown += (s, ev) => { if (ev.LeftButton == MouseButtonState.Pressed) DragMove(); };
        BuildMap();
    }

    private static readonly (string Act, string Label)[] AllActs =
    [
        ("act01_apprentice", "第一幕 · 产线学徒"),
        ("act02_debugger", "第二幕 · 设备调试员"),
        ("act03_automation", "第三幕 · 自动化工程师"),
        ("act04_architect", "第四幕 · 系统架构师"),
        ("act05_commander", "第五幕 · 产线指挥官"),
        ("bonus", "🏆 Bonus 挑战"),
    ];

    private void BuildMap()
    {
        var totalCards = 0;
        var totalDone = 0;
        var perRow = 4;

        foreach (var (act, label) in AllActs)
        {
            var courses = _loader.LoadAct(act);
            if (courses.Count == 0) continue;

            // 幕标题
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 4) };
            titleRow.Children.Add(new TextBlock
            {
                Text = label, FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = act == _currentAct
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#58a6ff"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e"))
            });
            LevelGrid.Children.Add(titleRow);

            for (int i = 0; i < courses.Count; i += perRow)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                for (int j = i; j < i + perRow && j < courses.Count; j++)
                {
                    var c = courses[j];
                    var key = $"{act}/{c.Id}";
                    var completed = _save.CompletedLevels.Contains(key);
                    var isCurrent = act == _currentAct && c.Id == _currentLevel;
                    if (completed) totalDone++;

                    var card = CreateLevelCard(c.Id, c.Title, c.Concepts, completed, isCurrent);
                    card.Tag = (act, c.Id);
                    card.MouseLeftButtonDown += (_, _) => { _onJump(act, c.Id); Close(); };
                    row.Children.Add(card);
                }
                LevelGrid.Children.Add(row);
                totalCards += Math.Min(perRow, courses.Count - i);
            }
        }

        ProgressText.Text = $"已通关: {totalDone}/{totalCards}";
        ProgressBar.Width = totalCards > 0 ? 400.0 * totalDone / totalCards : 0;
    }

    private Border CreateLevelCard(string id, string title, string[] concepts, bool completed, bool isCurrent)
    {
        var bg = isCurrent ? "#1f2937" : "#0d1117";
        var borderColor = completed ? "#238636" : isCurrent ? "#58a6ff" : "#21262d";
        var titleColor = completed ? "#3fb950" : isCurrent ? "#58a6ff" : "#c9d1d9";
        var status = completed ? "✓ 已通关" : isCurrent ? "● 当前" : "🔒";

        var stack = new StackPanel { Width = 180, Margin = new Thickness(6) };

        var card = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor)),
            BorderThickness = new Thickness(isCurrent ? 3 : 1),
            Padding = new Thickness(16),
            Cursor = Cursors.Hand,
            Child = stack,
            Tag = id
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = $"关 {id}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(titleColor)) });
        header.Children.Add(new TextBlock { Text = $"  {status}", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e")), VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = title, FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b949e")),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6)
        });

        var conceptText = string.Join(", ", concepts.Take(3));
        stack.Children.Add(new TextBlock
        {
            Text = conceptText, FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#484f58")),
            TextWrapping = TextWrapping.Wrap
        });

        return card;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
