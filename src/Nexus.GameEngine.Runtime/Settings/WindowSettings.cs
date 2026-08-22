namespace Nexus.GameEngine.Runtime.Settings;

public sealed class WindowSettings
{
    public string Title { get; set; } = "Nexus Game";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool VSync { get; set; } = true;
    public bool Resizable { get; set; } = true;
    public WindowMode Mode { get; set; } = WindowMode.Windowed;
}
