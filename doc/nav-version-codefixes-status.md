# Nav — Code-Fixes für die `#version`-Direktive (Referenz)

> Die drei `#version`-Diagnosen (`Nav3002`, `Nav3003`, `Nav5001`) haben automatische Quick-Fixes
> (VS-Lightbulb + LSP `codeAction`). **Feature komplett (Steps 1–5).** Dieses Dokument bleibt als
> Referenz erhalten: die Architektur und die Fallstricke (Direktiven als strukturierte Trivia,
> Diagnostics-Verortung, `.shproj`-`projitems`) sind Vorlage für künftige Direktiven-Arbeit
> (`#region`/`#if`, QuickInfo auf Direktiven). Die Step-Struktur unten spiegelt den umgesetzten Weg.

## Stand

- [x] **Step 1 — Lexer-LF-Fallstrick** (Voraussetzung, verhaltensneutral für CRLF). `ScanPreprocessor`
  terminiert bei jedem Zeilenende. Detail: `doc/nav-pragmas-versioning-status.md`.
- [x] **Step 2 — `Nav5001`-Fix** „nicht unterstützte Version auf `Latest` setzen".
  `Nav.Language/CodeFixes/ErrorFix/SetSupportedLanguageVersionCodeFix(+Provider).cs`.
- [x] **Step 3 — `Nav3002`-Fix** „fehlenden/ungültigen Wert setzen".
  `Nav.Language/CodeFixes/ErrorFix/SetValidLanguageVersionCodeFix(+Provider).cs`.
- [x] **Step 4 — `Nav3003`-Fix** „deplatzierte Direktive an den Dateikopf verschieben" (bzw. entfernen,
  wenn oben schon eine wirksame steht).
  `Nav.Language/CodeFixes/ErrorFix/MoveVersionDirectiveToTopCodeFix(+Provider).cs`. Trigger: nicht-wirksame
  `VersionDirectiveSyntax` aus `SyntaxTree.Directives()`, präzise über die tatsächlich gemeldete `Nav3003`
  (Location == Direktiv-Extent). **Wichtig:** die Platzierungs-Diagnosen (`Nav3003`) liegen in
  `SyntaxTree.Diagnostics`, **nicht** in den semantischen `CodeGenerationUnit.Diagnostics` — dort danach zu
  suchen war der erste Fehlschlag. Move = Zeile via `SourceText.GetTextLineAtPosition` komplett entfernen +
  Direktiv-Text (verbatim `Substring(Extent)`, ohne Newline) an Position 0 + `settings.NewLine` einfügen.
  Titel: „Move '#version' to top of file" (Fall 1) bzw. „Remove misplaced '#version' directive" (Fall 2,
  wirksame existiert). 5 neue Tests in `NavCodeActionServiceTests`, net10 (19/19) + net472 (1283/0) grün.
- [x] **Step 5 — VS-Lightbulb-Export** für alle drei neuen Fixes (SuggestedActionProvider). Je Fix ein
  `…SuggestedAction` (`CodeFixSuggestedAction<T>`) + `…SuggestedActionProvider`
  (`[ExportCodeFixSuggestedActionProvider]`) in `Nav.Language.ExtensionShared/CodeFixes/`, MEF-entdeckt via
  `[ImportMany]` (keine zentrale Liste). Zwei neue Icons in `ImageMonikers` (`SetLanguageVersion`,
  `MoveDirectiveToTop`). **Fallstrick `.shproj`:** jede neue `.cs` muss in `Nav.Language.ExtensionShared.projitems`
  eingetragen werden (kein Auto-Glob). Voller `nav build` (MSBuild.exe) grün.

Alle Engine-Fixes fließen automatisch durch den LSP (`NavCodeActionService` → `textDocument/codeAction`);
die VS-Extension zeigt sie über die je Fix exportierten SuggestedActionProvider. **Feature komplett.**

## Geteilte Architektur (gilt für alle drei Fixes)

