import { forwardRef, useImperativeHandle, useRef } from 'react';
import { StyleSheet } from 'react-native';
import WebView, { type WebViewMessageEvent } from 'react-native-webview';
import { agentUpTheme } from '@agent-up/design-system/native';
import type { ApplicationProxySource } from '../providers/ApplicationBrowserProvider';
import type { RemoteBrowserHandle } from '../providers/RemoteBrowserHandle';

type PendingEval = {
  resolve: (value: string | null) => void;
};

/** Hosts the tunneled application HTTP response in the platform-native WebView. */
export const RemoteBrowser = forwardRef<RemoteBrowserHandle, { source: ApplicationProxySource }>(
  function RemoteBrowser({ source }, ref) {
    const webViewRef = useRef<WebView>(null);
    const pending = useRef(new Map<number, PendingEval>());
    const nextId = useRef(1);

    useImperativeHandle(ref, () => ({
      eval(script: string) {
        const id = nextId.current++;
        return new Promise<string | null>(resolve => {
          pending.current.set(id, { resolve });
          const timer = setTimeout(() => {
            pending.current.delete(id);
            resolve(null);
          }, 8000);
          const wrapped = pending.current.get(id);
          if (wrapped) {
            wrapped.resolve = value => {
              clearTimeout(timer);
              resolve(value);
            };
          }
          webViewRef.current?.injectJavaScript(
            `(function(){var id=${id};var out;try{out=eval(${JSON.stringify(script)});}catch(e){out=JSON.stringify({error:String(e&&e.message?e.message:e)});}if(typeof out!=='string'){try{out=JSON.stringify(out);}catch(err){out=String(out);}}window.ReactNativeWebView.postMessage(JSON.stringify({id:id,value:out}));})();true;`,
          );
        });
      },
    }), []);

    const onMessage = (event: WebViewMessageEvent) => {
      try {
        const payload = JSON.parse(event.nativeEvent.data) as { id?: number; value?: string };
        if (typeof payload.id !== 'number') return;
        const waiter = pending.current.get(payload.id);
        if (!waiter) return;
        pending.current.delete(payload.id);
        waiter.resolve(payload.value ?? null);
      } catch {
        return;
      }
    };

    return (
      <WebView
        ref={webViewRef}
        source={source.html
          ? { html: source.html }
          : { uri: source.uri, headers: { 'X-Agent-Up-Ticket': source.ticket } }}
        style={styles.browser}
        javaScriptEnabled
        domStorageEnabled
        onMessage={onMessage}
      />
    );
  },
);

const styles = StyleSheet.create({ browser: { flex: 1, backgroundColor: agentUpTheme.colors.canvas } });
