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

/// <summary>
/// Liest die Nav-Annotationen aus dem generierten C#-Code eines Roslyn-<see cref="Document"/> und macht
/// damit die Nav↔C#-Brücke von der C#-Seite aus befahrbar. Die generierten <c>{Task}WFS</c>-Klassen und
/// ihre Member tragen als XML-Doku-Tags (<c>&lt;NavFile&gt;</c>, <c>&lt;NavTask&gt;</c>, <c>&lt;NavInit&gt;</c>,
/// <c>&lt;NavExit&gt;</c>, <c>&lt;NavTrigger&gt;</c>, <c>&lt;NavChoice&gt;</c>, <c>&lt;NavInitCall&gt;</c>,
/// <c>&lt;NavChoiceCall&gt;</c>) den Rückverweis auf ihre Nav-Herkunft; der Reader wertet diese Tags über
/// das <see cref="SemanticModel"/> des Dokuments aus und liefert sie als
/// <see cref="NavTaskAnnotation"/>-Objektbaum. Verbraucher ist vor allem <c>LocationFinder</c>, der aus
/// den Annotationen die Nav-<see cref="Location"/> auflöst (und umgekehrt).
/// </summary>
public static class AnnotationReader {

    #region ReadNavTaskAnnotations

    /// <summary>
    /// Der Einstiegspunkt: liest <b>alle</b> Nav-Annotationen eines Dokuments. Durchläuft jede
    /// Klassendeklaration und liefert je annotierter Task-Klasse zuerst ihre <see cref="NavTaskAnnotation"/>,
    /// danach — aus den Membern und Aufrufstellen der Klasse — die zugehörigen Methoden-Annotationen
    /// (<see cref="NavMethodAnnotation"/>: Init/Exit/Trigger/Choice) sowie die Aufruf-Annotationen
    /// (<see cref="NavInvocationAnnotation"/>: Init- und Choice-Aufrufe).
    /// </summary>
    /// <param name="document">Das Roslyn-Dokument mit dem generierten (oder handgeschriebenen) C#-Code.</param>
    /// <returns>Die Folge aller gefundenen Annotationen; leer, wenn das Dokument keine Task-Klasse trägt.</returns>
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

            var choiceCallAnnotations = ReadChoiceCallAnnotation(semanticModel, navTaskAnnotation, invocationExpressions);

