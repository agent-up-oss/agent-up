/**
 * Native Detox launches the harness with this URL so the Server origin and workspace arrive
 * without typing into the connect form. Android Fabric's replaceText does not fire onChangeText,
 * which left Connect as a no-op and the suite waiting for a picker the chat never mounted.
 */

function connectLaunchUrl(serverUrl, workspaceId) {
  const url = new URL('agent-up-chat://connect');
  url.searchParams.set('server', serverUrl);
  url.searchParams.set('workspace', workspaceId);
  return url.href;
}

function parseConnectLaunchUrl(url) {
  if (!url) return null;
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }
  if (parsed.protocol !== 'agent-up-chat:') return null;
  const server = parsed.searchParams.get('server')?.trim();
  const workspace = parsed.searchParams.get('workspace')?.trim();
  if (!server || !workspace) return null;
  return { url: server.replace(/\/+$/, ''), workspaceId: workspace };
}

module.exports = { connectLaunchUrl, parseConnectLaunchUrl };
