export function resolveMobilePort(value) {
  if (value === undefined || value === '') return 8081;
  if (!/^\d+$/.test(value)) throw new Error('WEB_PORT must be a numeric TCP port.');

  const port = Number(value);
  if (port < 1 || port > 65535) throw new Error('WEB_PORT must be between 1 and 65535.');
  return port;
}
