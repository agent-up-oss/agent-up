export function parseArgs(argv) {
  let outDir = null;
  let flowId = null;

  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (arg === '--out' || arg === '--flow') {
      const value = argv[index + 1];
      if (!value || value.startsWith('--'))
        throw new Error(`${arg} requires a value.`);
      if (arg === '--out')
        outDir = value;
      else
        flowId = value;
      index++;
      continue;
    }
    if (arg.startsWith('--'))
      throw new Error(`Unknown argument '${arg}'.`);
  }

  if (!outDir)
    throw new Error('--out is required.');

  return { outDir, flowId };
}
