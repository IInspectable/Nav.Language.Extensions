using System.Collections.Generic;

using Pharmatechnik.Nav.Language.CodeGen;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer; 

/// <summary>
/// Nav2000 (<c>Identifier expected</c>, Fehler): Task-Namen müssen gültige C#-Bezeichner sein
/// (<see cref="CSharp.IsValidIdentifier"/> — lexikalisch gültiger Bezeichner und kein reserviertes
/// C#-Schlüsselwort), denn sie fließen unverändert als Typ-/Member-Namen in den generierten
/// C#-Code. z.B. melden <c>taskref 1T { … }</c> und <c>task 2T { … }</c> je ein <c>Identifier
/// expected</c>. Geprüft werden Task-Definitionen und explizite <c>taskref</c>-Deklarationen
/// (<see cref="TaskDeclarationOrigin.TaskDeclaration"/>); die implizit aus einer Task-Definition
/// entstandene Deklaration (<see cref="TaskDeclarationOrigin.TaskDefinition"/>) wird übersprungen —
/// denselben Namen prüft bereits das Definitions-Overload. Die Diagnose sitzt am Task-Namen.
/// </summary>
public class Nav2000IdentifierExpected: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav2000IdentifierExpected;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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