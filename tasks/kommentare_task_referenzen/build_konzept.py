import json
import os
import re

ROOT = r"c:\Daten\Entwicklung\Ralf\AiNetLinter"

# Patterns
RE_TASK_REF = re.compile(r'\b(EPIC-\d+|step-\d+|TD-\d+|unit\s*\d+|[MQS]\d+(\.\d+)?|Issue\s*#?\d+|PR\s*#?\d+)\b', re.IGNORECASE)
RE_TASK_FILE = re.compile(r'\b(tasks/features/[^\s\)]+|tasks/[^\s\)]+)\b', re.IGNORECASE)
RE_HISTORY = re.compile(r'\b(war früher|früher war|vormals|ehemalig|ursprünglich|altes|alte Version|bisher|ausgelagert aus|ausgelagert in|verschoben aus|verschoben von|ehemalige|vorher inline|neu hinzugefügt|geändert am|geändert in)\b', re.IGNORECASE)

violations = []

def inspect_cs_file(filepath):
    rel = os.path.relpath(filepath, ROOT)
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()
        
    for idx, line in enumerate(lines, 1):
        stripped = line.strip()
        if '//' in line or '/*' in line:
            comment_text = ""
            if '//' in line:
                parts = line.split('//')
                if parts[0].count('"') % 2 == 0:
                    comment_text = '//' + '//'.join(parts[1:])
                else:
                    continue
            else:
                comment_text = line
                
            # 1. Task File references (e.g. tasks/features/07-drift-audit-ideen.md)
            for m in RE_TASK_FILE.finditer(comment_text):
                match_str = m.group(0)
                violations.append({
                    'scope': 'Code (C#)',
                    'file': rel,
                    'line': idx,
                    'rule': 'Task-Datei-Referenz im Code (§5)',
                    'match': match_str,
                    'current_line': stripped,
                    'reason': f"Referenz auf Planungsdatei '{match_str}' verstößt gegen §5 (keine Verweise auf temporäre Task-Artefakte im Code).",
                    'category': 'Core' if rel.startswith('src\\AiNetLinter\\') else 'Test'
                })
                
            # 2. Task / Milestone IDs
            for m in RE_TASK_REF.finditer(comment_text):
                match_str = m.group(0)
                violations.append({
                    'scope': 'Code (C#)',
                    'file': rel,
                    'line': idx,
                    'rule': 'Task- / Milestone-Referenz im Code (§5)',
                    'match': match_str,
                    'current_line': stripped,
                    'reason': f"Referenz auf '{match_str}' im Code-Kommentar verstößt gegen §5 (keine Task-/Milestone-IDs in Code-Kommentaren).",
                    'category': 'Core' if rel.startswith('src\\AiNetLinter\\') else 'Test'
                })
                
            # 3. History
            for m in RE_HISTORY.finditer(comment_text):
                match_str = m.group(0)
                violations.append({
                    'scope': 'Code (C#)',
                    'file': rel,
                    'line': idx,
                    'rule': 'Refactoring-Historie / Vergangenheitsbeschreibung (§1 & §5)',
                    'match': match_str,
                    'current_line': stripped,
                    'reason': f"Historie/Vergangenheitsbeschreibung ('{match_str}') verstößt gegen §1 & §5 ('Der Code hat keine Vergangenheit').",
                    'category': 'Core' if rel.startswith('src\\AiNetLinter\\') else 'Test'
                })

def inspect_md_file(filepath):
    rel = os.path.relpath(filepath, ROOT)
    if rel.startswith('tasks\\') or rel == r'Docs\ROADMAP.md':
        return
        
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()
        
    for idx, line in enumerate(lines, 1):
        stripped = line.strip()
        if 'AiNetLinterRichtlinien.mdc' in rel and ('Verboten:' in line or 'Beispiel:' in line):
            continue
            
        for m in RE_TASK_REF.finditer(line):
            match_str = m.group(0)
            violations.append({
                'scope': 'Aktive Doku (.md / .mdc)',
                'file': rel,
                'line': idx,
                'rule': 'Task- / Milestone-Referenz in aktiver Doku (§1 & §5)',
                'match': match_str,
                'current_line': stripped,
                'reason': f"Referenz auf '{match_str}' in aktiver Doku verstößt gegen §1 & §5.",
                'category': 'Doc'
            })
            
        for m in RE_HISTORY.finditer(line):
            match_str = m.group(0)
            violations.append({
                'scope': 'Aktive Doku (.md / .mdc)',
                'file': rel,
                'line': idx,
                'rule': 'Vergangenheitsbeschreibung in Doku (§1)',
                'match': match_str,
                'current_line': stripped,
                'reason': f"Historie/Vergangenheitsbeschreibung ('{match_str}') in Doku verstößt gegen §1.",
                'category': 'Doc'
            })

