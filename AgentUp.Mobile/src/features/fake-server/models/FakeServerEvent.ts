export type FakeServerEvent = {
  sequence: number;
  type: string;
  payload: unknown;
  timestamp: string;
};

export function fakeServerEventFrame(event: FakeServerEvent): string {
  return `data: ${JSON.stringify(event)}\n\n`;
}
