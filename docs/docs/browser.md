---
title: Browser
---

# Browser

Every workspace owns an isolated browser profile. The Server manages browser instances and the Desktop displays them.

Browser state includes:

- Cookies.
- Local Storage.
- Session Storage.
- IndexedDB.
- Cache.

Changing workspaces restores browser state. Restarting applications should reload the existing browser session instead of creating new tabs.

## Browser Abstraction

Browser implementations must be abstracted behind an interface:

```csharp
public interface IBrowserHost
{
    Task NavigateAsync(Uri uri);

    Task ReloadAsync();

    Task<string> GetHtmlAsync();

    Task ClickAsync(string selector);

    Task FillAsync(string selector, string value);

    Task<byte[]> ScreenshotAsync();
}
```

Platform implementations can vary as long as they preserve the shared abstraction.

## Structured Inspection

The browser should expose:

- Accessibility tree.
- Interactive elements.
- Page metadata.
- DOM snapshot.
- HTML.
- Browser history.
- Screenshot.

Accessibility data should be preferred over raw HTML.
