#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Baut aus einer <see cref="CodeGenerationUnitSyntax"/> die Task-Deklarationen der Datei
/// (<see cref="TaskDeclarationSymbol"/>): aus <c>taskref Name { … }</c>-Deklarationen, implizit
/// aus <c>task</c>-Definitionen sowie aus per Include-Direktive (<c>taskref "datei.nav";</c>)
/// eingebundenen Dateien (<see cref="IncludeSymbol"/>). Erster Schritt des
/// <see cref="CodeGenerationUnitBuilder"/>; die entstehende Deklarationstabelle ist anschließend
/// die Auflösungs-Grundlage für Task-Knoten und Exit-Transitionen im
/// <see cref="TaskDefinitionSymbolBuilder"/>.
/// </summary>
sealed class TaskDeclarationSymbolBuilder {

    /// <summary>
    /// Cache für die Deklarations-Extraktion inkludierter Dateien, gekeyt auf die Syntax-Instanz
    /// der Include-Datei. Die Extraktion hängt ausschließlich von dieser Syntax ab (Include-
    /// Direktiven werden in inkludierten Dateien nicht weiterverfolgt) — eine neue Syntax-Instanz
    /// (z.B. nach Re-Parse einer geänderten Datei) bedeutet damit automatisch einen neuen
    /// Cache-Eintrag, alte Einträge werden mit ihrer Syntax vom GC eingesammelt.
    /// </summary>
    static readonly ConditionalWeakTable<CodeGenerationUnitSyntax, IncludeExtraction> IncludeExtractionCache = new();

    /// <summary>
    /// Das cachebare Extraktions-Ergebnis einer Include-Datei: die Task-Deklarationen als
    /// Prototypen plus die zusammengeführten Diagnostics der Include-Datei (ihre Syntax-Fehler
    /// und die Diagnostics der Deklarations-Extraktion — eine Voll-Semantik der Include-Datei
    /// wird hier bewusst nicht berechnet).
    /// </summary>
    /// <remarks>
    /// Die Prototypen selbst dürfen nicht in die Modelle der inkludierenden Dateien gelangen,
    /// da dort per-Datei-Zustand an den Deklarationen verdrahtet wird
    /// (<see cref="TaskDeclarationSymbol.References"/>) — Konsumenten erhalten daher Klone
    /// (<see cref="CloneTaskDeclarations"/>). Die Diagnostics-Liste ist unveränderlich und wird
    /// von allen Konsumenten geteilt.
    /// </remarks>
    sealed class IncludeExtraction {

        IncludeExtraction(SymbolCollection<TaskDeclarationSymbol> taskDeclarationPrototypes, IReadOnlyList<Diagnostic> diagnostics) {
            _taskDeclarationPrototypes = taskDeclarationPrototypes;
            Diagnostics                = diagnostics;
        }

        readonly SymbolCollection<TaskDeclarationSymbol> _taskDeclarationPrototypes;

        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        /// <summary>
        /// Führt die Extraktion für eine Include-Datei aus (<c>processAsIncludedFile: true</c>)
        /// und vereinigt deren Syntax-Diagnostics mit den Extraktions-Diagnostics.
        /// </summary>
        public static IncludeExtraction Create(CodeGenerationUnitSyntax includeFileSyntax, CancellationToken cancellationToken) {
            // syntaxProvider: null — in inkludierten Dateien werden keine Include-Direktiven
            // verarbeitet, das Ergebnis ist damit unabhängig vom Provider des Konsumenten.
            var result      = FromCodeGenerationUnitSyntax(includeFileSyntax, processAsIncludedFile: true, syntaxProvider: null, cancellationToken: cancellationToken);
            var diagnostics = includeFileSyntax.SyntaxTree.Diagnostics.Union(result.Diagnostics).ToList();

            return new IncludeExtraction(result.TaskDeclarations, diagnostics);
        }

        /// <summary>
        /// Liefert die Task-Deklarationen als frische Klone (<see cref="TaskDeclarationSymbol.Clone"/>)
        /// — die gecachten Prototypen selbst bleiben zustandsfrei.
        /// </summary>
        public SymbolCollection<TaskDeclarationSymbol> CloneTaskDeclarations() {
            return new SymbolCollection<TaskDeclarationSymbol>(_taskDeclarationPrototypes.Select(td => td.Clone()));
        }

    }

