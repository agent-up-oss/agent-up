// Guards state updates against responses that a newer request has superseded. The Git screen loads
// a change tree and a file diff independently, and either can be overtaken when the user switches
// workspace, server, or file, so each gets its own gate. Keeping the rule here lets it be tested
// without a renderer.
export type RequestGate = {
  // Claims the gate for a new request and returns its ticket.
  begin(): number;
  // Reads the ticket in force without claiming a new one. Work whose validity is decided by
  // something other than its own start — a commit invalidated by switching workspace, say — must
  // read the ticket rather than begin one, since beginning would make the stale work current.
  current(): number;
  // Whether the ticket still belongs to the newest request.
  isCurrent(ticket: number): boolean;
};

export function createRequestGate(): RequestGate {
  let generation = 0;
  return {
    begin: () => ++generation,
    current: () => generation,
    isCurrent: ticket => ticket === generation,
  };
}
