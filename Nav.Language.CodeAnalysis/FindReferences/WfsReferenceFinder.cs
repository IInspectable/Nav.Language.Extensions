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

public static partial class WfsReferenceFinder {

    static readonly ImmutableArray<ClassInfo> NavlessClasses = new[] {
            new ClassInfo(projectName: "XTplus.Kasse",             className: "Pharmatechnik.Apotheke.XTplus.Kasse.WFL.KasseWFS"),
            new ClassInfo(projectName: "XTplus.Kontaktverwaltung", className: "Pharmatechnik.Apotheke.XTplus.Kontaktverwaltung.StandardKontakteSuche.WFL.StandardKontakteSucheWFS")
        }
       .ToImmutableArray();

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

        if (args.OriginatingSymbol is IChoiceNodeSymbol choiceNode) {
            await FindChoiceReferencesAsync(args.Context, solution, choiceNode).ConfigureAwait(false);
        }

    }

    static async Task FindChoiceReferencesAsync(IFindReferencesContext context,
                                                Solution solution,
                                                IChoiceNodeSymbol choiceNode) {

        // Nav-Symbol → generierte {Choice}Logic. Jede Quelle forwardet über new(() => _wfs.{Choice}Logic(…));
        // genau diese Aufrufstellen der abstrakten Logic-Methode im generierten Code sind die C#-"Referenzen"
        // der Choice — das C#-Pendant zu den „… --> Choice_X"-Kanten, die der Nav-seitige ReferenceFinder liefert.
        var choiceCodeInfo    = ChoiceCodeInfo.FromChoiceNode(choiceNode);
        var choiceLogicMethod = await FindChoiceLogicMethodAsync(solution, choiceCodeInfo, context.CancellationToken).ConfigureAwait(false);

        // Keine {Choice}Logic auffindbar (z.B. #version 1, das Choices platt-faltet, oder Code noch nicht
        // gebaut) → schlicht keine C#-Referenzen; die Nav-Kanten liefert der Nav-seitige Finder bereits.
        if (choiceLogicMethod == null) {
            return;
        }

        // Dieselbe Definition wie der Nav-seitige Finder: gleicher Anzeige-Text ⇒ gemeinsamer Bucket im
        // „Find All References"-Fenster (der DefinitionBucket wird über den Namen/SortText zusammengeführt).
        var definitionItem = DefinitionItem.Create(choiceNode, choiceNode.ToDisplayParts());

        foreach (var referenceItem in (await GetReferenceItems().ConfigureAwait(false)).OrderByLocation()) {
            await context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);
        }

        async Task<ImmutableArray<ReferenceItem>> GetReferenceItems() {

            var referenceItems = ImmutableArray.CreateBuilder<ReferenceItem>();

            var references = await SymbolFinder.FindReferencesAsync(choiceLogicMethod, solution, context.CancellationToken).ConfigureAwait(false);

            foreach (var reference in references) {

                foreach (var referenceLocation in reference.Locations) {

                    if (!TryGetLocation(referenceLocation.Location, out var location)) {
                        continue;
                    }

                    var syntaxTree = referenceLocation.Location.SourceTree;
                    if (syntaxTree == null) {
                        continue;
                    }

                    var referenceItem = await CreateReferenceItemAsync(definition       : definitionItem,
                                                                       referenceLocation: location,
                                                                       solution         : solution,
                                                                       syntaxTree       : syntaxTree,
                                                                       cancellationToken: context.CancellationToken).ConfigureAwait(false);

                    referenceItems.Add(referenceItem);
                }
            }

            return referenceItems.ToImmutableArray();
        }
    }

    static async Task<IMethodSymbol> FindChoiceLogicMethodAsync(Solution solution, ChoiceCodeInfo choiceCodeInfo, CancellationToken cancellationToken) {

        // Die generierte {Task}WFSBase trägt die abstrakte {Choice}Logic; von ihrem voll qualifizierten
        // Namen aus finden wir das Methoden-Symbol, auf das die Forwards verweisen.
        foreach (var project in solution.Projects) {

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

            var wfsBase = compilation?.GetTypeByMetadataName(choiceCodeInfo.ContainingTask.FullyQualifiedWfsBaseName);

            var choiceLogicMethod = wfsBase?.GetMembers(choiceCodeInfo.ChoiceLogicMethodName)
                                            .OfType<IMethodSymbol>()
                                            .FirstOrDefault();

            if (choiceLogicMethod != null) {
                return choiceLogicMethod;
            }
        }

        return null;
    }

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
        
    static Task<ImmutableArray<IFieldSymbol>> GetBeginInterfaceFieldsAsync(INamedTypeSymbol wfsClass, TaskDeclarationCodeInfo taskDeclarationCodeInfo) {

        var fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

        var fields = wfsClass.GetMembers()
                             .OfType<IFieldSymbol>()
                             .Where(f => f.Type.ToDisplayString(fullyQualifiedFormat) == taskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName)
                             .ToImmutableArray();

        return Task.FromResult(fields);
    }

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

    static TextSpan GetPreviewSpan(SourceText sourceText, int position, int previewLines = 0) {

        var lineNumber = sourceText.Lines.GetLineFromPosition(position).LineNumber;

        int startLineNumber = Math.Max(0, lineNumber          - previewLines);
        int endLine         = Math.Min(sourceText.Lines.Count - 1, lineNumber + previewLines);

        return TextSpan.FromBounds(sourceText.Lines[startLineNumber].Start,
                                   sourceText.Lines[endLine].End);

    }

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