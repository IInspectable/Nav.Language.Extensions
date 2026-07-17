using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator;

/// <summary>
/// Inkrementeller Quellgenerator für das Annotation-Besucher-Paar (<c>INavTaskAnnotationVisitor</c>/
/// <c>NavTaskAnnotationVisitor</c> samt generischer Variante) über alle konkreten
/// <c>NavTaskAnnotation</c>-Ableitungen. Anders als beim Symbol-Besucher sind die Besuchsmethoden auf die
/// konkreten Klassen typisiert (nicht auf ein Interface), und die <c>Accept</c>-Überschreibungen sind
/// <c>internal</c>. Löst das host-spezifische (EnvDTE) T4-Template ab und läuft damit unter
/// <c>dotnet build</c> wie unter <c>MSBuild.exe</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AnnotationVisitorGenerator: IIncrementalGenerator {

    const string RootTypeName  = "Pharmatechnik.Nav.Language.CodeAnalysis.Annotation.NavTaskAnnotation";
    const string RootClassName = "NavTaskAnnotation";
    const string Namespace     = "Pharmatechnik.Nav.Language.CodeAnalysis.Annotation";

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var mappings = context.SyntaxProvider
                              .CreateSyntaxProvider(
                                   predicate: static (node, _) => IsCandidateAnnotationClass(node),
                                   transform: static (ctx, _) => ToAnnotationMapping(ctx))
                              .Where(static mapping => mapping != null)
                              .Select(static (mapping, _) => mapping!)
                              .Collect();

        context.RegisterSourceOutput(mappings, static (spc, m) => Emit(spc, m));
    }

    static bool IsCandidateAnnotationClass(SyntaxNode node) {

        if (node is not ClassDeclarationSyntax cls) {
            return false;
        }

        return !cls.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword));
    }

    static AnnotationMapping? ToAnnotationMapping(GeneratorSyntaxContext ctx) {

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol) {
            return null;
        }

        return DerivesFromRoot(symbol)
            ? new AnnotationMapping { ClassName = symbol.Name, IsRoot = symbol.ToDisplayString() == RootTypeName }
            : null;
    }

    static bool DerivesFromRoot(INamedTypeSymbol symbol) {
        for (var type = symbol; type != null; type = type.BaseType) {
            if (type.ToDisplayString() == RootTypeName) {
                return true;
            }
        }

        return false;
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<AnnotationMapping> mappings) {

        // Wurzel zuerst, danach alphabetisch. Auf Projekten ohne die Annotation-Typen (leeres Set) nichts
        // erzeugen — so darf die Generator-Assembly gefahrlos auch aus anderen Projekten referenziert werden.
        var ordered = mappings.Distinct()
                              .OrderByDescending(m => m.IsRoot)
                              .ThenBy(m => m.ClassName, StringComparer.Ordinal)
                              .ToImmutableArray();

        if (ordered.IsEmpty) {
            return;
        }

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Namespace(Namespace, ns => {

            ns.Block("public interface INavTaskAnnotationVisitor", c => {
                foreach (var m in ordered) {
                    c.AppendLine($"void {VisitName(m)}({m.ClassName} {ArgName(m)});");
                }
            });
            ns.AppendLine();

            ns.Block("public interface INavTaskAnnotationVisitor<T>", c => {
                foreach (var m in ordered) {
                    c.AppendLine($"T {VisitName(m)}({m.ClassName} {ArgName(m)});");
                }
            });
            ns.AppendLine();

            foreach (var m in ordered) {
                var visit    = VisitName(m);
                var modifier = m.IsRoot ? "virtual" : "override";
                ns.Block("partial class " + m.ClassName, c => {
                    c.Block($"internal {modifier} void Accept(INavTaskAnnotationVisitor visitor)",
                            b => b.AppendLine($"visitor.{visit}(this);"));
                    c.Block($"internal {modifier} T Accept<T>(INavTaskAnnotationVisitor<T> visitor)",
                            b => b.AppendLine($"return visitor.{visit}(this);"));
                });
                ns.AppendLine();
            }

            ns.Block("public abstract class NavTaskAnnotationVisitor: INavTaskAnnotationVisitor", c => {
                c.Block($"public void Visit({RootClassName} annotation)", b => b.AppendLine("annotation.Accept(this);"));
                c.AppendLine();
                c.Block($"protected virtual void DefaultVisit({RootClassName} annotation)", _ => { });
                c.AppendLine();
                foreach (var m in ordered) {
                    c.Block($"public virtual void {VisitName(m)}({m.ClassName} {ArgName(m)})",
                            b => b.AppendLine($"DefaultVisit({ArgName(m)});"));
                }
            });
            ns.AppendLine();

            ns.Block("public abstract class NavTaskAnnotationVisitor<T>: INavTaskAnnotationVisitor<T>", c => {
                c.Block($"public T Visit({RootClassName} annotation)", b => b.AppendLine("return annotation.Accept(this);"));
                c.AppendLine();
                c.Block($"protected virtual T DefaultVisit({RootClassName} annotation)", b => b.AppendLine("return default(T)!;"));
                c.AppendLine();
                foreach (var m in ordered) {
                    c.Block($"public virtual T {VisitName(m)}({m.ClassName} {ArgName(m)})",
                            b => b.AppendLine($"return DefaultVisit({ArgName(m)});"));
                }
            });
        });

        spc.AddSource("NavTaskAnnotationVisitor.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // Methodenname aus dem Klassennamen: NavChoiceAnnotation -> VisitNavChoiceAnnotation.
    static string VisitName(AnnotationMapping m) => "Visit" + m.ClassName;

    static string ArgName(AnnotationMapping m) {
        return m.ClassName.Length == 0 ? m.ClassName : char.ToLowerInvariant(m.ClassName[0]) + m.ClassName.Substring(1);
    }

}
