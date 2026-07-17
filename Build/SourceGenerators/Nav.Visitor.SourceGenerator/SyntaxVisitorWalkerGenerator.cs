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
/// Inkrementeller Quellgenerator, der über alle konkreten <c>SyntaxNode</c>-Ableitungen das
/// Besucher-Doppelpaar (<c>ISyntaxNodeVisitor</c>/<c>SyntaxNodeVisitor</c> samt generischer Variante) und
/// den Tiefendurchlauf (<c>SyntaxNodeWalker</c>) erzeugt. Die jeweiligen Knotenklassen bekommen ihre
/// <c>Accept</c>- bzw. <c>Walk</c>-Überschreibungen als <c>partial</c>-Ergänzung. Löst die VS-only
/// T4-Templates ab und läuft damit unter <c>dotnet build</c> wie unter <c>MSBuild.exe</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SyntaxVisitorWalkerGenerator: IIncrementalGenerator {

    const string BaseTypeName = "Pharmatechnik.Nav.Language.SyntaxNode";
    const string NodeSuffix    = "Syntax";

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var nodes = context.SyntaxProvider
                           .CreateSyntaxProvider(
                                predicate: static (node, _) => IsCandidateNodeClass(node),
                                transform: static (ctx, _) => ToSyntaxNodeInfo(ctx))
                           .Where(static info => info != null)
                           .Select(static (info, _) => info!)
                           .Collect();

        context.RegisterSourceOutput(nodes, static (spc, infos) => Emit(spc, infos));
    }

    static bool IsCandidateNodeClass(SyntaxNode node) {

        if (node is not ClassDeclarationSyntax cls) {
            return false;
        }

        if (cls.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword))) {
            return false;
        }

        return cls.Identifier.ValueText.EndsWith(NodeSuffix, StringComparison.Ordinal);
    }

    static SyntaxNodeInfo? ToSyntaxNodeInfo(GeneratorSyntaxContext ctx) {

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol) {
            return null;
        }

        return DerivesFromSyntaxNode(symbol) ? new SyntaxNodeInfo { TypeName = symbol.Name } : null;
    }

    static bool DerivesFromSyntaxNode(INamedTypeSymbol symbol) {
        for (var baseType = symbol.BaseType; baseType != null; baseType = baseType.BaseType) {
            if (baseType.ToDisplayString() == BaseTypeName) {
                return true;
            }
        }

        return false;
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<SyntaxNodeInfo> infos) {

        var names = infos.Select(i => i.TypeName)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(n => n, StringComparer.Ordinal)
                         .ToImmutableArray();

        // Auf Projekten ohne SyntaxNode-Klassen (leeres Set) nichts erzeugen — so darf die Generator-
        // Assembly gefahrlos auch aus Projekten referenziert werden, die keine SyntaxNode-Typen deklarieren.
        if (names.IsEmpty) {
            return;
        }

        spc.AddSource("SyntaxNodeVisitor.g.cs", SourceText.From(EmitVisitor(names), Encoding.UTF8));
        spc.AddSource("SyntaxNodeWalker.g.cs",  SourceText.From(EmitWalker(names),  Encoding.UTF8));
    }

    static string EmitVisitor(ImmutableArray<string> names) {

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Namespace("Pharmatechnik.Nav.Language", ns => {

            ns.Block("partial class SyntaxNode", c => {
                c.AppendLine("internal abstract void Accept(ISyntaxNodeVisitor visitor);");
                c.AppendLine("internal abstract T Accept<T>(ISyntaxNodeVisitor<T> visitor);");
            });
            ns.AppendLine();

            foreach (var name in names) {
                var visit = VisitName(name);
                ns.Block("partial class " + name, c => {
                    c.Block("internal override void Accept(ISyntaxNodeVisitor visitor)",
                            b => b.AppendLine($"visitor.{visit}(this);"));
                    c.Block("internal override T Accept<T>(ISyntaxNodeVisitor<T> visitor)",
                            b => b.AppendLine($"return visitor.{visit}(this);"));
                });
                ns.AppendLine();
            }

            ns.Block("public interface ISyntaxNodeVisitor", c => {
                foreach (var name in names) {
                    c.AppendLine($"void {VisitName(name)}({name} {ArgName(name)});");
                }
            });
            ns.AppendLine();

            ns.Block("public abstract class SyntaxNodeVisitor: ISyntaxNodeVisitor", c => {
                c.Block("public void Visit(SyntaxNode node)", b => b.AppendLine("node.Accept(this);"));
                c.AppendLine();
                c.Block("protected virtual void DefaultVisit(SyntaxNode node)", _ => { });
                c.AppendLine();
                foreach (var name in names) {
                    c.Block($"public virtual void {VisitName(name)}({name} {ArgName(name)})",
                            b => b.AppendLine($"DefaultVisit({ArgName(name)});"));
                }
            });
            ns.AppendLine();

            ns.Block("public interface ISyntaxNodeVisitor<T>", c => {
                foreach (var name in names) {
                    c.AppendLine($"T {VisitName(name)}({name} {ArgName(name)});");
                }
            });
            ns.AppendLine();

            ns.Block("public abstract class SyntaxNodeVisitor<T>: ISyntaxNodeVisitor<T>", c => {
                c.Block("public T Visit(SyntaxNode node)", b => b.AppendLine("return node.Accept(this);"));
                c.AppendLine();
                c.Block("protected virtual T DefaultVisit(SyntaxNode node)", b => b.AppendLine("return default(T)!;"));
                c.AppendLine();
                foreach (var name in names) {
                    c.Block($"public virtual T {VisitName(name)}({name} {ArgName(name)})",
                            b => b.AppendLine($"return DefaultVisit({ArgName(name)});"));
                }
            });
        });

        return sb.ToString();
    }

    static string EmitWalker(ImmutableArray<string> names) {

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Namespace("Pharmatechnik.Nav.Language", ns => {

            ns.Block("partial class SyntaxNode", c =>
                c.AppendLine("public abstract void Walk(SyntaxNodeWalker walker);"));
            ns.AppendLine();

            foreach (var name in names) {
                var walk = WalkName(name);
                ns.Block("partial class " + name, c =>
                    c.Block("public override void Walk(SyntaxNodeWalker walker)", b => {
                        b.Block($"if (!walker.{walk}(this))", g => g.AppendLine("return;"));
                        b.Block("foreach (var child in ChildNodes())", g => g.AppendLine("child.Walk(walker);"));
                        b.AppendLine($"walker.Post{walk}(this);");
                    }));
                ns.AppendLine();
            }

            ns.Block("public abstract class SyntaxNodeWalker", c => {
                c.Block("public void Walk(SyntaxNode node)", b => b.AppendLine("node.Walk(this);"));
                c.AppendLine();
                c.Block("public virtual bool DefaultWalk(SyntaxNode node)", b => b.AppendLine("return true;"));
                c.AppendLine();
                foreach (var name in names) {
                    var walk = WalkName(name);
                    var arg  = ArgName(name);
                    c.AppendLine($"public virtual bool {walk}({name} {arg}) {{ return DefaultWalk({arg}); }}");
                    c.AppendLine($"public virtual void Post{walk}({name} {arg}) {{ }}");
                }
            });
        });

        return sb.ToString();
    }

    static string VisitName(string typeName) => "Visit" + StripSuffix(typeName);
    static string WalkName(string typeName)  => "Walk"  + StripSuffix(typeName);

    static string StripSuffix(string typeName) {
        return typeName.EndsWith(NodeSuffix, StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - NodeSuffix.Length)
            : typeName;
    }

    static string ArgName(string typeName) {
        return typeName.Length == 0 ? typeName : char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }

}
