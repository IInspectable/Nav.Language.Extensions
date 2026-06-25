#region Using Directives

using System;
using System.Collections.Generic;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Baut die CodeLens-Marken (<c>textDocument/codeLens</c>) rein syntaktisch aus dem
/// <see cref="SyntaxTree"/>: über jeder Task-Definition erscheint eine Lens. Hier wird NUR die Position
/// erzeugt (billig, synchron) — die eigentliche Beschriftung („N Verweise") und der Klick-Command
/// folgen erst träge über <c>codeLens/resolve</c>, weil die solution-weite Referenzsuche teuer ist und
/// nur für tatsächlich sichtbare Lenses laufen soll.
/// </summary>
static class CodeLensBuilder {

    public static Lsp.CodeLens[] Build(SyntaxTree syntaxTree, Uri documentUri) {

        var source = syntaxTree.SourceText;
        var result = new List<Lsp.CodeLens>();

        foreach (var task in syntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {

            var identifier = task.Identifier;
            if (identifier.Extent.IsEmptyOrMissing) {
                continue;
            }

            result.Add(new Lsp.CodeLens {
                Range = LspMapper.ToRange(source.GetLocation(identifier.Extent)),
                // Genug, um in resolve das Symbol wiederzufinden: Dokument + Offset des Bezeichners.
                // OriginalString bewahrt die exakte (ggf. %3A-kodierte) Wire-Form der VS-Code-URI.
                Data = new CodeLensData {
                    Uri    = documentUri.OriginalString,
                    Offset = identifier.Extent.Start
                }
            });
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