**Pattern (Provider + Fix-Paar), Vorlage z.B. der bestehende `AddMissingSemicolonsOnIncludeDirectives`:**
- `…CodeFix : ErrorCodeFix` (Kategorie `ErrorFix`, da alle drei `DiagnosticSeverity.Error`). Pflicht-
  Member: `Name`, `Impact`, `ApplicableTo`, `Prio` (+ `Category` aus `ErrorCodeFix`). Dazu eine
  eigene `public IList<TextChange> GetTextChanges()` und ein internes `CanApplyFix()`.
- `…CodeFixProvider` mit `static IEnumerable<…CodeFix> SuggestCodeFixes(CodeFixContext, CancellationToken)`.
- **Verdrahtung** in `Nav.Language/CodeActions/NavCodeActionService.cs`: je Provider eine `foreach`-Schleife,
  die `new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList())` sammelt.

**Direktiven sind strukturierte Trivia** — das ist der zentrale Fallstrick und der Grund, warum die
`#version`-Fixes anders funktionieren als die bestehenden 6 Fixes:
- Eine `VersionDirectiveSyntax`/`BadDirectiveTriviaSyntax` liegt **nicht** im signifikanten
  `SyntaxTree.Tokens`-Strom. `context.FindNodes<…>()`/`FindTokens()` finden sie daher **nicht**.
- Erreichbar sind Direktiven über `SyntaxTree.Directives()` (alle) bzw.
  `CodeGenerationUnitSyntax.LanguageVersionDirective` (nur die **wirksame** am Kopf, sonst `null`).
- **`ExpandCaret` in `NavCodeActionService` wurde in Step 2 erweitert:** ein Caret **in** einer Direktive
  dehnt sich jetzt auf deren Extent aus (statt via Owning-`FindToken` auf das signifikante Token, an dem
  die Direktive als Leading-Trivia hängt). Ohne das könnte kein direktiven-bezogener Provider greifen.
  Step 4 profitiert davon automatisch.
- Provider-Trigger-Muster (Step 2/3): `context.Range.IntersectsWith(directive.Extent)`.

**Nützliche Bausteine:**
- `VersionDirectiveSyntax.VersionKeyword` (das `version`-Token), `.VersionNumber` (das `PreprocessorNumber`-
  Wert-Token, `IsMissing` wenn fehlend/nicht-numerisch), `.Version` (die aufgelöste `NavLanguageVersion`).
- `NavLanguageVersion.Latest` / `.SupportedVersions` / `.TryParse(text, out v)` / `.IsSupported`.
- `TextChange.NewReplace(extent, text)` / `.NewInsert(pos, text)` / `.NewRemove(extent)`.
- `context.CodeGenerationUnit.Syntax` = `CodeGenerationUnitSyntax`; `.SyntaxTree.SourceText.Substring(extent)`.
- `SyntaxTree.SourceText.GetTextLineAtPosition(pos)` → `SourceTextLine` (`.Extent`,
  `.ExtentWithoutLineEndings`, `.Start`, `.End`).
- `CodeFix.GetRemoveSyntaxNodeChanges(node)` / `SyntaxTree.GetRemoveSyntaxNodeChanges(node, settings)` —
  **für signifikante Knoten** gebaut (die RemoveUnused-Fixes nutzen es). Für eine **Trivia-Direktive**
  vor Verwendung **verifizieren** (evtl. tut es das Falsche → dann Zeilen-Entfernung von Hand über die
  `SourceTextLine`-API bauen).

## Step 4 — `Nav3003`-Fix (Detailspezifikation)

**Bedeutung `Nav3003`:** „`#version` must appear at the top of the file, preceded only by comments or
whitespace." Erzeugt in `NavParser.ResolveLanguageVersion()` (`Nav.Language/Syntax/NavParser.cs`, ~Z. 360):
eine `#version`-Direktive ist **nicht wirksam**, wenn ihr echter Code (`codeBefore`) **oder** eine andere
Direktive (`sawAnyDirective`) vorausgeht. Wirksam ist **nur die erste** `#version` ganz oben (nur Trivia
davor); die landet in `CodeGenerationUnitSyntax.LanguageVersionDirective`.

