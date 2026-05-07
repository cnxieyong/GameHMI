namespace GameHMI.Devices;

public class VirtualPlc : VirtualDevice
{
    public Dictionary<int, ushort> HoldingRegisters { get; } = new();
    public Dictionary<int, bool> Coils { get; } = new();

    public ushort ReadHoldingRegister(int addr) =>
        HoldingRegisters.TryGetValue(addr, out var v) ? v : (ushort)0;

    public void WriteHoldingRegister(int addr, ushort value) =>
        HoldingRegisters[addr] = value;

    public bool ReadCoil(int addr) =>
        Coils.TryGetValue(addr, out var v) && v;

    public void WriteCoil(int addr, bool value) =>
        Coils[addr] = value;

    public override void Update(double dt) { }
}
