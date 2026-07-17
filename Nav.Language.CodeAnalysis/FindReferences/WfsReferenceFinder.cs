#region Using Directives

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.CodeAnalysis.Common;
using Pharmatechnik.Nav.Language.FindReferences;

using SourceText = Microsoft.CodeAnalysis.Text.SourceText;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindReferences; 

/// <summary>
/// Findet die Referenzen eines Nav-Symbols (einer <see cref="ITaskDefinitionSymbol"/> samt ihrer
/// Init-/Exit-Verbindungspunkte) im generierten bzw. handgeschriebenen C#-Code der zugehörigen
/// WFS-Klasse — die C#-seitige Ergänzung zur nav-internen Suche
/// (<see cref="Pharmatechnik.Nav.Language.FindReferences.ReferenceFinder"/>). Verwertet die
/// Roslyn-Referenzsuche (<see cref="SymbolFinder"/>) über die <see cref="Solution"/> und meldet jede
/// Fundstelle als <see cref="ReferenceItem"/> an den <see cref="IFindReferencesContext"/> der
/// <see cref="FindReferencesArgs"/>. Deckt gezielt die <see cref="NavlessClasses">nav-losen
/// WFS-Klassen</see> ab — jene handgeschriebenen WFS-Klassen ohne eigene <c>.nav</c>-Quelle, die die
/// Annotation-basierte Auflösung nicht erreicht. Aufgerufen vom „Alle Referenzen suchen"-Befehl der
/// VS-Extension (<c>FindReferencesCommandHandler</c>).
/// </summary>
public static partial class WfsReferenceFinder {

    /// <summary>
    /// Die fest verdrahtete Liste der nav-losen WFS-Klassen: handgeschriebene Workflow-Service-Klassen
    /// ohne zugrunde liegende <c>.nav</c>-Datei, deren voll qualifizierter Typname je
    /// <see cref="ClassInfo.ProjectName">Projekt</see> bekannt ist. Nur in diesen Klassen sucht
    /// <see cref="FindReferencesAsync"/> nach Verwendungen des Ausgangssymbols.
    /// </summary>
    static readonly ImmutableArray<ClassInfo> NavlessClasses = new[] {
            new ClassInfo(projectName: "XTplus.Kasse",             className: "Pharmatechnik.Apotheke.XTplus.Kasse.WFL.KasseWFS"),
            new ClassInfo(projectName: "XTplus.Kontaktverwaltung", className: "Pharmatechnik.Apotheke.XTplus.Kontaktverwaltung.StandardKontakteSuche.WFL.StandardKontakteSucheWFS")
        }
       .ToImmutableArray();

