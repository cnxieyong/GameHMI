namespace GameHMI.Devices;

public class VirtualMotor : VirtualDevice
{
    public bool IsRunning { get; set; }
    public int TargetSpeed { get; set; } = 1500;
    public int CurrentSpeed { get; set; }
    public int MaxSpeed { get; set; } = 3000;

    public string Status => IsRunning ? $"运转中 {CurrentSpeed}RPM" : "已停止";
    public string Color => IsRunning ? "#3fb950" : "#484f58";

    public void Start() => IsRunning = true;
    public void Stop() { IsRunning = false; CurrentSpeed = 0; }
    public void SetSpeed(int rpm) => TargetSpeed = Math.Clamp(rpm, 0, MaxSpeed);

    public override void Update(double dt)
    {
        if (IsRunning)
        {
            // 加减速模拟
            if (CurrentSpeed < TargetSpeed)
                CurrentSpeed += (int)(500 * dt);
            else if (CurrentSpeed > TargetSpeed)
                CurrentSpeed -= (int)(300 * dt);
            CurrentSpeed = Math.Clamp(CurrentSpeed, 0, MaxSpeed);
        }
    }
}
