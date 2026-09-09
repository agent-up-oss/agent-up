// Guards state updates against responses that a newer request has superseded. The Git screen loads
// a change tree and a file diff independently, and either can be overtaken when the user switches
// workspace, server, or file, so each gets its own gate. Keeping the rule here lets it be tested
// without a renderer.
export type RequestGate = {
  // Claims the gate for a new request and returns its ticket.
  begin(): number;
  // Whether the ticket still belongs to the newest request.
  isCurrent(ticket: number): boolean;
};

export function createRequestGate(): RequestGate {
  let generation = 0;
  return {
    begin: () => ++generation,
    isCurrent: ticket => ticket === generation,
  };
}
