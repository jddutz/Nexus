namespace Nexus.Input;

/// <summary>
/// Identifies a keyboard key.
/// </summary>
public enum KeyEnum
{
    /// <summary>An unknown or unsupported key.</summary>
    Unknown,

    /// <summary>The space key.</summary>
    Space,

    /// <summary>The apostrophe key.</summary>
    Apostrophe,

    /// <summary>The comma key.</summary>
    Comma,

    /// <summary>The minus key.</summary>
    Minus,

    /// <summary>The period key.</summary>
    Period,

    /// <summary>The slash key.</summary>
    Slash,

    /// <summary>The 0 key in the number row.</summary>
    Number0,

    /// <summary>The 1 key in the number row.</summary>
    Number1,

    /// <summary>The 2 key in the number row.</summary>
    Number2,

    /// <summary>The 3 key in the number row.</summary>
    Number3,

    /// <summary>The 4 key in the number row.</summary>
    Number4,

    /// <summary>The 5 key in the number row.</summary>
    Number5,

    /// <summary>The 6 key in the number row.</summary>
    Number6,

    /// <summary>The 7 key in the number row.</summary>
    Number7,

    /// <summary>The 8 key in the number row.</summary>
    Number8,

    /// <summary>The 9 key in the number row.</summary>
    Number9,

    /// <summary>The semicolon key.</summary>
    Semicolon,

    /// <summary>The equal key.</summary>
    Equal,

    /// <summary>The A key.</summary>
    A,

    /// <summary>The B key.</summary>
    B,

    /// <summary>The C key.</summary>
    C,

    /// <summary>The D key.</summary>
    D,

    /// <summary>The E key.</summary>
    E,

    /// <summary>The F key.</summary>
    F,

    /// <summary>The G key.</summary>
    G,

    /// <summary>The H key.</summary>
    H,

    /// <summary>The I key.</summary>
    I,

    /// <summary>The J key.</summary>
    J,

    /// <summary>The K key.</summary>
    K,

    /// <summary>The L key.</summary>
    L,

    /// <summary>The M key.</summary>
    M,

    /// <summary>The N key.</summary>
    N,

    /// <summary>The O key.</summary>
    O,

    /// <summary>The P key.</summary>
    P,

    /// <summary>The Q key.</summary>
    Q,

    /// <summary>The R key.</summary>
    R,

    /// <summary>The S key.</summary>
    S,

    /// <summary>The T key.</summary>
    T,

    /// <summary>The U key.</summary>
    U,

    /// <summary>The V key.</summary>
    V,

    /// <summary>The W key.</summary>
    W,

    /// <summary>The X key.</summary>
    X,

    /// <summary>The Y key.</summary>
    Y,

    /// <summary>The Z key.</summary>
    Z,

    /// <summary>The left bracket key.</summary>
    LeftBracket,

    /// <summary>The backslash key.</summary>
    BackSlash,

    /// <summary>The right bracket key.</summary>
    RightBracket,

    /// <summary>The grave accent key.</summary>
    GraveAccent,

    /// <summary>The first non-US keyboard layout key.</summary>
    World1,

    /// <summary>The second non-US keyboard layout key.</summary>
    World2,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The Enter key.</summary>
    Enter,

    /// <summary>The Tab key.</summary>
    Tab,

    /// <summary>The Backspace key.</summary>
    Backspace,

    /// <summary>The Insert key.</summary>
    Insert,

    /// <summary>The Delete key.</summary>
    Delete,

    /// <summary>The right arrow key.</summary>
    Right,

    /// <summary>The left arrow key.</summary>
    Left,

    /// <summary>The down arrow key.</summary>
    Down,

    /// <summary>The up arrow key.</summary>
    Up,

    /// <summary>The Page Up key.</summary>
    PageUp,

    /// <summary>The Page Down key.</summary>
    PageDown,

    /// <summary>The Home key.</summary>
    Home,

    /// <summary>The End key.</summary>
    End,

    /// <summary>The Caps Lock key.</summary>
    CapsLock,

    /// <summary>The Scroll Lock key.</summary>
    ScrollLock,

