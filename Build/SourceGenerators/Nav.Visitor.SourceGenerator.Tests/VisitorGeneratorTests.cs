using NUnit.Framework;

using Pharmatechnik.Nav.Language.Visitor.SourceGenerator.Tests.Shared;

namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator.Tests;

[TestFixture]
public class VisitorGeneratorTests: GeneratorTestBase {

    // Minimaler Syntaxbaum: abstrakte Zwischenbasis (ausgeschlossen), eine zweistufige Ableitung
    // (prüft den transitiven Basistyp-Lauf) und eine direkte Ableitung.
    const string SyntaxModel = """
        namespace Pharmatechnik.Nav.Language {
            public abstract partial class SyntaxNode { }
            public abstract partial class CodeTypeSyntax: SyntaxNode { }
            public partial class SimpleTypeSyntax: CodeTypeSyntax { }
            public partial class IdentifierSyntax: SyntaxNode { }
        }
        """;

    // Minimale Symbol-Hierarchie: ISymbol-Basis, eine zweistufige Interface-Kette
    // (INodeReferenceSymbol -> IInitNodeReferenceSymbol) zum Prüfen des hierarchischen Fallbacks, ein
    // unabhängiges Interface, die abstrakte Symbol-Klasse (ausgeschlossen) und die konkreten Klassen, die
    // jeweils über ihr I{Name}-Interface besucht werden.
    const string SymbolModel = """
        namespace Pharmatechnik.Nav.Language {
            public partial interface ISymbol { }
            public interface INodeReferenceSymbol: ISymbol { }
            public interface IInitNodeReferenceSymbol: INodeReferenceSymbol { }
            public interface ITaskNodeSymbol: ISymbol { }
            public abstract partial class Symbol: ISymbol { }
            public partial class NodeReferenceSymbol: Symbol, INodeReferenceSymbol { }
            public partial class InitNodeReferenceSymbol: NodeReferenceSymbol, IInitNodeReferenceSymbol { }
            public partial class TaskNodeSymbol: Symbol, ITaskNodeSymbol { }
        }
        """;

    [Test]
    public void Generates_SyntaxNodeVisitor() {

        var driver    = RunGenerator(new SyntaxVisitorWalkerGenerator(), CreateCompilation(SyntaxModel));
        var generated = GetGeneratedFile(driver, "SyntaxNodeVisitor.g.cs");

        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_SyntaxNodeVisitor));
    }

    [Test]
    public void Generates_SyntaxNodeWalker() {

        var driver    = RunGenerator(new SyntaxVisitorWalkerGenerator(), CreateCompilation(SyntaxModel));
        var generated = GetGeneratedFile(driver, "SyntaxNodeWalker.g.cs");

        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_SyntaxNodeWalker));
    }

    [Test]
    public void Generates_SymbolVisitor() {

        var driver    = RunGenerator(new SymbolVisitorGenerator(), CreateCompilation(SymbolModel));
        var generated = GetGeneratedFile(driver, "SymbolVisitor.g.cs");

        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_SymbolVisitor));
    }

    // Minimale Annotation-Hierarchie: die konkrete Wurzel NavTaskAnnotation, eine abstrakte Zwischenbasis
    // (ausgeschlossen), eine zweistufige Ableitung (NavInitAnnotation über NavMethodAnnotation, prüft den
    // transitiven Basistyp-Lauf) und eine direkte Ableitung (NavExitAnnotation).
    const string AnnotationModel = """
        namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation {
            public partial class NavTaskAnnotation { }
            public abstract class NavMethodAnnotation: NavTaskAnnotation { }
            public partial class NavInitAnnotation: NavMethodAnnotation { }
            public partial class NavExitAnnotation: NavTaskAnnotation { }
        }
        """;

    [Test]
    public void Generates_NavTaskAnnotationVisitor() {

        var driver    = RunGenerator(new AnnotationVisitorGenerator(), CreateCompilation(AnnotationModel));
        var generated = GetGeneratedFile(driver, "NavTaskAnnotationVisitor.g.cs");

        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_NavTaskAnnotationVisitor));
    }

}