# Traverse codebase
for root, dirs, files in os.walk(os.path.join(ROOT, 'src')):
    for file in files:
        if file.endswith('.cs'):
            inspect_cs_file(os.path.join(root, file))

for path in [os.path.join(ROOT, 'Docs'), os.path.join(ROOT, '.agents'), os.path.join(ROOT, 'README.md')]:
    if os.path.isfile(path):
        inspect_md_file(path)
    else:
        for root, dirs, files in os.walk(path):
            for file in files:
                if file.endswith('.md') or file.endswith('.mdc'):
                    inspect_md_file(os.path.join(root, file))

# De-duplicate identical line findings
unique_violations = []
seen = set()
for v in violations:
    key = (v['file'], v['line'], v['match'], v['rule'])
    if key not in seen:
        seen.add(key)
        unique_violations.append(v)

core_code = [v for v in unique_violations if v['category'] == 'Core']
test_code = [v for v in unique_violations if v['category'] == 'Test']
docs = [v for v in unique_violations if v['category'] == 'Doc']

def generate_clean_soll(line):
    res = line
    # Strip Task/Roadmap file references
    res = re.sub(r'\(<c>tasks/features/[^<]+</c>\s*(§\d+)?\s*(Z\.\s*\d+)?\)', '', res)
    res = re.sub(r',?\s*<c>tasks/features/[^<]+</c>\s*(§\d+)?\s*(Z\.\s*\d+)?', '', res)
    res = re.sub(r'\s*\(Q\d+\s*—\s*<c>tasks/features/[^<]+</c>\s*§\d+\)', '', res)
    res = re.sub(r'\s*\(Q\d+,\s*<c>tasks/features/[^<]+</c>\s*§\d+\)', '', res)
    res = re.sub(r'\s*\(siehe\s+<c>tasks/features/[^<]+</c>[^)]*\)', '', res)
    res = re.sub(r'\s*\(siehe\s+tasks/features/[^\s\)]+[^\)]*\)', '', res)
    res = re.sub(r'tasks/features/[^\s\)]+', '', res)
    
    # Strip Milestone / Epic tokens inside parentheses or brackets
    res = re.sub(r'\s*\(\s*(EPIC-\d+|step-\d+|TD-\d+|unit\s*\d+|[MQS]\d+(\.\d+)?)\s*\)', '', res)
    res = re.sub(r'\s*\(\s*(EPIC-\d+|step-\d+|TD-\d+|unit\s*\d+|[MQS]\d+(\.\d+)?)\s*,\s*', ' (', res)
    res = re.sub(r'\s*,\s*(EPIC-\d+|step-\d+|TD-\d+|unit\s*\d+|[MQS]\d+(\.\d+)?)\s*\)', ')', res)
    res = re.sub(r'\s*\(\s*S1\.3\s*,\s*', ' (', res)
    res = re.sub(r'\b(EPIC-\d+|step-\d+|TD-\d+|unit\s*\d+)\b-?', '', res)
    res = re.sub(r'\bM\d+-Regressionslehre,?\s*', '', res)
    res = re.sub(r'\bM\d+-Lehre,?\s*', '', res)
    res = re.sub(r'\bQ\d+-Muster,?\s*', '', res)
    res = re.sub(r'\bQ\d+-Muster\b', '', res)
    res = re.sub(r'\bS2\.2-Einfuehrung\b', 'Einfuehrung', res)
    res = re.sub(r'\bin S2\.2\b', '', res)
    res = re.sub(r'\bS2\.2\b', '', res)
    res = re.sub(r'\bS2\.5-Akzeptanzkriterien\b', 'Akzeptanzkriterien', res)
    res = re.sub(r'\bS1\.3-Praezedenzfall\b', 'Praezedenzfall', res)
    res = re.sub(r'\bS1\.3\b', '', res)
    res = re.sub(r'\bQ\d+\b', '', res)
    res = re.sub(r'\bM\d+\b', '', res)
    res = re.sub(r'\bS1\.4/S2\.2/M2/M9-Teil-A\b', 'Teil-A', res)
    res = re.sub(r'Ausgelagert aus <see cref="[^"]+"/> — ', '', res)
    res = re.sub(r'Ausgelagert in eigene Datei, damit ', 'Datei aufgeteilt, damit ', res)
    
    # Specific German phrase fixes
    res = res.replace('Live-Dogfood-Befund 2026-08-11, ).', 'Live-Dogfood-Befund 2026-08-11).')
    res = res.replace('Live-Dogfood-Befund 2026-08-11, M9).', 'Live-Dogfood-Befund 2026-08-11).')
    res = res.replace(' (EPIC-06)', '').replace(' (EPIC-08)', '')
    res = res.replace('vormals ', '').replace('bisher ', '')
    res = res.replace(' ( , ', ' (').replace(' ( ', ' (').replace(' (, ', ' (').replace(' ( ,', ' (')
    res = res.replace(' ()', '').replace(' (,', '').replace(' , )', ')')
    res = res.replace(' , ', ', ')
    res = re.sub(r'\s{2,}', ' ', res)
    return res.strip()

