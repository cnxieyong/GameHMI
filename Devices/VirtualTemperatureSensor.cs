namespace GameHMI.Devices;

public class VirtualTemperatureSensor : VirtualDevice
{
    public double Temperature { get; set; } = 25.0;
    public double AmbientTemp { get; set; } = 25.0;
    public double HeatingRate { get; set; } = 0.3;
    public double CoolingRate { get; set; } = 0.1;
    public bool HeaterOn { get; set; }

    public string Color => Temperature switch
    {
        >= 40 => "#d63031",
        >= 35 => "#e17055",
        >= 30 => "#fdcb6e",
        >= 27 => "#ffeaa7",
        _ => "#00cec9"
    };

    public string Status => Temperature switch
    {
        >= 40 => "⚠ 危险",
        >= 35 => "⚠ 高温",
        >= 30 => "注意",
        _ => "运行正常"
    };

    public override void Update(double dt)
    {
        // 自然漂移 + 加热/散热
        if (HeaterOn)
            Temperature += HeatingRate * dt;
        else
            Temperature += (AmbientTemp - Temperature) * CoolingRate * dt;

        // 环境噪声：模拟真实传感器的微小波动 (±0.5°C/s)
        Temperature += (Random.Shared.NextDouble() - 0.5) * 1.0 * dt;

        // 偶尔的小尖峰（模拟电气噪声）
        if (Random.Shared.NextDouble() < 0.02)
            Temperature += (Random.Shared.NextDouble() - 0.5) * 0.3;

        Temperature = Math.Round(Temperature, 1);
    }
}