    /// <summary>The Num Lock key.</summary>
    NumLock,

    /// <summary>The Print Screen key.</summary>
    PrintScreen,

    /// <summary>The Pause key.</summary>
    Pause,

    /// <summary>The F1 key.</summary>
    F1,

    /// <summary>The F2 key.</summary>
    F2,

    /// <summary>The F3 key.</summary>
    F3,

    /// <summary>The F4 key.</summary>
    F4,

    /// <summary>The F5 key.</summary>
    F5,

    /// <summary>The F6 key.</summary>
    F6,

    /// <summary>The F7 key.</summary>
    F7,

    /// <summary>The F8 key.</summary>
    F8,

    /// <summary>The F9 key.</summary>
    F9,

    /// <summary>The F10 key.</summary>
    F10,

    /// <summary>The F11 key.</summary>
    F11,

    /// <summary>The F12 key.</summary>
    F12,

    /// <summary>The F13 key.</summary>
    F13,

    /// <summary>The F14 key.</summary>
    F14,

    /// <summary>The F15 key.</summary>
    F15,

    /// <summary>The F16 key.</summary>
    F16,

    /// <summary>The F17 key.</summary>
    F17,

    /// <summary>The F18 key.</summary>
    F18,

    /// <summary>The F19 key.</summary>
    F19,

    /// <summary>The F20 key.</summary>
    F20,

    /// <summary>The F21 key.</summary>
    F21,

    /// <summary>The F22 key.</summary>
    F22,

    /// <summary>The F23 key.</summary>
    F23,

    /// <summary>The F24 key.</summary>
    F24,

    /// <summary>The F25 key.</summary>
    F25,

    /// <summary>The 0 key on the numeric keypad.</summary>
    Numpad0,

    /// <summary>The 1 key on the numeric keypad.</summary>
    Numpad1,

    /// <summary>The 2 key on the numeric keypad.</summary>
    Numpad2,

    /// <summary>The 3 key on the numeric keypad.</summary>
    Numpad3,

    /// <summary>The 4 key on the numeric keypad.</summary>
    Numpad4,

    /// <summary>The 5 key on the numeric keypad.</summary>
    Numpad5,

    /// <summary>The 6 key on the numeric keypad.</summary>
    Numpad6,

    /// <summary>The 7 key on the numeric keypad.</summary>
    Numpad7,

    /// <summary>The 8 key on the numeric keypad.</summary>
    Numpad8,

    /// <summary>The 9 key on the numeric keypad.</summary>
    Numpad9,

    /// <summary>The decimal key on the numeric keypad.</summary>
    NumpadDecimal,

    /// <summary>The divide key on the numeric keypad.</summary>
    NumpadDivide,

    /// <summary>The multiply key on the numeric keypad.</summary>
    NumpadMultiply,

    /// <summary>The subtract key on the numeric keypad.</summary>
    NumpadSubtract,

    /// <summary>The add key on the numeric keypad.</summary>
    NumpadAdd,

    /// <summary>The Enter key on the numeric keypad.</summary>
    NumpadEnter,

    /// <summary>The equal key on the numeric keypad.</summary>
    NumpadEqual,

    /// <summary>The left Shift key.</summary>
    ShiftLeft,

    /// <summary>The left Control key.</summary>
    ControlLeft,

    /// <summary>The left Alt key.</summary>
    AltLeft,

    /// <summary>The left Super key.</summary>
    SuperLeft,

    /// <summary>The right Shift key.</summary>
    ShiftRight,

    /// <summary>The right Control key.</summary>
    ControlRight,

    /// <summary>The right Alt key.</summary>
    AltRight,

    /// <summary>The right Super key.</summary>
    SuperRight,

    /// <summary>The Menu key.</summary>
    Menu,
}