md = []
md.append("# Vollumfängliches Konzept & Analyse: Bereinigung von Kommentaren, Task-Referenzen und Refactoring-Historie")
md.append("")
md.append("> [!IMPORTANT]")
md.append("> **HINWEIS FÜR NACHFOLGENDE AGENTEN (INSTRUCTION & CONTEXT):**")
md.append("> Dieses Dokument ist das zentrale Umsetzungs-Konzept zur Eliminierung von Verstößen gegen `.agents/rules/AiNetLinterRichtlinien.mdc` (§1 und §5).")
md.append("> **Auftrag für die spätere Durchführung:**")
md.append("> 1. Arbeite die in den folgenden Tabellen aufgeführten Vorher/Nachher-Bereinigungen schrittweise ab.")
md.append("> 2. Ändere **ausschließlich** die Kommentare und Dokumentations-Texte — keine funktionale Logik im C#-Code anpassen.")
md.append("> 3. Führe nach der Bereinigung die Verifikation via `dotnet test --filter Category!=Stress` durch.")
md.append("> 4. Synchronisiere am Ende bei Bedarf die Agenten-Regeln via `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.")
md.append("")
md.append("---")
md.append("")
md.append("## 1. Ausgangslage & Regelbasis")
md.append("")
md.append("In `AiNetLinterRichtlinien.mdc` gelten folgende strikte Regeln für Kommentare und Dokumentation:")
md.append("")
md.append("1. **Sparsamer Kommentar-Einsatz & Intentions-Fokus (§5):**")
md.append("   - Kommentare dürfen keine Bezeichner/Signaturen wiederholen.")
md.append("   - Nur die fachliche Intention (*Why*) soll beschrieben werden, wenn der Code nicht selbst-erklärend ist.")
md.append("2. **Verbot von Task-/Planungs-Referenzen (§5):**")
md.append("   - Keine Referenzen im Code oder der aktiven Doku auf `step-008`, `TD-005`, `EPIC-06`, `unit 009`, `M1-M16`, `Q1-Q5`, `S1.1-S2.5`, `tasks/features/*.md` sowie Ticket- oder Issue-IDs.")
md.append("   - **Grund:** Task-Ordner und temporäre Planungsdokumente werden nach Task-Abschluss gelöscht. Referenzen darauf werden bedeutungslos.")
md.append("3. **Verbot von Refactoring-Historie & Vergangenheitsbeschreibung (§1 & §5):**")
md.append("   - Keinesfalls beschreiben, was früher war (z. B. `war früher private`, `vormals inline`, `bisher`, `ausgelagert aus`, `ursprünglich 10 Patterns`).")
md.append("   - Der Code und die Dokumentation beschreiben ausschließlich den **aktuellen IST-Zustand** („Der Code hat keine Vergangenheit“).")
md.append("")
md.append("---")
md.append("")
md.append("## 2. Zusammenfassung der Analyseergebnisse")
md.append("")
md.append(f"Insgesamt wurden **{len(unique_violations)} Einzelstellen in {len(set(v['file'] for v in unique_violations))} Dateien** identifiziert, die gegen diese Richtlinien verstoßen:")
md.append("")
md.append(f"- **AiNetLinter Core Engine (`src/AiNetLinter/`):** {len(core_code)} Fundstellen in {len(set(v['file'] for v in core_code))} Dateien (inklusive direkter Referenzen auf `tasks/features/07-drift-audit-ideen.md` & `tasks/features/05-roadmap.md` in `DuplicateDetectionEngine.cs` & `GlobalConfig.cs`).")
md.append(f"- **Unit & Integration Tests (`src/AiNetLinter.Tests/`):** {len(test_code)} Fundstellen in {len(set(v['file'] for v in test_code))} Dateien (Milestone- und Epic-Erwähnungen sowie Historie in Test-Kommentaren).")
md.append(f"- **Aktive Dokumentation & Rules (`Docs/agent-api.md` & `.agents/`):** {len(docs)} Fundstellen (Epic-Referenzen & Historienwörter wie `ursprünglich` / `bisher`).")
md.append("")
md.append("---")
md.append("")
md.append("## 3. Detaillierter Maßnahmenkatalog (Core Engine: `src/AiNetLinter/`)")
md.append("")
md.append("Tabelle aller Fundstellen in der Core-Engine mit konkretem Bereinigungsvorschlag:")
md.append("")
md.append("| Datei & Zeile | Vorhandener Kommentar (Ist) | Regelverstoß & Grund | Vorschlag für Bereinigung (Soll) |")
md.append("|:---|:---|:---|:---|")