**Entscheidender Unterschied zu Step 2/3:** Der Nav3003-Fix hängt an einer **NICHT-wirksamen** Direktive —
also **nicht** `LanguageVersionDirective`, sondern an einer der übrigen aus
`SyntaxTree.Directives().OfType<VersionDirectiveSyntax>()`. Der Provider muss die vom Bereich getroffene
Direktive suchen, **die nicht die wirksame ist** (bzw. äquivalent: die eine `Nav3003` trägt).

**Die zwei Fälle (Nuance „verschieben vs. entfernen"):**
1. **Keine wirksame Direktive vorhanden** (`LanguageVersionDirective == null`), z.B.
   `task A {…}\r\n#version 2` oder `#pragma …\r\n#version 2\r\ntask …`. → **Verschieben** an den Dateikopf
   macht die Direktive wirksam. Das ist der eigentliche „move"-Fix.
2. **Es gibt bereits eine wirksame Direktive** (`LanguageVersionDirective != null`), z.B.
   `#version 1\r\n…code…\r\n#version 3`. → Verschieben an den Kopf erzeugte ein **Duplikat** (`Nav3004`).
   Sinnvoll ist hier **Entfernen** der deplatzierten Direktive, nicht Verschieben. Empfehlung: in diesem
   Fall den Titel „Remove misplaced '#version' directive" anbieten (oder den Fix in Fall 2 gar nicht
   anbieten und das der RemoveUnused-Familie überlassen — Design-Entscheidung beim Bau treffen).

**Move-Mechanik (Fall 1):** zwei `TextChange`:
- **Entfernen** der Direktivzeile an ihrer jetzigen Stelle — inkl. des abschließenden Zeilenumbruchs, damit
  keine Leerzeile zurückbleibt. Über `SourceText.GetTextLineAtPosition(directive.Extent.Start)` die Zeile
  holen und ihre volle Ausdehnung (mit Zeilenende) entfernen. **Verifizieren**, ob `GetRemoveSyntaxNodeChanges`
  das für eine Trivia-Direktive korrekt tut; sonst von Hand.
- **Einfügen** von `#version <N> + settings.NewLine` an **Position 0** (Dateianfang). „Nur Trivia davor" ist
  erfüllt; ein evtl. vorhandener Kopf-Kommentar darf davor stehen bleiben (Trivia ist erlaubt) — Einfügen an
  Position 0 ist der einfachste korrekte Punkt. `<N>` = der bereits geparste Wert der Direktive
  (`directive.Version`), damit die vom Nutzer gemeinte Version erhalten bleibt (nicht `Latest`!).

**Edge-Cases zum Testen:**
- Fall 1 mit Code davor (`task …\r\n#version 2`) → nach Fix steht `#version 2` oben, Datei sonst unverändert,
  `LanguageVersion == 2`, kein `Nav3003` mehr.
- Fall 1 mit anderer Direktive davor (`#pragma …\r\n#version 2\r\ntask …`) → `#version 2` wandert **vor** das
  `#pragma`.
- Fall 2 (`#version 1\r\n…\r\n#version 3`) → gewählte Aktion (remove) entfernt die zweite; die erste bleibt
  wirksam.
- LF-Datei (nach Step 1 zulässig): `#version` mit `\n` terminiert — Move darf keine `\r\n` hart annehmen,
  `settings.NewLine` verwenden.
- Caret nicht auf der Direktive → nicht angeboten.

**Zu erstellen:**
- `Nav.Language/CodeFixes/ErrorFix/MoveVersionDirectiveToTopCodeFix.cs` (+ `…Provider.cs`) — Namen frei
  wählbar, aber am Muster bleiben.
- Verdrahten in `NavCodeActionService.GetCodeActions` (nächste `foreach` nach dem Nav3002-Provider).
- Tests in `Nav.Language.Tests/CodeActions/NavCodeActionServiceTests.cs` (Muster: `ParseModel` + `Caret` +
  `Apply`); Titel-basiert asserten wie die bestehenden `SetValidLanguageVersion_*`/`SetSupportedLanguageVersion_*`.

