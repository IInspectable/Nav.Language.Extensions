#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using JetBrains.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

public static class AnnotationReader {

    #region ReadNavTaskAnnotations

    public static IEnumerable<NavTaskAnnotation> ReadNavTaskAnnotations(Document document) {

        var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
        var rootNode      = semanticModel.SyntaxTree.GetRoot();

        var classDeclarations = rootNode.DescendantNodesAndSelf()
                                        .OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classDeclarations) {

            var navTaskAnnotation = ReadNavTaskAnnotation(semanticModel, classDeclaration);

            if (navTaskAnnotation == null) {
                continue;
            }

            yield return navTaskAnnotation;

            // Analysiere Method Annotations
            var methodDeclarations = classDeclaration.DescendantNodes()
                                                     .OfType<MethodDeclarationSyntax>();
            var methodAnnotations = ReadMethodAnnotations(semanticModel, navTaskAnnotation, methodDeclarations);

            foreach (var methodAnnotation in methodAnnotations) {
                yield return methodAnnotation;
            }

            // Analysiere Method Invocations
            var invocationExpressions = classDeclaration.DescendantNodes()
                                                        .OfType<InvocationExpressionSyntax>();
            var callAnnotations = ReadInitCallAnnotation(semanticModel, navTaskAnnotation, invocationExpressions);

            foreach (var initCallAnnotation in callAnnotations) {
                yield return initCallAnnotation;
            }
        }
    }

    #endregion

    #region ReadNavTaskAnnotation

    [CanBeNull]
    internal static NavTaskAnnotation ReadNavTaskAnnotation(SemanticModel semanticModel, ClassDeclarationSyntax classDeclarationSyntax) {

        if(semanticModel == null || classDeclarationSyntax == null) {
            return null;
        }

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        var navTaskInfo = ReadNavTaskAnnotation(classDeclarationSyntax, classSymbol);
           
        return navTaskInfo;
    }

    [CanBeNull]
    internal static NavTaskAnnotation ReadNavTaskAnnotation(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol) {

        if (classDeclaration == null || classSymbol ==null) {
            return null;
        }

        var navTaskInfo = ReadNavTaskAnnotationInternal(classDeclaration, declaringClass: classSymbol) ??                               
                          ReadNavTaskAnnotationInternal(classDeclaration, declaringClass: classSymbol.BaseType); // Nicht gefunden? Dann in der Basisklasse nachsehen...

        return navTaskInfo;
    }

    [CanBeNull]
    static NavTaskAnnotation ReadNavTaskAnnotationInternal(
        [NotNull] ClassDeclarationSyntax classDeclaration, 
        [CanBeNull] INamedTypeSymbol declaringClass) {

        // Die Klasse kann in mehrere partial classes aufgeteilt sein
        var navTaskInfo = declaringClass?.DeclaringSyntaxReferences
                                         .Select(dsr => dsr.GetSyntax())
                                         .OfType<ClassDeclarationSyntax>()
                                         .Select(syntax => ReadNavTaskAnnotationInternal(
                                                     classDeclaration         : classDeclaration,
                                                     declaringClassDeclaration: syntax))
                                         .FirstOrDefault(nti => nti != null);
        return navTaskInfo;
    }

    [CanBeNull]
    static NavTaskAnnotation ReadNavTaskAnnotationInternal(
        [NotNull] ClassDeclarationSyntax classDeclaration,
        [NotNull] ClassDeclarationSyntax declaringClassDeclaration) {

        var tags = ReadNavTags(declaringClassDeclaration).ToList();

        var navFileTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavFile);
        var navFileName = navFileTag?.Content;

        var navTaskTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavTask);
        var navTaskName = navTaskTag?.Content;

        // Dateiname und Taskname müssen immer paarweise vorhanden sein
        if (String.IsNullOrWhiteSpace(navFileName) || String.IsNullOrWhiteSpace(navTaskName)) {
            return null;
        }

        // Relative Pfadangaben in absolute auflösen
        if (!Path.IsPathRooted(navFileName)) {
            var declaringNodePath = declaringClassDeclaration.GetLocation().GetLineSpan().Path;

            if (String.IsNullOrWhiteSpace(declaringNodePath)) {
                return null;
            }

            var declaringDir = Path.GetDirectoryName(declaringNodePath);
            if (declaringDir == null) {
                return null;
            }

            navFileName = Path.GetFullPath(Path.Combine(declaringDir, navFileName));
        }

        var taskAnnotation = new NavTaskAnnotation(
            classDeclarationSyntax         : classDeclaration, 
            declaringClassDeclarationSyntax: declaringClassDeclaration,
            taskName                       : navTaskName, 
            navFileName                    : navFileName); 

        return taskAnnotation;
    }

    #endregion

    #region ReadMethodAnnotations

    static IEnumerable<NavMethodAnnotation> ReadMethodAnnotations(
        SemanticModel semanticModel, 
        NavTaskAnnotation navTaskAnnotation, 
        IEnumerable<MethodDeclarationSyntax> methodDeclarations) {

        foreach (var methodDeclaration in methodDeclarations) {

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
            if(methodSymbol == null) {
                continue;
            }

            // Init Annotation
            var initAnnotation = ReadNavInitAnnotation(navTaskAnnotation, methodDeclaration, methodSymbol);
            if(initAnnotation != null) {
                yield return initAnnotation;
            }

            // Exit Annotation
            var navExitAnnotation = ReadNavExitAnnotation(navTaskAnnotation, methodDeclaration, methodSymbol);
            if (navExitAnnotation != null) {
                yield return navExitAnnotation;
            }

            // Trigger Annotation
            var triggerAnnotation = ReadNavTriggerAnnotation(navTaskAnnotation, methodDeclaration, methodSymbol);
            if (triggerAnnotation != null) {
                yield return triggerAnnotation;
            }               
        }
    }

    #endregion
        
    #region ReadNavInitAnnotation

    [CanBeNull]
    internal static NavInitAnnotation ReadNavInitAnnotation(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration, 
        [CanBeNull] IMethodSymbol methodSymbol) {

        if(navTaskAnnotation == null) {
            throw new ArgumentNullException(nameof(navTaskAnnotation));
        }

        var initAnnotation = ReadNavInitAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol) ?? 
                             ReadNavInitAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol?.OverriddenMethod); // In der überschriebenen Methode nachsehen
            
        return initAnnotation;
    }

    [CanBeNull]
    static NavInitAnnotation ReadNavInitAnnotationInternal(
        [NotNull] NavTaskAnnotation taskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol declaringMethod) {

        var initAnnotation = declaringMethod?.DeclaringSyntaxReferences
                                             .Select(dsr => dsr.GetSyntax())
                                             .OfType<MethodDeclarationSyntax>()
                                             .Select(syntax => ReadNavInitAnnotationInternal(
                                                         taskAnnotation   : taskAnnotation,
                                                         methodDeclaration: methodDeclaration,
                                                         declaringMethod  : syntax))
                                             .FirstOrDefault(nti => nti != null);

        return initAnnotation;
    }

    [CanBeNull]
    static NavInitAnnotation ReadNavInitAnnotationInternal(
        [NotNull] NavTaskAnnotation taskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [NotNull] MethodDeclarationSyntax declaringMethod) {

        var tags        = ReadNavTags(declaringMethod);
        var navInitTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavInit);
        var navInitName = navInitTag?.Content;

        if (String.IsNullOrEmpty(navInitName)) {
            return null;
        }

        return new NavInitAnnotation(taskAnnotation, methodDeclaration, navInitName);
    }

    #endregion

    #region ReadNavExitAnnotation

    [CanBeNull]
    static NavExitAnnotation ReadNavExitAnnotation(
        [NotNull] NavTaskAnnotation navTaskAnnotation, 
        [NotNull] MethodDeclarationSyntax methodDeclaration, 
        [CanBeNull] IMethodSymbol methodSymbol) {

        if (navTaskAnnotation == null) {
            throw new ArgumentNullException(nameof(navTaskAnnotation));
        }
            
        var navExitAnnotation = ReadNavExitAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol) ?? 
                                ReadNavExitAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol?.OverriddenMethod); // In der überschriebenen Methode nachsehen            

        return navExitAnnotation;
    }

    [CanBeNull]
    static NavExitAnnotation ReadNavExitAnnotationInternal(
        [NotNull] NavTaskAnnotation taskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol declaringMethod) {

        var navExitAnnotation = declaringMethod?.DeclaringSyntaxReferences
                                                .Select(dsr => dsr.GetSyntax())
                                                .OfType<MethodDeclarationSyntax>()
                                                .Select(syntax => ReadNavExitAnnotationInternal(
                                                            taskAnnotation   : taskAnnotation, 
                                                            methodDeclaration: methodDeclaration,
                                                            declaringMethod  : syntax))
                                                .FirstOrDefault(nti => nti != null);

        return navExitAnnotation;
    }

    [CanBeNull]
    static NavExitAnnotation ReadNavExitAnnotationInternal(
        [NotNull] NavTaskAnnotation taskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [NotNull] MethodDeclarationSyntax declaringMethod) {

        var tags        = ReadNavTags(declaringMethod);
        var navExitTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavExit);
        var navExitName = navExitTag?.Content;

        if (String.IsNullOrEmpty(navExitName)) {
            return null;
        }

        return new NavExitAnnotation(taskAnnotation, methodDeclaration, navExitName);
    }

    #endregion

    #region ReadNavTriggerAnnotation

    [CanBeNull]
    static NavTriggerAnnotation ReadNavTriggerAnnotation(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol methodSymbol) {

        if (navTaskAnnotation == null) {
            throw new ArgumentNullException(nameof(navTaskAnnotation));
        }

        var triggerAnnotation = ReadNavTriggerAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol) ?? 
                                ReadNavTriggerAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol?.OverriddenMethod); // In der überschriebenen Methode nachsehen
            
        return triggerAnnotation;
    }

    [CanBeNull]
    static NavTriggerAnnotation ReadNavTriggerAnnotationInternal(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol declaringMethod) {

        var triggerAnnotation = declaringMethod?.DeclaringSyntaxReferences
                                                .Select(dsr => dsr.GetSyntax())
                                                .OfType<MethodDeclarationSyntax>()
                                                .Select(syntax => ReadNavTriggerAnnotationInternal(
                                                            navTaskAnnotation         : navTaskAnnotation,
                                                            methodDeclaration         : methodDeclaration,
                                                            declaringMethodDeclaration: syntax))
                                                .FirstOrDefault(nti => nti != null);

        return triggerAnnotation;
    }

    [CanBeNull]
    static NavTriggerAnnotation ReadNavTriggerAnnotationInternal(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [NotNull] MethodDeclarationSyntax declaringMethodDeclaration) {

        var tags           = ReadNavTags(declaringMethodDeclaration);
        var navTriggerTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavTrigger);
        var navTriggerName = navTriggerTag?.Content;

        if (String.IsNullOrEmpty(navTriggerName)) {
            return null;
        }

        return new NavTriggerAnnotation(navTaskAnnotation, methodDeclaration, navTriggerName);
    }

    #endregion 

    #region ReadInitCallAnnotation

    static IEnumerable<NavInitCallAnnotation> ReadInitCallAnnotation(
        SemanticModel semanticModel,
        NavTaskAnnotation navTaskAnnotation,
        IEnumerable<InvocationExpressionSyntax> invocationExpressions) {

        if (semanticModel == null || navTaskAnnotation == null) {
            yield break;
        }

        foreach (var invocationExpression in invocationExpressions) {

            // Der Begin-Wrapper wird je nach Codegen-Generation unterschiedlich aufgerufen:
            //   V1: Begin{Node}(…)      — bloßer Bezeichner in der generierten {Task}WFSBase
            //   V2: ctx.Begin{Node}(…)  — Member-Zugriff auf den Call-Context (im Nutzer-Logic-Code)
            // In beiden Fällen ist der navigierbare Anker der Methoden-Bezeichner selbst.
            var identifier = invocationExpression.Expression switch {
                IdentifierNameSyntax id                                          => id,
                MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name,
                _                                                                => null
            };

            if (identifier == null) {
                continue;
            }

            if (!(semanticModel.GetSymbolInfo(identifier).Symbol is IMethodSymbol methodSymbol)) {
                continue;
            }

            var declaringMethodNode = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            var navInitCallTag      = ReadNavTags(declaringMethodNode).FirstOrDefault(tag => tag.TagName == CodeGenFacts.AnnotationTagNavInitCall);
            if (navInitCallTag == null) {
                continue;
            }

            var callAnnotation = new NavInitCallAnnotation(
                taskAnnotation            : navTaskAnnotation,
                identifier                : identifier,
                beginItfFullyQualifiedName: navInitCallTag.Content,
                parameter                 : ToComparableParameterTypeList(methodSymbol.Parameters));


            yield return callAnnotation;
        }
    }

    #endregion

    internal static List<string> ToComparableParameterTypeList(IEnumerable<IParameterSymbol> beginLogicParameter) {
        return beginLogicParameter.OrderBy(p => p.Ordinal)
                                  .Select(p => p.ToDisplayString())
                                  .ToList();
    }

    [NotNull]
    static IEnumerable<NavTag> ReadNavTags(Microsoft.CodeAnalysis.SyntaxNode node) {

        if (node == null) {
            yield break;
        }

        var trivias = node.GetLeadingTrivia()
                          .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        foreach (var trivia in trivias) {

            if (!trivia.HasStructure) {
                continue;
            }

            var xmlElementSyntaxes = trivia.GetStructure()
                                          ?.ChildNodes()
                                           .OfType<XmlElementSyntax>()
                                           .ToList() ?? new List<XmlElementSyntax>();

            // Wir suchen alle Tags, deren Namen mit Nav beginnen
            foreach (var xmlElementSyntax in xmlElementSyntaxes) {
                var startTagName = xmlElementSyntax.StartTag.Name.ToString();
                if (startTagName.StartsWith(CodeGenFacts.AnnotationTagPrefix)) {
                    yield return new NavTag {
                        TagName = startTagName,
                        Content = xmlElementSyntax.Content.ToString()
                    };
                }
            }
        }
    }

    sealed class NavTag {
        public string TagName { get; init; }
        public string Content { get; init; }
    }        
}