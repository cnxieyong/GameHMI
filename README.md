# 智造工厂 — GameHMI

一个把 C# 上位机编程教程做成沙盒游戏的教学平台。

写真实 C# 代码控制虚拟工厂设备，在解决问题的过程中学会编程。从零基础到系统架构师。

## 安装

1. 下载 [智造工厂_v1.0.zip](https://github.com/cnxieyong/GameHMI/releases) 解压到任意位置
2. 双击 `GameHMI.exe` 运行

> 不需要安装 .NET，不需要装任何东西。Windows 10/11 64位即可。

## 课程

共 69 关，分 5 幕 + Bonus：

| 幕 | 关卡 | 学什么 |
|----|------|--------|
| 🟢 产线学徒 | 01-20 | Print → 变量 → if/else → 数组 → for → 方法 → List → class → interface |
| 🔵 设备调试员 | 21-35 | SerialPort → ReadLine/WriteLine → 协议解析 → 帧格式 → 数据校验 |
| 🟡 自动化工程师 | 36-50 | Modbus/PLC → 寄存器 → 线圈 → 配方 → 报警分级 → 巡检 |
| 🟣 系统架构师 | 51-60 | 抽象类 → 配置驱动 → 回调模式 → 热更新 → 策略模式 → 审计 |
| 🔴 产线指挥官 | 61-65 | 五段联调 → 故障恢复 → 并行调度 → 报表 → 总控系统 |
| 🏆 Bonus | B1-B4 | 极简挑战 / 容错极限 / 极速轮询 / 沙盒自由模式 |

每个关卡：一个真实工厂问题 → 引入一个新语法点。边解决问题边学编程。

## 技术栈

| 组件 | 技术 |
|------|------|
| 界面 | .NET 9 WPF |
| 代码编辑器 | AvalonEdit + 40+ 词 IntelliSense |
| 代码执行 | Roslyn CSharpScript 独立进程沙箱 |
| 关卡数据 | JSON (System.Text.Json) |
| 画 UI | Canvas 2D 色块 + 文字 |

## 开发

```bash
# 构建
dotnet build CodeRunner
dotnet build GameHMI

# 运行测试（68/68 通过）
cd TestRunner && dotnet run

# 打包
dotnet publish -c Release -r win-x64 --self-contained
```

## License

MIT
