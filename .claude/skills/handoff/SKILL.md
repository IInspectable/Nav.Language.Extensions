---
name: handoff
description: Verwenden, wenn die Vorarbeit (Diskussion, Recherche, Entscheidungen) fertig ist und der nächste Schritt in einer FRISCHEN Session beginnen soll — Trigger 'Handoff', 'für neue Session vorbereiten', 'Kontext ist voll', 'nächsten Schritt in neuer Session starten'. Destilliert die Session zu einem Status-Dokument in doc/ plus einem Kickoff-Prompt, sodass die neue Session ohne Wiederholung der Vorarbeit sofort umsetzen kann.
---

Die Vorarbeit ist teuer, der Kontext ist es auch. Dieser Skill überführt das Ergebnis einer
Diskussions-/Recherche-Session in **zwei Artefakte**, mit denen eine Session ohne jede Vorgeschichte
sofort am nächsten Schritt weiterarbeitet:

1. **Status-Dokument** `doc/<thema>-status.md` — die dauerhafte Wahrheit (Entscheidungen, Fakten, Fallen, Plan).
2. **Kickoff-Prompt** — ein kurzer Text als Chat-Ausgabe, den der Nutzer in die neue Session einfügt.

Der Handoff ist gelungen, wenn die neue Session **keine Frage mehr stellen muss**, um Schritt 1 zu beginnen.

## Ablauf

### 1. Zielschnitt klären (eine Rückfrage, wenn nötig)

Was genau soll die neue Session tun — nur den **nächsten Schritt** oder den **ganzen Plan**? Der Handoff
beschreibt immer den ganzen Plan, aber der Kickoff-Prompt adressiert genau einen Schritt. Ist der Schnitt aus
der Session eindeutig, nicht fragen.

### 2. Bestandsaufnahme statt Gedächtnis

**Nichts aus dem Kopf schreiben, was der Code beantwortet.** Vor dem Schreiben verifizieren:

- Repo-Zustand: `git status --short`, `git log --oneline -3`, aktueller Branch.
- Existiert schon ein `doc/*-status.md` zum Thema? Dann **das fortschreiben**, kein zweites anlegen.
- Existiert ein Memory-Eintrag zum Thema (`MEMORY.md`)? Er zeigt, was frühere Sessions als teuer erkannt haben.
- Jede Datei-/Zeilenangabe, die ins Dokument soll, einmal nachschlagen. Eine falsche Zeilenangabe kostet die
  neue Session mehr, als sie spart.

### 3. Status-Dokument schreiben

`doc/<thema>-status.md`, Deutsch, echte Umlaute, UTF-8 mit BOM (Hook erledigt das), **in die `.slnx` unter
`/doc/` einhängen** (CLAUDE.md-Regel). Struktur:

- **Ziel** — was am Ende gilt, in 2–3 Sätzen. Nicht der Weg, das Ergebnis.
- **Entscheidungen (mit Begründung)** — der eigentliche Wert der Vorarbeit. Je Entscheidung: was gilt, **warum**,
  und **was verworfen wurde**. Ohne das „Warum" wird die neue Session die Alternative erneut vorschlagen und die
  Diskussion läuft ein zweites Mal.
- **Verifizierte Fakten** — Befunde aus der Codebase mit `Pfad:Zeile`: wo der Einstiegspunkt sitzt, welche Zahlen
  gemessen wurden, was bereits als tot/unbenutzt bewiesen ist. Trennen von Vermutungen (**als Vermutung markieren**).
- **Fallen** — was in dieser Session schon einmal fehlgeschlagen ist (Build-Eigenheiten, Multi-Target, Kodierung,
  Reihenfolge). Eine Falle, die hier fehlt, tritt in der neuen Session erneut zu.
- **Plan** — nummerierte Steps, je Step ein Ergebnis. Pro Step: berührte Dateien, Definition of Done.
- **Stand** — welcher Step ist fertig, committet/uncommittet; welcher ist der **nächste**.
- **Verifikation** — die konkreten Kommandos für dieses Thema (z.B. `nav build`, `nav test`, `dotnet test … -f net10.0
  --filter "…"`), nicht „Tests laufen lassen".

**Was NICHT hineingehört:** Gesprächsverlauf, verworfene Zwischenstände ohne Lehre, Wiederholung von CLAUDE.md,
Plan-Step-Nummern in Quellcode-Doku (CLAUDE.md-Regel — die Nummern leben nur hier). Steht etwas schon in CLAUDE.md
oder im Code, verlinken statt kopieren.

### 4. Kickoff-Prompt liefern

Als Chat-Ausgabe (nicht als Datei), kurz und selbsttragend:

```
Arbeite Step <N> aus doc/<thema>-status.md ab.
Lies zuerst das Dokument komplett — es enthält die Entscheidungen samt Begründung und die bereits
verifizierten Fakten; nichts davon erneut aufrollen oder neu diskutieren.
Ziel dieses Steps: <ein Satz>. Fertig ist er, wenn: <Definition of Done>.
Danach: Review + <Verifikations-Kommandos>, dann Commit-Message liefern (nicht committen).
```

Die neue Session beginnt damit **eine Datei-Lektüre** entfernt vom Arbeiten — nicht eine Diskussion.

### 5. Memory-Eintrag, wenn die Arbeit über die Session hinausläuft

Zieht sich das Thema über mehrere Sessions (mehrere Steps, offene Commits), gehört ein `project`-Memory dazu, das
auf `doc/<thema>-status.md` als Quelle der Wahrheit zeigt — mit Stand und dem nächsten Schritt, nicht mit einer
Zusammenfassung des Dokuments. Existiert der Eintrag schon: aktualisieren, nicht duplizieren.

## Selbstprüfung vor der Übergabe

Das Dokument mit den Augen einer Session lesen, die **nichts** weiß:

- Kann Step 1 begonnen werden, ohne eine Datei zu öffnen, die nicht genannt ist?
- Ist zu jeder Entscheidung das „Warum" da — oder nur das „Was"? (Nur „Was" ⇒ die Diskussion läuft erneut.)
- Steht irgendwo ein Pronomen/Verweis („wie oben besprochen", „das Problem von vorhin"), der nur aus dem
  Gesprächsverlauf auflösbar ist? Auflösen.
- Sind Vermutungen als solche markiert, damit die neue Session sie nicht als Fakt behandelt?
- Sind uncommittete Änderungen benannt? Die neue Session sieht einen Arbeitsbaum, den sie nicht erklärt bekommt.

## Abschluss

Handoff-Dokument + `.slnx`-Eintrag sind eine Änderung wie jede andere: **nicht selbst committen**, sondern eine
fertige Commit-Message liefern (CLAUDE.md-Regel). Zum Schluss im Chat nur den Kickoff-Prompt und den Dateipfad
nennen — der Rest steht im Dokument.
