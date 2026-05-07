# 智造工厂 — GameHMI

一个把 C# 上位机编程教程做成沙盒游戏的教学平台。

写真实 C# 代码控制虚拟工厂设备，在解决问题的过程中学会编程。

## 界面

三栏布局：虚拟车间 | 代码编辑器 + IntelliSense | 任务面板 + 提示系统

## 课程

共 69 关，分 5 幕 + Bonus：

| 幕 | 关卡 | 主题 |
|----|------|------|
| 产线学徒 | 01-20 | C# 基础：Print → 变量 → if → for → class → interface |
| 设备调试员 | 21-35 | 串口通讯：Serial → 协议解析 → 帧处理 → 校验 |
| 自动化工程师 | 36-50 | PLC/Modbus：寄存器读写 → 联动 → 分拣 → 巡检 |
| 系统架构师 | 51-60 | 架构设计：继承体系 → 配置驱动 → 容错 → 审计 |
| 产线指挥官 | 61-65 | 总控系统：联调 → 故障恢复 → 调度 → 报表 |
| Bonus | B1-B4 | 极简挑战 / 容错极限 / 极速轮询 / 沙盒自由模式 |

## 技术栈

- .NET 9 WPF — 桌面界面
- AvalonEdit — 代码编辑器 + IntelliSense
- Microsoft.CodeAnalysis.CSharp.Scripting (Roslyn) — 代码执行沙箱
- System.Text.Json — 关卡数据与存档
- 自建虚拟设备：温度传感器、电机、加热器、串口、PLC/Modbus

## 架构

```
GameHMI/                     # WPF 主应用
├── MainWindow.xaml          # 三栏主界面（工厂 · 编辑器 · 任务）
├── Devices/                 # 虚拟设备（温度传感器、电机、PLC）
├── Services/                # 关卡加载、任务管理、错误翻译、存档
├── Models/                  # 关卡数据结构
├── CodeRunner/              # 独立子进程 — Roslyn 脚本编译执行
├── TestRunner/              # 关卡验证工具（68/68 通过）
└── Data/courses/            # 69 关 JSON 定义
```

代码执行通过独立子进程（CodeRunner.exe）隔离：玩家代码超时自动终止、崩溃不影响主界面。

## 构建

```bash
# 开发环境
cd GameHMI && dotnet build
cd CodeRunner && dotnet build

# 运行测试
cd TestRunner && dotnet run

# 打包（自包含，免装 .NET）
dotnet publish -c Release -r win-x64 --self-contained
```

## License

MIT
