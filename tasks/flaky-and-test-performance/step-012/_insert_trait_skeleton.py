# Python-Helper analog step-007 fuer SkeletonStableIdTests.cs
# Datei ist LF-only (CR=0, LF=42). Byte-genaues Einfuegen einer
# '\n'-Trait-Zeile OHNE EOL-Aenderung. Standard-Edit wuerde CRLF draus machen.

import os

path = r"C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Maps\Skeleton\SkeletonStableIdTests.cs"

with open(path, "rb") as f:
    data = f.read()

# Pre-state Verifikation
pre_cr = data.count(b"\r")
pre_lf = data.count(b"\n")
pre_bom = data[:3] == b"\xef\xbb\xbf"
pre_first3 = data[:3].hex(" ")
print(f"PRE  : bytes={len(data):5}  CR={pre_cr}  LF={pre_lf}  BOM={pre_bom}  first3={pre_first3}")

# Ziel-Substring: in der Datei steht
#   "...namespace AiNetLinter.Tests.Maps.Skeleton;\n\npublic sealed class SkeletonStableIdTests..."
# Wir fuegen vor 'public sealed class SkeletonStableIdTests' die Trait-Zeile ein,
# mit \n-Trennzeichen, weil Datei LF-only ist.
target = b"public sealed class SkeletonStableIdTests"
idx = data.index(target)

trait_line = b'[Trait("Category", "Unit")]\n'
new_data = data[:idx] + trait_line + data[idx:]

with open(path, "wb") as f:
    f.write(new_data)

# Post-state Verifikation
with open(path, "rb") as f:
    data2 = f.read()
post_cr = data2.count(b"\r")
post_lf = data2.count(b"\n")
post_bom = data2[:3] == b"\xef\xbb\xbf"
post_first3 = data2[:3].hex(" ")
print(f"POST : bytes={len(data2):5}  CR={post_cr}  LF={post_lf}  BOM={post_bom}  first3={post_first3}")
print(f"DIFF : CR={post_cr - pre_cr}  LF={post_lf - pre_lf}  bytes={len(data2) - len(data)}")
print(f"OK   : CR=0 (LF-only erhalten), LF=+1, bytes=+27 (29 chars ohne leading newline - wir fuegen '...\n' = 28 bytes; re-check)")

# Verify: the inserted trait line is present and EOL preserved
assert post_cr == 0, f"EOL changed: CR={post_cr}, must be 0"
assert post_lf == pre_lf + 1, f"LF delta wrong: {post_lf} vs expected {pre_lf + 1}"
assert b'[Trait("Category", "Unit")]\npublic sealed class SkeletonStableIdTests' in data2
print("VERIFY: trait line inserted, EOL preserved (LF-only) -> OK")
