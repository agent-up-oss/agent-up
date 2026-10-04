using System.Text.Json;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// The page scripts the Mobile screens route evaluates: activate Demo, route inside the app,
/// and locate the element a step names.
/// </summary>
/// <remarks>
/// Routing is client-side on purpose. The Demo backend is an in-page object, so a document
/// load would throw away the transcript, files, and commit the earlier screens built - and
/// the static export has no file for a dynamic route such as
/// <c>/workspace/harbor-shop/git/review</c> anyway. Pushing the history entry and letting
/// the router pick it up keeps one session for the whole run.
/// </remarks>
public static class MobileScreenScriptProvider
{
    public static string Navigate(string path)
        => $$"""
            (() => {
              window.history.pushState({}, '', {{JsonSerializer.Serialize(path)}});
              window.dispatchEvent(new PopStateEvent('popstate'));
              return window.location.pathname;
            })()
            """;

    /// <summary>
    /// A point that taps the element a step names, or null while it has none.
    /// </summary>
    /// <remarks>
    /// Two things make a text match the wrong target. React Native Web renders nested views,
    /// so a label matches several ancestors and pressing one hits whatever else it wraps;
    /// only the innermost match is the control. And an element can be matched and still not
    /// be tappable where it is measured: the sidebar scrim covers the window, but the drawer
    /// sits on its left half, so its centre belongs to the drawer. Hit-testing settles both,
    /// and scanning the box rather than only its centre finds the part of a partly covered
    /// control that a finger would actually reach.
    /// <para>
    /// Candidates are narrowed in order: an exact accessible label, then exact visible text,
    /// then a prefix. Both halves matter. The confirmation button is labelled "Commit" and
    /// the text inside the commit button reads "Commit" too, so without preferring the label
    /// a step aimed at the dialog presses the button behind it; and the field below is
    /// labelled "Commit message", so without preferring exact over prefix it wins instead.
    /// </para>
    /// </remarks>
    public static string Locate(string label)
        => $$"""
            (() => {
              const want = {{JsonSerializer.Serialize(label)}};
              const all = [...document.querySelectorAll('*')];
              const labelled = all.filter(node => (node.getAttribute('aria-label') || '').trim() === want);
              const read = node => ((node.getAttribute && node.getAttribute('aria-label')) || node.innerText || '').trim();
              const exact = labelled.length > 0 ? labelled : all.filter(node => read(node) === want);
              const matches = exact.length > 0 ? exact : all.filter(node => read(node).startsWith(want));
              const innermost = matches.filter(node => !matches.some(other => other !== node && node.contains(other)));
              const target = innermost[0] || matches[0];
              if (!target) return null;
              // Fill leaves the composer focused. Headless mobile Chromium then keeps the
              // visual viewport on that field, so a control at the top of the shell — Go back
              // after the agent prompts — is on the page and still misses every hit-test.
              if (document.activeElement && document.activeElement !== target && typeof document.activeElement.blur === 'function') {
                document.activeElement.blur();
              }
              if (typeof target.scrollIntoView === 'function') {
                target.scrollIntoView({ block: 'nearest', inline: 'nearest' });
              }
              const box = target.getBoundingClientRect();
              if (box.width === 0 || box.height === 0) return null;
              const fractions = [0.5, 0.25, 0.75, 0.1, 0.9];
              for (const fy of fractions) {
                for (const fx of fractions) {
                  const x = Math.round(box.x + (box.width * fx));
                  const y = Math.round(box.y + (box.height * fy));
                  const hit = document.elementFromPoint(x, y);
                  if (hit && (hit === target || target.contains(hit))) return { x, y };
                }
              }
              return null;
            })()
            """;

    /// <summary>
    /// The text the screen is showing, for <c>screens compare</c> to hold the design system's
    /// documented copy against.
    /// </summary>
    /// <remarks>
    /// <c>innerText</c> rather than <c>textContent</c>: it reports what is laid out and visible,
    /// so copy inside a closed drawer or a hidden tab does not count as being on the screen.
    /// </remarks>
    public static string ReadText()
        => "(() => (document.body && document.body.innerText) || '')()";

    /// <summary>Focus the field a step names and report where it is, so the tap lands on it.</summary>
    public static string FocusField(string label)
        => $$"""
            (() => {
              const want = {{JsonSerializer.Serialize(label)}};
              const field = [...document.querySelectorAll('input,textarea')].find(node =>
                (node.placeholder || '').includes(want) || (node.getAttribute('aria-label') || '').includes(want));
              if (!field) return null;
              field.focus();
              const box = field.getBoundingClientRect();
              return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
            })()
            """;
}
