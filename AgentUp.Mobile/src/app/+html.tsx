import { ScrollViewStyleReset } from 'expo-router/html';
import type { PropsWithChildren } from 'react';
import { agentUpTheme } from '@agent-up/design-system/native';

export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="en">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta
          name="viewport"
          content="width=device-width, initial-scale=1, shrink-to-fit=no, viewport-fit=cover"
        />
        <meta name="theme-color" content={agentUpTheme.colors.canvas} />
        <link rel="manifest" href="/manifest.json" />
        <link rel="apple-touch-icon" href="/agent-up-icon-192.png" />
        <ScrollViewStyleReset />
        <style dangerouslySetInnerHTML={{ __html: `html, body { margin: 0; background: ${agentUpTheme.colors.canvas}; color: ${agentUpTheme.colors.textPrimary}; }` }} />
      </head>
      <body style={{ backgroundColor: agentUpTheme.colors.canvas, color: agentUpTheme.colors.textPrimary }}>{children}</body>
    </html>
  );
}