**Titel-Kollision beachten:** Steps 2/3 nutzen beide `"Change language version to {Latest}"` (mutually
exclusive). Der Move-Fix braucht einen **eigenen** Titel (z.B. `"Move '#version' to top of file"`).

## Step 5 — VS-Lightbulb-Export (Detailspezifikation)

Die VS-Extension (`Nav.Language.Extension2026`, non-SDK, WPF/VSIX) zeigt SuggestedActions über eigene
Provider in `Nav.Language.ExtensionShared/CodeFixes/`. Für die 6 bestehenden Fixes gibt es je einen, z.B.:
- `AddMissingExitTransitionSuggestedActionProvider.cs`, `AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider.cs`,
  `RemoveUnusedNodes…`, `RemoveUnusedTaskDeclaration…`, `RemoveUnusedIncludeDirective…`, `IntroduceChoice…`.

**Aufgabe:** je einen SuggestedActionProvider für `SetSupportedLanguageVersionCodeFix`,
`SetValidLanguageVersionCodeFix` und den neuen Nav3003-Fix nach genau diesem Muster anlegen (einen der
bestehenden als Vorlage lesen). Achtung: MEF-Export-Attribute, `ITextBuffer`→`CodeGenerationUnit`-Auflösung
und die Caret→Range-Bildung laufen dort VS-spezifisch.

**Build/Verifikation:** Die VS-Extension baut **nur** über Full-Framework `MSBuild.exe` (`nav build`), nicht
über `dotnet build`. Deshalb ist Step 5 bewusst vom Engine-Teil getrennt. Nach dem Bau ggf. `nav install`
+ manueller Lightbulb-Test auf `#version 99` / `#version abc` / deplatziertem `#version`.

## Build / Test / Konventionen (Kurzref)

- **net10:** `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (baut selbst).
- **net472:** erst `dotnet build Nav.Language.Tests\Nav.Language.Tests.csproj -f net472`, **dann**
  `nav test` (der Runner baut **nicht** selbst — sonst läuft die alte DLL). Alias `nav` vorher per
  `. "Tools\Commands\Import-NavCommands.ps1"` dot-sourcen.
- Neue `.cs`-Dateien als **UTF-8 mit BOM** (Write erzeugt es hier via Hook; danach auf BOM + 0×`U+FFFD`
  prüfen). Echte Umlaute, keine ASCII-Ersätze.
- **Niemals selbst committen** — nach Review + Check eine fertige Commit-Message liefern.
- `.nav`-Test-Strings dürfen seit Step 1 LF (`\n`) nutzen.

## Datei-Landkarte

- Fixes: `Nav.Language/CodeFixes/ErrorFix/Set*LanguageVersionCodeFix(+Provider).cs` (Vorlage für Step 4).
- Bündelung: `Nav.Language/CodeActions/NavCodeActionService.cs` (`GetCodeActions` + `ExpandCaret`).
- Direktiv-Knoten: `Nav.Language/Syntax/VersionDirectiveSyntax.cs`.
- Direktiv-Erzeugung/Wirksamkeit: `Nav.Language/Syntax/NavParser.cs` (`ResolveLanguageVersion`),
  `Nav.Language/Syntax/NavDirectiveParser.cs` (`ParseVersion`, Nav3002-Spannen).
- Diagnosen: `Nav.Language/Diagnostic/DiagnosticDescriptors.{Syntax,Semantic}.cs`.
- Tests: `Nav.Language.Tests/CodeActions/NavCodeActionServiceTests.cs`,
  `Nav.Language.Tests/Syntax/LanguageVersionTests.cs`.
- LSP-Handler: `Nav.Language.Lsp/NavLanguageServer.cs` (`CodeAction`, reicht rohen Range durch).
- VS-Export (Step 5): `Nav.Language.ExtensionShared/CodeFixes/*SuggestedActionProvider.cs`.
