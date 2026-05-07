$runner = "C:\Users\Administrator\Desktop\GameHMI\CodeRunner\bin\Debug\net9.0\CodeRunner.exe"
$courseDir = "C:\Users\Administrator\Desktop\GameHMI\Data\courses\act01_apprentice"

if (!(Test-Path $runner)) { Write-Host "CodeRunner not built" -ForegroundColor Red; exit 1 }

$solutions = @{
    "01" = @'
Print($"🌡 烘干箱温度: {SensorTemp}°C");
Print($"⚙ 电机状态: {(MotorRunning ? "运行中" : "已停止")}");
'@
    "02" = @'
double t = SensorTemp;
Print($"当前烘干箱温度: {t}°C");
'@
    "03" = @'
if (SensorTemp > 30)
{
    Print("🔴 警告：温度过高！");
}
else
{
    Print("🟢 温度正常");
}
'@
    "04" = @'
for (int i = 0; i < SensorTemps.Length; i++)
{
    Print($"温区 {i}: {SensorTemps[i]}°C");
    if (SensorTemps[i] > 30)
        Print("  ⚠ 超标！");
}
'@
    "05" = @'
void CheckAll()
{
    Print($"温度: {SensorTemp}°C");
    Print($"电机: {(MotorRunning ? "运行中" : "已停止")}");
    Print($"加热器: {(HeaterOn ? "开启" : "关闭")}");
}
CheckAll();
CheckAll();
'@
    "06" = @'
var temps = new List<double>();
for (int i = 0; i < SensorTemps.Length; i++)
    temps.Add(SensorTemps[i]);
foreach (var t in temps)
    Print($"温区温度: {t}°C");
double max = 0;
foreach (var t in temps)
    if (t > max) max = t;
Print($"最高温度: {max}°C");
'@
    "07" = @'
if (SensorTemp >= 40) { Print("🔴 红色停机！"); HeaterOn = false; }
else if (SensorTemp >= 35) { Print("🟠 橙色告警！"); }
else if (SensorTemp >= 30) { Print("🟡 黄色预警"); }
else { Print("🟢 正常运行"); }
'@
    "08" = @'
int count = 0;
while (count < 10)
{
    Print($"第 {count + 1} 次采集: {SensorTemp:F1}°C");
    count++;
}
Print("采集完成！");
'@
    "09" = @'
class DryerBox { public double Temperature; public double Pressure; }
var boxA = new DryerBox();
boxA.Temperature = 28;
boxA.Pressure = 1.2;
var boxB = new DryerBox();
boxB.Temperature = 35;
boxB.Pressure = 1.5;
Print($"烘干箱A: {boxA.Temperature}°C, {boxA.Pressure}MPa");
Print($"烘干箱B: {boxB.Temperature}°C, {boxB.Pressure}MPa");
'@
    "10" = @'
class DryerBox
{
    public double TargetTemp { get; }
    public double CurrentTemp { get; set; }
    public DryerBox(double target) { TargetTemp = target; }
}
var productA = new DryerBox(85);
productA.CurrentTemp = 84.5;
var productB = new DryerBox(120);
productB.CurrentTemp = 118.2;
Print($"A产品 目标:{productA.TargetTemp}°C 当前:{productA.CurrentTemp}°C");
Print($"B产品 目标:{productB.TargetTemp}°C 当前:{productB.CurrentTemp}°C");
'@
}

$expected = @{
    "01" = "电机"
    "02" = "°C"
    "03" = "警告"
    "04" = "超标"
    "05" = "加热器"
    "06" = "最高温度"
    "07" = "橙色"
    "08" = "采集完成"
    "09" = "MPa"
    "10" = "目标"
}

$pass = 0; $fail = 0

foreach ($id in 1..10 | ForEach-Object { $_.ToString("D2") }) {
    $code = $solutions[$id]
    $want = $expected[$id]
    $json = @{ Code = $code; TimeoutMs = 5000; SensorTemp = 32.5; MotorRunning = $false; MotorSpeed = 0; SensorTemps = @(28.0, 32.0, 26.0, 34.0, 29.0) } | ConvertTo-Json -Compress

    $json | & $runner 2>$null | Out-Null -ErrorAction SilentlyContinue
    $result = $json | & $runner 2>$null

    try {
        $r = $result | ConvertFrom-Json
        if ($r.Success -and $r.Output -match [regex]::Escape($want)) {
            Write-Host "  ✓ 关 $id 通过" -ForegroundColor Green
            $pass++
        } elseif ($r.Success) {
            Write-Host "  ✗ 关 $id — 输出不包含 '$want'" -ForegroundColor Red
            Write-Host "    输出: $($r.Output)" -ForegroundColor DarkGray
            $fail++
        } else {
            Write-Host "  ✗ 关 $id — 执行失败: $($r.Error)" -ForegroundColor Red
            $fail++
        }
    } catch {
        Write-Host "  ✗ 关 $id — JSON解析失败" -ForegroundColor Red
        Write-Host "    Raw: $result" -ForegroundColor DarkGray
        $fail++
    }
}

Write-Host "`n结果: $pass 通过, $fail 失败" -ForegroundColor White
