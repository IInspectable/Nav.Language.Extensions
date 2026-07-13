#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Handgeschriebener Recursive-Descent-Parser für die Nav-Sprache. Läuft über den flachen Token-Strom
/// des <see cref="NavLexer"/> und baut in einem Durchlauf direkt den immutablen <see cref="SyntaxNode"/>-Baum,
/// vergibt die kontextabhängige <see cref="TextClassification"/> je signifikantem Token und hängt es an
/// seinen Knoten. Trivia (Whitespace, Zeilenenden, Kommentare), unbekannte Zeichen und Präprozessor-Token
/// sieht der Parser nicht; sie werden nach dem Parsen als (teils strukturierte) Trivia an die Token gehängt
/// (siehe <see cref="FinalizeTrivia"/>), nur das abschließende <see cref="SyntaxTokenType.EndOfFile"/> wird
/// als Token an die Wurzel gehängt.
/// <para/>
/// Die Struktur folgt 1:1 der bisherigen Grammatik/dem Visitor: jede <c>Parse*</c>-Methode entspricht einer
/// Grammatikregel.
/// <para/>
/// Der Parser ist fehlertolerant. Zwei Mechanismen sorgen dafür, dass jede Eingabe — auch eine
/// unvollständige — einen vollständigen Baum ergibt und <b>kein Zeichen verloren geht</b>:
/// <list type="number">
///   <item><description><b>Insertion</b> — fehlt ein erwartetes Token, synthetisiert <see cref="Eat"/>
///   ein nullbreites Missing-Token (es landet nicht im Token-Strom), meldet eine Diagnose und rückt
///   <i>nicht</i> vor.</description></item>
///   <item><description><b>Deletion</b> — steht an einer Stelle ein unerwartetes Token, überspringt es
///   der Panic-Mode (<see cref="Recover"/>) bis zu einem Wiedereinstiegs- oder äußeren Anker-Token, mit
///   garantiertem Fortschritt je Schleifendurchlauf. Übersprungene signifikante Token werden abschließend
///   je Lauf zu einer strukturierten <see cref="SyntaxTokenType.SkippedTokensTrivia"/> gefaltet (die Token
///   liegen lokal am <see cref="SkippedTokensTriviaSyntax"/>-Knoten, Klassifikation
///   <see cref="TextClassification.Skiped"/>).</description></item>
/// </list>
/// Die Recovery liefert bewusst knappe, treffende Diagnosen; abgesichert ist sie über einen
/// Golden-Satz je Korpus-Datei (Token, Baum und Diagnostics werden gepinnt).
/// <para/>
/// Jede <c>Parse*</c>-Methode trägt die zugehörige Grammatikregel als EBNF-Fragment. Lesehilfe zur
/// Notation:
/// <list type="bullet">
///   <item><description><c>"…"</c> — ein literales Schlüsselwort, ein Operator oder ein
///   Punctuation-Zeichen (z.B. <c>"task"</c>, <c>"--&gt;"</c>, <c>";"</c>).</description></item>
///   <item><description><c>Identifier</c>, <c>StringLiteral</c>, <c>EOF</c> — kategorische Terminale
///   (kein fester Text), groß geschrieben und ohne Anführungszeichen.</description></item>
///   <item><description>Nichtterminale in <c>camelCase</c> verweisen auf die gleichnamige
///   <c>Parse*</c>-Methode.</description></item>
///   <item><description><c>?</c> optional, <c>*</c> null bis n, <c>+</c> ein bis n, <c>|</c> Alternative,
///   <c>( … )</c> Gruppierung, <c>(* … *)</c> erläuternder Kommentar.</description></item>
/// </list>
/// Die EBNF beschreibt die <i>akzeptierte Sprache</i>; bewusste Implementierungs-Eigenheiten (Baum-
/// Gruppierung, Lookahead-Disambiguierung, fehlertolerante Bestandteile) stehen als Prosa-Hinweis im
/// jeweiligen <c>&lt;remarks&gt;</c>.
/// </summary>
sealed partial class NavParser {

    readonly SourceText                          _sourceText;
    readonly ImmutableArray<RawToken>            _raw;
    readonly List<SyntaxToken>                   _tokens;
    readonly ImmutableArray<Diagnostic>.Builder  _diagnostics;
    readonly int                                 _eofPos;

    // Aus _raw berechnete Token-Trivia (echtes Roslyn-Modell): alle Trivia der Datei liegen in genau
    // einem geteilten Array (_allTrivia, Strom-Reihenfolge); je signifikantem Token — Schlüssel ist seine
    // eindeutige Start-Position — merkt _tokenTrivia nur Index-Bereiche (Leading/Trailing) hinein, plus
    // separat die Leading-Trivia des abschließenden EndOfFile (die finale Datei-Trivia). Anders als die
    // Direktiven (vor dem Parsen bekannt) stehen die übersprungenen Token erst nach dem Parsen fest —
    // die Trivia wird daher erst in FinalizeTrivia gebaut und dort per Finalisierungs-Pass an die bereits
    // erzeugten Token gesetzt (siehe LookupTrivia).
    ImmutableArray<SyntaxTrivia> _allTrivia;
    Dictionary<int, TriviaRange> _tokenTrivia = null!; // In FinalizeTrivia gesetzt, bevor LookupTrivia darauf zugreift.
    SyntaxTriviaList             _eofLeadingTrivia;

    int  _pos;                     // Index in _raw; Invariante: zeigt stets auf ein parser-sichtbares Token (signifikant oder EOF).
    int? _firstSignificantStart;   // Start des ersten konsumierten signifikanten Tokens (für den Wurzel-Extent).
    bool _reportedMissingAtEof;    // Ein "missing …" am Dateiende wurde bereits gemeldet — weitere am EOF werden unterdrückt.

    // Die strukturiert erkannten Präprozessor-Direktiven (je #-Lauf ein Knoten mit lokalen Token). Sie
    // werden im Direktiven-Vorlauf (NavDirectiveParser) erzeugt, anschließend in BuildTrivia zu strukturierter
    // DirectiveTrivia gefaltet und nach dem Baum-Aufbau finalisiert.
    readonly List<DirectiveRun> _directiveRuns;

    // Die aus den übersprungenen Läufen gefalteten SkippedTokensTrivia-Knoten (je Lauf ein Knoten mit
    // lokalen Token) — erzeugt in BuildTrivia, nach dem Baum-Aufbau finalisiert (wie die Direktiven).
    readonly List<SkippedTokensTriviaSyntax> _skippedTokensRuns = new();

    // Wiederverwendete Recovery-Prädikate: Methodengruppen- und Lambda-Konvertierungen allozieren bei
    // jedem Aufruf ein neues Func<bool> — auch auf dem Happy Path (EatCloseBracket je [ … ]-Deklaration,
    // die Body-Resynchronisation je task/taskref). Einmal im Konstruktor erzeugt, sind alle
    // Recover-Aufrufe allokationsfrei.
    readonly Func<bool> _closesBracketRegion;
    readonly Func<bool> _atMemberOrEof;                  // task | taskref | EOF (Top-Level-Anker)
    readonly Func<bool> _atTaskDeclarationBodyOrAnchor;  // { | Connection-Point | äußerer Anker
    readonly Func<bool> _atConnectionPointOrAnchor;      // Connection-Point | äußerer Anker
    readonly Func<bool> _atTaskDefinitionBodyOrAnchor;   // { | Knoten | Transition | äußerer Anker
    readonly Func<bool> _atTransitionOrAnchor;           // Transition | äußerer Anker

    /// <summary>
    /// Richtet den Parser auf <paramref name="sourceText"/> ein: lext den Text zum flachen Roh-Token-Strom,
    /// parst die Präprozessor-Direktiven strukturiert vorab (<see cref="NavDirectiveParser"/>) und stellt
    /// den Cursor auf das erste parser-sichtbare Token (<see cref="SkipHidden"/>).
    /// </summary>
    NavParser(SourceText sourceText) {
        _sourceText  = sourceText;
        _raw         = NavLexer.Lex(sourceText.Text);
        _tokens      = new List<SyntaxToken>(_raw.Length / 2 + 1); // Signifikante Token ≈ Hälfte des Roh-Stroms (der Rest ist Trivia).
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        _eofPos      = _raw[_raw.Length - 1].Start; // Das abschließende EndOfFile ist nullbreit am Textende.

        _closesBracketRegion           = ClosesBracketRegion;
        _atMemberOrEof                 = () => At(SyntaxTokenType.TaskrefKeyword) || At(SyntaxTokenType.TaskKeyword) || AtEof;
        _atTaskDeclarationBodyOrAnchor = () => At(SyntaxTokenType.OpenBrace) || StartsConnectionPoint() || BreaksBody();
        _atConnectionPointOrAnchor     = () => StartsConnectionPoint() || BreaksBody();
        _atTaskDefinitionBodyOrAnchor  = () => At(SyntaxTokenType.OpenBrace) || StartsNodeDeclaration() || StartsTransition() || BreaksBody();
        _atTransitionOrAnchor          = () => StartsTransition() || BreaksBody();

        // Präprozessor-Direktiven strukturiert vorab parsen — der eigentliche Cursor sieht die „hidden"
        // Präprozessor-Token nicht. Ergebnis: je #-Lauf ein Direktiv-Knoten samt lokalen Token; die Läufe
        // faltet BuildTrivia (nach dem Parsen, siehe FinalizeTrivia) zu strukturierter DirectiveTrivia.
        _directiveRuns = new NavDirectiveParser(_raw, _sourceText, _diagnostics).Parse();

        _pos = 0;
        SkipHidden();
    }

    /// <summary>
    /// Der Einstieg des Parsers: parst <paramref name="text"/> als ganze <c>.nav</c>-Datei zu einem
    /// vollständigen <see cref="SyntaxTree"/> (Wurzel <see cref="CodeGenerationUnitSyntax"/>) — dank der
    /// Fehlertoleranz für <b>jede</b> Eingabe, auch eine leere oder unvollständige; <c>null</c> wird wie
    /// leerer Text behandelt. Syntaxfehler landen als <see cref="SyntaxTree.Diagnostics"/> im Ergebnis,
    /// nie als Exception. <paramref name="filePath"/> fließt nur in die <see cref="Location"/>s der
    /// Diagnosen ein. <paramref name="cancellationToken"/> wird zwischen den Top-Level-Members geprüft.
    /// </summary>
    public static SyntaxTree Parse(string? text, string? filePath = null, CancellationToken cancellationToken = default) {

        var sourceText = SourceText.From(text ?? String.Empty, filePath);

        return new NavParser(sourceText).ParseCodeGenerationUnit(cancellationToken);
    }

    #region Per-Regel-Einstiege (test-only)

    /// <summary>
    /// Grammatikregeln, die einzeln (statt als ganze Datei) geparst werden können — die test-seitige
    /// Snippet-Schnittstelle hinter <see cref="Syntax"/>. Jeder Wert verweist auf die gleichnamige private
    /// <c>Parse*</c>-Methode (Ausnahme: <see cref="ArrayType"/> nutzt <see cref="ParseCodeType"/>, das die
    /// Array-Regel mit abdeckt).
    /// </summary>
    internal enum Rule {
        DoClause,                      GoToEdge,                  ArrayType,
        ModalEdge,                     Parameter,                 Identifier,
        SimpleType,                    GenericType,               NonModalEdge,
        EndTargetNode,                 ParameterList,             SignalTrigger,
        StringLiteral,                 InitSourceNode,            TaskDefinition,
        CodeDeclaration,               TaskDeclaration,           IncludeDirective,
        IfConditionClause,             ArrayRankSpecifier,        EndNodeDeclaration,
        SpontaneousTrigger,            CodeBaseDeclaration,       ElseConditionClause,
        ExitNodeDeclaration,           InitNodeDeclaration,       TaskNodeDeclaration,
        ViewNodeDeclaration,           CodeUsingDeclaration,      IdentifierSourceNode,
        IdentifierTargetNode,          NodeDeclarationBlock,      TransitionDefinition,
        ChoiceNodeDeclaration,         CodeParamsDeclaration,     CodeResultDeclaration,
        ElseIfConditionClause,         DialogNodeDeclaration,     CodeNamespaceDeclaration,
        ExitTransitionDefinition,      CodeGenerateToDeclaration, TransitionDefinitionBlock,
        CodeDoNotInjectDeclaration,    CodeAbstractMethodDeclaration,
        CodeNotImplementedDeclaration, ContinuationTransition
    }

    /// <summary>
    /// Test-seitiger Einstieg je Grammatikregel (siehe <see cref="Syntax"/>): parst ein Snippet, das genau
    /// einer Regel entspricht, und liefert dessen Knoten als Wurzel. Setzt — wie <see cref="Parse"/> — den
    /// Cursor auf, ruft die zur Regel gehörende private <c>Parse*</c>-Methode, finalisiert die Trivia
    /// (inkl. im Panic-Mode übersprungener Token als <see cref="SyntaxTokenType.SkippedTokensTrivia"/>) und
    /// hängt das abschließende <see cref="SyntaxTokenType.EndOfFile"/> an die so entstandene Wurzel.
    /// Produktionscode parst ausschließlich ganze Dateien über <see cref="Parse"/>.
    /// </summary>
    internal static SyntaxTree ParseRule(string? text, Rule rule, string? filePath = null, CancellationToken cancellationToken = default) {

        var sourceText = SourceText.From(text ?? String.Empty, filePath);
        var parser     = new NavParser(sourceText);

        cancellationToken.ThrowIfCancellationRequested();

        var root = parser.ParseRuleRoot(rule);

        // Wie beim Whole-File-Parsing: Trivia (inkl. Direktiven und übersprungener Token) finalisieren,
        // dann das abschließende EndOfFile an die Wurzel (hier den Regel-Knoten) hängen.
        parser.FinalizeTrivia();
        parser.AttachEndOfFile(root);

        var syntaxTree = new SyntaxTree(sourceText : sourceText,
                                        root       : root,
                                        tokens     : parser.TakeSortedTokens(),
                                        diagnostics: parser._diagnostics.ToImmutable());

        root.FinalConstruct(syntaxTree, null);
        parser.FinalizeStructuredTrivia(syntaxTree, root);

        return syntaxTree;
    }

