#region Using Directives

using System.Linq;


using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Baut den hierarchischen Dokument-Outline (Strg+Shift+O, Breadcrumbs) aus dem semantischen Modell:
/// jede Task-Definition mit ihren deklarierten Knoten als Kinder. <c>Range</c> ist der volle
/// Deklarationsbereich (Syntax-Knoten), <c>SelectionRange</c> der Name.
/// </summary>
static class DocumentSymbolBuilder {

    public static Protocol.DocumentSymbol[] Build(CodeGenerationUnit unit) {
        return unit.TaskDefinitions.Select(ToSymbol).ToArray();
    }

    static Protocol.DocumentSymbol ToSymbol(ITaskDefinitionSymbol task) {
        return new Protocol.DocumentSymbol {
            Name           = task.Name,
            Kind           = Protocol.SymbolKind.Class,
            Range          = LspMapper.ToRange(task.Syntax.GetLocation()),
            SelectionRange = LspMapper.ToRange(task.Location),
            Children       = task.NodeDeclarations.Select(ToSymbol).ToArray()
        };
    }

    static Protocol.DocumentSymbol ToSymbol(INodeSymbol node) {
        return new Protocol.DocumentSymbol {
            Name           = string.IsNullOrEmpty(node.Name) ? "<node>" : node.Name,
            Detail         = NodeDetail(node),
            Kind           = NodeKind(node),
            Range          = LspMapper.ToRange(node.Syntax.GetLocation()),
            SelectionRange = LspMapper.ToRange(node.Location)
        };
    }

    static Protocol.SymbolKind NodeKind(INodeSymbol node) => node switch {
        IInitNodeSymbol   => Protocol.SymbolKind.Constant,
        IChoiceNodeSymbol => Protocol.SymbolKind.EnumMember,
        IGuiNodeSymbol    => Protocol.SymbolKind.Interface,
        _                 => Protocol.SymbolKind.Field
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
