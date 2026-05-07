using System.Windows.Threading;

namespace GameHMI.Devices;

public class SimulationEngine
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly List<VirtualDevice> _devices = new();
    private DateTime _lastTick;

    public event Action? Tick;

    public SimulationEngine()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            var dt = (_lastTick == default) ? 0.1 : (now - _lastTick).TotalSeconds;
            _lastTick = now;

            foreach (var d in _devices)
                d.Update(dt);

            Tick?.Invoke();
        };
    }

    public void Register(VirtualDevice device) => _devices.Add(device);
    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
}
