#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Das semantische Modell einer <c>.nav</c>-Datei und Wurzel-Artefakt des Modellbaus: entsteht per
/// <see cref="FromCodeGenerationUnitSyntax"/> (intern <see cref="CodeGenerationUnitBuilder"/>) aus
/// der <see cref="CodeGenerationUnitSyntax"/> und bündelt die gebundenen Task-Deklarationen
/// (<see cref="TaskDeclarations"/>), Task-Definitionen (<see cref="TaskDefinitions"/>) und Includes
/// (<see cref="Includes"/>), den flachen Symbol-Strom (<see cref="Symbols"/>) sowie die
/// <see cref="Diagnostics"/> der semantischen Ebene — die semantischen Analyzer sind hier bereits
/// gelaufen. Mittelstück der Pipeline Syntaxbaum → Semantikmodell → Codegenerierung.
/// </summary>
public sealed class CodeGenerationUnit {

    /// <summary>
    /// Wird ausschließlich vom <see cref="CodeGenerationUnitBuilder"/> aufgerufen;
    /// <c>null</c>-Kollektionen werden auf leere normalisiert.
    /// </summary>
    internal CodeGenerationUnit(CodeGenerationUnitSyntax syntax,
                                ImmutableArray<string> codeUsings,
                                IReadOnlySymbolCollection<ITaskDeclarationSymbol>? taskDeclarations,
                                IReadOnlySymbolCollection<ITaskDefinitionSymbol>? taskDefinitions,
                                IReadOnlySymbolCollection<IIncludeSymbol>? includes,
                                IEnumerable<ISymbol>? symbols,
                                ImmutableArray<Diagnostic> diagnostics) {

        CodeUsings       = codeUsings;
        Syntax           = syntax           ?? throw new ArgumentNullException(nameof(syntax));
        TaskDeclarations = taskDeclarations ?? new SymbolCollection<ITaskDeclarationSymbol>();
        TaskDefinitions  = taskDefinitions  ?? new SymbolCollection<ITaskDefinitionSymbol>();
        Includes         = includes         ?? new SymbolCollection<IIncludeSymbol>();
        Symbols          = new SymbolList(symbols ?? Enumerable.Empty<IIncludeSymbol>());
        Diagnostics      = diagnostics;
    }

    /// <summary>
    /// Liefert eine Kopie dieses Modells mit ersetzten <see cref="Diagnostics"/> — der Schritt des
    /// <see cref="CodeGenerationUnitBuilder"/>s vom temporären Analyse-Modell zum finalen Modell
    /// samt aller Analyzer-Ergebnisse.
    /// </summary>
    internal CodeGenerationUnit WithDiagnostics(ImmutableArray<Diagnostic> diagnostics) {
        return new CodeGenerationUnit(
            Syntax,
            CodeUsings,
            TaskDeclarations,
            TaskDefinitions,
            Includes,
            Symbols,
            diagnostics);
    }

    /// <summary>Die Wurzel-Syntax der Datei, aus der dieses Modell gebaut wurde.</summary>
    public CodeGenerationUnitSyntax Syntax { get; }

    /// <summary>
    /// Die Sprach-Version dieser Datei (aus <c>#version</c>, sonst
    /// <see cref="NavLanguageVersion.Default"/>) — der Ankerpunkt künftiger versionsabhängiger Syntax-
    /// und Codegen-Entscheidungen.
    /// </summary>
    public NavLanguageVersion LanguageVersion => Syntax.LanguageVersion;

    /// <summary>
    /// Der Quelltext der <c>[namespaceprefix …]</c>-Deklaration am Datei-Kopf
    /// (<see cref="CodeGenerationUnitSyntax.CodeNamespace"/>) — die komplette Deklaration samt
    /// Klammern, nicht nur der Namespace-Wert (den liefert
    /// <see cref="ITaskDefinitionSymbol.CodeNamespace"/>); <see cref="String.Empty"/>, wenn die
    /// Datei keine trägt.
    /// </summary>
    public string CodeNamespace => Syntax.CodeNamespace?.ToString() ?? String.Empty;

    /// <summary>
    /// Die Namespaces der <c>[using …]</c>-Deklarationen des Datei-Kopfs als Text, in
    /// Quelltext-Reihenfolge; Deklarationen ohne Namespace-Angabe sind ausgelassen.
    /// </summary>
    public ImmutableArray<string> CodeUsings { get; }

    /// <summary>
    /// Die Include-Direktiven der Datei (<c>taskref "datei.nav";</c>) als
    /// <see cref="IIncludeSymbol"/>e — gekeyt auf den kleingeschriebenen Dateipfad, je Zieldatei
    /// höchstens ein Eintrag (Doppel-Includes sind als Nav1001 gemeldet).
    /// </summary>
    public IReadOnlySymbolCollection<IIncludeSymbol> Includes { get; }

    /// <summary>
    /// Die Deklarationstabelle der Datei: <c>taskref Name { … }</c>-Deklarationen, implizite
    /// Deklarationen aus <c>task</c>-Definitionen sowie die aus den <see cref="Includes"/>
    /// übernommenen Deklarationen — die Auflösungs-Grundlage für Task-Knoten (siehe
    /// <see cref="TaskDeclarationSymbolBuilder"/>); Namensduplikate meldet Nav0020.
    /// </summary>
    public IReadOnlySymbolCollection<ITaskDeclarationSymbol> TaskDeclarations { get; }

    /// <summary>
    /// Die <c>task</c>-Definitionen der Datei — je Name höchstens eine (Duplikate werden nicht
    /// aufgenommen; der Namenskonflikt ist bereits über die Deklarationstabelle als Nav0020
    /// gemeldet).
    /// </summary>
    public IReadOnlySymbolCollection<ITaskDefinitionSymbol> TaskDefinitions { get; }

    /// <summary>
    /// Der flache, positions-sortierte Symbol-Strom der Datei — Grundlage der Caret-Auflösung
    /// (<see cref="SymbolList.FindAtPosition"/>). Enthält die Symbole der eigenen
    /// <c>taskref Name { … }</c>-Deklarationen, alle Symbole der Task-Definitionen samt Kindern
    /// sowie die <see cref="Includes"/> — nicht aber die aus Includes übernommenen Deklarationen
    /// (sie liegen in einer anderen Datei).
    /// </summary>
    public SymbolList Symbols { get; }

    /// <summary>
    /// Die Diagnostics der semantischen Ebene: die Ergebnisse der semantischen Analyzer plus die
    /// beim Modellbau angefallenen Diagnosen (Include-Auflösung, Namensduplikate, Bindung der
    /// Knoten und Transitionen). Die reinen Syntax-Fehler der Datei stehen nicht hier, sondern an
    /// <see cref="SyntaxTree.Diagnostics"/>.
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Baut das semantische Modell zur übergebenen Datei-Syntax: bindet Task-Deklarationen,
    /// -Definitionen und Includes und lässt die semantischen Analyzer laufen.
    /// </summary>
    /// <param name="syntax">Die Wurzel-Syntax der Datei.</param>
    /// <param name="cancellationToken">Zum Abbrechen des Vorgangs.</param>
    /// <param name="syntaxProvider">Liefert die Syntax inkludierter Dateien; <c>null</c> fällt auf
    /// <see cref="SyntaxProvider.Default"/> zurück.</param>
    /// <returns>Das fertige semantische Modell der Datei.</returns>
    public static CodeGenerationUnit FromCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default, ISyntaxProvider? syntaxProvider = null) {
        return CodeGenerationUnitBuilder.FromCodeGenerationUnitSyntax(syntax, cancellationToken, syntaxProvider);
    }

}
