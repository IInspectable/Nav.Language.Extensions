using System.Collections.Generic;

using Pharmatechnik.Nav.Language.CodeGen;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

public class Nav2000IdentifierExpected: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav2000IdentifierExpected;

    public override IEnumerable<Diagnostic> Analyze(ITaskDeclarationSymbol taskDeclaration, AnalyzerContext context) {
        //==============================
        // Identifier expected
        //==============================
        if (taskDeclaration.Origin == TaskDeclarationOrigin.TaskDeclaration &&
            !CSharp.IsValidIdentifier(taskDeclaration.Name)) {

            yield return new Diagnostic(
                location: taskDeclaration.Location,
                descriptor: DiagnosticDescriptors.Semantic.Nav2000IdentifierExpected);
        }
    }

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // Identifier expected
        //==============================
        if (!CSharp.IsValidIdentifier(taskDefinition.Name)) {
            yield return new Diagnostic(
                taskDefinition.Location,
                Descriptor);
        }
    }

}