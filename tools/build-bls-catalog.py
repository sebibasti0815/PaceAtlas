#!/usr/bin/env python3
"""Convert the official BLS 4.0 XLSX into the app's reproducible offline seed.

Usage: python3 tools/build-bls-catalog.py /path/to/BLS_4_0_Daten_2025_DE.xlsx
Source: Max Rubner-Institut (2025), BLS 4.0, DOI 10.25826/Data20251217-134202-0, CC BY 4.0.
"""
import gzip
import json
from pathlib import Path
import sys
import openpyxl

source = Path(sys.argv[1])
target = Path(__file__).resolve().parents[1] / 'PaceAtlas.Core' / 'Bls4Catalog.tsv.gz'
sheet = openpyxl.load_workbook(source, read_only=True, data_only=True).active
rows = sheet.values
headers = next(rows)
assert headers[0:3] == ('BLS Code', 'Lebensmittelbezeichnung', 'Food name')
assert headers[18].startswith('CHO Kohlenhydrate, verfügbar')
components = [(headers[i].split(' ', 1)[0], i) for i in range(3, len(headers), 3)]
with gzip.open(target, 'wt', encoding='utf-8', newline='\n') as output:
    for row in rows:
        code, name, english = row[:3]
        if not code or not name:
            continue
        nutrients = {}
        for key, i in components:
            value = row[i]
            if isinstance(value, (int, float)):
                nutrients[key] = [value, row[i + 1] if row[i + 1] != '-' else None,
                                  row[i + 2] if row[i + 2] != '-' else None]
        clean = lambda s: str(s or '').replace('\t', ' ').replace('\n', ' ').replace('\r', ' ')
        output.write('\t'.join((clean(code), clean(name), clean(english),
            str(row[18]) if isinstance(row[18], (float, int)) else '',
            json.dumps(nutrients, ensure_ascii=False, separators=(',', ':')))) + '\n')
print(target, target.stat().st_size)
