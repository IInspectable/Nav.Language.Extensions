namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Klassifiziert ein Engine-Symbol zu einer kurzen, stabilen Art-Kennung für die KI-Sicht — analog zum
/// <c>NodeDetail</c> des LSP-<c>DocumentSymbolBuilder</c>, aber als neutraler String (kein LSP-Typ).
/// </summary>
static class NavSymbolKind {

    public static string Of(ISymbol symbol) => symbol switch {
        IInitNodeSymbol        => "init",
        IExitNodeSymbol        => "exit",
        IEndNodeSymbol         => "end",
        IChoiceNodeSymbol      => "choice",
        IGuiNodeSymbol         => "gui",
        ITaskNodeSymbol        => "tasknode",
        INodeSymbol            => "node",
        ITaskDefinitionSymbol  => "task",
        ITaskDeclarationSymbol => "task",
        _                      => "symbol"
    };
}
