namespace Nexus.Input.Devices;

public interface IInputDevice
{
    InputDeviceId Id { get; }
    string Name { get; }
    bool IsConnected { get; }
}
