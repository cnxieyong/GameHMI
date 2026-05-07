namespace GameHMI.Services;

public static class ErrorTranslator
{
    private static readonly Dictionary<string, string> _map = new()
    {
        ["CS1002"] = "这里少了一个分号 ;",
        ["CS1003"] = "语法错误，检查括号、逗号、分号是否配对",
        ["CS0103"] = "这个名字在当前上下文中不存在，检查是否拼写错误或漏了声明",
        ["CS0117"] = "这个类型没有这个属性或方法，检查拼写",
        ["CS0029"] = "类型不匹配，不能把一种类型直接赋给另一种",
        ["CS0103"] = "变量或方法名不存在，检查大小写和拼写",
        ["CS1525"] = "这里有一个无效的符号，检查括号和分号",
        ["CS1001"] = "这里需要一个标识符（变量名或方法名）",
        ["CS1026"] = "这里需要一个 ) 来关闭括号",
        ["CS1513"] = "这里需要一个 } 来关闭大括号",
        ["CS1010"] = "字符串常量需要换行时用 \\n，不是真的回车",
        ["CS0136"] = "这里不能声明同名变量，换一个名字",
        ["CS0165"] = "这个变量在使用前没有赋值，先给它一个初始值",
        ["CS0201"] = "只有赋值、调用、++、--、new 才能作为语句，单独的表达式不行",
        ["CS0266"] = "类型转换可能丢失精度，用 (int) 或 Convert.ToInt32() 显式转换",
        ["CS0019"] = "运算符不能用于这两种类型，比如数字和文字不能比大小",
        ["CS1061"] = "这个类型不包含这个方法或属性，检查拼写或是否漏了引用",
        ["CS0246"] = "找不到这个类型或命名空间，检查拼写",
        ["CS1503"] = "参数类型不匹配，传入的类型和需要的类型不一致",
        ["CS0115"] = "找不到匹配的签名来重写，检查方法名和参数",
        ["CS0260"] = "声明 partial 类时缺少 partial 关键字",
        ["CS1501"] = "方法参数数量不对，多了或少了参数",
        ["CS1502"] = "重载匹配出问题，最好的匹配有些参数不正确",
        ["CS0161"] = "不是所有路径都有返回值，检查方法中是否每个分支都 return 了",
        ["CS0162"] = "检测到无法到达的代码，前面的代码一定会走所以这里永远跑不到",
        ["CS0023"] = "运算符不能用于这种类型，参数类型不兼容",
        ["CS0050"] = "返回类型和声明的返回类型不一致",
        ["CS0051"] = "访问修饰符不一致，方法签名参数类型不匹配",
        ["CS0120"] = "这里需要一个对象引用，不能直接用类型名调用实例方法",
    };

    public static string Translate(string rawError)
    {
        foreach (var (code, zh) in _map)
        {
            if (rawError.Contains(code))
            {
                // 提取行号信息
                var line = ExtractLine(rawError);
                var loc = line > 0 ? $"[第{line}行] " : "";
                return $"{loc}{zh}";
            }
        }
        return rawError;
    }

    private static int ExtractLine(string raw)
    {
        // 格式: (3,10): error CS1002: ...
        var paren = raw.IndexOf('(');
        if (paren < 0) return 0;
        var comma = raw.IndexOf(',', paren);
        if (comma < 0) return 0;
        var lineStr = raw[(paren + 1)..comma];
        return int.TryParse(lineStr, out var n) ? n : 0;
    }
}
