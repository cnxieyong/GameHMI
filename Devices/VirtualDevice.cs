namespace GameHMI.Devices;

public abstract class VirtualDevice
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public abstract void Update(double deltaTime);
}