    /// <summary>
    /// Sucht die Referenzen des <see cref="FindReferencesArgs.OriginatingSymbol">Ausgangssymbols</see>
    /// in den nav-losen WFS-Klassen der <paramref name="solution"/> und meldet jede Fundstelle an den
    /// <see cref="FindReferencesArgs.Context"/>. Wirkt nur, wenn das Ausgangssymbol eine
    /// <see cref="ITaskDefinitionSymbol"/> ist; für jede Task werden drei Sichten aufgebaut — die Task
    /// selbst (<see cref="DefinitionItem.CreateTaskDefinitionItem"/>), ihr Init-Verbindungspunkt und
    /// ihre Exit-Verbindungspunkte — und je Projekt in dessen <see cref="ClassInfo.ClassName">WFS-Klasse</see>
    /// aufgelöst. Fehlt die zugehörige Task-Deklaration (<see cref="ITaskDefinitionSymbol.AsTaskDeclaration"/>),
    /// existiert keine WFS-Klasse und die Suche endet ohne Treffer.
    /// </summary>
    /// <param name="solution">Die zu durchsuchende Roslyn-<see cref="Solution"/> des generierten C#-Codes.</param>
    /// <param name="args">Die Suchanfrage mit Ausgangssymbol, Solution-Kontext und Ergebnis-Senke.</param>
    public static async Task FindReferencesAsync(Solution solution, FindReferencesArgs args) {

        if (args.OriginatingSymbol is ITaskDefinitionSymbol taskDefinition) {

            var nodeDefinition                 = DefinitionItem.CreateTaskDefinitionItem(taskDefinition);
            var initConnectionPointDefinition  = DefinitionItem.CreateInitConnectionPointDefinition(taskDefinition, false);
            var exitConnectionPointDefinitions = DefinitionItem.CreateExitConnectionPointDefinitions(taskDefinition, false);
            // Ohne zugehörige Task-Declaration gibt es keine generierte WFS-Klasse — dann nichts zu finden.
            var taskDeclaration = taskDefinition.AsTaskDeclaration;
            if (taskDeclaration == null) {
                return;
            }

            var taskDeclarationCodeInfo        = TaskDeclarationCodeInfo.FromTaskDeclaration(taskDeclaration);

            foreach (var project in solution.Projects) {

                foreach (var classInfo in NavlessClasses.Where(ci => ci.ProjectName == project.Name)) {
                    try {

                        var compilation = await project.GetCompilationAsync(args.Context.CancellationToken).ConfigureAwait(false);

                        var wfsClass = compilation?.GetTypeByMetadataName(classInfo.ClassName);
                        if (wfsClass == null) {
                            continue;
                        }

                        var beginInterfaceFields = await GetBeginInterfaceFieldsAsync(wfsClass, taskDeclarationCodeInfo).ConfigureAwait(false);
                        var beginInvocations     = await GetBeginInvocationsAsync(args.Context, solution, beginInterfaceFields).ConfigureAwait(false);

                        await FindTaskFields(args.Context, solution, beginInterfaceFields, nodeDefinition).ConfigureAwait(false);
                        await FindInitMethods(args.Context, solution, beginInvocations, initConnectionPointDefinition).ConfigureAwait(false);
                        await FindTaskExitMethods(args.Context, solution, beginInvocations, exitConnectionPointDefinitions, compilation).ConfigureAwait(false);

                    } catch (Exception e) {
                        // TODO Error Handling
                        var messageItem = ReferenceItem.CreateSimpleMessage(nodeDefinition, e.Message);
                        await args.Context.OnReferenceFoundAsync(messageItem);
                    }

                }

            }
        }

    }

    /// <summary>
    /// Meldet die Task selbst als Fundstelle: Für jedes Begin-Interface-Feld der WFS-Klasse wird die
    /// Typ-Angabe seiner Feld-Deklaration (das <c>IBegin{Task}WFS</c>-Interface) lokalisiert und als
    /// Referenz auf <paramref name="nodeDefinition"/> an den <paramref name="context"/> gemeldet.
    /// </summary>
    /// <param name="context">Die Ergebnis-Senke der Referenzsuche.</param>
    /// <param name="solution">Die durchsuchte <see cref="Solution"/> (zur Vorschau-Erzeugung).</param>
    /// <param name="beginInterfaceFields">Die Felder der WFS-Klasse vom Typ des Begin-Interface der Task.</param>
    /// <param name="nodeDefinition">Die Definition der Task, der die Fundstellen zugeordnet werden.</param>
    static async Task FindTaskFields(IFindReferencesContext context,
                                     Solution solution,
                                     ImmutableArray<IFieldSymbol> beginInterfaceFields,
                                     DefinitionItem nodeDefinition) {

        foreach (var referenceItem in (await GetReferenceItems().ConfigureAwait(false)).OrderByLocation()) {
            await context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);
        }

        async Task<ImmutableArray<ReferenceItem>> GetReferenceItems() {

            var referenceItems = ImmutableArray.CreateBuilder<ReferenceItem>();

            foreach (var field in beginInterfaceFields) {

                foreach (var syntaxReference in field.DeclaringSyntaxReferences) {

                    var variableDeclarator = await syntaxReference.GetSyntaxAsync(context.CancellationToken).ConfigureAwait(false) as VariableDeclaratorSyntax;

                    if (!TryFindTypeSyntax(variableDeclarator, out var typeSyntax)) {
                        continue;
                    }

                    if (!TryGetLocation(typeSyntax.GetLocation(), out var location)) {
                        continue;
                    }

                    var referenceItem = await CreateReferenceItemAsync(definition       : nodeDefinition,
                                                                       referenceLocation: location,
                                                                       solution         : solution,
                                                                       syntaxTree       : typeSyntax.SyntaxTree,
                                                                       cancellationToken: context.CancellationToken).ConfigureAwait(false);

                    referenceItems.Add(referenceItem);

                }

            }

            return referenceItems.ToImmutableArray();

        }

