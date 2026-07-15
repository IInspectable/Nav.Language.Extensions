using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator;

/// <summary>
/// Inkrementeller Quellgenerator für das Symbol-Besucher-Paar (<c>ISymbolVisitor</c>/<c>SymbolVisitor</c>
/// samt kovarianter generischer Variante). Anders als beim Syntaxbaum sind die Besuchsmethoden auf die
/// <c>ISymbol</c>-Interfaces typisiert (nicht auf die Klassen); die <c>Accept</c>-Überschreibungen stehen
/// in den konkreten Symbol-Klassen. Löst das host-spezifische (EnvDTE) T4-Template ab.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SymbolVisitorGenerator: IIncrementalGenerator {

    const string SymbolInterfaceName = "Pharmatechnik.Nav.Language.ISymbol";

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var mappings = context.SyntaxProvider
                              .CreateSyntaxProvider(
                                   predicate: static (node, _) => IsCandidateSymbolClass(node),
                                   transform: static (ctx, _) => ToSymbolMapping(ctx))
                              .Where(static mapping => mapping != null)
                              .Select(static (mapping, _) => mapping!)
                              .Collect();

        context.RegisterSourceOutput(mappings, static (spc, m) => Emit(spc, m));
    }

    static bool IsCandidateSymbolClass(SyntaxNode node) {

        if (node is not ClassDeclarationSyntax cls) {
            return false;
        }

        return !cls.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword));
    }

    static SymbolMapping? ToSymbolMapping(GeneratorSyntaxContext ctx) {

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol) {
            return null;
        }

        if (!symbol.AllInterfaces.Any(IsSymbolInterface)) {
            return null;
        }

        var itf = FindVisitInterface(symbol);
        if (itf == null) {
            return null;
        }

        var baseInterfaces = itf.AllInterfaces
                                .Where(i => !IsSymbolInterface(i) && i.AllInterfaces.Any(IsSymbolInterface))
                                .Select(i => i.Name)
                                .OrderBy(n => n, StringComparer.Ordinal);

        return new SymbolMapping {
            ClassName         = symbol.Name,
            InterfaceName     = itf.Name,
            BaseInterfacesCsv = string.Join(",", baseInterfaces)
        };
    }

    static bool IsSymbolInterface(INamedTypeSymbol itf) {
        return itf.ToDisplayString() == SymbolInterfaceName;
    }

    /// <summary>
    /// Das Interface, über das eine Klasse besucht wird: bevorzugt das namensgleiche <c>I{Klasse}</c>; sonst
    /// das am stärksten abgeleitete direkt-implementierte <c>ISymbol</c>-Interface.
    /// </summary>
    static INamedTypeSymbol? FindVisitInterface(INamedTypeSymbol symbol) {

        var candidates = symbol.AllInterfaces
                               .Where(i => !IsSymbolInterface(i) && i.AllInterfaces.Any(IsSymbolInterface))
                               .ToImmutableArray();

        var byConvention = candidates.FirstOrDefault(i => i.Name == "I" + symbol.Name);
        if (byConvention != null) {
            return byConvention;
        }

        // Am stärksten abgeleitet: in keinem anderen Kandidaten als Basis enthalten.
        return candidates.FirstOrDefault(i =>
            !candidates.Any(other => !SymbolEqualityComparer.Default.Equals(other, i) &&
                                     other.AllInterfaces.Contains(i, SymbolEqualityComparer.Default)));
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<SymbolMapping> mappings) {

        var ordered = mappings.Distinct()
                              .OrderBy(m => m.ClassName, StringComparer.Ordinal)
                              .ToImmutableArray();

        // Auf Projekten ohne Symbol-Klassen (leeres Set) nichts erzeugen — so darf die Generator-Assembly
        // gefahrlos auch aus Projekten referenziert werden, die keine ISymbol-Typen deklarieren.
        if (ordered.IsEmpty) {
            return;
        }

        // Die tatsächlich besuchten Interfaces (jene mit einer Visit-Methode) und ihre ISymbol-Basen.
        var visited = new HashSet<string>(ordered.Select(m => m.InterfaceName), StringComparer.Ordinal);
        var baseOf  = ordered.GroupBy(m => m.InterfaceName, StringComparer.Ordinal)
                             .ToDictionary(
                                  g => g.Key,
                                  g => g.First().BaseInterfacesCsv.Split([','], StringSplitOptions.RemoveEmptyEntries),
                                  StringComparer.Ordinal);

        // Der Standard-Fallback einer Besuchsmethode: die Methode ihres nächsten besuchten Basis-Interfaces
        // (am stärksten abgeleitet), sonst DefaultVisit. Bildet die Interface-Hierarchie im Visitor ab.
        string Dispatch(SymbolMapping m) {

            var candidates = baseOf[m.InterfaceName].Where(visited.Contains).ToImmutableArray();

            var nearest = candidates.FirstOrDefault(c =>
                !candidates.Any(other => other != c &&
                                         baseOf.TryGetValue(other, out var ob) &&
                                         ob.Contains(c)));

            return nearest == null
                ? $"DefaultVisit({ArgName(m)})"
                : $"Visit{BaseName(nearest)}({ArgName(m)})";
        }

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Namespace("Pharmatechnik.Nav.Language", ns => {

            ns.Block("partial interface ISymbol", c => {
                c.AppendLine("void Accept(ISymbolVisitor visitor);");
                c.AppendLine("T Accept<T>(ISymbolVisitor<T> visitor);");
            });
            ns.AppendLine();

            ns.Block("public interface ISymbolVisitor", c => {
                foreach (var m in ordered) {
                    c.AppendLine($"void {VisitName(m)}({m.InterfaceName} {ArgName(m)});");
                }
            });
            ns.AppendLine();

            ns.Block("public interface ISymbolVisitor<out T>", c => {
                foreach (var m in ordered) {
                    c.AppendLine($"T {VisitName(m)}({m.InterfaceName} {ArgName(m)});");
                }
            });
            ns.AppendLine();

            ns.Block("partial class Symbol", c => {
                c.AppendLine("public abstract void Accept(ISymbolVisitor visitor);");
                c.AppendLine("public abstract T Accept<T>(ISymbolVisitor<T> visitor);");
            });
            ns.AppendLine();

            foreach (var m in ordered) {
                var visit = VisitName(m);
                ns.Block("partial class " + m.ClassName, c => {
                    c.Block("public override void Accept(ISymbolVisitor visitor)",
                            b => b.AppendLine($"visitor.{visit}(this);"));
                    c.Block("public override T Accept<T>(ISymbolVisitor<T> visitor)",
                            b => b.AppendLine($"return visitor.{visit}(this);"));
                });
                ns.AppendLine();
            }

            ns.Block("public abstract class SymbolVisitor: ISymbolVisitor", c => {
                c.Block("public void Visit(ISymbol symbol)", b => b.AppendLine("symbol.Accept(this);"));
                c.AppendLine();
                c.Block("protected virtual void DefaultVisit(ISymbol symbol)", _ => { });
                c.AppendLine();
                foreach (var m in ordered) {
                    c.Block($"public virtual void {VisitName(m)}({m.InterfaceName} {ArgName(m)})",
                            b => b.AppendLine($"{Dispatch(m)};"));
                }
            });
            ns.AppendLine();

            ns.Block("public abstract class SymbolVisitor<T>: ISymbolVisitor<T>", c => {
                c.Block("public T Visit(ISymbol symbol)", b => b.AppendLine("return symbol.Accept(this);"));
                c.AppendLine();
                c.Block("protected virtual T DefaultVisit(ISymbol symbol)", b => b.AppendLine("return default(T)!;"));
                c.AppendLine();
                foreach (var m in ordered) {
                    c.Block($"public virtual T {VisitName(m)}({m.InterfaceName} {ArgName(m)})",
                            b => b.AppendLine($"return {Dispatch(m)};"));
                }
            });
        });

        spc.AddSource("SymbolVisitor.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // Methodenname aus dem Interface: ITaskNodeSymbol -> VisitTaskNodeSymbol.
    static string VisitName(SymbolMapping m) => "Visit" + BaseName(m.InterfaceName);

    static string ArgName(SymbolMapping m) {
        var baseName = BaseName(m.InterfaceName);
        return baseName.Length == 0 ? baseName : char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
    }

    static string BaseName(string interfaceName) {
        return interfaceName.StartsWith("I", StringComparison.Ordinal)
            ? interfaceName.Substring(1)
            : interfaceName;
    }

}