    readonly CodeGenerationUnitSyntax                _codeGenerationUnitSyntax;
    readonly bool                                    _processAsIncludedFile;
    readonly ISyntaxProvider                         _syntaxProvider;
    readonly List<Diagnostic>                        _diagnostics;
    readonly SymbolCollection<TaskDeclarationSymbol> _taskDeclarations;
    readonly SymbolCollection<IncludeSymbol>         _includes;

    TaskDeclarationSymbolBuilder(CodeGenerationUnitSyntax codeGenerationUnitSyntax,
                                 bool processAsIncludedFile,
                                 ISyntaxProvider? syntaxProvider) {
        _codeGenerationUnitSyntax = codeGenerationUnitSyntax;
        _diagnostics              = new List<Diagnostic>();
        _processAsIncludedFile    = processAsIncludedFile;
        _syntaxProvider           = syntaxProvider ?? SyntaxProvider.Default;
        _taskDeclarations         = new SymbolCollection<TaskDeclarationSymbol>();
        _includes                 = new SymbolCollection<IncludeSymbol>();
    }

    /// <summary>
    /// Extrahiert die Task-Deklarationen, Includes und dabei angefallenen Diagnostics aus der
    /// übergebenen Datei-Syntax.
    /// </summary>
    /// <param name="syntax">Die Wurzel-Syntax der zu verarbeitenden Datei.</param>
    /// <param name="syntaxProvider">Liefert die Syntax inkludierter Dateien; <c>null</c> fällt
    /// auf <see cref="SyntaxProvider.Default"/> zurück.</param>
    /// <param name="cancellationToken">Zum Abbrechen des Vorgangs.</param>
    public static (
        IReadOnlyList<Diagnostic> Diagnostics,
        SymbolCollection<TaskDeclarationSymbol> TaskDeclarations,
        SymbolCollection<IncludeSymbol> Includes)
        FromCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, ISyntaxProvider? syntaxProvider, CancellationToken cancellationToken) {

        return FromCodeGenerationUnitSyntax(syntax, false, syntaxProvider, cancellationToken);
    }

    /// <summary>
    /// Kern der Extraktion — mit <paramref name="processAsIncludedFile"/> <c>true</c> läuft sie
    /// für eine inkludierte Datei (siehe <see cref="ProcessCodeGenerationUnitSyntax"/>).
    /// </summary>
    static (
        IReadOnlyList<Diagnostic> Diagnostics,
        SymbolCollection<TaskDeclarationSymbol> TaskDeclarations,
        SymbolCollection<IncludeSymbol> Includes)
        FromCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, bool processAsIncludedFile, ISyntaxProvider? syntaxProvider, CancellationToken cancellationToken) {

        var builder = new TaskDeclarationSymbolBuilder(syntax, processAsIncludedFile, syntaxProvider);
        builder.ProcessCodeGenerationUnitSyntax(syntax, cancellationToken);

        return (Diagnostics: builder._diagnostics,
                TaskDeclarations: builder._taskDeclarations,
                Includes: builder._includes);
    }

    /// <summary>
    /// Verarbeitet die Datei in fester Reihenfolge: erst Include-Direktiven, dann
    /// <c>taskref</c>-Deklarationen, zuletzt die <c>task</c>-Definitionen. In einer inkludierten
    /// Datei (<see cref="_processAsIncludedFile"/>) werden ausschließlich die
    /// <c>task</c>-Definitionen extrahiert — deren Include-Direktiven und
    /// <c>taskref</c>-Deklarationen werden bewusst nicht weiterverfolgt.
    /// </summary>
    void ProcessCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken) {

        if (!_processAsIncludedFile) {

            foreach (var includeDirectiveSyntax in syntax.DescendantNodes<IncludeDirectiveSyntax>()) {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessIncludeDirective(includeDirectiveSyntax, cancellationToken);
            }

            foreach (var taskDeclarationSyntax in syntax.DescendantNodes<TaskDeclarationSyntax>()) {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessTaskDeclaration(taskDeclarationSyntax);
            }
        }

        foreach (var taskDefinitionSyntax in syntax.DescendantNodes<TaskDefinitionSyntax>()) {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessTaskDefinition(taskDefinitionSyntax);
        }
    }

    /// <summary>
    /// Verarbeitet eine Include-Direktive (<c>taskref "datei.nav";</c>): löst den Pfad relativ
    /// zur eigenen Datei auf (Nav0003, wenn die Datei mangels Speicherorts noch nicht aufgelöst
    /// werden kann), meldet Selbst-Includes (Nav1006) und fehlende Dateien (Nav0004) und
    /// extrahiert andernfalls die Deklarationen der Zieldatei über den
    /// <see cref="IncludeExtractionCache"/>, um sie als <see cref="IncludeSymbol"/> einzuhängen
    /// (<see cref="AddInclude"/>). Unerwartete Ausnahmen (z.B. ungültige Pfadzeichen) werden als
    /// Internal-Error-Diagnostic gemeldet, statt den Modellbau abzubrechen.
    /// </summary>
    void ProcessIncludeDirective(IncludeDirectiveSyntax includeDirectiveSyntax, CancellationToken cancellationToken) {

        var location = includeDirectiveSyntax.StringLiteral.GetLocation();
        if (location == null) {
            // Ohne String-Literal-Token gibt es weder eine Pfadangabe noch einen Ankerpunkt
            // für Diagnostics — der Parser meldet die unvollständige Direktive bereits selbst.
            return;
        }

        try {

            var filePath = includeDirectiveSyntax.StringLiteral.ToString().Trim('"').Trim();
            if (!Path.IsPathRooted(filePath)) {

                var directory = includeDirectiveSyntax.SyntaxTree.SourceText.FileInfo?.Directory;
                if (directory == null) {

                    _diagnostics.Add(new Diagnostic(
                                         location,
                                         DiagnosticDescriptors.Semantic.Nav0003SourceFileNeedsToBeSavedBeforeIncludeDirectiveCanBeProcessed));

                    return;
                }

                filePath = Path.Combine(directory.FullName, filePath);
            }

            // Löst relative Pfadangaben auf...
            filePath = Path.GetFullPath(filePath);

            // nav File inkludiert sich selbst
            if (String.Equals(includeDirectiveSyntax.SyntaxTree.SourceText.FileInfo?.FullName, filePath, StringComparison.OrdinalIgnoreCase)) {

                _diagnostics.Add(new Diagnostic(
                                     location,
                                     DiagnosticDescriptors.DeadCode.Nav1006SelfReferencingIncludeNotRequired));

                return;
            }

            var includeFileSyntax = _syntaxProvider.GetSyntax(filePath, cancellationToken);
            if (includeFileSyntax == null) {
                _diagnostics.Add(new Diagnostic(
                                     location,
                                     DiagnosticDescriptors.Semantic.Nav0004File0NotFound,
                                     filePath));
                return;

            }

            var fileLocation = new Location(filePath);
            var extraction   = IncludeExtractionCache.GetValue(includeFileSyntax, syntax => IncludeExtraction.Create(syntax, cancellationToken));
            var include      = new IncludeSymbol(filePath, location, fileLocation, includeDirectiveSyntax, extraction.Diagnostics, extraction.CloneTaskDeclarations());

            AddInclude(include);

        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _diagnostics.Add(new Diagnostic(location, DiagnosticDescriptors.NewInternalError(ex)));
        }
    }

    /// <summary>
    /// Trägt das Include ein: Ein Doppel-Include derselben Datei wird nur gemeldet (Nav1001) und
    /// nicht erneut eingetragen; andernfalls werden Fehler der inkludierten Datei an der
    /// Direktive zusammengefasst (Nav0005) und ihre Task-Deklarationen in die
    /// Deklarationstabelle übernommen (<see cref="AddTaskDeclaration"/>).
    /// </summary>
    void AddInclude(IncludeSymbol include) {

        var existing = _includes.TryFindSymbol(include);
        if (existing != null) {

            _diagnostics.Add(new Diagnostic(
                                 include.Location,
                                 DiagnosticDescriptors.DeadCode.Nav1001IncludeDirectiveForFile0AppearedPreviously,
                                 Path.GetFileName(include.FileName)));

        } else {
            _includes.Add(include);

            if (include.Diagnostics.HasErrors()) {
                _diagnostics.Add(new Diagnostic(
                                     include.Location,
                                     DiagnosticDescriptors.Semantic.Nav0005IncludeFile0HasSomeErrors,
                                     include.FileName));
            }

            foreach (var decl in include.TaskDeclarations) {
                AddTaskDeclaration(decl);
            }
        }
    }

    /// <summary>
    /// Erzeugt aus einer <c>taskref Name { … }</c>-Deklaration das
    /// <see cref="TaskDeclarationSymbol"/> (<see cref="TaskDeclarationOrigin.TaskDeclaration"/>)
    /// samt seiner Verbindungspunkte. Beim Verarbeiten als inkludierte Datei wird der
    /// Syntax-Verweis bewusst verworfen (<see cref="ITaskDeclarationSymbol.Syntax"/> bleibt
    /// <c>null</c>), damit fremde Syntaxbäume nicht am Modell hängen bleiben. Deklarationen ohne
    /// Namen werden übersprungen — den fehlenden Bezeichner meldet bereits der Parser.
    /// </summary>
    void ProcessTaskDeclaration(TaskDeclarationSyntax taskDeclarationSyntax) {

        if (taskDeclarationSyntax is { Identifier.IsMissing: false }) {

            var identifier = taskDeclarationSyntax.Identifier;
            var location   = identifier.GetLocation();
            if (location != null) {

                var syntax = _processAsIncludedFile ? null : taskDeclarationSyntax;

                var taskDeclaration = new TaskDeclarationSymbol(
                    name: identifier.ToString(),
                    location: location,
                    origin: TaskDeclarationOrigin.TaskDeclaration,
                    isIncluded: _processAsIncludedFile,
                    codeTaskResult: CodeParameter.FromResultDeclaration(taskDeclarationSyntax.CodeResultDeclaration),
                    syntax: syntax,
                    codeNamespace: taskDeclarationSyntax.CodeNamespaceDeclaration?.Namespace?.Text,
                    codeNotImplemented: taskDeclarationSyntax.CodeNotImplementedDeclaration != null);

                AddConnectionPoints(taskDeclaration, taskDeclarationSyntax.ConnectionPoints);
                AddTaskDeclaration(taskDeclaration);
            }
        }
    }

    /// <summary>
    /// Erzeugt aus einer <c>task Name { … }</c>-Definition die implizite Task-Deklaration ihrer
    /// Schnittstelle (<see cref="TaskDeclarationOrigin.TaskDefinition"/>): Die Verbindungspunkte
    /// stammen aus dem Knoten-Deklarationsblock, der Namespace aus dem <c>[namespaceprefix …]</c>
    /// des Datei-Kopfs; <c>[notimplemented]</c> gibt es an Definitionen nicht. Anders als bei
    /// <see cref="ProcessTaskDeclaration"/> bleibt der Syntax-Verweis auch beim Verarbeiten als
    /// inkludierte Datei erhalten.
    /// </summary>
    void ProcessTaskDefinition(TaskDefinitionSyntax taskDefinitionSyntax) {

        if (taskDefinitionSyntax is { Identifier.IsMissing: false }) {

            var identifier = taskDefinitionSyntax.Identifier;
            var location   = identifier.GetLocation();
            if (location != null) {

                // Speicher checken...
                // var syntax = _processAsIncludedFile ? null : taskDefinitionSyntax;
                var syntax = taskDefinitionSyntax;

                var taskDeclaration = new TaskDeclarationSymbol(
                    name: identifier.ToString(),
                    location: location,
                    origin: TaskDeclarationOrigin.TaskDefinition,
                    isIncluded: _processAsIncludedFile,
                    codeTaskResult: CodeParameter.FromResultDeclaration(taskDefinitionSyntax.CodeResultDeclaration),
                    syntax: syntax,
                    codeNamespace: _codeGenerationUnitSyntax.CodeNamespace?.Namespace?.Text,
                    codeNotImplemented: false
                );

                AddConnectionPoints(taskDeclaration, taskDefinitionSyntax.NodeDeclarationBlock.ConnectionPoints().ToList());
                AddTaskDeclaration(taskDeclaration);
            }
        }
    }

    /// <summary>
    /// Erzeugt zu den <c>init</c>-/<c>exit</c>-/<c>end</c>-Deklarationen der Schnittstelle die
    /// <see cref="ConnectionPointSymbol"/>e und hängt sie an die Deklaration
    /// (<see cref="AddConnectionPoint"/>). Fehlt bei <c>init</c> bzw. <c>exit</c> der
    /// Bezeichner, dient das Schlüsselwort selbst als Name und Fundstelle; <c>end</c> ist stets
    /// namenlos und trägt immer den Schlüsselwort-Namen.
    /// </summary>
    void AddConnectionPoints(TaskDeclarationSymbol taskDeclaration, IReadOnlyList<ConnectionPointNodeSyntax>? connectionPoints) {

        if (connectionPoints != null) {

            foreach (var initNodeSyntax in connectionPoints.OfType<InitNodeDeclarationSyntax>()) {

                var identifier = initNodeSyntax.Identifier.IsMissing ? initNodeSyntax.InitKeyword : initNodeSyntax.Identifier;

                var location = identifier.GetLocation();
                if (location == null) {
                    continue;
                }

                var name = identifier.ToString();
                var init = new InitConnectionPointSymbol(name, location, initNodeSyntax, taskDeclaration);

                AddConnectionPoint(taskDeclaration, init);
            }

            foreach (var exitNodeSyntax in connectionPoints.OfType<ExitNodeDeclarationSyntax>()) {

                var identifier = exitNodeSyntax.Identifier.IsMissing ? exitNodeSyntax.ExitKeyword : exitNodeSyntax.Identifier;

                var location = identifier.GetLocation();
                if (location == null) {
                    continue;
                }

                var name = identifier.ToString();
                var exit = new ExitConnectionPointSymbol(name, location, exitNodeSyntax, taskDeclaration);

                AddConnectionPoint(taskDeclaration, exit);
            }

            foreach (var endNodeSyntax in connectionPoints.OfType<EndNodeDeclarationSyntax>()) {
                var identifier = endNodeSyntax.EndKeyword;

                var location = identifier.GetLocation();
                if (location == null) {
                    continue;
                }

                var name = identifier.ToString();
                var end  = new EndConnectionPointSymbol(name, location, endNodeSyntax, taskDeclaration);

                AddConnectionPoint(taskDeclaration, end);
            }
        }
    }

    /// <summary>
    /// Trägt die Deklaration in die Deklarationstabelle ein; ist der Name bereits vergeben, wird
    /// stattdessen Nav0020 gemeldet (mit Verweis auf die bestehende Deklaration) — es gewinnt
    /// also der zuerst eingetragene Kandidat.
    /// </summary>
    void AddTaskDeclaration(TaskDeclarationSymbol taskDeclaration) {
        if (_taskDeclarations.Contains(taskDeclaration.Name)) {

            var existing = _taskDeclarations[taskDeclaration.Name];

            _diagnostics.Add(new Diagnostic(
                                 location: taskDeclaration.Location,
                                 additionalLocation: existing.Location,
                                 descriptor: DiagnosticDescriptors.Semantic.Nav0020TaskWithName0AlreadyDeclared,
                                 messageArgs: taskDeclaration.Name));

        } else {

            _taskDeclarations.Add(taskDeclaration);
        }
    }

    /// <summary>
    /// Hängt den Verbindungspunkt an die Deklaration; ist der Name dort bereits vergeben, wird
    /// stattdessen Nav0021 gemeldet (mit Verweis auf den bestehenden Verbindungspunkt).
    /// </summary>
    void AddConnectionPoint(TaskDeclarationSymbol taskDeclaration, ConnectionPointSymbol connectionPoint) {

        if (taskDeclaration.ConnectionPoints.Contains(connectionPoint.Name)) {

            var existing = taskDeclaration.ConnectionPoints[connectionPoint.Name];

            _diagnostics.Add(new Diagnostic(
                                 location: connectionPoint.Location,
                                 additionalLocation: existing.Location,
                                 descriptor: DiagnosticDescriptors.Semantic.Nav0021ConnectionPointWithName0AlreadyDeclared,
                                 messageArgs: connectionPoint.Name));

        } else {
            taskDeclaration.ConnectionPoints.Add(connectionPoint);
        }
    }

}