    SyntaxNode ParseRuleRoot(Rule rule) {
        switch (rule) {
            case Rule.DoClause:                      return ParseDoClause();
            case Rule.GoToEdge:                      return ParseGoToEdge();
            case Rule.ArrayType:                     return ParseCodeType();
            case Rule.ModalEdge:                     return ParseModalEdge();
            case Rule.Parameter:                     return ParseParameter();
            case Rule.Identifier:                    return ParseIdentifier();
            case Rule.SimpleType:                    return ParseSimpleType();
            case Rule.GenericType:                   return ParseGenericType();
            case Rule.NonModalEdge:                  return ParseNonModalEdge();
            case Rule.EndTargetNode:                 return ParseEndTargetNode();
            case Rule.ParameterList:                 return ParseParameterList();
            case Rule.SignalTrigger:                 return ParseSignalTrigger();
            case Rule.StringLiteral:                 return ParseStringLiteral();
            case Rule.InitSourceNode:                return ParseInitSourceNode();
            case Rule.TaskDefinition:                return ParseTaskDefinition();
            case Rule.CodeDeclaration:               return ParseCodeDeclaration();
            case Rule.TaskDeclaration:               return ParseTaskDeclaration();
            case Rule.IncludeDirective:              return ParseIncludeDirective();
            case Rule.IfConditionClause:             return ParseIfConditionClause();
            case Rule.ArrayRankSpecifier:            return ParseArrayRankSpecifier();
            case Rule.EndNodeDeclaration:            return ParseEndNodeDeclaration();
            case Rule.SpontaneousTrigger:            return ParseSpontaneousTrigger();
            case Rule.CodeBaseDeclaration:           return ParseCodeBaseDeclaration();
            case Rule.ElseConditionClause:           return ParseElseConditionClause();
            case Rule.ExitNodeDeclaration:           return ParseExitNodeDeclaration();
            case Rule.InitNodeDeclaration:           return ParseInitNodeDeclaration();
            case Rule.TaskNodeDeclaration:           return ParseTaskNodeDeclaration();
            case Rule.ViewNodeDeclaration:           return ParseViewNodeDeclaration();
            case Rule.CodeUsingDeclaration:          return ParseCodeUsingDeclaration();
            case Rule.IdentifierSourceNode:          return ParseIdentifierSourceNode();
            case Rule.IdentifierTargetNode:          return ParseIdentifierTargetNode();
            case Rule.NodeDeclarationBlock:          return ParseNodeDeclarationBlock();
            case Rule.TransitionDefinition:          return ParseTransitionDefinition();
            case Rule.ChoiceNodeDeclaration:         return ParseChoiceNodeDeclaration();
            case Rule.CodeParamsDeclaration:         return ParseCodeParamsDeclaration();
            case Rule.CodeResultDeclaration:         return ParseCodeResultDeclaration();
            case Rule.ElseIfConditionClause:         return ParseElseIfConditionClause();
            case Rule.DialogNodeDeclaration:         return ParseDialogNodeDeclaration();
            case Rule.CodeNamespaceDeclaration:      return ParseCodeNamespaceDeclaration();
            case Rule.ExitTransitionDefinition:      return ParseExitTransitionDefinition();
            case Rule.CodeGenerateToDeclaration:     return ParseCodeGenerateToDeclaration();
            case Rule.TransitionDefinitionBlock:     return ParseTransitionDefinitionBlock();
            case Rule.CodeDoNotInjectDeclaration:    return ParseCodeDoNotInjectDeclaration();
            case Rule.CodeAbstractMethodDeclaration: return ParseCodeAbstractMethodDeclaration();
            case Rule.CodeNotImplementedDeclaration: return ParseCodeNotImplementedDeclaration();
            case Rule.ContinuationTransition:        return ParseContinuationTransition();
            default: throw new ArgumentOutOfRangeException(nameof(rule), rule, null);
        }
    }

    #endregion

    #region CodeGenerationUnit

    /// <summary>Grammatik-Einstiegsregel <c>codeGenerationUnit</c> → <see cref="CodeGenerationUnitSyntax"/> (Wurzel).</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeGenerationUnit ::= ( codeNamespaceDeclaration codeUsingDeclaration* )?
    ///                        memberDeclaration*
    ///                        EOF
    /// ]]></code>
    /// Der optionale Namespace-/Using-Kopf wird über Zwei-Token-Lookahead erkannt (<c>[</c> + <c>namespaceprefix</c>
    /// bzw. <c>using</c>). Auf Top-Level gibt es keinen äußeren Anker: alles, was kein Member beginnt, wird bis
    /// zum nächsten Member oder zum Dateiende übersprungen (Panic-Mode).
    /// </remarks>
    SyntaxTree ParseCodeGenerationUnit(CancellationToken cancellationToken) {

        CodeNamespaceDeclarationSyntax?   codeNamespace = null;
        var codeUsings = new List<CodeUsingDeclarationSyntax>();
        var members    = new List<MemberDeclarationSyntax>();

        // Die Präprozessor-Direktiven (#…) sind bereits im Konstruktor strukturiert erkannt und als
        // DirectiveTrivia gefaltet (der eigentliche Cursor sieht die „hidden" Präprozessor-Token nicht).

        // Kopf der CodeGenerationUnit: [namespaceprefix …] gefolgt von [using …]*. Der Using-Kopf
        // existiert nur, wenn ein namespaceprefix ihn eröffnet — usings ohne vorangehendes namespaceprefix
        // gehören grammatisch nicht in den Kopf (und werden in der Member-Schleife als Fehlerproduktion
        // behandelt). Ist der Kopf eröffnet, werden die usings — wie die Code-Deklarationen der Wirte
        // (siehe ParseCodeDeclarations) — verschränkt mit der Klammer-Recovery geparst: eine hier nicht
        // zuzuordnende Klammer (malforme/unfertige using-Klammer wie `[usin …]`, ein zweites
        // namespaceprefix, ein leeres [ … ]) wird als eigene Fehlerproduktion isoliert übersprungen, ohne
        // die folgenden — für sich wohlgeformten — usings mitzureißen.
        if (AtCodeDeclaration(SyntaxTokenType.NamespaceprefixKeyword)) {
            codeNamespace = ParseCodeNamespaceDeclaration();

            while (At(SyntaxTokenType.OpenBracket)) {
                if (AtCodeDeclaration(SyntaxTokenType.UsingKeyword)) {
                    codeUsings.Add(ParseCodeUsingDeclaration());
                    continue;
                }

                ParseMalformedBracketDeclaration(CodeBlockHost.CompilationUnit);
            }
        }

        while (!AtEof) {
            cancellationToken.ThrowIfCancellationRequested();

            if (At(SyntaxTokenType.TaskrefKeyword) || At(SyntaxTokenType.TaskKeyword) || AtMemberKeywordPrefix()) {
                members.Add(ParseMemberDeclaration());
                continue;
            }

            // Ein '[' auf Top-Level, das keiner Kopf-Deklaration (namespaceprefix/using) mehr entspricht,
            // läuft durch dieselbe Klammer-Recovery wie in den übrigen Wirten — ein leeres '[]' meldet
            // „expected 'namespaceprefix' or 'using'" statt des nackten „unexpected input '['".
            if (At(SyntaxTokenType.OpenBracket)) {
                SkipMalformedBrackets(CodeBlockHost.CompilationUnit);
                continue;
            }

            // Auf Top-Level gibt es keinen äußeren Anker: alles, was kein Member beginnt, wird bis zum
            // nächsten Member oder zum Dateiende übersprungen.
            Recover(_atMemberOrEof);
        }

        // Die Direktiven sind strukturierte Trivia und damit keine Kindknoten der Wurzel mehr; der
        // Wurzel-Extent beginnt am ersten signifikanten Token (die Direktiv-Trivia liegt als Leading davor).
        var rootStart  = _firstSignificantStart ?? _eofPos;
        var rootExtent = TextExtent.FromBounds(rootStart, _eofPos);

        // Wirksame Versions-Direktive aus den Läufen bestimmen — jetzt, da _firstSignificantStart endgültig
        // ist (Platzierungs-Semantik) und vor dem Einfrieren der Diagnostics (Nav3003/Nav3004).
        var languageVersionDirective = ResolveLanguageVersion();

        var root = new CodeGenerationUnitSyntax(rootExtent, languageVersionDirective, codeNamespace, codeUsings, members);

        // Trivia finalisieren (jetzt stehen die übersprungenen Token fest) und das abschließende EndOfFile
        // an die Wurzel hängen. Weder Präprozessor- noch Skip-Token stehen im flachen Strom — sie sind zu
        // strukturierter DirectiveTrivia bzw. SkippedTokensTrivia gefaltet.
        FinalizeTrivia();
        AttachEndOfFile(root);

        var syntaxTree = new SyntaxTree(sourceText : _sourceText,
                                        root       : root,
                                        tokens     : TakeSortedTokens(),
                                        diagnostics: _diagnostics.ToImmutable());

        root.FinalConstruct(syntaxTree, null);
        FinalizeStructuredTrivia(syntaxTree, root);

        return syntaxTree;
    }

    /// <summary>
    /// Schließt die strukturierten Trivia-Knoten — die im Vorlauf erzeugten Direktiven und die in
    /// <see cref="FinalizeTrivia"/> gefalteten Skip-Läufe — an den fertigen Baum an. Sie sind <b>keine</b>
    /// Kindknoten der Wurzel, brauchen aber — wie jeder Knoten — <see cref="SyntaxTree"/> und einen
    /// <see cref="SyntaxNode.Parent"/>, damit ihre lokalen Token Position und Quelltext auflösen
    /// (siehe <see cref="SyntaxToken.SyntaxTree"/>). Erreichbar bleiben sie über ihre Trivia (siehe
    /// <see cref="SyntaxTree.Directives"/> bzw. <see cref="SyntaxTree.SkippedTokens"/>).
    /// </summary>
    void FinalizeStructuredTrivia(SyntaxTree syntaxTree, SyntaxNode root) {
        foreach (var run in _directiveRuns) {
            run.Node.FinalConstruct(syntaxTree, root);
        }

        foreach (var skipped in _skippedTokensRuns) {
            skipped.FinalConstruct(syntaxTree, root);
        }
    }

