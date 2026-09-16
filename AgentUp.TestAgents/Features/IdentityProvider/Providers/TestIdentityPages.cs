namespace AgentUp.TestAgents.Features.IdentityProvider.Providers;

/// <summary>
/// The pages a user would see in the browser. Every actionable element carries a stable id so a
/// browser-driven test can find it without matching on prose.
/// </summary>
public static class TestIdentityPages
{
    public static string Consent(string clientId, string approveAction, string hidden) => Page(
        "Sign in",
        $"""
         <h1 id="title">Sign in to {Escape(clientId)}</h1>
         <p id="subtitle">This is the Agent-Up test identity provider.</p>
         <form id="consent" method="post" action="{Escape(approveAction)}">
           {hidden}
           <button id="approve" type="submit">Approve</button>
         </form>
         """);

    /// <summary>
    /// Where a pasted-code sign-in ends: the value is on screen for the user to copy back into
    /// the agent, exactly as the real console page does it.
    /// </summary>
    public static string CodeToCopy(string code) => Page(
        "Copy your code",
        $"""
         <h1 id="title">Copy this code</h1>
         <p id="instructions">Paste it back into Agent-Up to finish signing in.</p>
         <code id="code">{Escape(code)}</code>
         """);

    public static string DeviceEntry(string action) => Page(
        "Enter your code",
        $"""
         <h1 id="title">Enter your code</h1>
         <form id="device" method="post" action="{Escape(action)}">
           <input id="user-code" name="user_code" autocomplete="off" />
           <button id="approve" type="submit">Continue</button>
         </form>
         """);

    public static string Done(string message) => Page(
        "Signed in",
        $"""
         <h1 id="title">Signed in</h1>
         <p id="status">{Escape(message)}</p>
         """);

    public static string Problem(string message) => Page(
        "Something went wrong",
        $"""
         <h1 id="title">Something went wrong</h1>
         <p id="error">{Escape(message)}</p>
         """);

    private static string Page(string title, string body) =>
        $"""
         <!DOCTYPE html>
         <html lang="en">
         <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{Escape(title)}</title></head>
         <body>{body}</body>
         </html>
         """;

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