def escape_pipe(text):
    return text.replace('|', '\\|').replace('\n', ' ')

for v in core_code:
    f_line = f"`{v['file']}:{v['line']}`"
    curr = f"`{escape_pipe(v['current_line'])}`"
    reason = v['reason']
    soll_text = generate_clean_soll(v['current_line'])
    soll = f"`{escape_pipe(soll_text)}`"
    md.append(f"| {f_line} | {curr} | {reason} | {soll} |")

md.append("")
md.append("---")
md.append("")
md.append("## 4. Detaillierter Maßnahmenkatalog (Testsuite: `src/AiNetLinter.Tests/`)")
md.append("")
md.append("| Datei & Zeile | Vorhandener Kommentar (Ist) | Regelverstoß & Grund | Vorschlag für Bereinigung (Soll) |")
md.append("|:---|:---|:---|:---|")

for v in test_code:
    f_line = f"`{v['file']}:{v['line']}`"
    curr = f"`{escape_pipe(v['current_line'])}`"
    reason = v['reason']
    soll_text = generate_clean_soll(v['current_line'])
    soll = f"`{escape_pipe(soll_text)}`"
    md.append(f"| {f_line} | {curr} | {reason} | {soll} |")

md.append("")
md.append("---")
md.append("")
md.append("## 5. Detaillierter Maßnahmenkatalog (Aktive Dokumentation & Rules)")
md.append("")
md.append("| Datei & Zeile | Vorhandener Text (Ist) | Regelverstoß & Grund | Vorschlag für Bereinigung (Soll) |")
md.append("|:---|:---|:---|:---|")

for v in docs:
    f_line = f"`{v['file']}:{v['line']}`"
    curr = f"`{escape_pipe(v['current_line'])}`"
    reason = v['reason']
    soll_text = generate_clean_soll(v['current_line'])
    soll = f"`{escape_pipe(soll_text)}`"
    md.append(f"| {f_line} | {curr} | {reason} | {soll} |")

md.append("")
md.append("---")
md.append("")
md.append("## 6. Explizite Abgrenzung & Gültigkeitsbereich (Scope-Klarstellung)")
md.append("")
md.append("Folgende Artefakte sind **berechtigterweise von der Bereinigung ausgenommen**:")
md.append("1. **`tasks/` Verzeichnis:** Dieses Verzeichnis dient als lokaler Arbeits- und Planungsbereich für laufende/vergangene Tasks. Nach Fertigstellung des jeweiligen Epics werden diese temporären Artefakte gelöscht.")
md.append("2. **`Docs/ROADMAP.md`:** Das Roadmap-Dokument ist die historische Chronik des Projekts. Hier sind Meilenstein-Nummern (z. B. `M01`, `EPIC-01`) historisch gewollt und zulässig.")
md.append("3. **Linter-Suppression-Strings in Testdaten:** Zeichenketten in Unit-Tests wie `\"// ainetlinter-disable ...\"` sind Testdaten zur Verifizierung der Linter-Engine und keine Projektkommentare.")
md.append("")
md.append("---")
md.append("")
md.append("## 7. Umsetzungs- & Verifikationsplan")
md.append("")
md.append("Wenn die Anpassungen durch den nächsten Agenten ausgeführt werden, ist wie folgt vorzugehen:")
md.append("1. **Dateiweise Bereinigung:** Reines Editieren von Kommentaren/Doku gemäß Tabellen oben.")
md.append("2. **Kompilierung prüfen:** `dotnet build` (sicherstellen, dass keine Syntax-Fehler entstanden sind).")
md.append("3. **Testsuite ausführen:** `dotnet test --filter Category!=Stress` (vollständiger Testlauf muss grün bleiben).")
md.append("4. **Agenten-Regeln synchronisieren:** `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.")
md.append("5. **Commit:** `refactor: Task-Referenzen, Historie und redundante Kommentare im Code und der Doku bereinigt`.")

with open(r'tasks\kommentare_task_referenzen\Konzept.md', 'w', encoding='utf-8') as f:
    f.write('\n'.join(md))

print(f"Total unique violations in full audit: {len(unique_violations)}")
print(f"Generated updated Konzept.md ({len(md)} lines)")