    /// <summary>
    /// Wählt aus den strukturell erkannten Direktiv-Läufen die <b>wirksame</b> <see cref="VersionDirectiveSyntax"/>
    /// und meldet die Platzierungs-Verstöße: eine Versions-Direktive ist nur <i>ganz oben</i> wirksam (ihr darf
    /// ausschließlich Trivia vorausgehen — kein Code und keine andere Direktive), und nur die erste am Kopf zählt.
    /// <list type="bullet">
    ///   <item><description>hinter echtem Code ⇒ <c>Nav3003</c> (die Deplatzierung sticht eine etwaige
    ///   Duplikat-Meldung);</description></item>
    ///   <item><description>am Kopf, aber eine wirksame ging schon voraus ⇒ <c>Nav3004</c> (Duplikat, die
    ///   erste gewinnt);</description></item>
    ///   <item><description>am Kopf, aber eine andere Direktive ging voraus ⇒ <c>Nav3003</c> (nicht ganz
    ///   oben, da nur Trivia vorausgehen dürfte).</description></item>
    /// </list>
    /// Die generische Erkennung (jede <c>#version</c> ist ein <see cref="VersionDirectiveSyntax"/>) samt
    /// <c>Nav3000</c>/<c>Nav3002</c> liefert bereits der <see cref="NavDirectiveParser"/>; hier kommt nur die
    /// Platzierungs-Semantik hinzu. Läuft <b>nach</b> der Member-Schleife (damit <c>_firstSignificantStart</c>
    /// endgültig ist) und vor dem Einfrieren der Diagnostics.
    /// </summary>
    VersionDirectiveSyntax? ResolveLanguageVersion() {

        VersionDirectiveSyntax? effective = null;
        var                     sawAnyDirective = false;

        foreach (var run in _directiveRuns) {
            if (run.Node is VersionDirectiveSyntax version) {
                var codeBefore = run.ContentExtent.Start >= (_firstSignificantStart ?? _eofPos);

                if (codeBefore) {
                    _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(run.ContentExtent),
                                                    DiagnosticDescriptors.Syntax.Nav3003VersionDirectiveMustAppearAtTopOfFile));
                } else if (effective != null) {
                    _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(run.ContentExtent),
                                                    DiagnosticDescriptors.Syntax.Nav3004DuplicateVersionDirective));
                } else if (sawAnyDirective) {
                    _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(run.ContentExtent),
                                                    DiagnosticDescriptors.Syntax.Nav3003VersionDirectiveMustAppearAtTopOfFile));
                } else {
                    effective = version;
                }
            }

            sawAnyDirective = true; // Gilt für JEDE Direktive (Version wie Bad) — sie verschiebt den „Kopf".
        }

        return effective;
    }

    /// <summary>Grammatikregel <c>memberDeclaration</c> → <see cref="MemberDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// memberDeclaration ::= includeDirective
    ///                     | taskDeclaration
    ///                     | taskDefinition
    /// ]]></code>
    /// Disambiguierung per Lookahead: <c>taskref</c> + StringLiteral ⇒ <c>includeDirective</c>; <c>taskref</c> +
    /// Identifier ⇒ <c>taskDeclaration</c>; <c>task</c> ⇒ <c>taskDefinition</c>.
    /// <para/>
    /// Beim Tippen kann das Leit-Schlüsselwort noch unvollständig sein und als Identifier lexen
    /// (<c>tas</c> vor <c>task</c>/<c>taskref</c>); <see cref="AtMemberKeywordPrefix"/> hat die Position dann
    /// bereits als gemeinten Member erkannt. Tie-Break: fester Vorrang <c>task</c> (der häufigere Fall);
    /// <c>taskref</c> greift nur bei einem eindeutigen Präfix <c>taskr…</c>, das kein Präfix von <c>task</c>
    /// mehr ist. Die gewählte <c>ParseTask*</c>-Methode konsumiert das Schlüsselwort über
    /// <see cref="EatKeywordOrSkip"/> (Missing-Keyword + Störtoken als Skip-Trivia).
    /// </remarks>
    MemberDeclarationSyntax ParseMemberDeclaration() {
        if (At(SyntaxTokenType.TaskrefKeyword)) {
            return PeekType(1) == SyntaxTokenType.StringLiteral
                ? ParseIncludeDirective()
                : ParseTaskDeclaration();
        }

        if (At(SyntaxTokenType.TaskKeyword)) {
            return ParseTaskDefinition();
        }

        return IsKeywordPrefix(SyntaxTokenType.TaskKeyword, PeekText(0))
            ? ParseTaskDefinition()
            : ParseTaskDeclaration();
    }

    #endregion

    #region Code Namespace / Using

    /// <summary>Grammatikregel <c>codeNamespaceDeclaration</c> → <see cref="CodeNamespaceDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeNamespaceDeclaration ::= "[" "namespaceprefix" identifierOrString "]"
    /// ]]></code>
    /// </remarks>
    CodeNamespaceDeclarationSyntax ParseCodeNamespaceDeclaration() {

        var open      = Eat(SyntaxTokenType.OpenBracket);
        var keyword   = EatKeywordOrSkip(SyntaxTokenType.NamespaceprefixKeyword);
        var nsSyntax  = ParseIdentifierOrString();
        var close     = EatCloseBracket();

        var node = new CodeNamespaceDeclarationSyntax(Span(open, keyword, nsSyntax, close), nsSyntax);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeUsingDeclaration</c> → <see cref="CodeUsingDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeUsingDeclaration ::= "[" "using" identifierOrString "]"
    /// ]]></code>
    /// </remarks>
    CodeUsingDeclarationSyntax ParseCodeUsingDeclaration() {

        var open     = Eat(SyntaxTokenType.OpenBracket);
        var keyword  = EatKeywordOrSkip(SyntaxTokenType.UsingKeyword);
        var nsSyntax = ParseIdentifierOrString();
        var close    = EatCloseBracket();

        var node = new CodeUsingDeclarationSyntax(Span(open, keyword, nsSyntax, close), nsSyntax);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IncludeDirective

    /// <summary>Grammatikregel <c>includeDirective</c> → <see cref="IncludeDirectiveSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// includeDirective ::= "taskref" StringLiteral ";"
    /// ]]></code>
    /// </remarks>
    IncludeDirectiveSyntax ParseIncludeDirective() {

        var keyword = Eat(SyntaxTokenType.TaskrefKeyword);
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new IncludeDirectiveSyntax(Span(keyword, literal, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, literal, TextClassification.StringLiteral);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TaskDeclaration

    /// <summary>Grammatikregel <c>taskDeclaration</c> → <see cref="TaskDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskDeclaration ::= "taskref" Identifier
    ///                     codeNamespaceDeclaration?
    ///                     codeNotImplementedDeclaration?
    ///                     codeResultDeclaration?
    ///                     "{" connectionPointNodeDeclaration* "}"
    /// ]]></code>
    /// Die optionalen <c>code*</c>-Deklarationen werden über Zwei-Token-Lookahead erkannt (<c>[</c> +
    /// Schlüsselwort). Vor dem Body-<c>{</c> überspringt ein gezielter Panic-Mode ungültige Token bis zum
    /// <c>{</c>, zum Beginn eines Connection-Points oder zu einem äußeren Anker.
    /// </remarks>
    TaskDeclarationSyntax ParseTaskDeclaration() {

        var keyword = EatKeywordOrSkip(SyntaxTokenType.TaskrefKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        CodeNamespaceDeclarationSyntax?      codeNamespace  = null;
        CodeNotImplementedDeclarationSyntax? notImplemented = null;
        CodeResultDeclarationSyntax?         result         = null;

        ParseCodeDeclarations(CodeBlockHost.TaskRef, () => {
            if (codeNamespace  == null && AtCodeDeclaration(SyntaxTokenType.NamespaceprefixKeyword)) { codeNamespace  = ParseCodeNamespaceDeclaration();      return true; }
            if (notImplemented == null && AtCodeDeclaration(SyntaxTokenType.NotimplementedKeyword))  { notImplemented = ParseCodeNotImplementedDeclaration(); return true; }
            if (result         == null && AtCodeDeclaration(SyntaxTokenType.ResultKeyword))          { result         = ParseCodeResultDeclaration();         return true; }
            return false;
        });

        Recover(_atTaskDeclarationBodyOrAnchor);
        var open = Eat(SyntaxTokenType.OpenBrace);

        var connectionPoints = new List<ConnectionPointNodeSyntax>();
        while (true) {
            if (StartsConnectionPoint()) {
                connectionPoints.Add(ParseConnectionPointNodeDeclaration());
                continue;
            }

            if (BreaksBody()) {
                break;
            }

            Recover(_atConnectionPointOrAnchor);
        }

        var close = Eat(SyntaxTokenType.CloseBrace);

        var span = new ExtentBuilder();
        span.Add(keyword); span.Add(name); span.Add(codeNamespace); span.Add(notImplemented); span.Add(result);
        span.Add(open); span.AddRange(connectionPoints); span.Add(close);

        var node = new TaskDeclarationSyntax(span.ToExtent(), codeNamespace, notImplemented, result, connectionPoints);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>connectionPointNodeDeclaration</c> → <see cref="ConnectionPointNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// connectionPointNodeDeclaration ::= initNodeDeclaration
    ///                                  | exitNodeDeclaration
    ///                                  | endNodeDeclaration
    /// ]]></code>
    /// Disambiguierung per Start-Schlüsselwort (<c>init</c>/<c>exit</c>/<c>end</c>).
    /// </remarks>
    ConnectionPointNodeSyntax ParseConnectionPointNodeDeclaration() {
        switch (At0) {
            case SyntaxTokenType.InitKeyword: return ParseInitNodeDeclaration();
            case SyntaxTokenType.ExitKeyword: return ParseExitNodeDeclaration();
            default:                          return ParseEndNodeDeclaration();
        }
    }

    #endregion

    #region TaskDefinition

    /// <summary>Grammatikregel <c>taskDefinition</c> → <see cref="TaskDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskDefinition ::= "task" Identifier
    ///                    codeDeclaration?
    ///                    codeBaseDeclaration?
    ///                    codeGenerateToDeclaration?
    ///                    codeParamsDeclaration?
    ///                    codeResultDeclaration?
    ///                    "{" nodeDeclarationBlock transitionDefinitionBlock "}"
    /// ]]></code>
    /// Die optionalen <c>code*</c>-Deklarationen werden über Zwei-Token-Lookahead erkannt (<c>[</c> +
    /// Schlüsselwort). Vor dem Body-<c>{</c> überspringt ein gezielter Panic-Mode ungültige Token bis zum
    /// <c>{</c>, zum Beginn einer Knoten-/Transitions-Deklaration oder zu einem äußeren Anker.
    /// </remarks>
    TaskDefinitionSyntax ParseTaskDefinition() {

        var keyword = EatKeywordOrSkip(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        CodeDeclarationSyntax?           code       = null;
        CodeBaseDeclarationSyntax?       codeBase   = null;
        CodeGenerateToDeclarationSyntax? generateTo = null;
        CodeParamsDeclarationSyntax?     codeParams = null;
        CodeResultDeclarationSyntax?     result     = null;

        ParseCodeDeclarations(CodeBlockHost.TaskDefinition, () => {
            if (code       == null && AtCodeDeclaration(SyntaxTokenType.CodeKeyword))       { code       = ParseCodeDeclaration();           return true; }
            if (codeBase   == null && AtCodeDeclaration(SyntaxTokenType.BaseKeyword))       { codeBase   = ParseCodeBaseDeclaration();       return true; }
            if (generateTo == null && AtCodeDeclaration(SyntaxTokenType.GeneratetoKeyword)) { generateTo = ParseCodeGenerateToDeclaration(); return true; }
            if (codeParams == null && AtCodeDeclaration(SyntaxTokenType.ParamsKeyword))     { codeParams = ParseCodeParamsDeclaration();     return true; }
            if (result     == null && AtCodeDeclaration(SyntaxTokenType.ResultKeyword))     { result     = ParseCodeResultDeclaration();     return true; }
            return false;
        });

        Recover(_atTaskDefinitionBodyOrAnchor);
        var open = Eat(SyntaxTokenType.OpenBrace);

        var nodeBlock       = ParseNodeDeclarationBlock();
        var transitionBlock = ParseTransitionDefinitionBlock();

        var close = Eat(SyntaxTokenType.CloseBrace);

        var span = new ExtentBuilder();
        span.Add(keyword); span.Add(name); span.Add(code); span.Add(codeBase); span.Add(generateTo);
        span.Add(codeParams); span.Add(result); span.Add(open); span.Add(nodeBlock); span.Add(transitionBlock); span.Add(close);

        var node = new TaskDefinitionSyntax(span.ToExtent(), code, codeBase, generateTo, codeParams, result, nodeBlock, transitionBlock);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region Node Declarations

    /// <summary>Grammatikregel <c>nodeDeclarationBlock</c> → <see cref="NodeDeclarationBlockSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nodeDeclarationBlock ::= nodeDeclaration*
    /// ]]></code>
    /// Ein leerer Block (kein einziger Knoten) erhält den Extent <see cref="TextExtent.Missing"/>.
    /// </remarks>
    NodeDeclarationBlockSyntax ParseNodeDeclarationBlock() {

        var nodes = new List<NodeDeclarationSyntax>();
        var span  = new ExtentBuilder();

        while (StartsNodeDeclaration()) {
            var node = ParseNodeDeclaration();
            nodes.Add(node);
            span.Add(node);
        }

        return new NodeDeclarationBlockSyntax(span.ToExtent(), nodes);
    }

    /// <summary>
    /// Ob an der aktuellen Position eine Knoten-Deklaration beginnt (eines der Knoten-Schlüsselwörter).
    /// Sonderfall <c>init</c>: folgt eine Kante, ist es der Quellknoten einer Transition
    /// (<c>init --&gt; …</c>), keine Knoten-Deklaration.
    /// </summary>
    bool StartsNodeDeclaration() {
        switch (At0) {
            case SyntaxTokenType.ExitKeyword:
            case SyntaxTokenType.EndKeyword:
            case SyntaxTokenType.TaskKeyword:
            case SyntaxTokenType.ChoiceKeyword:
            case SyntaxTokenType.DialogKeyword:
            case SyntaxTokenType.ViewKeyword:
                return true;
            case SyntaxTokenType.InitKeyword:
                // 'init' gefolgt von einer Kante ist eine Transition (initSourceNode), keine Knoten-Deklaration.
                return !SyntaxFacts.IsEdgeKeyword(PeekType(1));
            default:
                return false;
        }
    }

    /// <summary>Grammatikregel <c>nodeDeclaration</c> → <see cref="NodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nodeDeclaration ::= connectionPointNodeDeclaration   (* init | exit | end *)
    ///                   | taskNodeDeclaration
    ///                   | choiceNodeDeclaration
    ///                   | dialogNodeDeclaration
    ///                   | viewNodeDeclaration
    /// ]]></code>
    /// Disambiguierung per Start-Schlüsselwort. Sonderfall <c>init</c>: nur dann eine Knoten-Deklaration, wenn
    /// <b>keine</b> Kante folgt — <c>init --&gt; …</c> ist eine Transition (<c>initSourceNode</c>), siehe
    /// <see cref="StartsNodeDeclaration"/>.
    /// </remarks>
    NodeDeclarationSyntax ParseNodeDeclaration() {
        switch (At0) {
            case SyntaxTokenType.InitKeyword:   return ParseInitNodeDeclaration();
            case SyntaxTokenType.ExitKeyword:   return ParseExitNodeDeclaration();
            case SyntaxTokenType.EndKeyword:    return ParseEndNodeDeclaration();
            case SyntaxTokenType.TaskKeyword:   return ParseTaskNodeDeclaration();
            case SyntaxTokenType.ChoiceKeyword: return ParseChoiceNodeDeclaration();
            case SyntaxTokenType.DialogKeyword: return ParseDialogNodeDeclaration();
            default:                            return ParseViewNodeDeclaration();
        }
    }

    /// <summary>Grammatikregel <c>initNodeDeclaration</c> → <see cref="InitNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// initNodeDeclaration ::= "init" Identifier?
    ///                         codeAbstractMethodDeclaration?
    ///                         codeParamsDeclaration?
    ///                         doClause?
    ///                         ";"
    /// ]]></code>
    /// </remarks>
    InitNodeDeclarationSyntax ParseInitNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var name    = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        CodeAbstractMethodDeclarationSyntax? abstractMethod = null;
        CodeParamsDeclarationSyntax?         codeParams     = null;

        // Die Code-Deklarationen werden verschränkt mit der Klammer-Recovery geparst: ein '[' an dieser
        // Stelle, das keiner bekannten Code-Deklaration entspricht (leeres `[]`, beim Tippen noch unfertiges
        // `[par`), wird als Fehlerproduktion in der Klammer verschluckt — sonst bräche die Knoten-Deklaration
        // hier ab und die restlichen Body-Zeilen liefen als Kaskade auf. Ein vorangestelltes malformes `[]`
        // verschluckt dabei keine nachfolgende, gültige Deklaration (siehe ParseCodeDeclarations).
        var malformedBracket = ParseCodeDeclarations(CodeBlockHost.InitNode, () => {
            if (abstractMethod == null && AtCodeDeclaration(SyntaxTokenType.AbstractmethodKeyword)) { abstractMethod = ParseCodeAbstractMethodDeclaration(); return true; }
            if (codeParams     == null && AtCodeDeclaration(SyntaxTokenType.ParamsKeyword))         { codeParams     = ParseCodeParamsDeclaration();         return true; }
            return false;
        });

        var doClause = At(SyntaxTokenType.DoKeyword) ? ParseDoClause() : null;

        var semi = malformedBracket ? TryEatSemicolonQuiet() : Eat(SyntaxTokenType.Semicolon);

        var node = new InitNodeDeclarationSyntax(Span(keyword, name, abstractMethod, codeParams, doClause, semi),
                                                 abstractMethod, codeParams, doClause);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>exitNodeDeclaration</c> → <see cref="ExitNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// exitNodeDeclaration ::= "exit" Identifier ";"
    /// ]]></code>
    /// </remarks>
    ExitNodeDeclarationSyntax ParseExitNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ExitKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new ExitNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>endNodeDeclaration</c> → <see cref="EndNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// endNodeDeclaration ::= "end" ";"
    /// ]]></code>
    /// </remarks>
    EndNodeDeclarationSyntax ParseEndNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new EndNodeDeclarationSyntax(Span(keyword, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>taskNodeDeclaration</c> → <see cref="TaskNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskNodeDeclaration ::= "task" Identifier Identifier?
    ///                         codeDoNotInjectDeclaration?
    ///                         codeAbstractMethodDeclaration?
    ///                         ";"
    /// ]]></code>
    /// Das zweite (optionale) <c>Identifier</c> ist der Alias des Task-Knotens.
    /// </remarks>
    TaskNodeDeclarationSyntax ParseTaskNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var alias   = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        CodeDoNotInjectDeclarationSyntax?    doNotInject    = null;
        CodeAbstractMethodDeclarationSyntax? abstractMethod = null;

        // Nicht erkanntes '[' als Fehlerproduktion in der Klammer verschlucken (siehe
        // ParseInitNodeDeclaration) — hält die Kaskade aus der abgebrochenen Knoten-Deklaration auf. Ein
        // vorangestelltes malformes `[]` verschluckt dabei keine nachfolgende, gültige Deklaration mehr.
        var malformedBracket = ParseCodeDeclarations(CodeBlockHost.TaskNode, () => {
            if (doNotInject    == null && AtCodeDeclaration(SyntaxTokenType.DonotinjectKeyword))    { doNotInject    = ParseCodeDoNotInjectDeclaration();    return true; }
            if (abstractMethod == null && AtCodeDeclaration(SyntaxTokenType.AbstractmethodKeyword)) { abstractMethod = ParseCodeAbstractMethodDeclaration(); return true; }
            return false;
        });

        var semi = malformedBracket ? TryEatSemicolonQuiet() : Eat(SyntaxTokenType.Semicolon);

        var node = new TaskNodeDeclarationSyntax(Span(keyword, name, alias, doNotInject, abstractMethod, semi),
                                                 doNotInject, abstractMethod);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, alias,   TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>choiceNodeDeclaration</c> → <see cref="ChoiceNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// choiceNodeDeclaration ::= "choice" Identifier codeParamsDeclaration? ";"
    /// ]]></code>
    /// Die optionale <c>[params …]</c>-Klausel (ab Sprachversion 2) wird — wie beim <c>init</c>-Knoten —
    /// verschränkt mit der Klammer-Recovery geparst.
    /// </remarks>
    ChoiceNodeDeclarationSyntax ParseChoiceNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ChoiceKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        CodeParamsDeclarationSyntax? codeParams = null;

        // Ein '[' an dieser Stelle, das keiner bekannten Code-Deklaration entspricht, als Fehlerproduktion in
        // der Klammer verschlucken (siehe ParseInitNodeDeclaration) — sonst bräche die Knoten-Deklaration ab.
        var malformedBracket = ParseCodeDeclarations(CodeBlockHost.ChoiceNode, () => {
            if (codeParams == null && AtCodeDeclaration(SyntaxTokenType.ParamsKeyword)) { codeParams = ParseCodeParamsDeclaration(); return true; }
            return false;
        });

        var semi = malformedBracket ? TryEatSemicolonQuiet() : Eat(SyntaxTokenType.Semicolon);

        var node = new ChoiceNodeDeclarationSyntax(Span(keyword, name, codeParams, semi), codeParams);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>dialogNodeDeclaration</c> → <see cref="DialogNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// dialogNodeDeclaration ::= "dialog" Identifier ";"
    /// ]]></code>
    /// </remarks>
    DialogNodeDeclarationSyntax ParseDialogNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.DialogKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new DialogNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.GuiNode);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>viewNodeDeclaration</c> → <see cref="ViewNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// viewNodeDeclaration ::= "view" Identifier ";"
    /// ]]></code>
    /// </remarks>
    ViewNodeDeclarationSyntax ParseViewNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ViewKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new ViewNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.GuiNode);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TransitionDefinitionBlock

    /// <summary>Grammatikregel <c>transitionDefinitionBlock</c> → <see cref="TransitionDefinitionBlockSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// transitionDefinitionBlock ::= ( transitionDefinition | exitTransitionDefinition )*
    /// ]]></code>
    /// Im Quelltext dürfen sich beide Alternativen mischen; im Baum werden sie jedoch getrennt gruppiert —
    /// erst alle <c>transitionDefinition</c>, dann alle <c>exitTransitionDefinition</c> (<b>nicht</b> in
    /// Quelltext-Reihenfolge). Disambiguierung per Lookahead: <c>Identifier</c> + <c>:</c> ⇒
    /// <c>exitTransitionDefinition</c>, sonst <c>transitionDefinition</c>.
    /// </remarks>
    TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock() {

        // Quelltext-Reihenfolge darf transitionDefinition und exitTransitionDefinition mischen; im Baum
        // werden sie jedoch — wie im bisherigen Modell — getrennt gruppiert (erst alle Transitionen, dann
        // alle Exit-Transitionen).
        var transitions     = new List<TransitionDefinitionSyntax>();
        var exitTransitions = new List<ExitTransitionDefinitionSyntax>();
        var span            = new ExtentBuilder();

        while (true) {
            if (StartsTransition()) {
                if (At(SyntaxTokenType.Identifier) && PeekType(1) == SyntaxTokenType.Colon) {
                    var exit = ParseExitTransitionDefinition();
                    exitTransitions.Add(exit);
                    span.Add(exit);
                } else {
                    var transition = ParseTransitionDefinition();
                    transitions.Add(transition);
                    span.Add(transition);
                }

                continue;
            }

            if (BreaksBody()) {
                break;
            }

            Recover(_atTransitionOrAnchor);
        }

        return new TransitionDefinitionBlockSyntax(span.ToExtent(), transitions, exitTransitions);
    }

    /// <summary>
    /// Ob an der aktuellen Position eine Transition beginnt — ein Quellknoten, also <c>init</c> oder ein
    /// Identifier.
    /// </summary>
    bool StartsTransition() {
        return At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.Identifier);
    }

    /// <summary>Grammatikregel <c>transitionDefinition</c> → <see cref="TransitionDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// transitionDefinition ::= sourceNode edge targetNode continuationTransition?
    ///                          trigger? conditionClause? doClause? ";"
    /// ]]></code>
    /// <c>edge</c> und <c>targetNode</c> werden fehlertolerant geparst: fehlt die Kante, wird
    /// <c>missing edge</c> gemeldet; fehlt — bei vorhandener Kante — das Zielknoten, <c>missing target node</c>
    /// (die beiden Fehlerproduktionen der ursprünglichen Grammatik). Die optionale
    /// <c>continuationTransition</c> (<c>o-^</c>/<c>--^</c> Task, ab Sprachversion 2) hängt hinter dem
    /// Zielknoten, vor Trigger/Bedingung.
    /// </remarks>
    TransitionDefinitionSyntax ParseTransitionDefinition() {

        var source = ParseSourceNode();

        var edge = StartsEdge() ? ParseEdge() : null;
        if (edge == null) {
            ReportMissing("edge");
        }

        // Beginnt der Zielknoten-Kandidat auf einer neuen Zeile eine neue Transition (Kante/':' voraus),
        // gehört er nicht mehr zur laufenden Transition: hier abbrechen, statt die Folgezeile einzusaugen.
        var continues = !TargetStartsNextTransition();

        var target = continues && StartsTargetNode() ? ParseTargetNode() : null;
        if (target == null && edge != null) {
            ReportMissing("target node");
        }

        var continuation = continues && StartsContinuation() ? ParseContinuationTransition() : null;

        var trigger   = continues && StartsTrigger()               ? ParseTrigger()         : null;
        var condition = continues && StartsCondition()             ? ParseConditionClause() : null;
        var doClause  = continues && At(SyntaxTokenType.DoKeyword)  ? ParseDoClause()        : null;

        // Nur eine Diagnose an der Divergenzstelle: das dann mechanisch fehlende ';' wird auf der
        // abgebrochenen Zeile unterdrückt (analog zur EOF-Kaskade).
        var semi = continues ? Eat(SyntaxTokenType.Semicolon) : TryEatSemicolonQuiet();

        var node = new TransitionDefinitionSyntax(Span(source, edge, target, continuation, trigger, condition, doClause, semi),
                                                  source, edge, target, continuation, trigger, condition, doClause);

        Tok(node, semi, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>exitTransitionDefinition</c> → <see cref="ExitTransitionDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// exitTransitionDefinition ::= identifierSourceNode ":" Identifier edge targetNode
    ///                              continuationTransition? conditionClause? doClause? ";"
    /// ]]></code>
    /// Wie bei <see cref="ParseTransitionDefinition"/> werden <c>edge</c>/<c>targetNode</c> fehlertolerant
    /// behandelt (<c>missing edge</c> / <c>missing target node</c>); die optionale
    /// <c>continuationTransition</c> (<c>o-^</c>/<c>--^</c> Task) hängt hinter dem Zielknoten.
    /// </remarks>
    ExitTransitionDefinitionSyntax ParseExitTransitionDefinition() {

        var source = ParseIdentifierSourceNode();
        var colon  = Eat(SyntaxTokenType.Colon);

        // Nach ':' steht der Name des Exit-Konnektors auf derselben Zeile. Beginnt der Kandidat auf einer
        // neuen Zeile bereits eine neue Transition (Kante/':' voraus), fehlt der Name: hier abbrechen,
        // statt den Quellknoten der Folgezeile als Namen einzusaugen (eine Diagnose, kein Folgefehler).
        var abort = TargetStartsNextTransition();
        if (abort) {
            ReportMissing(Describe(SyntaxTokenType.Identifier));
        }

        var name = abort ? null : Eat(SyntaxTokenType.Identifier);

        var edge = !abort && StartsEdge() ? ParseEdge() : null;
        if (!abort && edge == null) {
            ReportMissing("edge");
        }

        // Auch nach dem Namen kann die Folgezeile eine neue Transition beginnen (Zielknoten-Kandidat):
        // dann hier abbrechen, statt die Folgezeile einzusaugen.
        var continues = !abort && !TargetStartsNextTransition();

        var target = continues && StartsTargetNode() ? ParseTargetNode() : null;
        if (target == null && edge != null) {
            ReportMissing("target node");
        }

        var continuation = continues && StartsContinuation() ? ParseContinuationTransition() : null;

        var condition = continues && StartsCondition()             ? ParseConditionClause() : null;
        var doClause  = continues && At(SyntaxTokenType.DoKeyword)  ? ParseDoClause()        : null;

        // Nur eine Diagnose an der Divergenzstelle: das dann mechanisch fehlende ';' wird auf der
        // abgebrochenen Zeile unterdrückt (analog zur EOF-Kaskade).
        var semi = continues ? Eat(SyntaxTokenType.Semicolon) : TryEatSemicolonQuiet();

        var span = new ExtentBuilder();
        span.Add(source); span.Add(colon); span.Add(name); span.Add(edge); span.Add(target);
        span.Add(continuation); span.Add(condition); span.Add(doClause); span.Add(semi);

        var node = new ExitTransitionDefinitionSyntax(span.ToExtent(), source, edge, target, continuation, condition, doClause);

        Tok(node, colon, TextClassification.Punctuation);
        Tok(node, name,  TextClassification.Identifier);
        Tok(node, semi,  TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region SourceNode / TargetNode / Edge

    /// <summary>Grammatikregel <c>sourceNode</c> → <see cref="SourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// sourceNode ::= initSourceNode         (* "init" *)
    ///              | identifierSourceNode   (* Identifier *)
    /// ]]></code>
    /// </remarks>
    SourceNodeSyntax ParseSourceNode() {
        return At(SyntaxTokenType.InitKeyword) ? ParseInitSourceNode() : ParseIdentifierSourceNode();
    }

    /// <summary>Grammatikregel <c>initSourceNode</c> → <see cref="InitSourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// initSourceNode ::= "init"
    /// ]]></code>
    /// </remarks>
    InitSourceNodeSyntax ParseInitSourceNode() {
        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var node    = new InitSourceNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>identifierSourceNode</c> → <see cref="IdentifierSourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierSourceNode ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierSourceNodeSyntax ParseIdentifierSourceNode() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSourceNodeSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    /// <summary>Grammatikregel <c>targetNode</c> → <see cref="TargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// targetNode ::= endTargetNode          (* "end" *)
    ///              | identifierTargetNode   (* Identifier *)
    /// ]]></code>
    /// </remarks>
    TargetNodeSyntax ParseTargetNode() {
        return At(SyntaxTokenType.EndKeyword) ? ParseEndTargetNode() : ParseIdentifierTargetNode();
    }

    /// <summary>Grammatikregel <c>endTargetNode</c> → <see cref="EndTargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// endTargetNode ::= "end"
    /// ]]></code>
    /// </remarks>
    EndTargetNodeSyntax ParseEndTargetNode() {
        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var node    = new EndTargetNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>identifierTargetNode</c> → <see cref="IdentifierTargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierTargetNode ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierTargetNodeSyntax ParseIdentifierTargetNode() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierTargetNodeSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    /// <summary>Ob an der aktuellen Position eine Kante beginnt (<c>--&gt;</c>, <c>o-&gt;</c>, <c>==&gt;</c>; Autorität <see cref="SyntaxFacts.IsEdgeKeyword(SyntaxTokenType)"/>).</summary>
    bool StartsEdge() {
        return SyntaxFacts.IsEdgeKeyword(At0);
    }

    /// <summary>Ob an der aktuellen Position ein Zielknoten beginnt — <c>end</c> oder ein Identifier.</summary>
    bool StartsTargetNode() {
        return At(SyntaxTokenType.EndKeyword) || At(SyntaxTokenType.Identifier);
    }

    /// <summary>Grammatikregel <c>edge</c> → <see cref="EdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// edge ::= goToEdge       (* "-->" *)
    ///        | modalEdge      (* "o->" *)
    ///        | nonModalEdge   (* "==>" *)
    /// ]]></code>
    /// </remarks>
    EdgeSyntax ParseEdge() {
        switch (At0) {
            case SyntaxTokenType.GoToEdgeKeyword:    return ParseGoToEdge();
            case SyntaxTokenType.ModalEdgeKeyword:   return ParseModalEdge();
            default:                                 return ParseNonModalEdge();
        }
    }

    /// <summary>Grammatikregel <c>goToEdge</c> → <see cref="GoToEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// goToEdge ::= "-->"
    /// ]]></code>
    /// </remarks>
    GoToEdgeSyntax ParseGoToEdge() {
        var keyword = Eat(SyntaxTokenType.GoToEdgeKeyword);
        var node    = new GoToEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>modalEdge</c> → <see cref="ModalEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// modalEdge ::= "o->"
    /// ]]></code>
    /// </remarks>
    ModalEdgeSyntax ParseModalEdge() {
        var keyword = Eat(SyntaxTokenType.ModalEdgeKeyword);
        var node    = new ModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>nonModalEdge</c> → <see cref="NonModalEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nonModalEdge ::= "==>"
    /// ]]></code>
    /// </remarks>
    NonModalEdgeSyntax ParseNonModalEdge() {
        var keyword = Eat(SyntaxTokenType.NonModalEdgeKeyword);
        var node    = new NonModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    #endregion

    #region ContinuationTransition (o-^ / --^ Task)

    /// <summary>Ob an der aktuellen Position eine Continuation-Kante beginnt (<c>--^</c>/<c>o-^</c>; Autorität <see cref="SyntaxFacts.IsContinuationEdgeKeyword(SyntaxTokenType)"/>).</summary>
    bool StartsContinuation() {
        return SyntaxFacts.IsContinuationEdgeKeyword(At0);
    }

    /// <summary>Grammatikregel <c>continuationTransition</c> → <see cref="ContinuationTransitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// continuationTransition ::= continuationEdge targetNode
    /// ]]></code>
    /// Der Fortsetzungs-Anhang einer Transition (<c>… o-^ Task</c> / <c>… --^ Task</c>, ab Sprachversion 2).
    /// <c>continuationEdge</c> und <c>targetNode</c> werden fehlertolerant behandelt (fehlt der Zielknoten bei
    /// vorhandener Kante: <c>missing target node</c>). Ob der Ziel-Knoten ein Task ist bzw. der tragende Knoten
    /// ein GUI-Knoten, prüft erst das Semantic Model.
    /// </remarks>
    ContinuationTransitionSyntax ParseContinuationTransition() {

        var edge = StartsContinuation() ? ParseContinuationEdge() : null;
        if (edge == null) {
            ReportMissing("continuation edge");
        }

        var target = StartsTargetNode() ? ParseTargetNode() : null;
        if (target == null && edge != null) {
            ReportMissing("target node");
        }

        return new ContinuationTransitionSyntax(Span(edge, target), edge, target);
    }

    /// <summary>Grammatikregel <c>continuationEdge</c> → <see cref="ContinuationEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// continuationEdge ::= continuationGoToEdge     (* "--^" *)
    ///                    | continuationModalEdge    (* "o-^" *)
    /// ]]></code>
    /// </remarks>
    ContinuationEdgeSyntax ParseContinuationEdge() {
        return At(SyntaxTokenType.ContinuationModalEdgeKeyword)
            ? ParseContinuationModalEdge()
            : ParseContinuationGoToEdge();
    }

    /// <summary>Grammatikregel <c>continuationModalEdge</c> → <see cref="ContinuationModalEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// continuationModalEdge ::= "o-^"
    /// ]]></code>
    /// </remarks>
    ContinuationModalEdgeSyntax ParseContinuationModalEdge() {
        var keyword = Eat(SyntaxTokenType.ContinuationModalEdgeKeyword);
        var node    = new ContinuationModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>continuationGoToEdge</c> → <see cref="ContinuationGoToEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// continuationGoToEdge ::= "--^"
    /// ]]></code>
    /// </remarks>
    ContinuationGoToEdgeSyntax ParseContinuationGoToEdge() {
        var keyword = Eat(SyntaxTokenType.ContinuationGoToEdgeKeyword);
        var node    = new ContinuationGoToEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    #endregion

    #region Trigger

    /// <summary>Ob an der aktuellen Position ein Trigger beginnt (<c>on</c>, <c>spontaneous</c> oder <c>spont</c>).</summary>
    bool StartsTrigger() {
        return At(SyntaxTokenType.OnKeyword) || At(SyntaxTokenType.SpontaneousKeyword) || At(SyntaxTokenType.SpontKeyword);
    }

    /// <summary>Grammatikregel <c>trigger</c> → <see cref="TriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// trigger ::= signalTrigger        (* "on" … *)
    ///           | spontaneousTrigger   (* "spontaneous" | "spont" *)
    /// ]]></code>
    /// </remarks>
    TriggerSyntax ParseTrigger() {
        return At(SyntaxTokenType.OnKeyword) ? ParseSignalTrigger() : ParseSpontaneousTrigger();
    }

    /// <summary>Grammatikregel <c>signalTrigger</c> → <see cref="SignalTriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// signalTrigger ::= "on" identifier
    /// ]]></code>
    /// Der <c>identifier</c> wird fehlertolerant behandelt: fehlt er, entsteht der Knoten dennoch (ohne ihn).
    /// </remarks>
    SignalTriggerSyntax ParseSignalTrigger() {

        var keyword    = Eat(SyntaxTokenType.OnKeyword);
        var identifier = At(SyntaxTokenType.Identifier) ? ParseIdentifier() : null;

        var node = new SignalTriggerSyntax(Span(keyword, identifier), identifier);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>spontaneousTrigger</c> → <see cref="SpontaneousTriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// spontaneousTrigger ::= "spontaneous" | "spont"
    /// ]]></code>
    /// </remarks>
    SpontaneousTriggerSyntax ParseSpontaneousTrigger() {

        // "spontaneous" | "spont" ist eine Alternative: genau ein Keyword pro Trigger. Ein etwaiges
        // zweites Keyword (z.B. `spont spontaneous`) gehört nicht mehr zum Trigger und läuft in die
        // normale Recovery des Aufrufers.
        var keyword = At(SyntaxTokenType.SpontKeyword)
            ? Eat(SyntaxTokenType.SpontKeyword)
            : Eat(SyntaxTokenType.SpontaneousKeyword);

        var node = new SpontaneousTriggerSyntax(Span(keyword));

        Tok(node, keyword, TextClassification.Keyword);

        return node;
    }

    #endregion

    #region ConditionClause / DoClause

    /// <summary>Ob an der aktuellen Position eine Bedingungsklausel beginnt (<c>if</c> oder <c>else</c>).</summary>
    bool StartsCondition() {
        return At(SyntaxTokenType.IfKeyword) || At(SyntaxTokenType.ElseKeyword);
    }

    /// <summary>Grammatikregel <c>conditionClause</c> → <see cref="ConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// conditionClause ::= ifConditionClause       (* "if" … *)
    ///                   | elseIfConditionClause   (* "else" "if" … *)
    ///                   | elseConditionClause     (* "else" *)
    /// ]]></code>
    /// Disambiguierung per Lookahead: <c>if</c> ⇒ <c>ifConditionClause</c>; <c>else</c> + <c>if</c> ⇒
    /// <c>elseIfConditionClause</c>; sonst <c>elseConditionClause</c>.
    /// </remarks>
    ConditionClauseSyntax ParseConditionClause() {
        if (At(SyntaxTokenType.IfKeyword)) {
            return ParseIfConditionClause();
        }

        if (PeekType(1) == SyntaxTokenType.IfKeyword) {
            return ParseElseIfConditionClause();
        }

        return ParseElseConditionClause();
    }

    /// <summary>Grammatikregel <c>ifConditionClause</c> → <see cref="IfConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// ifConditionClause ::= "if" identifierOrString
    /// ]]></code>
    /// </remarks>
    IfConditionClauseSyntax ParseIfConditionClause() {

        var keyword            = Eat(SyntaxTokenType.IfKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new IfConditionClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>elseConditionClause</c> → <see cref="ElseConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// elseConditionClause ::= "else"
    /// ]]></code>
    /// </remarks>
    ElseConditionClauseSyntax ParseElseConditionClause() {

        var keyword = Eat(SyntaxTokenType.ElseKeyword);

        var node = new ElseConditionClauseSyntax(Span(keyword));

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>elseIfConditionClause</c> → <see cref="ElseIfConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// elseIfConditionClause ::= elseConditionClause ifConditionClause   (* "else" "if" identifierOrString *)
    /// ]]></code>
    /// </remarks>
    ElseIfConditionClauseSyntax ParseElseIfConditionClause() {

        var elseCondition = ParseElseConditionClause();
        var ifCondition   = ParseIfConditionClause();

        return new ElseIfConditionClauseSyntax(Span(elseCondition, ifCondition), elseCondition, ifCondition);
    }

    /// <summary>Grammatikregel <c>doClause</c> → <see cref="DoClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// doClause ::= "do" identifierOrString
    /// ]]></code>
    /// </remarks>
    DoClauseSyntax ParseDoClause() {

        var keyword            = Eat(SyntaxTokenType.DoKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new DoClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    #endregion

    #region Code Declarations

    /// <summary>Grammatikregel <c>codeNotImplementedDeclaration</c> → <see cref="CodeNotImplementedDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeNotImplementedDeclaration ::= "[" "notimplemented" "]"
    /// ]]></code>
    /// </remarks>
    CodeNotImplementedDeclarationSyntax ParseCodeNotImplementedDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.NotimplementedKeyword);
        var close   = EatCloseBracket();

        var node = new CodeNotImplementedDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeDoNotInjectDeclaration</c> → <see cref="CodeDoNotInjectDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeDoNotInjectDeclaration ::= "[" "donotinject" "]"
    /// ]]></code>
    /// </remarks>
    CodeDoNotInjectDeclarationSyntax ParseCodeDoNotInjectDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.DonotinjectKeyword);
        var close   = EatCloseBracket();

        var node = new CodeDoNotInjectDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeAbstractMethodDeclaration</c> → <see cref="CodeAbstractMethodDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeAbstractMethodDeclaration ::= "[" "abstractmethod" "]"
    /// ]]></code>
    /// </remarks>
    CodeAbstractMethodDeclarationSyntax ParseCodeAbstractMethodDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.AbstractmethodKeyword);
        var close   = EatCloseBracket();

        var node = new CodeAbstractMethodDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeDeclaration</c> → <see cref="CodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeDeclaration ::= "[" "code" StringLiteral* "]"
    /// ]]></code>
    /// </remarks>
    CodeDeclarationSyntax ParseCodeDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.CodeKeyword);

        var literals = new List<RawToken>();
        while (TryEat(SyntaxTokenType.StringLiteral, out var literal)) {
            literals.Add(literal);
        }

        var close = EatCloseBracket();

        var span = new ExtentBuilder();
        span.Add(open); span.Add(keyword); span.AddRange(literals); span.Add(close);

        var node = new CodeDeclarationSyntax(span.ToExtent());

        Tok(node, open, TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        foreach (var literal in literals) {
            Tok(node, literal, TextClassification.Text);
        }

        Tok(node, close, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeBaseDeclaration</c> → <see cref="CodeBaseDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeBaseDeclaration ::= "[" "base" codeType ( ":" codeType ( "," codeType )? )? "]"
    /// ]]></code>
    /// </remarks>
    CodeBaseDeclarationSyntax ParseCodeBaseDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.BaseKeyword);

        var baseTypes = new List<CodeTypeSyntax> { ParseCodeType() };

        RawToken? colon = null;
        RawToken? comma = null;
        if (At(SyntaxTokenType.Colon)) {
            colon = Eat(SyntaxTokenType.Colon);
            baseTypes.Add(ParseCodeType());

            if (At(SyntaxTokenType.Comma)) {
                comma = Eat(SyntaxTokenType.Comma);
                baseTypes.Add(ParseCodeType());
            }
        }

        var close = EatCloseBracket();

        var span = new ExtentBuilder();
        span.Add(open); span.Add(keyword); span.AddRange(baseTypes); span.Add(colon); span.Add(comma); span.Add(close);

        var node = new CodeBaseDeclarationSyntax(span.ToExtent(), baseTypes);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, colon,   TextClassification.Punctuation);
        Tok(node, comma,   TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeGenerateToDeclaration</c> → <see cref="CodeGenerateToDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeGenerateToDeclaration ::= "[" "generateto" StringLiteral "]"
    /// ]]></code>
    /// </remarks>
    CodeGenerateToDeclarationSyntax ParseCodeGenerateToDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.GeneratetoKeyword);
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var close   = EatCloseBracket();

        var node = new CodeGenerateToDeclarationSyntax(Span(open, keyword, literal, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, literal, TextClassification.StringLiteral);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeParamsDeclaration</c> → <see cref="CodeParamsDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeParamsDeclaration ::= "[" "params" parameterList? "]"
    /// ]]></code>
    /// </remarks>
    CodeParamsDeclarationSyntax ParseCodeParamsDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = EatKeywordOrSkip(SyntaxTokenType.ParamsKeyword);

        var parameterList = At(SyntaxTokenType.Identifier) ? ParseParameterList() : null;

        var close = EatCloseBracket();

        var node = new CodeParamsDeclarationSyntax(Span(open, keyword, parameterList, close), parameterList);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeResultDeclaration</c> → <see cref="CodeResultDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeResultDeclaration ::= "[" "result" parameter "]"
    /// ]]></code>
    /// </remarks>
    CodeResultDeclarationSyntax ParseCodeResultDeclaration() {

        var open      = Eat(SyntaxTokenType.OpenBracket);
        var keyword   = EatKeywordOrSkip(SyntaxTokenType.ResultKeyword);
        var parameter = ParseParameter();
        var close     = EatCloseBracket();

        var node = new CodeResultDeclarationSyntax(Span(open, keyword, parameter, close), parameter);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>
    /// Überspringt eine Folge von <c>[</c>-Klammern an einer Code-Deklarations-Position, die keiner
    /// bekannten <c>[keyword …]</c>-Deklaration entsprechen — jede als eigene Fehlerproduktion mit einer
    /// Diagnose. Anders als beim <see cref="ParseCodeDeclarations"/>-Wirt gibt es hier (Top-Level) kein
    /// mechanisch fehlendes <c>;</c> zu unterdrücken, weshalb kein „übersprungen"-Ergebnis zurückfließt.
    /// </summary>
    /// <param name="host">
    /// Der Wirt der Klammer — er bestimmt über <see cref="CodeBlockFacts.VisibleDeclarationKeywords"/> die
    /// an dieser Stelle gültigen <c>[keyword …]</c>-Schlüsselwörter. Für ein <b>leeres</b> <c>[]</c> — bei
    /// dem die Klammer hierher gehört und nur ihr Inhalt fehlt — werden sie zur Diagnose
    /// <c>expected 'a', 'b' or 'c'</c> statt des irreführenden <c>unexpected input '[]'</c>.
    /// </param>
    void SkipMalformedBrackets(CodeBlockHost host) {

        while (At(SyntaxTokenType.OpenBracket)) {
            ParseMalformedBracketDeclaration(host);
        }
    }

    /// <summary>
    /// Treibt die (in fester Grammatik-Reihenfolge notierten) <c>[keyword …]</c>-Code-Deklarationen eines
    /// Wirts an und verschränkt sie mit der Klammer-Recovery. Anders als ein <em>einmaliger</em> Durchlauf
    /// mit anschließendem <see cref="SkipMalformedBrackets"/> wird hier jede Klammer <em>einzeln</em>
    /// betrachtet: nach dem Überspringen einer fehlerhaften Klammer werden die noch offenen Deklarationen
    /// erneut angeboten. So „verschluckt" ein vorangestelltes malformes <c>[]</c> keine nachfolgende, gültige
    /// Deklaration mehr (<c>task A [] [code …]</c> parst <c>[code …]</c> weiterhin als Code-Deklaration statt
    /// es als Fehlerproduktion mitzunehmen). Die Reihenfolge-Grammatik bleibt gewahrt — jede Deklaration wird
    /// nur einmal geparst (Null-Wächter im Delegaten), eine echt deplatzierte oder doppelte Klammer wird
    /// weiterhin als malform übersprungen.
    /// <paramref name="tryParseNextDeclaration"/> parst die nächste an der aktuellen Position passende, noch
    /// offene Deklaration und meldet, ob sie eine konsumiert hat. Rückgabe: ob mindestens eine Klammer als
    /// malform übersprungen wurde (der Aufrufer unterdrückt dann das mechanisch fehlende <c>;</c>).
    /// </summary>
    bool ParseCodeDeclarations(CodeBlockHost host, Func<bool> tryParseNextDeclaration) {

        var skipped = false;
        while (true) {

            // Alle an der aktuellen Position passenden, noch offenen Deklarationen greifen.
            while (tryParseNextDeclaration()) {
            }

            if (!At(SyntaxTokenType.OpenBracket)) {
                break;
            }

            // Die hier nicht zuzuordnende Klammer als Fehlerproduktion verschlucken und die noch offenen
            // Deklarationen danach erneut anbieten — die nächste Klammer kann eine gültige sein.
            ParseMalformedBracketDeclaration(host);
            skipped = true;
        }

        return skipped;
    }

    /// <summary>
    /// Fehlerproduktion für ein <c>[</c> an einer Code-Deklarations-Position, das keiner bekannten
    /// <c>[keyword …]</c>-Deklaration entspricht (leeres <c>[]</c>, <c>[foo]</c>, beim Tippen noch
    /// unfertiges <c>[par</c>). Meldet genau eine Diagnose über die ganze Klammer und resynchronisiert
    /// bis zum schließenden <c>]</c> bzw. einem harten Anker (<see cref="ClosesBracketRegion"/>) — der
    /// Schaden bleibt auf die Klammer beschränkt. Die übersprungenen Token gehen nicht verloren: sie
    /// werden anschließend zu einer strukturierten <see cref="SyntaxTokenType.SkippedTokensTrivia"/> gefaltet.
    /// </summary>
    /// <remarks>
    /// Bei einem <b>leeren</b> <c>[]</c> (öffnende Klammer direkt gefolgt von der schließenden) lautet die
    /// Diagnose <c>expected …</c> mit den im <paramref name="host"/> gültigen Schlüsselwörtern: die Klammer
    /// ist an dieser Stelle vorgesehen, nur ihr Schlüsselwort fehlt. Enthält die Klammer dagegen (ungültigen)
    /// Inhalt, bleibt es beim treffenderen <c>unexpected input '…'</c>.
    /// </remarks>
    void ParseMalformedBracketDeclaration(CodeBlockHost host) {

        var start    = CurrentStart;
        var expected = CodeBlockFacts.VisibleDeclarationKeywords(host);

        // Leeres '[]' an einer Stelle, an der eine [keyword …]-Deklaration erwartet wird: die Klammer
        // gehört hierher, nur ihr Inhalt (das Schlüsselwort) fehlt — daher „expected …" statt
        // „unexpected input '[]'". Vor dem Konsumieren feststellen (danach ist der Cursor weiter).
        var emptyBracket = expected.Length > 0 && PeekType(1) == SyntaxTokenType.CloseBracket;

        // Das '[' und den (ungültigen) Inhalt bis zum ']' oder einem harten Anker überspringen. Das '['
        // selbst ist per Aufrufkontext gesichert, der Rumpf läuft also mindestens einmal (Fortschritt).
        do {
            _firstSignificantStart ??= CurrentStart;
            _pos++;
            SkipHidden();
        } while (!ClosesBracketRegion());

        // Steht ein ']' bereit, gehört es noch zur Klammer und wird mitverschluckt; sonst endet die
        // Diagnose am zuletzt übersprungenen Token (nicht in dessen Trailing-Trivia).
        int end;
        if (At(SyntaxTokenType.CloseBracket)) {
            end = CurrentEnd;
            _pos++;
            SkipHidden();
        } else {
            end = PreviousSignificantEnd() ?? CurrentStart;
        }

        var extent = TextExtent.FromBounds(start, end);
        if (emptyBracket) {
            _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(extent),
                                            DiagnosticDescriptors.NewSyntaxError($"expected {FormatExpectedKeywords(expected)}")));
        } else {
            ReportUnexpected(extent, _sourceText.Substring(extent));
        }
    }

    /// <summary>
    /// Formatiert die erwarteten Schlüsselwörter als gequotete, durch Komma getrennte Aufzählung mit
    /// <c>or</c> vor dem letzten Element — z.B. <c>'code', 'base' or 'params'</c>.
    /// </summary>
    static string FormatExpectedKeywords(ImmutableArray<string> keywords) {

        var sb = new StringBuilder();
        for (var i = 0; i < keywords.Length; i++) {
            if (i > 0) {
                sb.Append(i == keywords.Length - 1 ? " or " : ", ");
            }

            sb.Append('\'').Append(keywords[i]).Append('\'');
        }

        return sb.ToString();
    }

    #endregion

    #region ParameterList / Parameter

    /// <summary>Grammatikregel <c>parameterList</c> → <see cref="ParameterListSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// parameterList ::= parameter ( "," parameter )*
    /// ]]></code>
    /// </remarks>
    ParameterListSyntax ParseParameterList() {

        var parameters = new List<ParameterSyntax> { ParseParameter() };
        var commas     = new List<RawToken>();

        while (TryEat(SyntaxTokenType.Comma, out var comma)) {
            commas.Add(comma);
            parameters.Add(ParseParameter());
        }

        var span = new ExtentBuilder();
        span.AddRange(parameters); span.AddRange(commas);

        var node = new ParameterListSyntax(span.ToExtent(), parameters);

        foreach (var comma in commas) {
            Tok(node, comma, TextClassification.Punctuation);
        }

        return node;
    }

    /// <summary>Grammatikregel <c>parameter</c> → <see cref="ParameterSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// parameter ::= codeType Identifier?
    /// ]]></code>
    /// Das optionale <c>Identifier</c> ist der Parametername.
    /// </remarks>
    ParameterSyntax ParseParameter() {

        var type = ParseCodeType();
        var name = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        var node = new ParameterSyntax(Span(type, name), type);

        Tok(node, name, TextClassification.ParameterName);

        return node;
    }

    #endregion

    #region CodeType

    /// <summary>Grammatikregel <c>codeType</c> → <see cref="CodeTypeSyntax"/> (deckt auch <c>arrayType</c> ab).</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeType   ::= simpleType
    ///              | genericType
    ///              | arrayType
    /// arrayType  ::= ( simpleType | genericType ) arrayRankSpecifier+
    /// ]]></code>
    /// Die Regel <c>arrayType</c> ist hier eingefaltet: zuerst wird der Basistyp geparst (<c>Identifier</c> +
    /// <c>&lt;</c> ⇒ <c>genericType</c>, sonst <c>simpleType</c>); folgt ein <c>[</c> <c>]</c>, entsteht ein
    /// <see cref="ArrayTypeSyntax"/> mit einem oder mehreren <c>arrayRankSpecifier</c>.
    /// </remarks>
    CodeTypeSyntax ParseCodeType() {

        CodeTypeSyntax baseType = At(SyntaxTokenType.Identifier) && PeekType(1) == SyntaxTokenType.LessThan
            ? ParseGenericType()
            : ParseSimpleType();

        if (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.CloseBracket) {
            var rankSpecifiers = new List<ArrayRankSpecifierSyntax>();
            var span           = new ExtentBuilder();
            span.Add(baseType);

            while (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.CloseBracket) {
                var rank = ParseArrayRankSpecifier();
                rankSpecifiers.Add(rank);
                span.Add(rank);
            }

            return new ArrayTypeSyntax(span.ToExtent(), baseType, rankSpecifiers);
        }

        return baseType;
    }

    /// <summary>Grammatikregel <c>simpleType</c> → <see cref="SimpleTypeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// simpleType ::= Identifier "?"?
    /// ]]></code>
    /// Das optionale <c>?</c> markiert einen Nullable-Typ.
    /// </remarks>
    SimpleTypeSyntax ParseSimpleType() {

        var identifier   = Eat(SyntaxTokenType.Identifier);
        var questionmark = At(SyntaxTokenType.Questionmark) ? Eat(SyntaxTokenType.Questionmark) : null;

        var node = new SimpleTypeSyntax(Span(identifier, questionmark));

        Tok(node, identifier,   TextClassification.TypeName);
        Tok(node, questionmark, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>genericType</c> → <see cref="GenericTypeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// genericType ::= Identifier "<" codeType ( "," codeType )* ">"
    /// ]]></code>
    /// </remarks>
    GenericTypeSyntax ParseGenericType() {

        var identifier = Eat(SyntaxTokenType.Identifier);
        var lessThan   = Eat(SyntaxTokenType.LessThan);

        var arguments = new List<CodeTypeSyntax> { ParseCodeType() };
        var commas    = new List<RawToken>();

        while (TryEat(SyntaxTokenType.Comma, out var comma)) {
            commas.Add(comma);
            arguments.Add(ParseCodeType());
        }

        var greaterThan = Eat(SyntaxTokenType.GreaterThan);

        var span = new ExtentBuilder();
        span.Add(identifier); span.Add(lessThan); span.AddRange(arguments); span.AddRange(commas); span.Add(greaterThan);

        var node = new GenericTypeSyntax(span.ToExtent(), arguments);

        Tok(node, identifier, TextClassification.TypeName);
        Tok(node, lessThan,   TextClassification.Punctuation);
        foreach (var comma in commas) {
            Tok(node, comma, TextClassification.Punctuation);
        }

        Tok(node, greaterThan, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>arrayRankSpecifier</c> → <see cref="ArrayRankSpecifierSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// arrayRankSpecifier ::= "[" "]"
    /// ]]></code>
    /// </remarks>
    ArrayRankSpecifierSyntax ParseArrayRankSpecifier() {

        var open  = Eat(SyntaxTokenType.OpenBracket);
        var close = Eat(SyntaxTokenType.CloseBracket);

        var node = new ArrayRankSpecifierSyntax(Span(open, close));

        Tok(node, open,  TextClassification.Punctuation);
        Tok(node, close, TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IdentifierOrString

    /// <summary>Grammatikregel <c>identifierOrString</c> → <see cref="IdentifierOrStringSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierOrString ::= identifier      (* Identifier *)
    ///                      | stringLiteral   (* StringLiteral *)
    /// ]]></code>
    /// Steht weder ein <c>Identifier</c> noch ein <c>StringLiteral</c> an, liefert die Methode <c>null</c>
    /// (der aufrufende Knoten entsteht dann ohne diesen Bestandteil).
    /// </remarks>
    IdentifierOrStringSyntax? ParseIdentifierOrString() {
        if (At(SyntaxTokenType.Identifier)) {
            return ParseIdentifier();
        }

        if (At(SyntaxTokenType.StringLiteral)) {
            return ParseStringLiteral();
        }

        return null;
    }

    /// <summary>Grammatikregel <c>identifier</c> → <see cref="IdentifierSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifier ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierSyntax ParseIdentifier() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    /// <summary>Grammatikregel <c>stringLiteral</c> → <see cref="StringLiteralSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// stringLiteral ::= StringLiteral
    /// ]]></code>
    /// </remarks>
    StringLiteralSyntax ParseStringLiteral() {
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var node    = new StringLiteralSyntax(Span(literal));
        Tok(node, literal, TextClassification.StringLiteral);
        return node;
    }

    #endregion

    #region Token-Strom: Cursor, Konsum, Trivia-Anhang

    /// <summary>Typ des aktuellen Tokens; <see cref="SyntaxTokenType.EndOfFile"/> hinter dem Strom-Ende.</summary>
    SyntaxTokenType At0 => _pos < _raw.Length ? _raw[_pos].Type : SyntaxTokenType.EndOfFile;

    /// <summary>Ob das aktuelle Token vom Typ <paramref name="type"/> ist.</summary>
    bool At(SyntaxTokenType type) => At0 == type;

    /// <summary>Ob der Cursor das Dateiende erreicht hat.</summary>
    bool AtEof => At0 == SyntaxTokenType.EndOfFile;

    /// <summary>
    /// Ob an der aktuellen Position eine <c>[keyword …]</c>-Code-Deklaration mit dem erwarteten
    /// <paramref name="keyword"/> beginnt: ein <c>[</c>, gefolgt vom Keyword selbst — <b>oder</b> (Präfix-
    /// Rescue beim Tippen) von einem Identifier, dessen Text ein <b>echtes Präfix</b> des Keywords ist
    /// (<c>[namespace …]</c> für <c>namespaceprefix</c>, <c>[usin …]</c> für <c>using</c>). Nach einem
    /// <c>[</c> an einer Code-Deklarations-Position ist ein Identifier strukturell nie gültig; er kann daher
    /// gefahrlos als das gemeinte, noch unvollständige Keyword gedeutet werden. Die zugehörige
    /// <c>ParseCode*Declaration</c> konsumiert es dann über <see cref="EatKeywordOrSkip"/> (Missing-Keyword +
    /// Skip-Trivia). Steht statt eines Identifiers das echte Keyword einer <b>anderen</b> Deklaration, ist es
    /// ein Keyword-Token (kein Identifier) → der Präfix-Zweig greift nicht.
    /// </summary>
    bool AtCodeDeclaration(SyntaxTokenType keyword) {

        if (!At(SyntaxTokenType.OpenBracket)) {
            return false;
        }

        if (PeekType(1) == keyword) {
            return true;
        }

        return PeekType(1) == SyntaxTokenType.Identifier && IsKeywordPrefix(keyword, PeekText(1));
    }

    /// <summary>
    /// Ob <paramref name="text"/> ein <b>echtes</b> Präfix des kanonischen Literals von
    /// <paramref name="keyword"/> ist — nicht leer, kürzer als das Keyword und dessen Anfang (Ordinal). Das
    /// vollständige Keyword ist kein Präfix seiner selbst (es lext ohnehin als Keyword-Token, nicht als
    /// Identifier). Für Nicht-Keyword-Typen (kein kanonisches Literal) stets <c>false</c>.
    /// </summary>
    static bool IsKeywordPrefix(SyntaxTokenType keyword, string? text) {

        var keywordText = SyntaxFacts.GetKeywordText(keyword);

        return keywordText       != null &&
               text is { Length: > 0 }    &&
               text.Length < keywordText.Length &&
               keywordText.StartsWith(text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ob an der aktuellen Top-Level-Position ein Member beginnt, dessen Leit-Schlüsselwort beim Tippen
    /// noch unvollständig ist (als Identifier gelext) — ein Identifier, dessen Text ein <b>echtes Präfix</b>
    /// von <c>task</c> oder <c>taskref</c> ist (<c>tas SimpleTask …</c>). Auf Top-Level beginnt ein Member
    /// nur mit <c>task</c>/<c>taskref</c>; ein Identifier ist dort strukturell nie gültig und kann daher als
    /// das gemeinte, noch unvollständige Schlüsselwort gedeutet werden. Die <b>Form-Bestätigung</b>
    /// (dem Identifier folgt ein weiterer Identifier — der Task-Name) hält einen losen Top-Level-Identifier
    /// ohne folgenden Namen aus dem Rescue heraus; der bleibt dem Panic-Mode (<see cref="_atMemberOrEof"/>)
    /// überlassen. Die Tie-Break-/Konsum-Semantik liegt in <see cref="ParseMemberDeclaration"/> bzw.
    /// <see cref="EatKeywordOrSkip"/>.
    /// </summary>
    bool AtMemberKeywordPrefix() {

        if (!At(SyntaxTokenType.Identifier) || PeekType(1) != SyntaxTokenType.Identifier) {
            return false;
        }

        var text = PeekText(0);

        return IsKeywordPrefix(SyntaxTokenType.TaskKeyword,    text) ||
               IsKeywordPrefix(SyntaxTokenType.TaskrefKeyword, text);
    }

    /// <summary>Ob an der aktuellen Position ein Connection-Point einer <c>taskref</c>-Deklaration beginnt (<c>init</c>, <c>exit</c> oder <c>end</c>).</summary>
    bool StartsConnectionPoint() {
        return At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.ExitKeyword) || At(SyntaxTokenType.EndKeyword);
    }

    /// <summary>
    /// Äußere Anker eines Task-Körpers: das schließende <c>}</c> sowie der Beginn eines neuen Top-Level-
    /// Members (<c>task</c>/<c>taskref</c>) und das Dateiende. An diesen Token bricht lokale Recovery ab und
    /// überlässt das Token der äußeren Regel (gestaffelte Sync-Sets), statt die äußere Struktur mitzureißen.
    /// </summary>
    bool BreaksBody() {
        return At(SyntaxTokenType.CloseBrace)    ||
               At(SyntaxTokenType.TaskKeyword)   ||
               At(SyntaxTokenType.TaskrefKeyword) ||
               AtEof;
    }

    /// <summary>
    /// Anker, an denen die Recovery <b>innerhalb</b> einer eckigen Klammer abbricht: das schließende
    /// <c>]</c> selbst sowie harte äußere Anker (<c>;</c>, Body-<c>{</c>, Knoten-Start und die
    /// äußeren Body-Anker von <see cref="BreaksBody"/>). So bleibt der Schaden einer unvollständigen
    /// <c>[ … ]</c>-Deklaration auf die Klammer beschränkt, statt in die folgenden Deklarationen
    /// auszubluten (das Klammer-Pendant zu <see cref="TargetStartsNextTransition"/> bei Transitionen).
    /// <para/>
    /// Zusätzlicher Zeilen-Anker: beginnt eine <b>neue Zeile</b> mit einem <c>[</c>, gehört dieses zu
    /// einer neuen <c>[ … ]</c>-Deklaration — der laufenden Klammer fehlt also nur das <c>]</c>. Statt
    /// über die Zeilengrenze in die (für sich korrekte) nächste Deklaration hineinzulaufen (dort dann
    /// irreführend „unexpected input '['"), bricht die Recovery hier ab; <see cref="EatCloseBracket"/>
    /// meldet danach das treffende „missing ']'" am Ende der laufenden Zeile. Innerhalb einer Klammer
    /// steht auf einer Folgezeile nie legitim ein <c>[</c> (mehrzeilige Inhalte wie <c>[code …]</c>-
    /// Literale sind bereits konsumiert, bevor das <c>]</c> gesucht wird).
    /// </summary>
    bool ClosesBracketRegion() {
        return At(SyntaxTokenType.CloseBracket)          ||
               At(SyntaxTokenType.Semicolon)             ||
               At(SyntaxTokenType.OpenBrace)             ||
               StartsNodeDeclaration()                   ||
               BreaksBody()                              ||
               (OnNewLine() && At(SyntaxTokenType.OpenBracket));
    }

    /// <summary>Typ des n-ten parser-sichtbaren Tokens ab der aktuellen Position (Trivia übersprungen).</summary>
    SyntaxTokenType PeekType(int n) {
        var index = _pos;
        var seen  = 0;
        while (index < _raw.Length) {
            if (!IsHidden(_raw[index].Type)) {
                if (seen == n) {
                    return _raw[index].Type;
                }

                seen++;
            }

            index++;
        }

        return SyntaxTokenType.EndOfFile;
    }

    /// <summary>Das n-te parser-sichtbare Token ab der aktuellen Position (Trivia übersprungen) — <c>null</c> jenseits des Stroms.</summary>
    RawToken? PeekRaw(int n) {
        var index = _pos;
        var seen  = 0;
        while (index < _raw.Length) {
            if (!IsHidden(_raw[index].Type)) {
                if (seen == n) {
                    return _raw[index];
                }

                seen++;
            }

            index++;
        }

        return null;
    }

    /// <summary>Der Quelltext des n-ten parser-sichtbaren Tokens — <c>null</c>, wenn keins existiert.</summary>
    string? PeekText(int n) {
        return PeekRaw(n) is { } token ? _sourceText.Substring(token.Extent) : null;
    }

    /// <summary>
    /// Konsumiert das aktuelle Token, wenn es <paramref name="type"/> entspricht, und rückt auf das nächste
    /// sichtbare Token vor. Andernfalls (Insertion-Recovery) wird ein nullbreites Missing-Token
    /// synthetisiert: es kommt <i>nicht</i> in den Token-Strom (<c>null</c>-Rückgabe trägt nichts zum
    /// Extent bei), es wird eine Diagnose an der Einfügestelle gemeldet, und der Cursor rückt
    /// <b>nicht</b> vor. Alle optionalen Aufrufstellen sind durch ein vorheriges <see cref="At"/>
    /// abgesichert und erreichen den Fehlerzweig daher nie — so meldet nur ein wirklich fehlendes
    /// Pflicht-Token.
    /// </summary>
    RawToken? Eat(SyntaxTokenType type) {
        if (At0 != type) {
            ReportMissing(Describe(type));
            return null;
        }

        var token = _raw[_pos];
        _firstSignificantStart ??= token.Start;

        _pos++;
        SkipHidden();

        return token;
    }

    /// <summary>
    /// Konsumiert das führende Schlüsselwort einer <c>[keyword …]</c>-Code-Deklaration. Steht das
    /// erwartete Keyword an, verhält sich die Methode wie <see cref="Eat"/>. Andernfalls hat der Dispatch
    /// (<see cref="AtCodeDeclaration"/>) die Klammer bereits per <b>echtem Keyword-Präfix</b> dieser
    /// Deklaration zugeordnet — an dieser Position steht also ein beim Tippen noch unvollständiges Keyword,
    /// das als Identifier lext (<c>namespace</c> vor <c>namespaceprefix</c>, <c>usin</c> vor <c>using</c>).
    /// Statt das Keyword still zu synthetisieren <b>und</b> das Störtoken anschließend als Namen zu
    /// verschlucken, wird ein nullbreites Missing-Keyword gemeldet (eine Diagnose, an der Einfügestelle
    /// verankert) und das Störtoken übersprungen: nicht via <c>Tok(…)</c> angehängt, faltet es sich in
    /// <see cref="FinalizeTrivia"/> zu <see cref="SyntaxTokenType.SkippedTokensTrivia"/> — der Round-Trip
    /// bleibt vollständig. So parst der Rest der Deklaration (Name, <c>]</c>) normal weiter.
    /// </summary>
    RawToken? EatKeywordOrSkip(SyntaxTokenType keyword) {

        if (At(keyword)) {
            return Eat(keyword);
        }

        ReportMissing(Describe(keyword));

        // Das verunglückte Keyword (als Identifier gelext) überspringen — der Skip muss geschehen, sonst
        // konsumiert der folgende Name-Parse (ParseIdentifierOrString) das Störtoken als Namen.
        if (At(SyntaxTokenType.Identifier)) {
            _firstSignificantStart ??= CurrentStart;
            _pos++;
            SkipHidden();
        }

        return null;
    }

    /// <summary>
    /// Konsumiert das schließende <c>]</c> einer <c>[ … ]</c>-Code-Deklaration. Zuvor überspringt ein
    /// gezielter Panic-Mode überzähligen bzw. ungültigen Klammerinhalt bis zum <c>]</c> oder einem
    /// harten Anker (<see cref="ClosesBracketRegion"/>) — eine Diagnose an der Divergenzstelle. So blutet
    /// ein unvollständiges <c>[ … ]</c> (etwa ein beim Tippen noch nicht geschlossenes <c>[params …</c>)
    /// nicht in die folgenden Deklarationen aus, statt dass <see cref="Eat"/> das <c>]</c> still
    /// synthetisiert und die Folgetoken downstream als Kaskade auflaufen.
    /// </summary>
    RawToken? EatCloseBracket() {
        Recover(_closesBracketRegion);
        return Eat(SyntaxTokenType.CloseBracket);
    }

    /// <summary>
    /// Konsumiert das aktuelle Token, wenn es <paramref name="type"/> entspricht, gibt es über
    /// <paramref name="token"/> zurück und rückt vor (Ergebnis <c>true</c>). Andernfalls bleibt der Cursor
    /// stehen und das Ergebnis ist <c>false</c> — <b>ohne</b> Diagnose. Das ist die nicht-nullbare,
    /// konsumierende Form von <see cref="At(SyntaxTokenType)"/> für Listen-Schleifen (z.B. komma-getrennte
    /// Aufzählungen): es gibt hier kein Pflicht-Token, daher auch kein Missing-Token wie bei <see cref="Eat"/>.
    /// </summary>
    bool TryEat(SyntaxTokenType type, out RawToken token) {
        if (At0 != type) {
            token = default;
            return false;
        }

        token = _raw[_pos];
        _firstSignificantStart ??= token.Start;

        _pos++;
        SkipHidden();

        return true;
    }

    /// <summary>
    /// Konsumiert ein <c>;</c>, falls vorhanden, und gibt es zurück — andernfalls <c>null</c> <b>ohne</b>
    /// Diagnose. Genutzt, wenn eine Transition an der Zeilengrenze abgebrochen wurde: die treffende
    /// Diagnose (fehlende Kante bzw. fehlender Zielknoten) ist bereits gemeldet; das mechanisch ebenfalls
    /// fehlende <c>;</c> wäre nur ein Folgefehler und wird unterdrückt (eine Diagnose pro Divergenzstelle).
    /// </summary>
    RawToken? TryEatSemicolonQuiet() {
        return TryEat(SyntaxTokenType.Semicolon, out var semi) ? semi : null;
    }

    /// <summary>
    /// Rückt den Cursor über alle versteckten Token (<see cref="IsHidden"/>) hinweg und stellt so die
    /// Cursor-Invariante wieder her: <c>_pos</c> zeigt stets auf ein parser-sichtbares Token (signifikant
    /// oder <see cref="SyntaxTokenType.EndOfFile"/>). Wird nach jedem Vorrücken aufgerufen (Konsum,
    /// Recovery-Skips) — die <c>Parse*</c>-Methoden sehen versteckte Token dadurch nie.
    /// </summary>
    void SkipHidden() {
        while (_pos < _raw.Length && IsHidden(_raw[_pos].Type)) {
            _pos++;
        }
    }

    /// <summary>
    /// Panic-Mode (Deletion-Recovery): überspringt das aktuelle und alle folgenden signifikanten Token, bis
    /// <paramref name="recovered"/> zutrifft — also ein Wiedereinstiegs- oder äußeres Anker-Token (inkl.
    /// Dateiende) erreicht ist. Die übersprungenen Token werden hier nur überlesen; zu strukturierter
    /// <see cref="SyntaxTokenType.SkippedTokensTrivia"/> gefaltet werden sie erst in <see cref="FinalizeTrivia"/>
    /// (nichts geht verloren, der Round-Trip bleibt vollständig). Pro Lauf wird genau eine Diagnose an der
    /// ersten übersprungenen Stelle gemeldet. Fortschritts-Garantie: trifft <paramref name="recovered"/> nicht
    /// schon zu Beginn zu, rückt der Aufruf um mindestens ein signifikantes Token vor — Listen-Schleifen können
    /// also nicht endlos drehen.
    /// </summary>
    void Recover(Func<bool> recovered) {
        if (recovered()) {
            return;
        }

        ReportUnexpected(TextExtent.FromBounds(CurrentStart, CurrentEnd), CurrentText);

        do {
            _firstSignificantStart ??= CurrentStart;
            _pos++;
            SkipHidden();
        } while (!recovered());
    }

    /// <summary>Startoffset des aktuellen Tokens; hinter dem Strom-Ende die (nullbreite) EOF-Position.</summary>
    int CurrentStart => _pos < _raw.Length ? _raw[_pos].Extent.Start : _eofPos;
    /// <summary>Endoffset des aktuellen Tokens; hinter dem Strom-Ende die (nullbreite) EOF-Position.</summary>
    int CurrentEnd   => _pos < _raw.Length ? _raw[_pos].Extent.End   : _eofPos;

    /// <summary>Quelltext des aktuellen Tokens (leer am Dateiende) — für „unexpected input '…'"-Diagnosen.</summary>
    string CurrentText => _sourceText.Substring(TextExtent.FromBounds(CurrentStart, CurrentEnd));

    /// <summary>
    /// Meldet ein fehlendes Pflicht-Token (Insertion) nullbreit am <b>Ende des zuletzt konsumierten
    /// signifikanten Tokens</b> — also direkt hinter dem zuvor Getippten, vor dessen Trailing-Trivia.
    /// Das entspricht der Roslyn-Konvention (»X erwartet« hängt am vorigen Token, nicht am Anfang des
    /// folgenden): ein fehlendes <c>;</c> erscheint am Ende der vorigen Zeile statt vor dem nächsten
    /// Knoten. Gibt es kein vorheriges signifikantes Token (Fehlstelle am Dateianfang), wird nullbreit
    /// an der aktuellen Cursor-Position gemeldet.
    /// <para/>
    /// Am Dateiende wird die <b>Kaskade</b> nach der ersten Meldung abgebrochen: bricht die Eingabe
    /// vorzeitig ab, synthetisiert der Parser beim Aufrollen der Regeln eine Reihe fehlender Pflicht-Token
    /// (z.B. Zielknoten → <c>;</c> → <c>}</c>), die alle nullbreit an derselben EOF-Position landen würden.
    /// Gemeldet wird nur die <b>erste</b> (die den unvollständigen Bau benennt); die mechanischen
    /// Folgefehler werden unterdrückt.
    /// </summary>
    void ReportMissing(string what) {
        if (AtEof) {
            if (_reportedMissingAtEof) {
                return;
            }

            _reportedMissingAtEof = true;
        }

        var position = PreviousSignificantEnd() ?? CurrentStart;
        var at       = TextExtent.FromBounds(position, position);
        _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(at),
                                        DiagnosticDescriptors.NewSyntaxError($"missing {what}")));
    }

    /// <summary>
    /// Liefert das Extent-Ende des letzten signifikanten (nicht versteckten) Tokens vor dem Cursor —
    /// die Stelle unmittelbar hinter dem zuvor konsumierten Token, noch vor dessen Trailing-Trivia.
    /// <c>null</c>, wenn vor dem Cursor kein signifikantes Token liegt.
    /// </summary>
    int? PreviousSignificantEnd() {
        for (var i = _pos - 1; i >= 0; i--) {
            if (!IsHidden(_raw[i].Type)) {
                return _raw[i].Extent.End;
            }
        }

        return null;
    }

    /// <summary>
    /// True, wenn zwischen dem zuletzt konsumierten signifikanten Token und dem aktuellen Cursor ein
    /// Zeilenumbruch liegt — das aktuelle (sichtbare) Token also auf einer neuen Zeile beginnt.
    /// </summary>
    bool OnNewLine() {
        for (var i = _pos - 1; i >= 0; i--) {
            if (_raw[i].Type == SyntaxTokenType.NewLine) {
                return true;
            }

            if (!IsHidden(_raw[i].Type)) {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// True, wenn das aktuelle Token (ein Zielknoten-Kandidat) auf einer neuen Zeile beginnt und das
    /// darauffolgende Token eine neue Transition einleitet — eine Kante oder ein Doppelpunkt (Exit-
    /// Transition). Dann gehört der Kandidat zur nächsten Transition und darf nicht als Ziel der
    /// laufenden Transition geschluckt werden (sonst „blutet" eine unvollständige Transition über die
    /// Zeilengrenze in die nächste, für sich genommen korrekte Zeile).
    /// </summary>
    bool TargetStartsNextTransition() {
        return OnNewLine() && (SyntaxFacts.IsEdgeKeyword(PeekType(1)) || PeekType(1) == SyntaxTokenType.Colon);
    }

    /// <summary>Meldet ein unerwartetes (übersprungenes) Token (Deletion) über dessen Extent.</summary>
    void ReportUnexpected(TextExtent extent, string text) {
        _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(extent),
                                        DiagnosticDescriptors.NewSyntaxError($"unexpected input '{text}'")));
    }

    /// <summary>
    /// Lesbare Bezeichnung eines erwarteten Tokens für „missing …"-Diagnosen: der kanonische Text —
    /// Punctuation über <see cref="SyntaxFacts.GetText"/>, Keywords über <see cref="SyntaxFacts.GetKeywordText"/>
    /// (Autorität für beide Literal-Familien) — in Quotes, sonst ein Kategorie-Wort für die kategorischen
    /// Terminale.
    /// </summary>
    static string Describe(SyntaxTokenType type) {
        var text = SyntaxFacts.GetText(type) ?? SyntaxFacts.GetKeywordText(type);
        if (text != null) {
            return $"'{text}'";
        }

        switch (type) {
            case SyntaxTokenType.Identifier:    return "identifier";
            case SyntaxTokenType.StringLiteral: return "string literal";
            default:                            return $"'{type}'";
        }
    }

    /// <summary>
    /// Baut — nach dem Parsen, wenn feststeht, welche signifikanten Token konsumiert wurden — die endgültige
    /// Trivia der Datei und setzt sie per Finalisierungs-Pass an die bereits erzeugten Token. Der Pass ändert
    /// weder Baumstruktur noch Token-Identitäten (Parent, Extent, Typ, Klassifikation — die Gleichheits-
    /// Semantik von <see cref="SyntaxToken"/> klammert die Trivia ohnehin aus), nur die Trivia-Sichten.
    /// Nicht konsumierte signifikante Token und unbekannte Zeichen falten sich dabei je Lauf zu einer
    /// strukturierten <see cref="SyntaxTokenType.SkippedTokensTrivia"/> (siehe <see cref="BuildTrivia"/>).
    /// </summary>
    void FinalizeTrivia() {

        // Vom Parser an Knoten gehängte (signifikante) Token — anhand ihrer eindeutigen Start-Position.
        // Token überlappen nie, daher ist die Start-Position ein sicherer Identitätsschlüssel. (Eine
        // Vorab-Kapazität ist hier nicht möglich: der HashSet<T>(int)-Ctor fehlt auf netstandard2.0 — der
        // Ziel-TFM dieser Assembly.)
        var consumedStarts = new HashSet<int>();
        foreach (var token in _tokens) {
            consumedStarts.Add(token.Start);
        }

        _allTrivia        = BuildTrivia(consumedStarts, out _tokenTrivia, out var eofStart, out var eofLength);
        _eofLeadingTrivia = new SyntaxTriviaList(_allTrivia, eofStart, eofLength);

        for (var i = 0; i < _tokens.Count; i++) {
            var token = _tokens[i];
            var (leading, trailing) = LookupTrivia(token.Start);
            _tokens[i] = SyntaxTokenFactory.CreateToken(token.Extent, token.Type, token.Classification, token.Parent,
                                                        leading, trailing);
        }
    }

    /// <summary>
    /// Hängt — nach dem Aufbau des Wurzelknotens — das abschließende <see cref="SyntaxTokenType.EndOfFile"/>
    /// an die Wurzel; es trägt die finale Datei-Trivia als seine Leading-Trivia (siehe
    /// <see cref="FinalizeTrivia"/>, das zuvor gelaufen sein muss). Alle übrigen nicht konsumierten Token
    /// stehen nicht mehr im flachen Strom: Präprozessor-Token sind zu strukturierter
    /// <see cref="SyntaxTokenType.DirectiveTrivia"/>, übersprungene signifikante Token und unbekannte
    /// Zeichen zu strukturierter <see cref="SyntaxTokenType.SkippedTokensTrivia"/> gefaltet.
    /// </summary>
    void AttachEndOfFile(SyntaxNode root) {
        var eof = _raw[_raw.Length - 1]; // Der Lexer terminiert den Strom stets mit dem nullbreiten EndOfFile.
        _tokens.Add(SyntaxTokenFactory.CreateToken(eof.Extent, eof.Type, TextClassification.Whitespace, root,
                                                   _eofLeadingTrivia, SyntaxTriviaList.Empty));
    }

    /// <summary>
    /// Übergibt die konsumierten Token als flachen, nach Position sortierten Strom an den
    /// <see cref="SyntaxTree"/>: die Liste wird in-place sortiert und direkt angehängt — ohne die Kopie,
    /// die der öffentliche <see cref="SyntaxTokenList"/>-Konstruktor anlegen würde (der Parser fasst die
    /// Liste danach nicht mehr an). Sortieren ist nötig, weil <see cref="Tok"/> in Knoten-Konstruktions-
    /// Reihenfolge anhängt (Eltern-Token nach den Token ihrer Kindknoten), nicht in Strom-Reihenfolge.
    /// </summary>
    SyntaxTokenList TakeSortedTokens() {
        _tokens.Sort(SyntaxTokenComparer.Default);
        return SyntaxTokenList.AttachSortedTokens(_tokens);
    }

    /// <summary>
    /// Meldet die nur vom Lexer ableitbare Diagnose für ein loses, nicht-signifikantes Token: ein
    /// <see cref="SyntaxTokenType.Unknown"/> als unerwartetes Zeichen (<c>Nav0000</c>). Die
    /// Präprozessor-Diagnose (<c>Nav3000</c>) entsteht dagegen strukturiert im
    /// Direktiven-Vorlauf (siehe <see cref="NavDirectiveParser"/>). Location ist die nullbreite
    /// Start-Position des Tokens.
    /// </summary>
    void ReportLexicalDiagnostics(RawToken raw) {
        if (raw.Type == SyntaxTokenType.Unknown) {
            _diagnostics.Add(new Diagnostic(LexicalLocation(raw.Extent),
                                            DiagnosticDescriptors.Syntax.Nav0000UnexpectedCharacter,
                                            _sourceText.Substring(raw.Extent)));
        }
    }

    /// <summary>
    /// Baut die <see cref="Location"/> für eine lexikalische Diagnose. Anders als
    /// <see cref="SourceText.GetLocation"/> (Zeilen<i>bereich</i>) trägt sie — wie die bisherige
    /// ANTLR-Pipeline für diese Diagnosen — Start- und End-Zeilenposition <b>identisch</b> an der
    /// Startposition des Tokens; im Test-Formatter erscheint sie damit nullbreit.
    /// </summary>
    Location LexicalLocation(TextExtent extent) {
        var line         = _sourceText.GetTextLineAtPosition(extent.Start);
        var linePosition = new LinePosition(line.Line, extent.Start - line.Start);
        return new Location(extent, linePosition, _sourceText.FileInfo?.FullName);
    }

    /// <summary>
    /// Hängt ein konsumiertes Token mit seiner kontextabhängigen <see cref="TextClassification"/> und
    /// <paramref name="parent"/> als Parent-Knoten an den flachen Token-Strom. <c>null</c> — ein von
    /// <see cref="Eat"/> gemeldetes Missing-Token — wird ignoriert: Missing-Token stehen nie im Strom.
    /// Die Trivia bleibt zunächst leer; sie wird in <see cref="FinalizeTrivia"/> nachgereicht.
    /// </summary>
    void Tok(SyntaxNode parent, RawToken? raw, TextClassification classification) {
        if (raw == null) {
            return;
        }

        // Zunächst ohne Trivia — die endgültige Trivia steht erst nach dem Parsen fest (welche Token
        // übersprungen wurden) und wird in FinalizeTrivia per Finalisierungs-Pass gesetzt. Missing-Token
        // erreichen diese Stelle nie: Eat liefert für sie null (oben abgefangen), Raw-Token des Lexers
        // haben stets einen echten Extent.
        _tokens.Add(SyntaxTokenFactory.CreateToken(raw.Value.Extent, raw.Value.Type, classification, parent));
    }

    /// <summary>
    /// Leading/Trailing-Trivia eines Tokens (per Start-Position) als allokationsfreie Sicht auf das geteilte
    /// <see cref="_allTrivia"/>; leer, wenn dem Token keine Trivia zugeordnet ist.
    /// </summary>
    (SyntaxTriviaList Leading, SyntaxTriviaList Trailing) LookupTrivia(int start) {
        if (_tokenTrivia.TryGetValue(start, out var range)) {
            return (new SyntaxTriviaList(_allTrivia, range.LeadingStart,  range.LeadingLength),
                    new SyntaxTriviaList(_allTrivia, range.TrailingStart, range.TrailingLength));
        }

        return (SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
    }

    /// <summary>Geteilte leere Direktiv-Map für Dateien ohne Präprozessor-Direktiven (der Regelfall).</summary>
    static readonly Dictionary<int, DirectiveRun> EmptyDirectiveRuns = new();

    /// <summary>Leading-/Trailing-Bereich eines Tokens als Start/Länge in das geteilte <see cref="_allTrivia"/>.</summary>
    readonly struct TriviaRange {

        public TriviaRange(int leadingStart, int leadingLength, int trailingStart, int trailingLength) {
            LeadingStart   = leadingStart;
            LeadingLength  = leadingLength;
            TrailingStart  = trailingStart;
            TrailingLength = trailingLength;
        }

        public int LeadingStart   { get; }
        public int LeadingLength  { get; }
        public int TrailingStart  { get; }
        public int TrailingLength { get; }
    }

    /// <summary>
    /// Ordnet die rein lexikalische Trivia (Whitespace, Zeilenenden, Kommentare) nach der Roslyn-Regel den
    /// signifikanten Token zu — nach dem Parsen, wenn feststeht, welche Token konsumiert wurden
    /// (<paramref name="consumedStarts"/>):
    /// <list type="bullet">
    ///   <item><description><b>Trailing</b> eines Tokens: die anschließende Trivia bis <b>einschließlich</b>
    ///   des ersten Zeilenendes; folgt vor einem Zeilenende bereits das nächste (nicht-Trivia-)Token, endet
    ///   die Trailing-Trivia dort.</description></item>
    ///   <item><description><b>Leading</b> eines Tokens: die restliche Trivia bis zu ihm — also die kompletten
    ///   nachfolgenden (Leer-/Kommentar-)Zeilen samt der Einrückung seiner eigenen Zeile.</description></item>
    ///   <item><description>Das abschließende <see cref="SyntaxTokenType.EndOfFile"/> erhält die finale
    ///   Datei-Trivia als seine Leading-Trivia (Round-Trip bleibt lückenlos).</description></item>
    /// </list>
    /// Jeder Präprozessor-Direktiv-Lauf (<see cref="_directiveRuns"/>) wird zu genau einem strukturierten
    /// <see cref="SyntaxTokenType.DirectiveTrivia"/>-Stück gefaltet (mit Verweis auf seinen Knoten), gefolgt
    /// von seinem terminierenden <see cref="SyntaxTokenType.NewLine"/>; beides fließt als Trivia in den
    /// umgebenden Lauf und damit an das nächste Token. Analog wird jeder Lauf <b>übersprungener</b> Token
    /// (nicht konsumierte signifikante Token und unbekannte Zeichen) zu genau einem strukturierten
    /// <see cref="SyntaxTokenType.SkippedTokensTrivia"/>-Stück gefaltet (siehe <see cref="FoldSkippedRun"/>).
    /// </summary>
    ImmutableArray<SyntaxTrivia> BuildTrivia(HashSet<int> consumedStarts,
                                             out Dictionary<int, TriviaRange> tokenTrivia,
                                             out int eofLeadingStart, out int eofLeadingLength) {

        // Direktiv-Läufe nach dem Roh-Index ihres '#' — so lässt sich beim Erreichen eines Laufs die ganze
        // Direktivzeile in ein Trivia-Stück falten und die Präprozessor-Token des Laufs überspringen.
        // Ohne Direktiven (der Regelfall) genügt die geteilte leere Map.
        var runByStart = EmptyDirectiveRuns;
        if (_directiveRuns.Count > 0) {
            runByStart = new Dictionary<int, DirectiveRun>(_directiveRuns.Count);
            foreach (var run in _directiveRuns) {
                runByStart[run.RawStart] = run;
            }
        }

        // Alle Trivia der Datei in genau einem Array (Strom-Reihenfolge). Leading/Trailing eines Tokens sind
        // zusammenhängende Teilbereiche darin — sie werden nicht mehr je Token in eigene Arrays kopiert.
        // raw.Length/2 ist eine grobe Vorab-Kapazität (Trivia macht etwa die Hälfte des Stroms aus).
        var all = ImmutableArray.CreateBuilder<SyntaxTrivia>(Math.Max(16, _raw.Length / 2));

        // Genau ein Dictionary Token-Start -> Bereiche. Die Leading-Trivia wird beim Erreichen des Tokens
        // eingetragen, die Trailing-Trivia beim nächsten Trenner per Read-Modify-Write nachgezogen. Die
        // Zahl der Einträge ist exakt die der konsumierten signifikanten Token (je genau ein set im
        // signifikanten Trenner-Arm) — mit _tokens.Count vorab dimensioniert entfällt das wiederholte
        // Rehashen beim Wachsen (im Profil zuvor ein spürbarer Anteil der Parse-Zeit).
        tokenTrivia = new Dictionary<int, TriviaRange>(_tokens.Count);

        var pendingStart       = 0;  // Index in 'all', an dem der aktuelle pending-Lauf (Trivia seit letztem Trenner) beginnt.
        var lastSignificantKey = -1; // Start des letzten Tokens, das noch Trailing-Trivia aufnimmt; -1 = keins.

        eofLeadingStart  = 0;
        eofLeadingLength = 0;

        for (var index = 0; index < _raw.Length; index++) {

            // Direktiv-Lauf: die ganze Direktivzeile als ein strukturiertes DirectiveTrivia-Stück (mit Verweis
            // auf seinen Knoten) plus das terminierende Zeilenende als eigenes NewLine — beides zählt als
            // Trivia des umgebenden Laufs. Die Präprozessor-Token des Laufs werden übersprungen.
            if (runByStart.TryGetValue(index, out var directive)) {
                all.Add(new SyntaxTrivia(SyntaxTokenType.DirectiveTrivia, directive.ContentExtent, directive.Node));
                if (!directive.NewLineExtent.IsMissing) {
                    all.Add(new SyntaxTrivia(SyntaxTokenType.NewLine, directive.NewLineExtent));
                }

                index = directive.RawEnd - 1; // Die Schleife rückt über den ganzen Direktiv-Lauf hinweg.
                continue;
            }

            var token = _raw[index];

            if (token.IsTrivia) {
                all.Add(new SyntaxTrivia(token.Type, token.Extent));
                continue;
            }

            // Skip-Lauf: übersprungene Token sind keine Trenner, sondern werden — wie ein Direktiv-Lauf —
            // zu einem strukturierten SkippedTokensTrivia-Stück des umgebenden Laufs gefaltet.
            if (IsSkippedToken(token, consumedStarts)) {
                index = FoldSkippedRun(all, index, consumedStarts, runByStart);
                continue;
            }

            var pendingEnd = all.Count;

            // Trenner erreicht: zuerst die Trailing-Trivia des vorigen signifikanten Tokens vom Anfang des
            // pending-Laufs abspalten (bis einschließlich des ersten Zeilenendes; sonst — kein Zeilenende — alles).
            var splitCount = 0;
            if (lastSignificantKey >= 0) {
                splitCount = SplitTrailingCount(all, pendingStart, pendingEnd);
                var lead   = tokenTrivia[lastSignificantKey]; // beim Token-Eintritt gesetzt, daher stets vorhanden
                tokenTrivia[lastSignificantKey] = new TriviaRange(lead.LeadingStart, lead.LeadingLength, pendingStart, splitCount);
            }

            // Der Rest des pending-Laufs ist die Leading-Trivia des aktuellen Trenners (sofern er welche aufnimmt).
            var remainingStart  = pendingStart + splitCount;
            var remainingLength = pendingEnd - remainingStart;

            if (token.Type == SyntaxTokenType.EndOfFile) {
                eofLeadingStart    = remainingStart;
                eofLeadingLength   = remainingLength;
                lastSignificantKey = -1;
            } else if (IsHidden(token.Type)) {
                // Dritter Arm der vollständigen Trenner-Klassifikation (EOF / versteckt / signifikant):
                // ein versteckter Trenner — per IsHidden ein Token-Typ, der nie im flachen _tokens-Strom
                // landet (Autorität "für den Cursor unsichtbar"). Da es zu ihm keinen SyntaxToken gibt, der
                // seine Trivia je nachschlüge, darf er KEIN Trailing-Anker sein: lastSignificantKey bleibt
                // -1, damit die nachfolgende Trivia dem nächsten echten Token als Leading zufällt statt auf
                // einem Phantom-Schlüssel verloren zu gehen. Seine eigene restliche Leading-Trivia trägt er
                // dennoch, sonst ginge der Text zwischen vorigem Token und Trenner verloren.
                //
                // Praktisch bleibt dieser Arm unter der Lexer-Invariante leer: Präprozessor-Token entstehen
                // nur innerhalb eines #-Laufs, den der Direktiven-Vorlauf stets vollständig zu DirectiveTrivia
                // faltet (siehe runByStart oben) — ein verwaister Präprozessor-Token als Trenner kann daher
                // nicht auftreten. Der Arm bleibt bewusst als korrekte Behandlung erhalten, falls sich diese
                // Invariante je ändert; er ist keine Laufzeit-Absicherung (kein Pfad hier wirft ohnehin).
                if (remainingLength > 0) {
                    tokenTrivia[token.Start] = new TriviaRange(remainingStart, remainingLength, 0, 0);
                }

                lastSignificantKey = -1;
            } else {
                tokenTrivia[token.Start] = new TriviaRange(remainingStart, remainingLength, 0, 0);
                lastSignificantKey       = token.Start;
            }

            pendingStart = pendingEnd; // Nächster Lauf beginnt hinter der eben zugeordneten Trivia (am Trenner selbst wurde keine ergänzt).
        }

        return all.ToImmutable();
    }

    /// <summary>
    /// Ob das Roh-Token ein <b>übersprungenes</b> Token ist — ein parser-sichtbares Token, das der Parser
    /// nicht konsumiert hat (Panic-Mode-Deletion), oder ein lexikalisch unbekanntes Zeichen
    /// (<see cref="SyntaxTokenType.Unknown"/>, das nie konsumiert wird). Trivia, Präprozessor-Token
    /// (Direktiv-Läufe) und das <see cref="SyntaxTokenType.EndOfFile"/> zählen nicht dazu.
    /// </summary>
    static bool IsSkippedToken(RawToken token, HashSet<int> consumedStarts) {
        return !token.IsTrivia                                &&
               token.Type != SyntaxTokenType.EndOfFile        &&
               !SyntaxFacts.IsPreprocessorToken(token.Type)   &&
               !consumedStarts.Contains(token.Extent.Start);
    }

    /// <summary>
    /// Faltet — analog zum Direktiv-Zweig in <see cref="BuildTrivia"/> — den an <paramref name="first"/>
    /// beginnenden <b>maximalen</b> Lauf übersprungener Token zu genau einem strukturierten
    /// <see cref="SyntaxTokenType.SkippedTokensTrivia"/>-Stück: reine Trivia zwischen den Skip-Token bricht
    /// den Lauf nicht (sie fällt in dessen Extent), ein konsumiertes Token, ein Direktiv-Lauf oder das
    /// Dateiende beendet ihn. Der zugehörige <see cref="SkippedTokensTriviaSyntax"/>-Knoten hält die
    /// Skip-Token <b>lokal</b> (Klassifikation <see cref="TextClassification.Skiped"/>); für unbekannte
    /// Zeichen wird dabei die lexikalische Diagnose (<c>Nav0000</c>) gemeldet. Liefert den Roh-Index des
    /// letzten Tokens des Laufs (die aufrufende Schleife rückt dahinter weiter).
    /// </summary>
    int FoldSkippedRun(ImmutableArray<SyntaxTrivia>.Builder all, int first,
                       HashSet<int> consumedStarts, Dictionary<int, DirectiveRun> runByStart) {

        var last = first;
        for (var i = first + 1; i < _raw.Length; i++) {
            if (runByStart.ContainsKey(i)) {
                break;
            }

            if (_raw[i].IsTrivia) {
                continue;
            }

            if (!IsSkippedToken(_raw[i], consumedStarts)) {
                break;
            }

            last = i;
        }

        var extent = TextExtent.FromBounds(_raw[first].Extent.Start, _raw[last].Extent.End);
        var node   = new SkippedTokensTriviaSyntax(extent);

        var localTokens = new List<SyntaxToken>();
        for (var i = first; i <= last; i++) {
            var raw = _raw[i];
            if (raw.IsTrivia) {
                continue;
            }

            localTokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, TextClassification.Skiped, node));
            ReportLexicalDiagnostics(raw);
        }

        node.SetLocalTokens(new SyntaxTokenList(localTokens));
        _skippedTokensRuns.Add(node);

        all.Add(new SyntaxTrivia(SyntaxTokenType.SkippedTokensTrivia, extent, node));
        return last;
    }

    /// <summary>
    /// Zählt am Anfang des pending-Laufs <c>[start, end)</c> in <paramref name="all"/> die Trailing-Trivia:
    /// alle Elemente bis <b>einschließlich</b> des ersten Zeilenendes. Enthält der Lauf kein Zeilenende
    /// (der nächste Trenner steht in derselben Zeile), zählt alles als Trailing-Trivia.
    /// </summary>
    static int SplitTrailingCount(ImmutableArray<SyntaxTrivia>.Builder all, int start, int end) {
        for (var i = start; i < end; i++) {
            if (all[i].Type == SyntaxTokenType.NewLine) {
                return i - start + 1;
            }
        }

        return end - start;
    }

    /// <summary>
    /// Ob der Token-Typ für den Parser-Cursor unsichtbar ist: lexikalische Trivia (Autorität
    /// <see cref="SyntaxFacts.IsLexicalTrivia"/>), unbekannte Zeichen (werden nie konsumiert, sondern
    /// als Skip-Trivia gefaltet) und Präprozessor-Token (Autorität
    /// <see cref="SyntaxFacts.IsPreprocessorToken"/> — strukturiert im Direktiven-Vorlauf verarbeitet).
    /// Bewusst eine Komposition der drei Teilmengen statt einer eigenen Aufzählung, damit jede Menge
    /// genau eine Pflege-Stelle hat.
    /// </summary>
    static bool IsHidden(SyntaxTokenType type) {
        return SyntaxFacts.IsLexicalTrivia(type) ||
               type == SyntaxTokenType.Unknown   ||
               SyntaxFacts.IsPreprocessorToken(type);
    }

    #endregion

}