        bool TryFindTypeSyntax(VariableDeclaratorSyntax variableDeclaratorSyntax, out TypeSyntax typeSyntax) {

            typeSyntax = null;

            var node = variableDeclaratorSyntax?.Parent;

            while (node != null) {
                if (node is FieldDeclarationSyntax fds) {
                    typeSyntax = fds.Declaration.Type;
                    return true;
                }

                node = node.Parent;
            }

            return false;
        }
    }

    /// <summary>
    /// Meldet den Init-Verbindungspunkt als Fundstelle: In jedem Begin-Aufruf
    /// (<c>_beginItfField.Begin(…)</c>) der WFS-Klasse wird der Methodenname <c>Begin</c> lokalisiert
    /// und als Referenz auf <paramref name="initDefinitionItem"/> gemeldet.
    /// </summary>
    /// <param name="context">Die Ergebnis-Senke der Referenzsuche.</param>
    /// <param name="solution">Die durchsuchte <see cref="Solution"/> (zur Vorschau-Erzeugung).</param>
    /// <param name="beginInvocations">Die Aufrufe der Begin-Interface-Felder.</param>
    /// <param name="initDefinitionItem">Die Definition des Init-Verbindungspunkts der Task.</param>
    static async Task FindInitMethods(IFindReferencesContext context,
                                      Solution solution,
                                      ImmutableArray<InvocationExpressionSyntax> beginInvocations,
                                      DefinitionItem initDefinitionItem) {

        foreach (var referenceItem in (await GetReferenceItems().ConfigureAwait(false)).OrderByLocation()) {
            await context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);
        }

        async Task<ImmutableArray<ReferenceItem>> GetReferenceItems() {

            var referenceItems = ImmutableArray.CreateBuilder<ReferenceItem>();

            foreach (var beginInvocation in beginInvocations) {
                // _beginItfField.Begin()...)
                // ^---------------------^ => MemberAccessExpressionSyntax
                var methodName = (beginInvocation.Expression as MemberAccessExpressionSyntax)?.Name;
                if (methodName == null) {
                    continue;
                }

                if (!TryGetLocation(methodName.GetLocation(), out var location)) {
                    continue;
                }

                var referenceItem = await CreateReferenceItemAsync(definition       : initDefinitionItem,
                                                                   referenceLocation: location,
                                                                   solution         : solution,
                                                                   syntaxTree       : methodName.SyntaxTree,
                                                                   cancellationToken: context.CancellationToken).ConfigureAwait(false);

                referenceItems.Add(referenceItem);

            }

            return referenceItems.ToImmutableArray();
        }

    }

    /// <summary>
    /// Meldet die Exit-Verbindungspunkte als Fundstellen: Aus den Begin-Aufrufen werden die
    /// „After"-Methoden-Deklarationen ermittelt (das zweite Argument des äußeren Transitions-Aufrufs)
    /// und mit den passenden Exit-Definitionen aus <paramref name="exitConnectionPointDefinitions"/>
    /// verknüpft. Ohne Exit-Definitionen (leere Menge) entfällt die Suche.
    /// </summary>
    /// <param name="context">Die Ergebnis-Senke der Referenzsuche.</param>
    /// <param name="solution">Die durchsuchte <see cref="Solution"/> (zur Vorschau-Erzeugung).</param>
    /// <param name="beginInvocations">Die Aufrufe der Begin-Interface-Felder.</param>
    /// <param name="exitConnectionPointDefinitions">Die Exit-Definitionen, indiziert nach ihrer <see cref="Location"/>.</param>
    /// <param name="compilation">Die Roslyn-<see cref="Compilation"/> zur symbolischen Auflösung der After-Methode.</param>
    static async Task FindTaskExitMethods(IFindReferencesContext context,
                                          Solution solution,
                                          ImmutableArray<InvocationExpressionSyntax> beginInvocations,
                                          ImmutableDictionary<Location, DefinitionItem> exitConnectionPointDefinitions,
                                          Compilation compilation) {


        if (!exitConnectionPointDefinitions.Any()) {
            return;
        }

        foreach (var referenceItem in (await GetReferenceItems().ConfigureAwait(false)).OrderByLocation()) {
            await context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);
        }

        async Task<ImmutableArray<ReferenceItem>> GetReferenceItems() {

            var referenceItems = ImmutableArray.CreateBuilder<ReferenceItem>();

            var afterMethodDeclarations = await GetAfterMethodDeclarationsAsync(compilation, beginInvocations, context.CancellationToken).ConfigureAwait(false);
                
            foreach (var methodDeclaration in afterMethodDeclarations) {
                  
                if (!TryGetLocation(methodDeclaration.Identifier.GetLocation(), out var location)) {
                    continue;
                }

                foreach (var exitDefinition in exitConnectionPointDefinitions) {

                    var referenceItem = await CreateReferenceItemAsync(definition       : exitDefinition.Value,
                                                                       referenceLocation: location,
                                                                       solution         : solution,
                                                                       syntaxTree       : methodDeclaration.SyntaxTree,
                                                                       cancellationToken: context.CancellationToken).ConfigureAwait(false);

                    referenceItems.Add(referenceItem);
                }
            }

            return referenceItems.ToImmutableArray();
        }        

    }
        
    /// <summary>
    /// Liefert die Felder der WFS-Klasse, deren Typ genau das
    /// <see cref="TaskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName">voll qualifizierte
    /// Begin-Interface</see> (<c>IBegin{Task}WFS</c>) der Task ist — die Anker, über die Task-,
    /// Init- und Exit-Fundstellen in der Klasse gefunden werden.
    /// </summary>
    /// <param name="wfsClass">Das Roslyn-Symbol der WFS-Klasse.</param>
    /// <param name="taskDeclarationCodeInfo">Die Codegen-Info der Task-Deklaration mit dem gesuchten Begin-Interface-Namen.</param>
    static Task<ImmutableArray<IFieldSymbol>> GetBeginInterfaceFieldsAsync(INamedTypeSymbol wfsClass, TaskDeclarationCodeInfo taskDeclarationCodeInfo) {

        var fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

        var fields = wfsClass.GetMembers()
                             .OfType<IFieldSymbol>()
                             .Where(f => f.Type.ToDisplayString(fullyQualifiedFormat) == taskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName)
                             .ToImmutableArray();

        return Task.FromResult(fields);
    }

    /// <summary>
    /// Sammelt zu den Begin-Interface-Feldern die Stellen, an denen darauf eine Methode aufgerufen wird
    /// (<c>_beginItfField.Begin(…)</c>). Nutzt die Roslyn-Referenzsuche über
    /// <see cref="SymbolFinder"/> und behält nur jene Fundstellen, deren Syntax-Elternkette einen
    /// <see cref="InvocationExpressionSyntax">Methodenaufruf</see> bildet.
    /// </summary>
    /// <param name="context">Der Kontext der Referenzsuche (liefert das Abbruch-Token).</param>
    /// <param name="solution">Die durchsuchte <see cref="Solution"/>.</param>
    /// <param name="fields">Die Begin-Interface-Felder, deren Aufrufstellen gesucht werden.</param>
    static async Task<ImmutableArray<InvocationExpressionSyntax>> GetBeginInvocationsAsync(IFindReferencesContext context,
                                                                                           Solution solution,
                                                                                           ImmutableArray<IFieldSymbol> fields) {
        var invocations = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        foreach (var field in fields) {

            var references = await SymbolFinder.FindReferencesAsync(field, solution, context.CancellationToken)
                                               .ConfigureAwait(false);

            foreach (var reference in references) {

                foreach (var referenceLocation in reference.Locations) {

                    if (!referenceLocation.Document.TryGetSyntaxTree(out var syntaxTree)) {
                        continue;
                    }

                    var rootNode   = await syntaxTree.GetRootAsync();
                    var syntaxNode = rootNode.FindNode(referenceLocation.Location.SourceSpan);

                    // Sicherstellen, dass die Referenz ein Methodenaufruf darstellt
                    // node.Parent => MemberAccessExpressionSyntax
                    // node.Parent.Parent => InvocationExpressionSyntax
                    if (syntaxNode.Parent?.Parent is InvocationExpressionSyntax ies) {
                        invocations.Add(ies);
                    }

                }
            }
        }

        return invocations.ToImmutableArray();
    }

    /// <summary>
    /// Ermittelt zu den Begin-Aufrufen die Deklarationen der „After"-Methoden: Aus jedem äußeren
    /// Transitions-Aufruf mit zwei Argumenten wird das zweite Argument (die After-Methode) symbolisch
    /// aufgelöst (über das <see cref="SemanticModel"/> der <paramref name="compilation"/>) und auf seine
    /// <see cref="MethodDeclarationSyntax">Methoden-Deklaration(en)</see> zurückgeführt. Diese
    /// After-Methoden entsprechen den Exit-Verbindungspunkten der Task.
    /// </summary>
    /// <param name="compilation">Die Roslyn-<see cref="Compilation"/> zur symbolischen Auflösung.</param>
    /// <param name="beginInvocations">Die Begin-Aufrufe, deren umschließende Transitions-Aufrufe untersucht werden.</param>
    /// <param name="cancellationToken">Das Abbruch-Token.</param>
    static async Task<ImmutableArray<MethodDeclarationSyntax>> GetAfterMethodDeclarationsAsync(Compilation compilation, ImmutableArray<InvocationExpressionSyntax> beginInvocations, CancellationToken cancellationToken) {

        var afterMethods = ImmutableArray.CreateBuilder<MethodDeclarationSyntax>();
        var transitions  = GetTransitionInvocations();

        foreach (var transition in transitions) {

            var afterMethodName   = transition.ArgumentList.Arguments[1].Expression;
            var model             = compilation.GetSemanticModel(afterMethodName.SyntaxTree);
            var afterMethodSymbol = model.GetSymbolInfo(afterMethodName, cancellationToken).Symbol as IMethodSymbol;

            if (afterMethodSymbol == null) {
                continue;
            }

            foreach (var syntaxReference in afterMethodSymbol.DeclaringSyntaxReferences) {

                var methodDeclaration = await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) as MethodDeclarationSyntax;

                if (methodDeclaration == null) {
                    continue;
                }

                afterMethods.Add(methodDeclaration);

            }
        }

        return afterMethods.ToImmutableArray();

        ImmutableArray<InvocationExpressionSyntax> GetTransitionInvocations() {

            var transisionCalls = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

            foreach (var beginInvocation in beginInvocations) {

                if (TryFindOuterInvocation(beginInvocation, out var outer) &&
                    outer.ArgumentList.Arguments.Count == 2) {

                    transisionCalls.Add(outer);
                }

            }

            return transisionCalls.ToImmutable();

            bool TryFindOuterInvocation(InvocationExpressionSyntax ies, out InvocationExpressionSyntax outerIes) {

                outerIes = default;

                Microsoft.CodeAnalysis.SyntaxNode node = ies;

                while (node?.Parent != null) {

                    if (node.Parent is InvocationExpressionSyntax fds) {
                        outerIes = fds;
                        return true;
                    }

                    node = node.Parent;
                }

                return false;
            }

        }
    }

    /// <summary>
    /// Baut aus einer Fundstelle ein <see cref="ReferenceItem"/>: Ermittelt den Vorschau-Text (die
    /// Trefferzeile sowie einen dreizeiligen Tooltip-Kontext) als klassifizierten Text und hebt darin
    /// die Fund-<see cref="TextExtent">Stelle</see> hervor. Ist zum <paramref name="syntaxTree"/> kein
    /// <see cref="Document"/> in der <paramref name="solution"/> auffindbar, wird ein
    /// „keine Referenzen"-Eintrag (<see cref="ReferenceItem.NoReferencesFoundTo"/>) zurückgegeben.
    /// </summary>
    /// <param name="definition">Die Definition, der die Fundstelle zugeordnet ist.</param>
    /// <param name="referenceLocation">Die <see cref="Location"/> der Fundstelle im generierten Code.</param>
    /// <param name="solution">Die durchsuchte <see cref="Solution"/>.</param>
    /// <param name="syntaxTree">Der Syntaxbaum, in dem die Fundstelle liegt.</param>
    /// <param name="cancellationToken">Das Abbruch-Token.</param>
    static async Task<ReferenceItem> CreateReferenceItemAsync(DefinitionItem definition,
                                                              Location referenceLocation,
                                                              Solution solution,
                                                              Microsoft.CodeAnalysis.SyntaxTree syntaxTree,
                                                              CancellationToken cancellationToken) {

        var document = solution.GetDocument(syntaxTree);
        if (document == null) {
            return ReferenceItem.NoReferencesFoundTo(definition);
        }

        SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

        var textSpan = GetPreviewSpan(sourceText, referenceLocation.Start);

        var textParts           = await ToClassifiedTextAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
        var textHighlightExtent = new TextExtent(referenceLocation.Start - textSpan.Start, referenceLocation.Length);

        var toolTipSpan  = GetPreviewSpan(sourceText, referenceLocation.Start, 3);
        var toolTipParts = await ToClassifiedTextAsync(document, toolTipSpan, cancellationToken).ConfigureAwait(false);

        var toolTipHighlightExtent = new TextExtent(referenceLocation.Start - toolTipSpan.Start, referenceLocation.Length);

        var referenceItem = new ReferenceItem(
            definition            : definition,
            location              : referenceLocation,
            textParts             : textParts,
            textHighlightExtent   : textHighlightExtent,
            toolTipParts          : toolTipParts,
            toolTipHighlightExtent: toolTipHighlightExtent);

        return referenceItem;
    }

    /// <summary>
    /// Bestimmt den Vorschau-<see cref="TextSpan"/> um <paramref name="position"/>: die Zeile der
    /// Position, erweitert um <paramref name="previewLines"/> Zeilen davor und danach (an den
    /// Dateigrenzen gekappt).
    /// </summary>
    /// <param name="sourceText">Der Quelltext des Dokuments.</param>
    /// <param name="position">Die Zeichenposition der Fundstelle.</param>
    /// <param name="previewLines">Die Anzahl der Kontextzeilen ober- und unterhalb (Standard: 0 = nur die Trefferzeile).</param>
    static TextSpan GetPreviewSpan(SourceText sourceText, int position, int previewLines = 0) {

        var lineNumber = sourceText.Lines.GetLineFromPosition(position).LineNumber;

        int startLineNumber = Math.Max(0, lineNumber          - previewLines);
        int endLine         = Math.Min(sourceText.Lines.Count - 1, lineNumber + previewLines);

        return TextSpan.FromBounds(sourceText.Lines[startLineNumber].Start,
                                   sourceText.Lines[endLine].End);

    }

    /// <summary>
    /// Zerlegt den Text im <paramref name="span"/> zeilenweise in klassifizierte Fragmente
    /// (<see cref="ClassifiedText"/>) über den Roslyn-<see cref="Classifier"/> — die Grundlage für die
    /// farbige Vorschau einer Fundstelle. Vom Classifier nicht abgedeckte Lücken werden als reiner Text
    /// bzw. Leerraum nachgetragen.
    /// </summary>
    /// <param name="document">Das Roslyn-<see cref="Document"/> der Fundstelle.</param>
    /// <param name="span">Der zu klassifizierende Bereich.</param>
    /// <param name="cancellationToken">Das Abbruch-Token.</param>
    static async Task<ImmutableArray<ClassifiedText>> ToClassifiedTextAsync(Document document,
                                                                            TextSpan span,
                                                                            CancellationToken cancellationToken) {

        var builder    = ImmutableArray.CreateBuilder<ClassifiedText>();
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

        var firstLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
        var lastLine  = sourceText.Lines.GetLineFromPosition(span.End).LineNumber;

        int currentLine = firstLine;
        while (currentLine <= lastLine) {

            var lineSpan = currentLine == lastLine ? sourceText.Lines[currentLine].Span : sourceText.Lines[currentLine].SpanIncludingLineBreak;
            lineSpan = lineSpan.Trim(span);

            currentLine++;

            var classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, lineSpan, cancellationToken).ConfigureAwait(false);

            int currentEnd = lineSpan.Start;
            foreach (var classifiedSpan in classifiedSpans) {

                // Es kann sein, dass der Classifier den Span vergrößert (z.B. bei über mehrere Zeilen gehenden Verbatim Strings)
                var textSpan = classifiedSpan.TextSpan.Trim(range: span);

                if (textSpan.Start > currentEnd) {
                    ProcessUnclassifiedSpan(TextSpan.FromBounds(currentEnd, textSpan.Start));
                }

                var text = sourceText.ToString(textSpan);
                var tc   = GetClassification(classifiedSpan.ClassificationType);

                builder.Add(new ClassifiedText(text, tc));

                currentEnd = textSpan.End;
            }

            if (currentEnd < lineSpan.End) {
                ProcessUnclassifiedSpan(TextSpan.FromBounds(currentEnd, lineSpan.End));
            }

        }

        return builder.ToImmutableArray();

        void ProcessUnclassifiedSpan(TextSpan unhandledSpan) {

            var ws = sourceText.ToString(unhandledSpan);
            if (String.IsNullOrWhiteSpace(ws)) {
                builder.Add(ClassifiedTexts.Whitespace(ws));
            } else {
                builder.Add(ClassifiedTexts.Text(ws));
            }
        }

    }
        

    // Nicht vollständig. Haupsache die Vorschau sieht hübsch aus
    /// <summary>
    /// Bildet einen Roslyn-<see cref="ClassificationTypeNames">Klassifizierungstyp</see> auf die
    /// engine-eigene <see cref="TextClassification"/> ab. Bewusst unvollständig — es genügt, was die
    /// Vorschau optisch trägt; alles Übrige fällt auf <see cref="TextClassification.Text"/>.
    /// </summary>
    /// <param name="c">Der Name des Roslyn-Klassifizierungstyps.</param>
    static TextClassification GetClassification(string c) {

        if (c == ClassificationTypeNames.ClassName     ||
            c == ClassificationTypeNames.InterfaceName ||
            c == ClassificationTypeNames.EnumName      ||
            c == ClassificationTypeNames.StructName) {
            return TextClassification.TypeName;
        }

        if (c == ClassificationTypeNames.Keyword) {
            return TextClassification.Keyword;
        }

        if (c == ClassificationTypeNames.ControlKeyword) {
            return TextClassification.ControlKeyword;
        }

        if (c == ClassificationTypeNames.StringLiteral ||
            c == ClassificationTypeNames.VerbatimStringLiteral) {
            return TextClassification.StringLiteral;
        }

        if (c == ClassificationTypeNames.Punctuation) {
            return TextClassification.Punctuation;
        }

        if (c == ClassificationTypeNames.WhiteSpace) {
            return TextClassification.Whitespace;
        }

        if (c == ClassificationTypeNames.Comment) {
            return TextClassification.Comment;
        }

        return TextClassification.Text;
    }

    /// <summary>
    /// Übersetzt eine Roslyn-<see cref="Microsoft.CodeAnalysis.Location"/> in die engine-eigene
    /// <see cref="Location"/>. Liefert <c>false</c>, wenn die Roslyn-Location <c>null</c> ist, nicht im
    /// Quelltext liegt oder keine gültige Zeilen-Span besitzt.
    /// </summary>
    /// <param name="loc">Die zu übersetzende Roslyn-Location.</param>
    /// <param name="location">Die erzeugte engine-eigene <see cref="Location"/> bei Erfolg, sonst <c>default</c>.</param>
    /// <returns><c>true</c>, wenn eine gültige <see cref="Location"/> ermittelt werden konnte.</returns>
    private static bool TryGetLocation(Microsoft.CodeAnalysis.Location loc, out Location location) {

        location = default;

        if (loc == null) {
            return false;
        }

        var filePath = loc.SourceTree?.FilePath;
        if (filePath == null || !loc.IsInSource) {
            return false;
        }

        var lineSpan = loc.GetLineSpan();
        if (!lineSpan.IsValid) {
            return false;
        }

        var textExtent = loc.SourceSpan.ToTextExtent();
        var lineRange  = lineSpan.ToLineRange();

        location = new Location(textExtent, lineRange, filePath);
        return true;
    }

}