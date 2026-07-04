#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Behebt eine deplatzierte <c>#version</c>-Direktive (<c>Nav3003</c> — die Direktive steht nicht ganz oben,
/// da ihr echter Code oder eine andere Direktive vorausgeht). Wirksam ist ausschließlich die erste
/// <c>#version</c> am Dateikopf (nur Trivia davor); eine deplatzierte ist wirkungslos.
/// <list type="bullet">
///   <item><description>Gibt es <b>keine</b> wirksame Direktive (<see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/>
///   ist <c>null</c>), wird die deplatzierte an den Dateikopf <b>verschoben</b> — damit wird sie wirksam.</description></item>
///   <item><description>Gibt es bereits eine wirksame Direktive, würde ein Verschieben ein Duplikat
///   (<c>Nav3004</c>) erzeugen; dann wird die deplatzierte stattdessen <b>entfernt</b>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Der Fix hängt — anders als die <c>Nav3002</c>-/<c>Nav5001</c>-Fixes — an einer <b>nicht</b>-wirksamen
/// Direktive; sie ist strukturierte Trivia und wird über <see cref="SyntaxTree.Directives"/> erreicht, nicht
/// über <see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/>. Beim Verschieben bleibt der vom
/// Nutzer geschriebene Direktiv-Text unverändert erhalten (ein etwaiger <c>Nav3002</c>-Wertfehler wandert
/// mit an den Kopf und kann dort separat behoben werden).
/// </remarks>
public sealed class MoveVersionDirectiveToTopCodeFix: ErrorCodeFix {

    internal MoveVersionDirectiveToTopCodeFix(VersionDirectiveSyntax misplacedDirective,
                                              VersionDirectiveSyntax? effectiveDirective,
                                              CodeFixContext context)
        : base(context) {
        MisplacedDirective = misplacedDirective ?? throw new ArgumentNullException(nameof(misplacedDirective));
        EffectiveDirective = effectiveDirective;
    }

    public VersionDirectiveSyntax  MisplacedDirective { get; }
    public VersionDirectiveSyntax? EffectiveDirective { get; }

    // Steht bereits eine wirksame #version am Kopf, würde ein Verschieben ein Duplikat (Nav3004) erzeugen —
    // dann ist Entfernen der deplatzierten Direktive die richtige Aktion; sonst wird sie an den Kopf verschoben.
    bool RemovesDuplicate => EffectiveDirective != null;

    public override string Name => RemovesDuplicate
        ? "Remove misplaced '#version' directive"
        : "Move '#version' to top of file";

    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => MisplacedDirective.Extent;
    public override CodeFixPrio   Prio         => CodeFixPrio.High;

    internal bool CanApplyFix() {
        // Die wirksame Direktive selbst wird nie verschoben oder entfernt — nur eine echte deplatzierte.
        return !ReferenceEquals(MisplacedDirective, EffectiveDirective);
    }

    public IList<TextChange> GetTextChanges() {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        var changes = new List<TextChange> {
            RemoveDirectiveLine()
        };

        if (!RemovesDuplicate) {
            // Verschieben = Entfernen an alter Stelle (oben) + Wieder-Einfügen ganz am Dateianfang. „Nur Trivia
            // davor" ist an Position 0 erfüllt; der geschriebene Direktiv-Text wird unverändert übernommen.
            var directiveText = SyntaxTree.SourceText.Substring(MisplacedDirective.Extent);
            changes.Add(TextChange.NewInsert(0, directiveText + Context.TextEditorSettings.NewLine));
        }

        return changes;
    }

    // Entfernt die komplette Zeile der Direktive samt Einrückung und Zeilenende, sodass keine Leerzeile
    // zurückbleibt. Ein '#' steht (Lexer-Zeilenanfangsregel) stets am Zeilenanfang; die Zeile trägt außer
    // Zwischenraum nichts weiter als die Direktive.
    TextChange RemoveDirectiveLine() {
        var line = SyntaxTree.SourceText.GetTextLineAtPosition(MisplacedDirective.Extent.Start);
        return TextChange.NewRemove(line.Extent);
    }

}