            foreach (var choiceCallAnnotation in choiceCallAnnotations) {
                yield return choiceCallAnnotation;
            }
        }
    }

    #endregion

    #region ReadNavTaskAnnotation

    /// <summary>
    /// Liest die Task-Annotation einer einzelnen Klassendeklaration. Ermittelt das Klassensymbol über das
    /// <see cref="SemanticModel"/> und delegiert an <see cref="ReadNavTaskAnnotation(ClassDeclarationSyntax, INamedTypeSymbol)"/>.
    /// </summary>
    /// <param name="semanticModel">Das Semantikmodell des Dokuments.</param>
    /// <param name="classDeclarationSyntax">Die zu prüfende Klassendeklaration.</param>
    /// <returns>Die <see cref="NavTaskAnnotation"/> oder <see langword="null"/>, wenn die Klasse keine
    /// Nav-Task-Tags trägt (auch bei fehlendem Modell/Syntax).</returns>
    [CanBeNull]
    internal static NavTaskAnnotation ReadNavTaskAnnotation(SemanticModel semanticModel, ClassDeclarationSyntax classDeclarationSyntax) {

        if(semanticModel == null || classDeclarationSyntax == null) {
            return null;
        }

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        var navTaskInfo = ReadNavTaskAnnotation(classDeclarationSyntax, classSymbol);
           
        return navTaskInfo;
    }

    /// <summary>
    /// Liest die Task-Annotation zu einer Klasse und sieht — falls die Tags nicht an der Klasse selbst
    /// stehen — zusätzlich in deren Basisklasse nach. Die generierte <c>{Task}WFS</c>-Klasse erbt die Tags
    /// üblicherweise von ihrer <c>{Task}WFSBase</c>; genutzt u.a. vom <c>LocationFinder</c> beim Rückweg
    /// C#→Nav, wenn nur das Basisklassensymbol vorliegt.
    /// </summary>
    /// <param name="classDeclaration">Die Klassendeklaration, an der die Annotation verankert wird.</param>
    /// <param name="classSymbol">Das Symbol der Klasse; dessen Basisklasse dient als Rückfallebene.</param>
    /// <returns>Die <see cref="NavTaskAnnotation"/> oder <see langword="null"/>, wenn weder Klasse noch
    /// Basisklasse die Tags trägt.</returns>
    [CanBeNull]
    internal static NavTaskAnnotation ReadNavTaskAnnotation(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol) {

        if (classDeclaration == null || classSymbol ==null) {
            return null;
        }

        var navTaskInfo = ReadNavTaskAnnotationInternal(classDeclaration, declaringClass: classSymbol) ??                               
                          ReadNavTaskAnnotationInternal(classDeclaration, declaringClass: classSymbol.BaseType); // Nicht gefunden? Dann in der Basisklasse nachsehen...

        return navTaskInfo;
    }

    /// <summary>
    /// Sucht die Task-Tags über <em>alle</em> Teildeklarationen (<c>partial class</c>) des angegebenen
    /// Symbols: Der generierte Code kann eine Klasse auf mehrere Dateien aufteilen, die Tags stehen nur an
    /// einer davon. Liefert die erste gefundene Annotation.
    /// </summary>
    /// <param name="classDeclaration">Die Klassendeklaration, an der die Annotation verankert wird.</param>
    /// <param name="declaringClass">Das Symbol, dessen Teildeklarationen nach Tags durchsucht werden.</param>
    /// <returns>Die erste gefundene <see cref="NavTaskAnnotation"/> oder <see langword="null"/>.</returns>
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

    /// <summary>
    /// Baut die Task-Annotation aus den Tags einer konkreten deklarierenden Klasse: liest <c>&lt;NavFile&gt;</c>
    /// und <c>&lt;NavTask&gt;</c>, verlangt beide paarweise und löst einen relativen <c>&lt;NavFile&gt;</c>-Pfad
    /// gegen das Verzeichnis der deklarierenden Datei in einen absoluten Pfad auf.
    /// </summary>
    /// <param name="classDeclaration">Die Klasse, an der die entstehende Annotation verankert wird.</param>
    /// <param name="declaringClassDeclaration">Die Klasse, deren XML-Doku-Tags gelesen werden.</param>
    /// <returns>Die <see cref="NavTaskAnnotation"/> oder <see langword="null"/>, wenn Datei- oder Taskname
    /// fehlt bzw. der relative Pfad nicht aufgelöst werden kann.</returns>
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

    /// <summary>
    /// Prüft die Methoden einer annotierten Task-Klasse und liefert je Methode die passende
    /// Member-Annotation — Init, Exit, Trigger oder Choice. Eine Methode trägt höchstens eine dieser
    /// Annotationen; Methoden ohne Nav-Tag werden übersprungen.
    /// </summary>
    /// <param name="semanticModel">Das Semantikmodell des Dokuments.</param>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Member-Annotationen erben.</param>
    /// <param name="methodDeclarations">Die zu prüfenden Methodendeklarationen der Klasse.</param>
    /// <returns>Die Folge der gefundenen <see cref="NavMethodAnnotation"/>en.</returns>
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

            // Choice Annotation
            var choiceAnnotation = ReadNavChoiceAnnotation(navTaskAnnotation, methodDeclaration, methodSymbol);
            if (choiceAnnotation != null) {
                yield return choiceAnnotation;
            }
        }
    }

    #endregion
        
    #region ReadNavInitAnnotation

    /// <summary>
    /// Liest die Init-Annotation einer Methode. Sieht das <c>&lt;NavInit&gt;</c>-Tag zuerst an der Methode
    /// selbst und — falls dort nicht vorhanden — an der überschriebenen Basismethode nach, weil das Tag im
    /// generierten Code an der abstrakten <c>{Task}WFSBase</c>-Methode steht. Wird auch vom
    /// <c>LocationFinder</c> beim Rückweg C#→Nav aufgerufen.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="methodSymbol">Das Methodensymbol; dessen überschriebene Methode dient als Rückfallebene.</param>
    /// <returns>Die <see cref="NavInitAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavInit&gt;</c>-Tag gefunden wird.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navTaskAnnotation"/> ist <see langword="null"/>.</exception>
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

    /// <summary>
    /// Sucht das <c>&lt;NavInit&gt;</c>-Tag über alle Teildeklarationen (<c>partial</c>) des angegebenen
    /// Methodensymbols und liefert die erste gefundene Annotation.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Das Methodensymbol, dessen Teildeklarationen durchsucht werden.</param>
    /// <returns>Die erste gefundene <see cref="NavInitAnnotation"/> oder <see langword="null"/>.</returns>
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

    /// <summary>
    /// Baut die Init-Annotation aus dem <c>&lt;NavInit&gt;</c>-Tag einer konkreten Methodendeklaration.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Die Methode, deren Tags gelesen werden.</param>
    /// <returns>Die <see cref="NavInitAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavInit&gt;</c>-Tag vorhanden ist.</returns>
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

    /// <summary>
    /// Liest die Exit-Annotation einer Methode — analog zu <see cref="ReadNavInitAnnotation"/>: sucht das
    /// <c>&lt;NavExit&gt;</c>-Tag an der Methode und, als Rückfall, an der überschriebenen Basismethode.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="methodSymbol">Das Methodensymbol; dessen überschriebene Methode dient als Rückfallebene.</param>
    /// <returns>Die <see cref="NavExitAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavExit&gt;</c>-Tag gefunden wird.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navTaskAnnotation"/> ist <see langword="null"/>.</exception>
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

    /// <summary>
    /// Sucht das <c>&lt;NavExit&gt;</c>-Tag über alle Teildeklarationen (<c>partial</c>) des Methodensymbols.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Das Methodensymbol, dessen Teildeklarationen durchsucht werden.</param>
    /// <returns>Die erste gefundene <see cref="NavExitAnnotation"/> oder <see langword="null"/>.</returns>
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

    /// <summary>
    /// Baut die Exit-Annotation aus dem <c>&lt;NavExit&gt;</c>-Tag einer konkreten Methodendeklaration.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Die Methode, deren Tags gelesen werden.</param>
    /// <returns>Die <see cref="NavExitAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavExit&gt;</c>-Tag vorhanden ist.</returns>
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

    /// <summary>
    /// Liest die Trigger-Annotation einer Methode — analog zu <see cref="ReadNavInitAnnotation"/>: sucht das
    /// <c>&lt;NavTrigger&gt;</c>-Tag an der Methode und, als Rückfall, an der überschriebenen Basismethode.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="methodSymbol">Das Methodensymbol; dessen überschriebene Methode dient als Rückfallebene.</param>
    /// <returns>Die <see cref="NavTriggerAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavTrigger&gt;</c>-Tag gefunden wird.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navTaskAnnotation"/> ist <see langword="null"/>.</exception>
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

    /// <summary>
    /// Sucht das <c>&lt;NavTrigger&gt;</c>-Tag über alle Teildeklarationen (<c>partial</c>) des Methodensymbols.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Das Methodensymbol, dessen Teildeklarationen durchsucht werden.</param>
    /// <returns>Die erste gefundene <see cref="NavTriggerAnnotation"/> oder <see langword="null"/>.</returns>
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

    /// <summary>
    /// Baut die Trigger-Annotation aus dem <c>&lt;NavTrigger&gt;</c>-Tag einer konkreten Methodendeklaration.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethodDeclaration">Die Methode, deren Tags gelesen werden.</param>
    /// <returns>Die <see cref="NavTriggerAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavTrigger&gt;</c>-Tag vorhanden ist.</returns>
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

    #region ReadNavChoiceAnnotation

    /// <summary>
    /// Liest die Choice-Annotation einer Methode — analog zu <see cref="ReadNavInitAnnotation"/>: sucht das
    /// <c>&lt;NavChoice&gt;</c>-Tag an der Methode und, als Rückfall, an der überschriebenen Basismethode.
    /// Markiert die <c>{Choice}Logic</c>-Deklaration (nicht den aufrufseitigen Forward, siehe
    /// <see cref="ReadChoiceCallAnnotation"/>).
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="methodSymbol">Das Methodensymbol; dessen überschriebene Methode dient als Rückfallebene.</param>
    /// <returns>Die <see cref="NavChoiceAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavChoice&gt;</c>-Tag gefunden wird.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navTaskAnnotation"/> ist <see langword="null"/>.</exception>
    [CanBeNull]
    static NavChoiceAnnotation ReadNavChoiceAnnotation(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol methodSymbol) {

        if (navTaskAnnotation == null) {
            throw new ArgumentNullException(nameof(navTaskAnnotation));
        }

        var choiceAnnotation = ReadNavChoiceAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol) ??
                               ReadNavChoiceAnnotationInternal(navTaskAnnotation, methodDeclaration, methodSymbol?.OverriddenMethod); // In der überschriebenen Methode nachsehen

        return choiceAnnotation;
    }

    /// <summary>
    /// Sucht das <c>&lt;NavChoice&gt;</c>-Tag über alle Teildeklarationen (<c>partial</c>) des Methodensymbols.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethod">Das Methodensymbol, dessen Teildeklarationen durchsucht werden.</param>
    /// <returns>Die erste gefundene <see cref="NavChoiceAnnotation"/> oder <see langword="null"/>.</returns>
    [CanBeNull]
    static NavChoiceAnnotation ReadNavChoiceAnnotationInternal(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [CanBeNull] IMethodSymbol declaringMethod) {

        var choiceAnnotation = declaringMethod?.DeclaringSyntaxReferences
                                               .Select(dsr => dsr.GetSyntax())
                                               .OfType<MethodDeclarationSyntax>()
                                               .Select(syntax => ReadNavChoiceAnnotationInternal(
                                                           navTaskAnnotation         : navTaskAnnotation,
                                                           methodDeclaration         : methodDeclaration,
                                                           declaringMethodDeclaration: syntax))
                                               .FirstOrDefault(nti => nti != null);

        return choiceAnnotation;
    }

    /// <summary>
    /// Baut die Choice-Annotation aus dem <c>&lt;NavChoice&gt;</c>-Tag einer konkreten Methodendeklaration.
    /// </summary>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft die Annotation erbt.</param>
    /// <param name="methodDeclaration">Die Methode, an der die Annotation verankert wird.</param>
    /// <param name="declaringMethodDeclaration">Die Methode, deren Tags gelesen werden.</param>
    /// <returns>Die <see cref="NavChoiceAnnotation"/> oder <see langword="null"/>, wenn kein
    /// <c>&lt;NavChoice&gt;</c>-Tag vorhanden ist.</returns>
    [CanBeNull]
    static NavChoiceAnnotation ReadNavChoiceAnnotationInternal(
        [NotNull] NavTaskAnnotation navTaskAnnotation,
        [NotNull] MethodDeclarationSyntax methodDeclaration,
        [NotNull] MethodDeclarationSyntax declaringMethodDeclaration) {

        var tags          = ReadNavTags(declaringMethodDeclaration);
        var navChoiceTag  = tags.FirstOrDefault(t => t.TagName == CodeGenFacts.AnnotationTagNavChoice);
        var navChoiceName = navChoiceTag?.Content;

        if (String.IsNullOrEmpty(navChoiceName)) {
            return null;
        }

        return new NavChoiceAnnotation(navTaskAnnotation, methodDeclaration, navChoiceName);
    }

    #endregion

    #region ReadInitCallAnnotation

    /// <summary>
    /// Liest die Init-Aufruf-Annotationen aus den Aufrufausdrücken einer Task-Klasse. Zu jedem Aufruf wird
    /// über das <see cref="SemanticModel"/> das aufgerufene Methodensymbol bestimmt; trägt dessen
    /// Deklaration das <c>&lt;NavInitCall&gt;</c>-Tag, entsteht eine <see cref="NavInitCallAnnotation"/>
    /// samt Parametertypliste (zur Unterscheidung überladener Begin-Aufrufe).
    /// </summary>
    /// <param name="semanticModel">Das Semantikmodell des Dokuments.</param>
    /// <param name="navTaskAnnotation">Die Task-Annotation der aufrufenden Task, deren Herkunft übernommen wird.</param>
    /// <param name="invocationExpressions">Die zu prüfenden Aufrufausdrücke der Klasse.</param>
    /// <returns>Die Folge der gefundenen <see cref="NavInitCallAnnotation"/>en.</returns>
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

    #region ReadChoiceCallAnnotation

    /// <summary>
    /// Liest die Choice-Forward-Annotationen aus den Aufrufausdrücken einer Task-Klasse. Zu jedem Aufruf
    /// wird das aufgerufene Methodensymbol bestimmt; trägt dessen Deklaration das
    /// <c>&lt;NavChoiceCall&gt;</c>-Tag, entsteht eine <see cref="NavChoiceCallAnnotation"/>. Zusätzlich
    /// wird der voll qualifizierte Name der umgebenden <c>{Task}WFSBase</c> mitgeführt (das aufgerufene
    /// Symbol liegt im geschachtelten <c>{Choice}CallContext</c> innerhalb der <c>WFSBase</c>), damit der
    /// C#→C#-Sprung von der Aufrufstelle zur <c>{Choice}Logic</c> möglich ist.
    /// </summary>
    /// <param name="semanticModel">Das Semantikmodell des Dokuments.</param>
    /// <param name="navTaskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="invocationExpressions">Die zu prüfenden Aufrufausdrücke der Klasse.</param>
    /// <returns>Die Folge der gefundenen <see cref="NavChoiceCallAnnotation"/>en.</returns>
    static IEnumerable<NavChoiceCallAnnotation> ReadChoiceCallAnnotation(
        SemanticModel semanticModel,
        NavTaskAnnotation navTaskAnnotation,
        IEnumerable<InvocationExpressionSyntax> invocationExpressions) {

        if (semanticModel == null || navTaskAnnotation == null) {
            yield break;
        }

        foreach (var invocationExpression in invocationExpressions) {

            // Der Choice-Forward wird im Nutzer-Logic-Code über den Call-Context aufgerufen:
            //   next.{Choice}(…)  — Member-Zugriff auf den Context. Der navigierbare Anker ist der
            // Methoden-Bezeichner selbst.
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
            var navChoiceCallTag    = ReadNavTags(declaringMethodNode).FirstOrDefault(tag => tag.TagName == CodeGenFacts.AnnotationTagNavChoiceCall);
            if (navChoiceCallTag == null) {
                continue;
            }

            // Der Forward liegt im geschachtelten {Choice}CallContext, der wiederum in der {Task}WFSBase liegt:
            // methodSymbol → CallContext → WFSBase. Diesen Anker für den C#→C#-Sprung zur {Choice}Logic mitnehmen.
            var wfsBaseFullyQualifiedName = methodSymbol.ContainingType?.ContainingType?.ToDisplayString() ?? string.Empty;

            yield return new NavChoiceCallAnnotation(
                taskAnnotation           : navTaskAnnotation,
                identifier               : identifier,
                choiceName               : navChoiceCallTag.Content,
                wfsBaseFullyQualifiedName: wfsBaseFullyQualifiedName);
        }
    }

    #endregion

    /// <summary>
    /// Bildet eine nach Parameterposition geordnete, vergleichbare Liste der Parametertyp-Anzeigenamen.
    /// Dient dazu, einen konkreten Begin-Aufruf einer von mehreren gleichnamigen Begin-Überladungen
    /// zuzuordnen. Genutzt von <see cref="ReadInitCallAnnotation"/> und vom <c>LocationFinder</c>.
    /// </summary>
    /// <param name="beginLogicParameter">Die Parametersymbole der Begin-Methode.</param>
    /// <returns>Die Typ-Anzeigenamen in Parameterreihenfolge.</returns>
    internal static List<string> ToComparableParameterTypeList(IEnumerable<IParameterSymbol> beginLogicParameter) {
        return beginLogicParameter.OrderBy(p => p.Ordinal)
                                  .Select(p => p.ToDisplayString())
                                  .ToList();
    }

    /// <summary>
    /// Extrahiert die Nav-Annotation-Tags aus der führenden XML-Doku-Trivia eines Syntaxknotens: liest die
    /// Dokumentationskommentare, wählt daraus die XML-Elemente, deren Name mit dem Annotation-Präfix
    /// (<c>Nav</c>, siehe <see cref="Pharmatechnik.Nav.Language.CodeGen.CodeGenInvariants.AnnotationTagPrefix"/>)
    /// beginnt, und liefert je Treffer Tag-Name und -Inhalt. Grundlage aller <c>Read…</c>-Methoden dieses Readers.
    /// </summary>
    /// <param name="node">Der Syntaxknoten (Klasse bzw. Methode), dessen Doku-Trivia gelesen wird.</param>
    /// <returns>Die gefundenen Nav-Tags; leer, wenn der Knoten keine passende Doku-Trivia trägt.</returns>
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

    /// <summary>
    /// Ein einzelnes gelesenes Nav-Tag — der Roh-Tupel aus Tag-Name und -Inhalt, aus dem die
    /// <c>Read…</c>-Methoden die jeweilige Annotation bauen.
    /// </summary>
    sealed class NavTag {
        /// <summary>Der Name des XML-Tags (z.B. <c>NavTask</c>, <c>NavInit</c>).</summary>
        public string TagName { get; init; }
        /// <summary>Der Textinhalt des Tags (z.B. der Task- oder Verbindungspunkt-Name).</summary>
        public string Content { get; init; }
    }        
}