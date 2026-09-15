import { NeverGoingToHappen, waitFor } from './wait.mjs';

/**
 * Watches a process this stack started, so a stack that fails to come up says why.
 *
 * Without this a process that dies on startup is indistinguishable from one that is merely slow:
 * the wait runs its full minute and reports "fetch failed", which is true and useless. It happened
 * - the identity provider did not answer on one run of four and left nothing behind to explain it.
 * So what the process said is kept, and its death ends the wait immediately rather than a minute
 * later.
 */
const KEPT_CHUNKS = 200;

export function supervise(name, child) {
  const said = [];
  let ended = null;

  const keep = (chunk, stream) => {
    said.push(String(chunk));
    // A process that fails usually says so in its first breath, and a chatty one that succeeds is
    // never read at all, so keeping a bounded head-and-tail costs nothing either way.
    if (said.length > KEPT_CHUNKS) said.splice(KEPT_CHUNKS / 2, said.length - KEPT_CHUNKS);
    stream.write(`[${name}] ${chunk}`);
  };

  child.stdout?.on('data', chunk => keep(chunk, process.stdout));
  child.stderr?.on('data', chunk => keep(chunk, process.stderr));
  child.on('error', cause => { ended ??= { cause }; });
  child.on('exit', (code, signal) => { ended ??= { code, signal }; });

  const state = () => {
    if (!ended) return `${name} is running but has not answered yet`;
    if (ended.cause) return `${name} could not be started: ${ended.cause.message}`;
    return ended.signal
      ? `${name} was killed by ${ended.signal}`
      : `${name} exited with code ${ended.code}`;
  };

  const transcriptOf = () => said.join('').trim();

  return {
    child,
    get ended() { return ended; },
    said: transcriptOf,
    /** Waits for this process to answer, and gives up the moment it cannot. */
    async answers(description, condition, options) {
      try {
        return await waitFor(description, () => {
          if (ended) throw new NeverGoingToHappen(`${state()} before ${description}.`);
          return condition();
        }, options);
      } catch (cause) {
        const transcript = transcriptOf();
        throw new Error(
          `${cause.message}\n  ${state()}.\n  ${transcript ? `It said:\n${indent(transcript)}` : 'It said nothing at all.'}`,
        );
      }
    },
  };
}

function indent(text) {
  return text.split('\n').map(line => `    ${line}`).join('\n');
}
