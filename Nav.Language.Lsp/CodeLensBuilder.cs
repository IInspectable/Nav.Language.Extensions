#region Using Directives

using System;
using System.Collections.Generic;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Baut die CodeLens-Marken (<c>textDocument/codeLens</c>) rein syntaktisch aus dem
/// <see cref="SyntaxTree"/>: über jeder Task-Definition und jeder benannten Knoten-Deklaration erscheint
/// eine Lens. Hier wird NUR die Position erzeugt (billig, synchron) — die eigentliche Beschriftung
/// („N Verweise") und der Klick-Command folgen erst träge über <c>codeLens/resolve</c>, weil die
/// solution-weite Referenzsuche teuer ist und nur für tatsächlich sichtbare Lenses laufen soll.
/// </summary>
static class CodeLensBuilder {

    public static Protocol.CodeLens[] Build(SyntaxTree syntaxTree, Uri documentUri) {

        var source = syntaxTree.SourceText;
        var uri    = documentUri.OriginalString;
        var result = new List<Protocol.CodeLens>();

        void AddFor(SyntaxToken identifier) {
            // Anker ist stets der Bezeichner; fehlt er (z. B. anonyme end-Knoten: nur 'end;'), keine Lens.
            if (identifier.Extent.IsEmptyOrMissing) {
                return;
            }

            result.Add(new Protocol.CodeLens {
                Range = LspMapper.ToRange(source.GetLocation(identifier.Extent)),
                // Genug, um in resolve das Symbol wiederzufinden: Dokument + Offset des Bezeichners.
                // OriginalString bewahrt die exakte (ggf. %3A-kodierte) Wire-Form der VS-Code-URI.
                Data = new CodeLensData { Uri = uri, Offset = identifier.Extent.Start }
            });
        }

        // Task-Definitionen ...
        foreach (var task in syntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {
            AddFor(task.Identifier);
        }

        // ... und alle benannten Knoten-Deklarationen (init/exit/task/choice/dialog/view; end ist anonym
        // und fällt über den fehlenden Identifier von selbst heraus). Der resolve-Handler ist generisch
        // und zählt Referenzen je Symbol gleich, egal ob Task oder Knoten.
        foreach (var node in syntaxTree.Root.DescendantNodes<NodeDeclarationSyntax>()) {
            AddFor(node.ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier));
        }

        return result.ToArray();
    }
}

/// <summary>
/// Träger der Information, die <c>textDocument/codeLens</c> an <c>codeLens/resolve</c> durchreicht.
/// Geht als JSON über die Leitung und kommt in resolve als <c>JObject</c> zurück.
/// </summary>
sealed class CodeLensData {
    public string Uri    { get; set; } = "";
    public int    Offset { get; set; }
}
