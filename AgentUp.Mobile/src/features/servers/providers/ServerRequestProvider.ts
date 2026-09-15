// The transport moved to @agent-up/server-client so the chat module can reach a Server without
// depending on this app. The slice still owns connectivity, so feature slices keep importing it
// from here and nothing below this line knows the difference.
export {
  DEFAULT_TIMEOUT_MS,
  ServerRequestError,
  ensureCredentialTransportAllowed,
  isUnauthorized,
  jsonBody,
  readProblemDetail,
  requestServerJson,
  toReadableError,
} from '@agent-up/server-client';
export type { ServerSession } from '@agent-up/server-client';
