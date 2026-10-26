import { forwardRef, useImperativeHandle, useRef } from 'react';
import { StyleSheet, View } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import type { ApplicationProxySource } from '../providers/ApplicationBrowserProvider';
import { stringifyEvalResult, type RemoteBrowserHandle } from '../providers/RemoteBrowserHandle';

/** Hosts the tunneled application HTTP response in the installable web client. */
export const RemoteBrowser = forwardRef<RemoteBrowserHandle, { source: ApplicationProxySource }>(
  function RemoteBrowser({ source }, ref) {
    const frameRef = useRef<HTMLIFrameElement>(null);

    useImperativeHandle(ref, () => ({
      eval(script: string) {
        const win = frameRef.current?.contentWindow;
        if (!win) return Promise.resolve(null);
        try {
          return Promise.resolve(stringifyEvalResult(win.eval(script)));
        } catch (error) {
          return Promise.resolve(JSON.stringify({
            error: error instanceof Error ? error.message : String(error),
          }));
        }
      },
    }), []);

    return (
      <View style={styles.container}>
        <iframe
          ref={frameRef}
          allow="clipboard-read; clipboard-write"
          src={source.html ? undefined : `${source.uri}#ticket=${encodeURIComponent(source.ticket)}`}
          srcDoc={source.html}
          style={{ border: 0, width: '100%', height: '100%' }}
          title="Application"
        />
      </View>
    );
  },
);

const styles = StyleSheet.create({ container: { flex: 1, minHeight: 400, backgroundColor: agentUpTheme.colors.canvas } });
