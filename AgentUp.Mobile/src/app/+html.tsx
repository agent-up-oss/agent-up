import { ScrollViewStyleReset } from 'expo-router/html';
import type { PropsWithChildren } from 'react';

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
        <meta name="theme-color" content="#000000" />
        <link rel="manifest" href="/manifest.json" />
        <link rel="apple-touch-icon" href="/agent-up-icon-192.png" />
        <script dangerouslySetInnerHTML={{ __html: serviceWorkerRegistration }} />
        <ScrollViewStyleReset />
      </head>
      <body style={{ backgroundColor: '#000000' }}>{children}</body>
    </html>
  );
}

const serviceWorkerRegistration =
  process.env.NODE_ENV === 'production'
    ? `
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(error => {
      console.error('Service worker registration failed:', error);
    });
  });
}
`
    : '';
