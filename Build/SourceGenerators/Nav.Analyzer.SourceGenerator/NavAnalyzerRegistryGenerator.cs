using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

namespace Pharmatechnik.Nav.Language.Analyzer.SourceGenerator;

/// <summary>
/// Inkrementeller Quellgenerator, der eine statisch verweisende Fabrik für alle
/// <c>INavAnalyzer</c>-Implementierungen der Kompilation erzeugt (<c>Analyzer.CreateAll()</c>). Löst die
/// frühere reflektionsbasierte Analyzer-Erkennung (<c>Assembly.ExportedTypes</c> +
/// <c>Activator.CreateInstance</c>) ab: Der statische Verweis macht die Analyzer-Typen für den Trimmer
/// sichtbar, sodass keiner stumm weggetrimmt werden kann.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NavAnalyzerRegistryGenerator: IIncrementalGenerator {

    const string AnalyzerInterface = "Pharmatechnik.Nav.Language.SemanticAnalyzer.INavAnalyzer";

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var analyzers = context.SyntaxProvider
                               .CreateSyntaxProvider(
                                    predicate: static (node, _) => IsCandidateClass(node),
                                    transform: static (ctx, _) => ToAnalyzerInfo(ctx))
                               .Where(static info => info != null)
                               .Select(static (info, _) => info!)
                               .Collect();

        context.RegisterSourceOutput(analyzers, static (spc, items) => Emit(spc, items));
    }

    static bool IsCandidateClass(SyntaxNode node) {
        return node is ClassDeclarationSyntax cls &&
               !cls.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
    }

    static AnalyzerInfo? ToAnalyzerInfo(GeneratorSyntaxContext ctx) {

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol) {
            return null;
        }

        if (symbol.IsAbstract) {
            return null;
        }

        if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == AnalyzerInterface)) {
            return null;
        }

        // Heutige Activator.CreateInstance-Invariante: zugänglicher, parameterloser Konstruktor.
        var hasParameterlessCtor = symbol.InstanceConstructors.Any(
            c => c.Parameters.IsEmpty && c.DeclaredAccessibility != Accessibility.Private);
        if (!hasParameterlessCtor) {
            return null;
        }

        return new AnalyzerInfo(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<AnalyzerInfo> items) {

        var ordered = items.Distinct()
                           .OrderBy(i => i.FullTypeName, StringComparer.Ordinal)
                           .ToImmutableArray();

        const string itf = "global::Pharmatechnik.Nav.Language.SemanticAnalyzer.INavAnalyzer";

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Namespace("Pharmatechnik.Nav.Language.SemanticAnalyzer", ns =>
            ns.Block("static partial class Analyzer", c =>
                c.Block($"internal static {itf}[] CreateAll()", b => {
                    b.AppendLine($"return new {itf}[] {{");
                    using (b.Indent()) {
                        foreach (var a in ordered) {
                            b.AppendLine($"new {a.FullTypeName}(),");
                        }
                    }

                    b.AppendLine("};");
                })));

        spc.AddSource("Analyzer.Registry.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

}

/// <summary>
/// Rein wertbasierte Beschreibung einer <c>INavAnalyzer</c>-Implementierung (nur der voll qualifizierte
/// Typname), damit das inkrementelle Caching der Generator-Pipeline über Wertgleichheit greift. Der
/// Generator-Teilbaum erlaubt bewusst Positional-Records (lokale Ausnahme von der globalen Regel).
/// </summary>
/// <param name="FullTypeName">Der voll qualifizierte Typname (<c>global::…</c>) der Analyzer-Klasse.</param>
sealed record AnalyzerInfo(string FullTypeName);
