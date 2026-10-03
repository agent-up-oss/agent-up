namespace AgentUp.AUDebug.Features.Screens.Models;

/// <summary>
/// One deterministic interaction in a product screen route.
/// </summary>
/// <remarks>
/// Desktop drives an X11 window, so it points and types at window-relative coordinates.
/// Mobile drives a DOM, so it addresses elements by the text a user reads. The kinds are
/// one enum because a route is one ordered list whichever surface runs it; each driver
/// rejects the kinds that do not belong to its surface.
/// </remarks>
public enum ScreenStepKind
{
    /// <summary>Desktop: click window-relative X/Y.</summary>
    Click,

    /// <summary>Desktop: type literal text into the focused control.</summary>
    Type,

    /// <summary>Desktop: send a key chord such as <c>ctrl+a</c>.</summary>
    Key,

    /// <summary>Mobile: load a path on the hosted web client.</summary>
    Navigate,

    /// <summary>Mobile: tap the element whose visible text or accessible label matches Target.</summary>
    Tap,

    /// <summary>Mobile: focus the field whose placeholder matches Target and insert Text.</summary>
    Fill,

    /// <summary>Either surface: settle for DelayMs before the next step.</summary>
    Settle
}
