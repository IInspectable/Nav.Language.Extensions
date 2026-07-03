#region Using Directives

using System.ComponentModel;

using ModelContextProtocol.Server;


#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_grammar</c>: liefert die EBNF-Grammatik der Nav-Sprache (oder eine einzelne Produktion).
/// </summary>
[McpServerToolType]
public static class NavGrammarTool {

    [McpServerTool(Name = "nav_grammar")]
    [Description("Returns the EBNF grammar of the Nav language, assembled at compile time from the hand-written "  +
                 "parser's per-production fragments (so it always matches the parser). Without arguments it "      +
                 "returns the whole grammar; pass 'rule' for a single production by its non-terminal name (the "   +
                 "left-hand side, e.g. 'taskDefinition'). Set 'includeTerminals' to also get the terminal table "  +
                 "(keywords, punctuation, categorical terminals). This is static language reference — no .nav "    +
                 "file or workspace is involved.")]
    public static NavGrammarResult Grammar(
        [Description("Optional non-terminal name (left-hand side) of a single production, e.g. 'taskDefinition'. " +
                     "Omit to return the entire grammar.")]
        string? rule = null,
        [Description("When true, also include the terminal table (keywords, punctuation, categorical terminals).")]
        bool includeTerminals = false) {

        if (string.IsNullOrWhiteSpace(rule)) {
            return NavGrammarResult.Full(includeTerminals);
        }

        if (NavGrammar.Rules.TryGetValue(rule, out var ebnf)) {
            return NavGrammarResult.Single(rule, ebnf, includeTerminals);
        }

        return NavGrammarResult.UnknownRule(rule);
    }

}
