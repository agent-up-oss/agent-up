#!/usr/bin/env python3
import pathlib
import sys
import xml.etree.ElementTree as ET

path = pathlib.Path(sys.argv[1])
if not path.is_file():
    raise SystemExit(f"watchdog result was not produced: {path}")

root = ET.parse(path).getroot()
counters = next((element for element in root.iter() if element.tag.endswith("Counters")), None)
executed = int(counters.attrib.get("executed", "0")) if counters is not None else 0
if executed == 0:
    raise SystemExit(f"watchdog filter executed no tests: {path}")

print(f"{path}: verified {executed} executed tests")
