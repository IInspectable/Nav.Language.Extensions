using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator;

/// <summary>
/// Inkrementeller Quellgenerator, der die über die <c>Parse*</c>-Methoden des handgeschriebenen
/// <c>NavParser</c> verstreuten EBNF-Fragmente einsammelt und zur zusammenhängenden Grammatik
/// zusammenfügt. Ergebnis ist eine <c>partial class NavGrammar</c> mit der Gesamt-Grammatik als
/// <c>const string Ebnf</c> sowie einem <c>Rules</c>-Wörterbuch je Produktion. Die Inline-EBNF in
/// <c>NavParser.cs</c> bleibt die alleinige Quelle der Wahrheit.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NavGrammarGenerator: IIncrementalGenerator {

    const string ParserClassName  = "NavParser";
    const string RuleEnumName      = "Rule";
    const string ParseMethodPrefix = "Parse";

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var rules = context.SyntaxProvider
                           .CreateSyntaxProvider(
                                predicate: static (node, _) => IsCandidateParseMethod(node),
                                transform: static (ctx, _) => ExtractRule((MethodDeclarationSyntax)ctx.Node))
                           .Where(static rule => rule != null)
                           .Select(static (rule, _) => rule!)
                           .Collect();

        var ruleEnumMembers = context.SyntaxProvider
                                     .CreateSyntaxProvider(
                                          predicate: static (node, _) => IsRuleEnumMember(node),
                                          transform: static (ctx, _) => ToRuleEnumMember((EnumMemberDeclarationSyntax)ctx.Node))
                                     .Collect();

        context.RegisterSourceOutput(rules.Combine(ruleEnumMembers), static (spc, pair) => {
            Emit(spc, pair.Left);
            Report(spc, pair.Left, pair.Right);
        });
    }

    static bool IsCandidateParseMethod(SyntaxNode node) {

        if (node is not MethodDeclarationSyntax method) {
            return false;
        }

        if (!method.Identifier.ValueText.StartsWith(ParseMethodPrefix, StringComparison.Ordinal)) {
            return false;
        }

        return method.Parent is ClassDeclarationSyntax cls &&
               cls.Identifier.ValueText == ParserClassName;
    }

    static bool IsRuleEnumMember(SyntaxNode node) {

        if (node is not EnumMemberDeclarationSyntax member) {
            return false;
        }

        return member.Parent is EnumDeclarationSyntax enumDecl &&
               enumDecl.Identifier.ValueText == RuleEnumName &&
               enumDecl.Parent is ClassDeclarationSyntax cls &&
               cls.Identifier.ValueText == ParserClassName;
    }

    static RuleEnumMember ToRuleEnumMember(EnumMemberDeclarationSyntax member) {
        return new RuleEnumMember(member.Identifier.ValueText, member.Identifier.GetLocation());
    }

    static GrammarRule? ExtractRule(MethodDeclarationSyntax method) {

        var ebnf = method.GetEbnfFragment();
        if (ebnf == null) {
            return null;
        }

        return new GrammarRule(GrammarRuleName(ebnf, method), ebnf, method.SpanStart, method.Identifier.GetLocation());
    }

    static string GrammarRuleName(string ebnf, MethodDeclarationSyntax method) {

        var idx = ebnf.IndexOf("::=", StringComparison.Ordinal);
        if (idx > 0) {
            var lhs   = ebnf.Substring(0, idx).Trim();
            var parts = lhs.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) {
                return parts[parts.Length - 1];
            }
        }

        // Fallback: ParseTaskDefinition -> taskDefinition
        var name = method.Identifier.ValueText.Substring(ParseMethodPrefix.Length);
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<GrammarRule> rules) {

        var ordered = rules.OrderBy(r => r.Order).ToImmutableArray();
        var grammar = string.Join("\n\n", ordered.Select(r => r.Ebnf));

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.Namespace("Pharmatechnik.Nav.Language", ns =>
            ns.Block("public static partial class NavGrammar", cls => {

                cls.AppendLine("/// <summary>Die vollständige, aus den <c>Parse*</c>-EBNF-Fragmenten zusammengesetzte Grammatik der Nav-Sprache.</summary>");
                cls.Append("public const string Ebnf = ");
                // Roh anhängen: die eingebetteten Zeilenumbrüche des Verbatim-Literals dürfen nicht eingerückt werden.
                cls.AppendRaw(ToVerbatim(grammar));
                cls.AppendRaw(";");
                cls.AppendLine();
                cls.AppendLine();

                cls.AppendLine("/// <summary>Die einzelnen Produktionen, je Nichtterminal-Name (linke Seite) das zugehörige EBNF-Fragment.</summary>");
                cls.Block("public static IReadOnlyDictionary<string, string> Rules { get; } = new Dictionary<string, string>", dict => {
                    foreach (var rule in ordered) {
                        dict.Append("{ ");
                        dict.AppendRaw(ToVerbatim(rule.RuleName));
                        dict.AppendRaw(", ");
                        dict.AppendRaw(ToVerbatim(rule.Ebnf));
                        dict.AppendRaw(" },");
                        dict.AppendLine();
                    }
                }, suffix: ";");
            }));

        spc.AddSource("NavGrammar.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    static void Report(SourceProductionContext spc, ImmutableArray<GrammarRule> rules, ImmutableArray<RuleEnumMember> ruleEnumMembers) {

        var defined = new HashSet<string>(rules.SelectMany(rule => EbnfFacts.DefinedNames(rule.Ebnf)), StringComparer.Ordinal);

        // NAV001 — jeder Wert von NavParser.Rule braucht eine definierte Produktion.
        foreach (var member in ruleEnumMembers) {
            var expected = ToLowerCamel(member.Name);
            if (!defined.Contains(expected)) {
                spc.ReportDiagnostic(Diagnostic.Create(GrammarDiagnostics.MissingFragment, member.Location, expected));
            }
        }

        // NAV002 — jedes referenzierte Nichtterminal muss definiert sein (geschlossene Grammatik).
        foreach (var rule in rules) {
            foreach (var reference in EbnfFacts.ReferencedNonterminals(rule.Ebnf).Distinct(StringComparer.Ordinal)) {
                if (!defined.Contains(reference)) {
                    spc.ReportDiagnostic(Diagnostic.Create(GrammarDiagnostics.UndefinedNonterminal, rule.Location, reference, rule.RuleName));
                }
            }
        }
    }

    static string ToLowerCamel(string name) {
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    // C#-10-kompatibles Verbatim-String-Literal (die Engine kennt keine Raw-String-Literale).
    static string ToVerbatim(string value) {
        return "@\"" + value.Replace("\"", "\"\"") + "\"";
    }

}
