namespace Nexus.GUI;

/// <summary>
/// Defines lifecycle operations for a graphical user interface.
/// </summary>
public interface IGraphicalUserInterface
{
    /// <summary>Gets the element currently focused by the GUI.</summary>
    IElement? FocusedElement { get; }

    /// <summary>Sets the focused element, or clears focus when null is supplied.</summary>
    /// <param name="element">The focusable active element, or null to clear focus.</param>
    /// <exception cref="ArgumentException">The element is not active, registered, or focusable.</exception>
    void SetFocus(IElement? element);

    /// <summary>Moves focus to the next or previous eligible element in active hierarchy order.</summary>
    /// <param name="direction">The traversal direction.</param>
    /// <returns>True when an eligible element received focus; otherwise, false.</returns>
    /// <remarks>Traversal wraps at either end. With no current focus, Next starts at the first element and Previous at the last.</remarks>
    bool MoveFocus(FocusDirection direction);

    /// <summary>
    /// Initializes the graphical user interface.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the graphical user interface for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time, in seconds, since the previous frame.</param>
    void Update(double deltaTime);
}