/// <summary>
/// Provides conversions for <see cref="KeyEnum"/>.
/// </summary>
public static class KeyEnumExtensions
{
    /// <summary>
    /// Converts a keyboard key identifier to the corresponding Silk.NET input key.
    /// </summary>
    /// <param name="key">The keyboard key identifier to convert.</param>
    /// <returns>The corresponding Silk.NET input key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="KeyEnum"/>.</exception>
    public static SilkKey ToSilkKey(this KeyEnum key) =>
        key switch
        {
            KeyEnum.Unknown => SilkKey.Unknown,
            KeyEnum.Space => SilkKey.Space,
            KeyEnum.Apostrophe => SilkKey.Apostrophe,
            KeyEnum.Comma => SilkKey.Comma,
            KeyEnum.Minus => SilkKey.Minus,
            KeyEnum.Period => SilkKey.Period,
            KeyEnum.Slash => SilkKey.Slash,
            KeyEnum.Number0 => SilkKey.Number0,
            KeyEnum.Number1 => SilkKey.Number1,
            KeyEnum.Number2 => SilkKey.Number2,
            KeyEnum.Number3 => SilkKey.Number3,
            KeyEnum.Number4 => SilkKey.Number4,
            KeyEnum.Number5 => SilkKey.Number5,
            KeyEnum.Number6 => SilkKey.Number6,
            KeyEnum.Number7 => SilkKey.Number7,
            KeyEnum.Number8 => SilkKey.Number8,
            KeyEnum.Number9 => SilkKey.Number9,
            KeyEnum.Semicolon => SilkKey.Semicolon,
            KeyEnum.Equal => SilkKey.Equal,
            KeyEnum.A => SilkKey.A,
            KeyEnum.B => SilkKey.B,
            KeyEnum.C => SilkKey.C,
            KeyEnum.D => SilkKey.D,
            KeyEnum.E => SilkKey.E,
            KeyEnum.F => SilkKey.F,
            KeyEnum.G => SilkKey.G,
            KeyEnum.H => SilkKey.H,
            KeyEnum.I => SilkKey.I,
            KeyEnum.J => SilkKey.J,
            KeyEnum.K => SilkKey.K,
            KeyEnum.L => SilkKey.L,
            KeyEnum.M => SilkKey.M,
            KeyEnum.N => SilkKey.N,
            KeyEnum.O => SilkKey.O,
            KeyEnum.P => SilkKey.P,
            KeyEnum.Q => SilkKey.Q,
            KeyEnum.R => SilkKey.R,
            KeyEnum.S => SilkKey.S,
            KeyEnum.T => SilkKey.T,
            KeyEnum.U => SilkKey.U,
            KeyEnum.V => SilkKey.V,
            KeyEnum.W => SilkKey.W,
            KeyEnum.X => SilkKey.X,
            KeyEnum.Y => SilkKey.Y,
            KeyEnum.Z => SilkKey.Z,
            KeyEnum.LeftBracket => SilkKey.LeftBracket,
            KeyEnum.BackSlash => SilkKey.BackSlash,
            KeyEnum.RightBracket => SilkKey.RightBracket,
            KeyEnum.GraveAccent => SilkKey.GraveAccent,
            KeyEnum.World1 => SilkKey.World1,
            KeyEnum.World2 => SilkKey.World2,
            KeyEnum.Escape => SilkKey.Escape,
            KeyEnum.Enter => SilkKey.Enter,
            KeyEnum.Tab => SilkKey.Tab,
            KeyEnum.Backspace => SilkKey.Backspace,
            KeyEnum.Insert => SilkKey.Insert,
            KeyEnum.Delete => SilkKey.Delete,
            KeyEnum.Right => SilkKey.Right,
            KeyEnum.Left => SilkKey.Left,
            KeyEnum.Down => SilkKey.Down,
            KeyEnum.Up => SilkKey.Up,
            KeyEnum.PageUp => SilkKey.PageUp,
            KeyEnum.PageDown => SilkKey.PageDown,
            KeyEnum.Home => SilkKey.Home,
            KeyEnum.End => SilkKey.End,
            KeyEnum.CapsLock => SilkKey.CapsLock,
            KeyEnum.ScrollLock => SilkKey.ScrollLock,
            KeyEnum.NumLock => SilkKey.NumLock,
            KeyEnum.PrintScreen => SilkKey.PrintScreen,
            KeyEnum.Pause => SilkKey.Pause,
            KeyEnum.F1 => SilkKey.F1,
            KeyEnum.F2 => SilkKey.F2,
            KeyEnum.F3 => SilkKey.F3,
            KeyEnum.F4 => SilkKey.F4,
            KeyEnum.F5 => SilkKey.F5,
            KeyEnum.F6 => SilkKey.F6,
            KeyEnum.F7 => SilkKey.F7,
            KeyEnum.F8 => SilkKey.F8,
            KeyEnum.F9 => SilkKey.F9,
            KeyEnum.F10 => SilkKey.F10,
            KeyEnum.F11 => SilkKey.F11,
            KeyEnum.F12 => SilkKey.F12,
            KeyEnum.F13 => SilkKey.F13,
            KeyEnum.F14 => SilkKey.F14,
            KeyEnum.F15 => SilkKey.F15,
            KeyEnum.F16 => SilkKey.F16,
            KeyEnum.F17 => SilkKey.F17,
            KeyEnum.F18 => SilkKey.F18,
            KeyEnum.F19 => SilkKey.F19,
            KeyEnum.F20 => SilkKey.F20,
            KeyEnum.F21 => SilkKey.F21,
            KeyEnum.F22 => SilkKey.F22,
            KeyEnum.F23 => SilkKey.F23,
            KeyEnum.F24 => SilkKey.F24,
            KeyEnum.F25 => SilkKey.F25,
            KeyEnum.Numpad0 => SilkKey.Keypad0,
            KeyEnum.Numpad1 => SilkKey.Keypad1,
            KeyEnum.Numpad2 => SilkKey.Keypad2,
            KeyEnum.Numpad3 => SilkKey.Keypad3,
            KeyEnum.Numpad4 => SilkKey.Keypad4,
            KeyEnum.Numpad5 => SilkKey.Keypad5,
            KeyEnum.Numpad6 => SilkKey.Keypad6,
            KeyEnum.Numpad7 => SilkKey.Keypad7,
            KeyEnum.Numpad8 => SilkKey.Keypad8,
            KeyEnum.Numpad9 => SilkKey.Keypad9,
            KeyEnum.NumpadDecimal => SilkKey.KeypadDecimal,
            KeyEnum.NumpadDivide => SilkKey.KeypadDivide,
            KeyEnum.NumpadMultiply => SilkKey.KeypadMultiply,
            KeyEnum.NumpadSubtract => SilkKey.KeypadSubtract,
            KeyEnum.NumpadAdd => SilkKey.KeypadAdd,
            KeyEnum.NumpadEnter => SilkKey.KeypadEnter,
            KeyEnum.NumpadEqual => SilkKey.KeypadEqual,
            KeyEnum.ShiftLeft => SilkKey.ShiftLeft,
            KeyEnum.ControlLeft => SilkKey.ControlLeft,
            KeyEnum.AltLeft => SilkKey.AltLeft,
            KeyEnum.SuperLeft => SilkKey.SuperLeft,
            KeyEnum.ShiftRight => SilkKey.ShiftRight,
            KeyEnum.ControlRight => SilkKey.ControlRight,
            KeyEnum.AltRight => SilkKey.AltRight,
            KeyEnum.SuperRight => SilkKey.SuperRight,
            KeyEnum.Menu => SilkKey.Menu,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };

    /// <summary>
    /// Converts a Silk.NET key identifier to the corresponding engine key identifier.
    /// </summary>
    /// <param name="key">The Silk.NET key identifier.</param>
    /// <returns>The corresponding engine key, or <see cref="KeyEnum.Unknown"/> when unsupported.</returns>
    public static KeyEnum ToKeyEnum(this SilkKey key)
    {
        var name = key.ToString();
        if (name.StartsWith("Keypad", StringComparison.Ordinal))
            name = "Numpad" + name["Keypad".Length..];

        return Enum.TryParse<KeyEnum>(name, out var engineKey) && Enum.IsDefined(engineKey)
            ? engineKey
            : KeyEnum.Unknown;
    }
}
