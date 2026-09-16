#!/usr/bin/env python3
import json, pathlib, sys

report_path, baseline_path = map(pathlib.Path, sys.argv[1:3])
report = json.loads(report_path.read_text())
baseline = json.loads(baseline_path.read_text())['benchmarks']
seen = set()
failures = []
for result in report['Benchmarks']:
    name = result['FullName']
    if name not in baseline:
        continue
    seen.add(name)
    expected = baseline[name]
    mean = result['Statistics']['Mean']
    allocated = result.get('Memory', {}).get('BytesAllocatedPerOperation', 0)
    time_limit = expected['meanNanoseconds'] * expected['maxTimeRatio']
    allocation_limit = expected['allocatedBytes'] * expected['maxAllocationRatio']
    print(f'{name}: {mean:.1f} ns (limit {time_limit:.1f}), {allocated:.0f} B (limit {allocation_limit:.0f})')
    if mean > time_limit:
        failures.append(f'{name} mean {mean:.1f} ns exceeds {time_limit:.1f} ns')
    if allocated > allocation_limit:
        failures.append(f'{name} allocation {allocated:.0f} B exceeds {allocation_limit:.0f} B')
missing = set(baseline) - seen
if missing:
    failures.append('missing benchmark results: ' + ', '.join(sorted(missing)))
if failures:
    print('\nPerformance gate failed:', file=sys.stderr)
    print('\n'.join(f'- {failure}' for failure in failures), file=sys.stderr)
    sys.exit(1)
