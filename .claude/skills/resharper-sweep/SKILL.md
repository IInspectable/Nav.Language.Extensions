---
name: resharper-sweep
description: Verwenden, um ReSharper-Inspektionen (v.a. redundante Nullable-Guards ?./??/! und die Danger-Familie) im Nav-Repo systematisch abzuarbeiten — Trigger 'ReSharper aufräumen', 'jb inspectcode', 'redundante Guards', 'nullable-Warnungen wegräumen'. Führt den jb-inspectcode-Lauf, die ehrliche Auflösungs-Reihenfolge (annotieren vor suppress) und die Keep/Remove/Fix-Taxonomie.
---

ReSharper-Inspektionen im Nav-Repo systematisch abarbeiten. Diese Inspektionen sind **rein ReSharper** —
`nav nullaudit` / `WarningsAsErrors=Nullable` fängt sie **nicht** (Roslyn fragt „darf ich dereferenzieren?",
ReSharper „ist dieser Guard laut Annotation überflüssig?"). Orthogonal zur Nullable-Kampagne.

## Werkzeug + Lauf

`jb inspectcode` (dotnet global tool `JetBrains.ReSharper.GlobalTools`, i.d.R. installiert):

```
jb inspectcode Nav.Language.Extensions.slnx --output=<f>.xml --severity=SUGGESTION --no-build
```

Trotz `.xml`-Endung ist die Ausgabe **SARIF-JSON** — mit `ConvertFrom-Json` einlesen und per `ruleId` filtern.
VS zeigt oft die **doppelte** Trefferzahl, weil `Nav.Language.Tests` multi-target ist (net472 + net10 je einmal).

## Relevante ruleIds

**Aufräum-Familie** (redundant laut Annotation):
- `ConditionalAccessQualifierIsNonNullableAccordingToAPIContract` — redundantes `?.`
- `NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract` — redundantes `??`
- `ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract` — toter `== null`
- `RedundantSuppressNullableWarningExpression` — redundantes `!`

**Danger-Familie** (Fehl-Annotation → potentielle NRE): `AssignNullToNotNullAttribute`, `PossibleNullReferenceException`.

## Auflösungs-Reihenfolge — Suppress ist die LETZTE Wahl

Ein „…AccordingToAPIContract"-Treffer ist ein **Verifikations-Anlass**, nie blind kürzen: sonst wird eine
Fehl-Annotation zur Laufzeit-NRE. In dieser Reihenfolge auflösen:

1. **Eigener Parameter kann null sein → Signatur auf `T?` annotieren.** Der Guard wird ehrlich, die Warnung
   verschwindet ohne Kommentar. (Achtung: `params object[]` **kann** `null` sein → `params object[]?`.)
2. **Eigene non-null-Property, Guard tot → Guard entfernen.** (z.B. `symbol.Name ?? ""` → nur `symbol.Name`,
   wenn `Name` non-null ist.)
3. **Zu-strenge eigene Signatur an die tatsächliche Speicherung angleichen.** (z.B. Ctor `Location(string)`,
   das in ein `string?`-Feld schreibt → `Location(string?)`; verhaltensneutral.)
4. **Echte Null-Lücke (Danger-Familie) → Guard einziehen, NICHT suppress.** Ein `AssignNull`/`PossibleNRE`
   kann eine echte Lücke maskieren — erst die Quelle klären, dann `if (x == null) return;` statt Unterdrückung.
5. **Suppress nur, wenn die Annotation NICHT in eigener Hand liegt:** fremde DTOs aus JSON-Deserialisierung
   (LSP `param.*` aus dem VS-Protocol-Paket), Roslyn-Struct-`default` (`GeneratedSourceResult.SourceText`),
   MCP-Tool-Params (nullable würde ins JSON-**Schema** durchschlagen: required→optional), bewusste
   Test-Assertions, unpraktikable Generics mit Struct-Implementierern (`TElement?` semantisch schief).

## Keep/Remove-Taxonomie (Kurzform für die Aufräum-Familie)

- **REMOVE**, wenn non-null strukturell/compiler-erzwungen ist: ctor-`throw`, Parser konstruiert immer non-null,
  non-null-Property, im selben Scope schon ohne `?.` dereferenziert. In nullable-grünem Code (Engine/LSP/MCP mit
  WAE) ist ein Treffer beweisbar tot.
- **KEEP** an Rändern, wo die Annotation optimistisch ist: JSON-Deserialisierungs-DTOs, `params object[]`,
  public-API-„Try"-Kontrakte, Extension-`this`, DTO-Rand-`?? ""`, `default(struct)`-Fall. Bewusste, geprüfte
  Keeps dokumentieren (siehe Suppress-Mechanik).

## Multi-Target-Falle (WICHTIG)

ReSharper inspiziert nur **ein** TFM. Ein „redundant"-Treffer in einem multi-target-Projekt (`Nav.Language.Tests`
= net472 + net10) kann unter net10 redundant, unter net472 **Pflicht** sein. Klassiker: `Regex.Matches(...).Cast<Match>()`
— `MatchCollection` implementiert unter net472 nur nicht-generisches `IEnumerable`, dort ist der Cast Pflicht →
**False Positive, KEEP.** Immer gegen alle TFMs prüfen; `nav build` baut net472 mit.

## Suppress-Mechanik

- **Kategorial** (Projekt-/Ordner-Muster, analog IDE0130): `resharper_<snake_case_id>_highlighting = none` in
  `.editorconfig`. Orthogonal zu Roslyn `dotnet_diagnostic.*`. Beispiele: `unused_auto_property_accessor_global`
  (DTO-/CodeModel-/Polyfill-Props, die nur via Serialisierung/StringTemplate/Compiler genutzt werden — für die
  „Global"-Heuristik unsichtbar), Test-Ordner `possible_null_reference_exception`/`assign_null_to_not_null_attribute`
  (inhärenter Test-Stil), `xaml_binding_with_context_not_resolved` (Designtime-DataContext).
- **Inline gezielt** (mit Kurzbegründung eine Zeile darüber): `// ReSharper disable once <PascalCaseId>` bzw.
  Block `// ReSharper disable <Id>` … `// ReSharper restore <Id>`. **IDs sind PascalCase** (nicht snake_case) —
  das ist der häufigste Fehler.

## Abschluss

Nach jedem Sweep: `nav build` (0/0, inkl. VS-Ext net472) + Tests auf **beiden** TFMs (`nav test` + net10). Endziel
ist eine Restliste aus ausschließlich dokumentierten, geprüften Keeps/FPs — keine blind gekürzten Treffer.
Historische Sweep-Ergebnisse/Zahlen gehören ins Memory bzw. `doc/`, nicht in diesen Skill.
