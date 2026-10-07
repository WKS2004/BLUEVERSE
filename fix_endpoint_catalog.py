#!/usr/bin/env python3
"""Fix the broken endpoint-catalog.json by rewriting it cleanly."""
import json

with open('docs/api/endpoint-catalog.json', 'r', encoding='utf-8') as f:
    content = f.read()

# Parse what we can to understand structure
# The file has a broken publicApiEndpoints array followed by orphaned endpoint objects

# Find where publicApiEndpoints array starts and where it should end
lines = content.split('\n')

# Locate the start of publicApiEndpoints
start_idx = None
end_idx = None
for i, line in enumerate(lines):
    if '"publicApiEndpoints":' in line:
        start_idx = i
    if start_idx is not None and '"testOnlyEndpoints":' in line:
        end_idx = i
        break

if start_idx is None or end_idx is None:
    print(f"Could not find boundaries: start={start_idx}, end={end_idx}")
    exit(1)

print(f"Found publicApiEndpoints at line {start_idx}, testOnlyEndpoints at line {end_idx}")

# Extract the orphaned endpoint objects between the broken array end and testOnlyEndpoints
# They start after the first "}]," following publicApiEndpoints opening
orphaned = []
in_orphan = False
brace_count = 0
for i in range(start_idx, end_idx):
    line = lines[i]
    if i == start_idx:
        # First line has the array opening
        continue
    if not in_orphan:
        # Look for the end of the broken array "}],"
        if '}],' in line and brace_count == 0:
            in_orphan = True
            continue
        continue
    
    # Collect orphaned endpoint objects
    orphaned.append(line)

print(f"Found {len(orphaned)} lines of orphaned content")

# Build the correct publicApiEndpoints from orphaned content
# First, reconstruct valid JSON from the orphaned lines
orphan_text = '\n'.join(orphaned)
print("Orphaned text preview:")
print(orphan_text[:500])
