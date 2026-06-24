#region Using Directives

using System.Linq;

using Pharmatechnik.Nav.Language;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Baut den hierarchischen Dokument-Outline (Strg+Shift+O, Breadcrumbs) aus dem semantischen Modell:
/// jede Task-Definition mit ihren deklarierten Knoten als Kinder. <c>Range</c> ist der volle
/// Deklarationsbereich (Syntax-Knoten), <c>SelectionRange</c> der Name.
/// </summary>
static class DocumentSymbolBuilder {

    public static Lsp.DocumentSymbol[] Build(CodeGenerationUnit unit) {
        return unit.TaskDefinitions.Select(ToSymbol).ToArray();
    }

    static Lsp.DocumentSymbol ToSymbol(ITaskDefinitionSymbol task) {
        return new Lsp.DocumentSymbol {
            Name           = task.Name,
            Kind           = Lsp.SymbolKind.Class,
            Range          = LspMapper.ToRange(task.Syntax.GetLocation()),
            SelectionRange = LspMapper.ToRange(task.Location),
            Children       = task.NodeDeclarations.Select(ToSymbol).ToArray()
        };
    }

    static Lsp.DocumentSymbol ToSymbol(INodeSymbol node) {
        return new Lsp.DocumentSymbol {
            Name           = string.IsNullOrEmpty(node.Name) ? "<node>" : node.Name,
            Detail         = NodeDetail(node),
            Kind           = NodeKind(node),
            Range          = LspMapper.ToRange(node.Syntax.GetLocation()),
            SelectionRange = LspMapper.ToRange(node.Location)
        };
    }

    static Lsp.SymbolKind NodeKind(INodeSymbol node) => node switch {
        IInitNodeSymbol   => Lsp.SymbolKind.Constant,
        IChoiceNodeSymbol => Lsp.SymbolKind.EnumMember,
        IGuiNodeSymbol    => Lsp.SymbolKind.Interface,
        _                 => Lsp.SymbolKind.Field
    };

    static string NodeDetail(INodeSymbol node) => node switch {
        IInitNodeSymbol   => "init",
        IExitNodeSymbol   => "exit",
        IEndNodeSymbol    => "end",
        IChoiceNodeSymbol => "choice",
        IGuiNodeSymbol    => "gui",
        _                 => "node"
    };
}
