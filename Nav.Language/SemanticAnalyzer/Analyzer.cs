#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Ein semantischer Analyzer der Nav-Sprache: prüft ein fertig gebautes semantisches Modell
/// (<see cref="CodeGenerationUnit"/>) auf genau eine Sprachregel und meldet Verstöße als
/// <see cref="Diagnostic"/>s. Alle Implementierungen werden beim Modellbau vom
/// <see cref="CodeGenerationUnitBuilder"/> über die Registry <see cref="Analyzer.GetAnalyzer"/>
/// ausgeführt; ihre Diagnostics fließen in <see cref="CodeGenerationUnit.Diagnostics"/> ein.
/// </summary>
public interface INavAnalyzer {

    /// <summary>
    /// Führt die Prüfung dieses Analyzers über dem übergebenen Modell aus.
    /// </summary>
    /// <param name="codeGenerationUnit">Das zu prüfende semantische Modell.</param>
    /// <param name="context">Gemeinsamer Kontext des Analyse-Laufs, z.B. für die Auswertung von
    /// <c>// disable Nav####</c>-Kommentaren (<see cref="AnalyzerContext.IsWarningDisabled"/>).</param>
    /// <returns>Die gefundenen Diagnostics; leer, wenn die Regel eingehalten ist.</returns>
    IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context);

}

/// <summary>
/// Basisklasse der semantischen Analyzer: jede Ableitung prüft genau eine Sprachregel und trägt
/// den zugehörigen <see cref="DiagnosticDescriptor"/> als <see cref="Descriptor"/> (Namensschema
/// <c>Nav####&lt;Meldungs-Kurzform&gt;</c> = Diagnose-Id plus Message-Template). Der Einstieg
/// <see cref="Analyze(CodeGenerationUnit, AnalyzerContext)"/> fächert das Modell auf seine
/// Task-Deklarationen und Task-Definitionen auf; eine Ableitung überschreibt nur das für ihre
/// Regel relevante Overload.
/// </summary>
public abstract class NavAnalyzer: INavAnalyzer {

    /// <summary>
    /// Der Deskriptor der Diagnose, die dieser Analyzer meldet — Id, Meldungsvorlage, Kategorie
    /// und Standard-Schweregrad (definiert in <see cref="DiagnosticDescriptors"/>).
    /// </summary>
    public abstract DiagnosticDescriptor Descriptor { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Fächert auf die Einzelprüfungen auf: <see cref="Analyze(ITaskDeclarationSymbol, AnalyzerContext)"/>
    /// je <see cref="CodeGenerationUnit.TaskDeclarations"/>, danach
    /// <see cref="Analyze(ITaskDefinitionSymbol, AnalyzerContext)"/> je
    /// <see cref="CodeGenerationUnit.TaskDefinitions"/>.
    /// </remarks>
    public virtual IEnumerable<Diagnostic> Analyze(CodeGenerationUnit codeGenerationUnit, AnalyzerContext context) {
            
        foreach (var diag in codeGenerationUnit.TaskDeclarations.SelectMany(taskDeclaration=> Analyze(taskDeclaration, context))) {
            yield return diag;
        }

        foreach (var diag in codeGenerationUnit.TaskDefinitions.SelectMany(taskDefinition=> Analyze(taskDefinition, context))) {
            yield return diag;
        }
    }

    /// <summary>
    /// Prüft eine einzelne Task-Deklaration (<see cref="ITaskDeclarationSymbol"/>) auf die Regel
    /// dieses Analyzers. Die Basisimplementierung liefert nichts — deklarationsbezogene Analyzer
    /// überschreiben dieses Overload.
    /// </summary>
    /// <param name="taskDeclaration">Die zu prüfende Task-Deklaration.</param>
    /// <param name="context">Gemeinsamer Kontext des Analyse-Laufs.</param>
    /// <returns>Die gefundenen Diagnostics; leer, wenn die Regel eingehalten ist.</returns>
    public virtual IEnumerable<Diagnostic> Analyze(ITaskDeclarationSymbol taskDeclaration, AnalyzerContext context) {
        yield break;
    }

    /// <summary>
    /// Prüft eine einzelne Task-Definition (<see cref="ITaskDefinitionSymbol"/>) auf die Regel
    /// dieses Analyzers. Die Basisimplementierung liefert nichts — definitionsbezogene Analyzer
    /// (die große Mehrheit) überschreiben dieses Overload.
    /// </summary>
    /// <param name="taskDefinition">Die zu prüfende Task-Definition.</param>
    /// <param name="context">Gemeinsamer Kontext des Analyse-Laufs.</param>
    /// <returns>Die gefundenen Diagnostics; leer, wenn die Regel eingehalten ist.</returns>
    public virtual IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        yield break;
    }

}

/// <summary>
/// Statische Registry aller semantischen Analyzer. Die instanziierende Methode <c>CreateAll()</c>
/// ist die vom Quellgenerator <c>Nav.Analyzer.SourceGenerator</c> erzeugte andere Hälfte dieser
/// partial-Klasse: ein statisch verdrahtetes <c>new</c> je <see cref="INavAnalyzer"/>-Implementierung
/// der Assembly — reflektionsfrei und damit trim-sicher.
/// </summary>
static partial class Analyzer {

    // Die Analyzer-Liste wird vom Nav.Analyzer.SourceGenerator als CreateAll() erzeugt (statische Verweise
    // auf alle INavAnalyzer-Implementierungen). Der statische Verweis ersetzt die frühere Reflection
    // (Assembly.ExportedTypes + Activator.CreateInstance) und ist trim-sicher.
    private static readonly Lazy<IList<INavAnalyzer>> AnalyzerList = new(
        () => CreateAll(),
        LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Liefert die Instanzen aller registrierten Analyzer — einmalig erzeugt und danach
    /// wiederverwendet (<see cref="LazyThreadSafetyMode.PublicationOnly"/>). Aufrufstelle ist der
    /// <see cref="CodeGenerationUnitBuilder"/>, der beim Modellbau jeden Analyzer über das frisch
    /// gebaute Modell laufen lässt.
    /// </summary>
    public static IEnumerable<INavAnalyzer> GetAnalyzer() {
        return AnalyzerList.Value;
    }

}