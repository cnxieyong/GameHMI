using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var sw = Stopwatch.StartNew();
await CSharpScript.EvaluateAsync<int>("42");
sw.Stop();
Console.Error.WriteLine($"WARMUP_OK:{sw.ElapsedMilliseconds}ms");

var stdin = await Console.In.ReadToEndAsync();
if (string.IsNullOrWhiteSpace(stdin)) { Console.Error.WriteLine("ERROR:empty stdin"); return 1; }

var request = JsonSerializer.Deserialize<RunRequest>(stdin) ?? new();
using var cts = new CancellationTokenSource(request.TimeoutMs > 0 ? request.TimeoutMs : 5000);

try
{
    var options = ScriptOptions.Default
        .WithImports("System", "System.Collections.Generic", "System.Linq")
        .WithReferences(typeof(object).Assembly, typeof(Enumerable).Assembly);

    var host = new ScriptHost
    {
        SensorTemp = request.SensorTemp,
        SensorTemps = request.SensorTemps ?? [request.SensorTemp, request.SensorTemp + 2, request.SensorTemp - 1],
        MotorRunning = request.MotorRunning,
        MotorSpeed = request.MotorSpeed,
        Serial = { ResponseData = request.SerialResponse }
    };

    // 注入 PLC 寄存器初值
    if (request.PlcRegisters != null)
        foreach (var (k, v) in request.PlcRegisters)
            host.Plc.HoldingRegisters[int.Parse(k)] = v;
    if (request.PlcCoils != null)
        foreach (var (k, v) in request.PlcCoils)
            host.Plc.Coils[int.Parse(k)] = v;

    // 记录初始值
    var prevMotorOn = host._motorOn;
    var prevMotorSpeed = host._motorSpeed;
    var prevHeaterOn = host._heaterOn;

    var script = CSharpScript.Create(request.Code, options, globalsType: typeof(ScriptHost));
    var task = script.RunAsync(host, ex => true, cts.Token);
    if (await Task.WhenAny(task, Task.Delay(request.TimeoutMs, cts.Token)) != task)
        { cts.Cancel(); throw new OperationCanceledException(); }
    await task;

    Console.WriteLine(JsonSerializer.Serialize(new RunResult
    {
        Success = true,
        Output = host.GetOutput(),
        MotorSet = host._motorOn != prevMotorOn,
        MotorOn = host._motorOn,
        SpeedSet = host._motorSpeed != prevMotorSpeed,
        MotorSpeedOut = host._motorSpeed,
        HeaterSet = host._heaterOn != prevHeaterOn,
        HeaterOn = host._heaterOn
    }));
}
catch (OperationCanceledException)
    { Console.WriteLine(JsonSerializer.Serialize(new RunResult { Success = false, Error = "TIMEOUT" })); }
catch (CompilationErrorException ex)
    { Console.WriteLine(JsonSerializer.Serialize(new RunResult { Success = false, Error = "COMPILE:" + string.Join("\n", ex.Diagnostics.Select(d => d.ToString())) })); }
catch (Exception ex)
    { Console.WriteLine(JsonSerializer.Serialize(new RunResult { Success = false, Error = "RUNTIME:" + ex.Message })); }
return 0;

public class RunRequest
{
    public string Code { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
    public double SensorTemp { get; set; }
    public double[]? SensorTemps { get; set; }
    public bool MotorRunning { get; set; }
    public int MotorSpeed { get; set; }
    public string SerialResponse { get; set; } = "";
    public Dictionary<string, ushort>? PlcRegisters { get; set; }
    public Dictionary<string, bool>? PlcCoils { get; set; }
}

public class RunResult
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

public class ScriptHost
{
    private readonly StringWriter _w = new();

    // 只读设备状态
    public double SensorTemp { get; set; }
    public double[] SensorTemps { get; set; } = [];
    public bool MotorRunning { get; set; }
    public int MotorSpeed { get; set; }

    // 虚拟串口
    public VirtualSerialPort Serial { get; set; } = new();

    // 虚拟 PLC (Modbus)
    public ModbusPlc Plc { get; set; } = new();

    // 可控字段（用 _ 前缀内部字段 + 公开访问的方式让 CSharpScript 能读写）
    public bool _motorOn;
    public int _motorSpeed;
    public bool _heaterOn;

    // 用户代码通过属性访问（实际读写 _ 字段）
    public bool MotorOn { get => _motorOn; set => _motorOn = value; }
    public int MotorSpeed_Result { get => _motorSpeed; set => _motorSpeed = value; }
    public bool HeaterOn { get => _heaterOn; set => _heaterOn = value; }

    public void Print(object? o) => _w.Write(o?.ToString());
    public string GetOutput() => _w.ToString();
}

public class VirtualSerialPort
{
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public bool IsOpen { get; private set; }
    public string ResponseData { get; set; } = "";

    private readonly Queue<string> _buffer = new();

    public void Open()
    {
        IsOpen = true;
        // 把预设响应数据装入缓冲区
        if (!string.IsNullOrEmpty(ResponseData))
            foreach (var line in ResponseData.Split('\n'))
                _buffer.Enqueue(line.Trim());
    }

    public void Close() { IsOpen = false; _buffer.Clear(); }

    public void WriteLine(string text) { /* 发送指令，不模拟回显 */ }

    public string? ReadLine()
    {
        if (!IsOpen) return null;
        return _buffer.Count > 0 ? _buffer.Dequeue() : null;
    }

    public string? ReadExisting()
    {
        if (!IsOpen) return null;
        var lines = new List<string>();
        while (_buffer.Count > 0) lines.Add(_buffer.Dequeue());
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }
}

public class ModbusPlc
{
    public Dictionary<int, ushort> HoldingRegisters { get; set; } = new();
    public Dictionary<int, bool> Coils { get; set; } = new();

    public ushort ReadHoldingRegister(int addr) =>
        HoldingRegisters.TryGetValue(addr, out var v) ? v : (ushort)0;

    public void WriteHoldingRegister(int addr, ushort value) =>
        HoldingRegisters[addr] = value;

    public bool ReadCoil(int addr) =>
        Coils.TryGetValue(addr, out var v) && v;

    public void WriteCoil(int addr, bool value) =>
        Coils[addr] = value;
}
