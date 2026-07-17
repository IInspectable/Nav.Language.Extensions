#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.Common;
using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

/// <summary>
/// Die Nav↔C#-Brücke auf Roslyn-Ebene: löst ein Nav-Symbol (Task, Trigger, Choice, Init, Exit) in die
/// zugehörige <see cref="Location"/> im generierten C#-Code auf — und umgekehrt eine aus dem generierten
/// Code gelesene <see cref="NavTaskAnnotation"/> zurück in die <see cref="Location"/>en des zugehörigen
/// <c>.nav</c>. Rein VS-frei: die Methoden nehmen entweder einen Roslyn-<see cref="Project"/> plus eine
/// <c>CodeInfo</c>/Annotation (C#-Seite) oder den Nav-Quelltext plus eine Annotation (Nav-Seite)
/// entgegen und liefern die neutrale Nav-<see cref="Location"/> — nie einen Roslyn- oder VS-Typ.
/// Konsumenten sind die GoTo-Provider der VS-Extension (Ordner <c>GoToLocation</c>). Schlägt die Auflösung
/// fehl, wirft der jeweilige Pfad eine <see cref="LocationNotFoundException"/> (die Ausnahme-Pfade sind je
/// Methode ausgewiesen); die annotationsgetriebenen Aufrufer-Pfade liefern statt der Ausnahme <c>null</c>.
/// </summary>
public static class LocationFinder {

    static readonly Logger Logger = Logger.Create(typeof(LocationFinder));

    const string MsgUnableToFind0                            = "Unable to find {0}";
    const string MsgUnableToFindTask0InFile1                 = "Unable to find task '{0}' in file '{1}'";
    const string MsgUnableToFindSignalTrigger0InTask1        = "Unable to find signal trigger '{0}' in task '{1}'";
    const string MsgUnableToFindChoice0InTask1               = "Unable to find choice '{0}' in task '{1}'";
    const string MsgUnableToFindInit0InTask1                 = "Unable to find init '{0}' in task '{1}'";
    const string MsgUnableToFindTheExitTransitionsInTask0    = "Unable to find the exit transitions in task '{0}'";
    const string MsgUnableToFindInterface0                   = "Unable to find interface '{0}'";
    const string MsgUnableToFindAClassImplementingInterface0 = "Unable to find a class implementing interface '{0}'";
    const string MsgUnableToFindMatchingOverloadForMethod0   = "Unable to find a matching overload for method '{0}'";
    const string MsgUnableToFindAnyClassesDerivedFrom0       = "Unable to find any classes derived from '{0}'";
    const string MsgUnableToFindTheTaskAnnotation            = "Unable to find the task annotation";
    const string MsgUnableToGetMemberLoation                 = "Unable to get the member location";

    const string MsgErrorWhileParsingNavFile0  = "Error while parsing nav file '{0}'";
    const string MsgMissingProjectForAssembly0 = "Missing project for assembly '{0}'.";

    // Methodenname der abstrakten BeginLogic-Methode für die annotationsgetriebene C#→BeginLogic-
    // Navigation (FindCallBeginLogicDeclarationLocationsAsync). Dieser Einstieg startet an einer
    // <NavInitCall>-Annotation im generierten Code und hat kein Nav-Symbol zur Hand — also auch keine
    // Sprach-Version, aus der sich die versionierbaren Namensbausteine ableiten ließen. Er läuft daher
    // (wie die C#-seitigen Navigations-Regexes der VS-Extension) noch auf der Default-Generation;
    // versionsbewusst wird dieser Pfad erst mit der versionierten Nav→C#-Such-Strategie ("Option B").
    // Der genuine Nav→C#-Pfad (FindTaskBeginDeclarationLocationAsync) zieht seinen Namen dagegen
    // versionsrichtig aus TaskInitCodeInfo.BeginLogicMethodName.
    static readonly string DefaultBeginLogicMethodName = GetDefaultBeginLogicMethodName();

    static string GetDefaultBeginLogicMethodName() {
        var facts = NavCodeGenFacts.For(NavLanguageVersion.Default);
        return $"{facts.BeginMethodPrefix}{facts.LogicMethodSuffix}";
    }

    // Logic-Suffix der Default-Generation — für den annotationsgetriebenen C#→{Choice}Logic-Sprung
    // (FindCallChoiceLogicDeclarationLocationAsync), der kein Nav-Symbol und damit keine Sprach-Version hat.
    static readonly string DefaultLogicMethodSuffix = NavCodeGenFacts.For(NavLanguageVersion.Default).LogicMethodSuffix;

    // Prefix der generierten Begin-Wrapper (Begin{Node}) der Default-Generation — für den Rücksprung von
    // einer <NavInitCall>-Aufrufstelle auf die zugehörige After{Node}-Methode (FindInitCallAfterLocation).
    // Wie die übrigen annotationsgetriebenen Call-Site-Pfade kennt er keine Sprach-Version; die
    // Namensgleichheit über alle Versionen ist in CallSiteVersionAssumptionTests als Invariante gepinnt.
    static readonly string DefaultBeginMethodPrefix = NavCodeGenFacts.For(NavLanguageVersion.Default).BeginMethodPrefix;

    #region FindNavLocationsAsync

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavTaskAnnotation"/> einer generierten WFS-Klasse auf die
    /// <see cref="Location"/> der Task-Definition (den Task-Bezeichner) im <c>.nav</c> auf.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavTaskAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetTaskLocations, cancellationToken);
    }

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavInitAnnotation"/> auf die <see cref="Location"/> des
    /// <c>init</c>-Knotens im <c>.nav</c> auf (adressiert über <see cref="NavInitAnnotation.InitName"/>).
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavInitAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetInitLocations, cancellationToken);
    }

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavExitAnnotation"/> auf die <see cref="Location"/>en der
    /// Exit-Verbindungspunkt-Referenzen im <c>.nav</c> auf. Liefert <see cref="AmbiguousLocation"/>en, weil
    /// ein Exit über mehrere Exit-Transitionen zu mehreren benannten Verbindungspunkten führen kann
    /// (deshalb der eigene, den Namen tragende Location-Typ).
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<AmbiguousLocation>> FindNavLocationsAsync(string sourceText, NavExitAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetExitLocations, cancellationToken);
    }

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavTriggerAnnotation"/> auf die <see cref="Location"/> des zugehörigen
    /// <c>on</c>-Triggers im <c>.nav</c> auf (adressiert über <see cref="NavTriggerAnnotation.TriggerName"/>).
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavTriggerAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetTriggerLocations, cancellationToken);
    }

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavChoiceAnnotation"/> (der <c>{Choice}Logic</c>-Methode) auf die
    /// <see cref="Location"/> des <c>choice</c>-Knotens im <c>.nav</c> auf.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavChoiceAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetChoiceLocations, cancellationToken);
    }

    /// <summary>
    /// C#→Nav: Löst die <see cref="NavChoiceCallAnnotation"/> (die Aufrufstelle <c>next.{Choice}(…)</c>) auf
    /// dieselbe <see cref="Location"/> des <c>choice</c>-Knotens auf wie <see cref="NavChoiceAnnotation"/> —
    /// Knoten und Aufrufstelle adressieren dasselbe Sprungziel im <c>.nav</c>.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavChoiceCallAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetChoiceCallLocations, cancellationToken);
    }

    // TODO Hier sollte bereits eine CodeGenerationUnit an Stelle des Source Texts rein. Alternativ eine "echte "SourceText" Implementierung
    /// <summary>
    /// Gemeinsame Maschinerie aller C#→Nav-Auflösungen: parst den Nav-Quelltext zur
    /// <see cref="CodeGenerationUnit"/>, sucht darin die per <see cref="NavTaskAnnotation.TaskName"/>
    /// benannte <see cref="ITaskDefinitionSymbol">Task-Definition</see> und überlässt das eigentliche
    /// Auflösen dem symbolspezifischen <paramref name="locBuilder"/>. Läuft auf einem Hintergrund-
    /// <see cref="Task"/> (<see cref="Task.Run(Action, CancellationToken)"/>).
    /// </summary>
    /// <param name="sourceText">Der Nav-Quelltext, aus dem die <see cref="CodeGenerationUnit"/> geparst wird.</param>
    /// <param name="annotation">Die aus dem generierten Code gelesene Annotation; liefert Dateiname und Task-Name.</param>
    /// <param name="locBuilder">Baut aus der gefundenen Task und der Annotation die konkreten <see cref="Location"/>en.</param>
    /// <param name="cancellationToken">Bricht Parsen und Symbolsuche ab.</param>
    /// <exception cref="LocationNotFoundException">
    /// Wenn der Quelltext keine gültige <see cref="CodeGenerationUnit"/> ergibt oder die benannte Task fehlt.
    /// </exception>
    static Task<IEnumerable<TLocation>> FindNavLocationsAsync<TAnnotation, TLocation>(
        string sourceText,
        TAnnotation annotation,
        Func<ITaskDefinitionSymbol, TAnnotation, IEnumerable<TLocation>> locBuilder,
        CancellationToken cancellationToken)
        where TAnnotation : NavTaskAnnotation
        where TLocation : Location {

        var locationResult = Task.Run(() => {

            var syntaxTree = SyntaxTree.ParseText(sourceText, annotation.NavFileName, cancellationToken);
            if (!(syntaxTree.Root is CodeGenerationUnitSyntax codeGenerationUnitSyntax)) {
                throw new LocationNotFoundException(String.Format(MsgErrorWhileParsingNavFile0, annotation.NavFileName));
            }

            var codeGenerationUnit = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax, cancellationToken);

            var task = codeGenerationUnit.Symbols
                                         .OfType<ITaskDefinitionSymbol>()
                                         .FirstOrDefault(t => t.Name == annotation.TaskName);

            if (task == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindTask0InFile1, annotation.TaskName, annotation.NavFileName));
            }

            return locBuilder(task, annotation);

        }, cancellationToken);

        return locationResult;
    }

    /// <summary>
    /// Location-Builder für <see cref="NavTaskAnnotation"/>: die <see cref="Location"/> des Task-Bezeichners
    /// in der Task-Definition.
    /// </summary>
    static IEnumerable<Location> GetTaskLocations(ITaskDefinitionSymbol task, NavTaskAnnotation nav) {

        return ToEnumerable(task.Syntax.Identifier.GetLocation());
    }

    /// <summary>
    /// Location-Builder für <see cref="NavTriggerAnnotation"/>: sucht in den Trigger-Transitionen der Task
    /// den per <see cref="NavTriggerAnnotation.TriggerName"/> benannten Trigger und liefert dessen
    /// <see cref="Location"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn kein Trigger dieses Namens existiert.</exception>
    static IEnumerable<Location> GetTriggerLocations(ITaskDefinitionSymbol task, NavTriggerAnnotation triggerAnnotation) {

        var trigger = task.TriggerTransitions
                          .SelectMany(t => t.Triggers)
                          .FirstOrDefault(t => t.Name == triggerAnnotation.TriggerName);

        if (trigger == null) {
            // TODO Evtl. sollte es Locations mit Fehlern geben? Dann würden wir in diesem Fall wenigstens zum task selbst navigieren,
            //      nachdem wir eine Fehlermeldung angezeigt haben.
            throw new LocationNotFoundException(String.Format(MsgUnableToFindSignalTrigger0InTask1, triggerAnnotation.TriggerName, task.Name));
        }

        return ToEnumerable(trigger.Location);
    }

    /// <summary>
    /// Location-Builder für <see cref="NavChoiceAnnotation"/>: die <see cref="Location"/> des Choice-Knotens,
    /// gesucht über <see cref="NavChoiceAnnotation.ChoiceName"/>.
    /// </summary>
    static IEnumerable<Location> GetChoiceLocations(ITaskDefinitionSymbol task, NavChoiceAnnotation choiceAnnotation) {
        return GetChoiceLocationByName(task, choiceAnnotation.ChoiceName);
    }

    /// <summary>
    /// Location-Builder für <see cref="NavChoiceCallAnnotation"/>: dieselbe Choice-Knoten-Suche wie
    /// <see cref="GetChoiceLocations"/>, hier über <see cref="NavChoiceCallAnnotation.ChoiceName"/>.
    /// </summary>
    static IEnumerable<Location> GetChoiceCallLocations(ITaskDefinitionSymbol task, NavChoiceCallAnnotation choiceCallAnnotation) {
        return GetChoiceLocationByName(task, choiceCallAnnotation.ChoiceName);
    }

    // Der Choice-Knoten und die Choice-Aufrufstelle adressieren dasselbe Sprungziel — den Choice-Knoten
    // im .nav. Beide Annotationstypen (NavChoiceAnnotation/NavChoiceCallAnnotation) tragen den ChoiceName,
    // deshalb teilen sie diese Suche.
    /// <summary>
    /// Geteilte Choice-Suche beider Choice-Annotationstypen: findet den <see cref="IChoiceNodeSymbol"/>
    /// dieses Namens in der Task und liefert dessen <see cref="Location"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn kein Choice-Knoten dieses Namens existiert.</exception>
    static IEnumerable<Location> GetChoiceLocationByName(ITaskDefinitionSymbol task, string choiceName) {

        var choiceNode = task.NodeDeclarations
                             .OfType<IChoiceNodeSymbol>()
                             .FirstOrDefault(n => n.Name == choiceName);

        if (choiceNode == null) {
            throw new LocationNotFoundException(String.Format(MsgUnableToFindChoice0InTask1, choiceName, task.Name));
        }

        return ToEnumerable(choiceNode.Location);
    }

    /// <summary>
    /// Location-Builder für <see cref="NavInitAnnotation"/>: findet den <see cref="IInitNodeSymbol"/> mit
    /// <see cref="NavInitAnnotation.InitName"/> in der Task und liefert dessen <see cref="Location"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn kein <c>init</c>-Knoten dieses Namens existiert.</exception>
    static IEnumerable<Location> GetInitLocations(ITaskDefinitionSymbol task, NavInitAnnotation initAnnotation) {

        var initNode = task.NodeDeclarations
                           .OfType<IInitNodeSymbol>()
                           .FirstOrDefault(n => n.Name == initAnnotation.InitName);

        if (initNode == null) {
            // TODO Evtl. sollte es Locations mit Fehlern geben? Dann würden wir in diesem Fall wenigstens zum task selbst navigieren,
            //      nachdem wir eine Fehlermeldung angezeigt haben.
            throw new LocationNotFoundException(String.Format(MsgUnableToFindInit0InTask1, initAnnotation.InitName, task.Name));
        }

        return ToEnumerable(initNode.Location);
    }

    /// <summary>
    /// Location-Builder für <see cref="NavExitAnnotation"/>: sammelt die Exit-Transitionen der Task, deren
    /// Quelle <see cref="NavExitAnnotation.ExitTaskName"/> entspricht, und liefert je Transition den
    /// referenzierten Exit-Verbindungspunkt als benannte <see cref="AmbiguousLocation"/> (mehrere möglich).
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn es keine passende Exit-Transition gibt.</exception>
    static IEnumerable<AmbiguousLocation> GetExitLocations(ITaskDefinitionSymbol task, NavExitAnnotation exitAnnotation) {

        var exitTransitions = task.ExitTransitions
                                  .Where(et => et.SourceReference?.Name        == exitAnnotation.ExitTaskName)
                                  .Where(et => et.ExitConnectionPointReference != null)
                                  .Select(et => new AmbiguousLocation(et.ExitConnectionPointReference?.Location, et.ExitConnectionPointReference?.Name))
                                  .ToList();

        if (!exitTransitions.Any()) {
            // TODO Evtl. sollte es Locations mit Fehlern geben? Dann würden wir in diesem Fall wenigstens zum task selbst navigieren,
            //      nachdem wir eine Fehlermeldung angezeigt haben.
            throw new LocationNotFoundException(String.Format(MsgUnableToFindTheExitTransitionsInTask0, exitAnnotation.ExitTaskName));
        }

        return exitTransitions;
    }

    #endregion

    #region FindCallerLocations

    /// <summary>
    /// Findet — VS-frei, am Roslyn-Level — die C#-Aufrufstellen innerhalb einer (ggf. partiellen) Klasse:
    /// alle <see cref="NavInvocationAnnotation"/>en (die <c>next.{Choice}(…)</c>-Forwards bzw. die
    /// <c>Begin{Node}(…)</c>-Wrapper) über sämtliche Deklarationsdokumente von <paramref name="classSymbol"/>,
    /// eingegrenzt durch <paramref name="filter"/>. Das ist die gemeinsame Suchlogik der beiden
    /// VS-Aufrufer-Provider (Choice-Logik→Aufrufer, Exit-After→Begin-Aufrufer): Klasse samt aller
    /// <c>partial</c>-Deklarationen einsammeln → Annotationen lesen → filtern → auf die Aufrufstelle mappen →
    /// stabil nach Datei und Position sortieren.
    /// </summary>
    public static Task<IList<CallerLocation>> FindCallerLocations(Project project,
                                                                  INamedTypeSymbol classSymbol,
                                                                  Func<NavInvocationAnnotation, bool> filter,
                                                                  CancellationToken cancellationToken) {

        if (project == null) {
            throw new ArgumentNullException(nameof(project));
        }

        if (classSymbol == null) {
            throw new ArgumentNullException(nameof(classSymbol));
        }

        if (filter == null) {
            throw new ArgumentNullException(nameof(filter));
        }

        return Task.Run<IList<CallerLocation>>(() => {

            // Alle Dokumente, in denen die (ggf. partielle) Klasse deklariert ist.
            var documents = classSymbol.DeclaringSyntaxReferences
                                       .Select(reference => project.Solution.GetDocument(reference.SyntaxTree))
                                       .Where(doc => doc != null)
                                       .GroupBy(doc => doc.Id)
                                       .Select(group => group.First());

            var callers = new List<CallerLocation>();
            foreach (var doc in documents) {

                cancellationToken.ThrowIfCancellationRequested();

                var invocations = AnnotationReader.ReadNavTaskAnnotations(doc)
                                                  .OfType<NavInvocationAnnotation>()
                                                  .Where(filter);

                foreach (var invocation in invocations) {

                    var location = ToLocation(invocation.Identifier.GetLocation());
                    if (location == null) {
                        continue;
                    }

                    callers.Add(new CallerLocation(location, invocation.Identifier.Identifier.Text));
                }
            }

            return callers.OrderBy(caller => caller.FilePath)
                          .ThenBy(caller => caller.Start)
                          .ToList();

        }, cancellationToken);
    }

    #endregion

    #region FindCallBeginLogicDeclarationLocationsAsync

    /// <summary>
    /// Annotationsgetriebener C#→C#-Pfad: von einer <see cref="NavInitCallAnnotation"/> (der Aufrufstelle
    /// <c>next.Begin{Node}(…)</c>) auf die <c>BeginLogic</c>-Implementierung des aufgerufenen Sub-Tasks.
    /// Löst dazu das <c>IBegin…WFS</c>-Interface über seinen voll qualifizierten Metadaten-Namen auf, sucht
    /// die implementierende WFS-Klasse und darin die zu den Aufruf-Argumenten passende
    /// <c>BeginLogic</c>-Überladung (siehe <see cref="FindBestBeginLogicOverload"/>). Kennt — wie die
    /// übrigen Call-Site-Pfade — keine Sprach-Version und läuft auf der Default-Generation
    /// (<see cref="DefaultBeginLogicMethodName"/>).
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn Interface, implementierende Klasse, passende Überladung oder deren <see cref="Location"/> fehlen —
    /// oder das Interface nur in Metadaten (ohne Projekt/Quelle) vorliegt.
    /// </exception>
    /// <returns>Die <see cref="Location"/> des <c>BeginLogic</c>-Methodenbezeichners im generierten Code.</returns>
    public static Task<Location> FindCallBeginLogicDeclarationLocationsAsync(Project project, NavInitCallAnnotation initCallAnnotation, CancellationToken cancellationToken) {

        var task = Task.Run(async () => {

            (var beginItf, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, initCallAnnotation.BeginItfFullyQualifiedName, cancellationToken).ConfigureAwait(false);
            if (beginItf == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindInterface0, initCallAnnotation.BeginItfFullyQualifiedName));
            }

            var metaLocation = beginItf.Locations.FirstOrDefault(l => l.IsInMetadata);
            if (metaLocation != null) {
                throw new LocationNotFoundException(String.Format(MsgMissingProjectForAssembly0, metaLocation.MetadataModule?.MetadataName));
            }

            var wfsClass = (await SymbolFinder.FindImplementationsAsync(beginItf, project.Solution, null, cancellationToken))
                          .OfType<INamedTypeSymbol>()
                          .FirstOrDefault();

            if (wfsClass == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindAClassImplementingInterface0, beginItf.ToDisplayString()));
            }

            var beginLogicMethods = wfsClass.GetMembers()
                                            .OfType<IMethodSymbol>()
                                            .Where(m => m.Name == DefaultBeginLogicMethodName);

            // Der erste Parameter ist immer das IBegin---WFS interface.
            var beginParameter = initCallAnnotation.Parameter.Skip(1).ToList();
            var beginMethod    = FindBestBeginLogicOverload(beginParameter, beginLogicMethods);

            if (beginMethod == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindMatchingOverloadForMethod0, DefaultBeginLogicMethodName));
            }

            var syntaxReference = beginMethod.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxReference == null) {
                throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
            }

            var memberSyntax   = await syntaxReference.GetSyntaxAsync(cancellationToken) as MethodDeclarationSyntax;
            var memberLocation = memberSyntax?.Identifier.GetLocation();
            var location       = ToLocation(memberLocation);

            if (location == null) {
                throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
            }

            return location;

        }, cancellationToken);

        return task;
    }

    /// <summary>
    /// Wählt aus den <c>BeginLogic</c>-Überladungen die beste passende zur Aufruf-Argumentliste: bewertet je
    /// Kandidat, wie viele führende Parametertypen übereinstimmen (siehe <see cref="GetParameterMatchCount"/>),
    /// und bevorzugt die höchste Trefferzahl, bei Gleichstand die Überladung mit den wenigsten Parametern.
    /// </summary>
    static IMethodSymbol FindBestBeginLogicOverload(IList<string> beginParameter, IEnumerable<IMethodSymbol> beginLogicMethods) {

        var bestMatch = beginLogicMethods.Select(m => new {
                                              MatchCount     = GetParameterMatchCount(beginParameter, AnnotationReader.ToComparableParameterTypeList(m.Parameters)),
                                              ParameterCount = m.Parameters.Length,
                                              Method         = m
                                          })
                                         .Where(x => x.MatchCount >= 0) // 0 ist OK, falls der Init keine Argumente hat!
                                         .OrderByDescending(x => x.MatchCount)
                                         .ThenBy(x => x.ParameterCount)
                                         .Select(x => x.Method)
                                         .FirstOrDefault();

        return bestMatch;
    }

    /// <summary>
    /// Zählt, wie viele führende Parametertypen von <paramref name="beginParameter"/> (den Aufruf-Argumenten)
    /// und <paramref name="beginLogicParameter"/> (der Kandidaten-Signatur) übereinstimmen — abgebrochen beim
    /// ersten Unterschied.
    /// </summary>
    /// <returns>
    /// Die Zahl der übereinstimmenden führenden Parameter (0 ist zulässig, wenn der Init keine Argumente hat),
    /// oder <c>-1</c>, wenn der Kandidat zu wenige Parameter hat und damit ausscheidet.
    /// </returns>
    static int GetParameterMatchCount(IList<string> beginParameter, IList<string> beginLogicParameter) {

        if (beginLogicParameter.Count < beginParameter.Count) {
            return -1;
        }

        var matchCount = 0;
        for (int i = 0; i < beginParameter.Count; i++) {

            if (beginParameter[i] != beginLogicParameter[i]) {
                break;
            }
            matchCount++;
        }
        return matchCount;
    }

    #endregion

    #region FindInitCallAfterLocation

    /// <summary>
    /// C#→C#: von der Aufrufstelle <c>next.Begin{Node}()</c> eines modal geöffneten Sub-Tasks auf die
    /// zugehörige <c>After{Node}</c>-Rücksprungmethode desselben Tasks. Der Begin-Prefix der
    /// Default-Generation wird abgestreift (<c>Begin{Node}</c> → <c>{Node}</c>) und darüber die
    /// <see cref="NavExitAnnotation"/> mit passendem <see cref="NavExitAnnotation.ExitTaskName"/> (bei
    /// gleicher Task-/Datei-Verankerung) gesucht. Wie
    /// <see cref="FindCallBeginLogicDeclarationLocationsAsync"/> kennt dieser annotationsgetriebene Pfad
    /// keine Sprach-Version (Default-Generation, in <c>CallSiteVersionAssumptionTests</c> gepinnt).
    /// </summary>
    /// <returns>
    /// Das <c>After{Node}</c>-Ziel als benannte Location (Anzeigename = Methoden-Bezeichner) oder
    /// <c>null</c>, wenn es zu dieser Aufrufstelle keine passende Exit-Annotation gibt — dann bietet der
    /// Host nur das <c>BeginLogic</c>-Ziel an. Wirft also bewusst keine <see cref="LocationNotFoundException"/>.
    /// </returns>
    [CanBeNull]
    public static CallerLocation FindInitCallAfterLocation(
        NavInitCallAnnotation initCallAnnotation,
        IEnumerable<NavExitAnnotation> exitAnnotations) {

        if (initCallAnnotation == null) {
            throw new ArgumentNullException(nameof(initCallAnnotation));
        }

        if (exitAnnotations == null) {
            throw new ArgumentNullException(nameof(exitAnnotations));
        }

        var beginIdentifier = initCallAnnotation.Identifier.Identifier.Text;
        if (!beginIdentifier.StartsWith(DefaultBeginMethodPrefix, StringComparison.Ordinal)) {
            return null;
        }

        var exitTaskName = beginIdentifier.Substring(DefaultBeginMethodPrefix.Length);

        var exitAnnotation = exitAnnotations.FirstOrDefault(
            a => a.ExitTaskName == exitTaskName                &&
                 a.TaskName     == initCallAnnotation.TaskName &&
                 a.NavFileName  == initCallAnnotation.NavFileName);

        if (exitAnnotation == null) {
            return null;
        }

        var identifier = exitAnnotation.MethodDeclarationSyntax.Identifier;
        var location   = ToLocation(identifier.GetLocation());
        if (location == null) {
            return null;
        }

        return new CallerLocation(location, identifier.Text);
    }

    #endregion

    #region FindTaskIBeginInterfaceDeclarationLocations

    /// <summary>
    /// Nav→C#: Löst eine Task-Deklaration (<c>taskref</c>) auf die <see cref="Location"/>en des generierten
    /// <c>IBegin…WFS</c>-Interfaces auf. Der Interface-Name kommt aus
    /// <see cref="TaskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName"/>; geliefert werden die
    /// Bezeichner-Locations aller Teil-Deklarationen des Interfaces.
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn das Interface nicht auflösbar ist, nur in Metadaten vorliegt oder keine gültige Location liefert.
    /// </exception>
    public static Task<IList<Location>> FindTaskIBeginInterfaceDeclarationLocations(Project project, TaskDeclarationCodeInfo codegenInfo, CancellationToken cancellationToken) {
        var task = Task.Run(async () => {

            (var beginItf, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, codegenInfo.FullyQualifiedBeginInterfaceName, cancellationToken).ConfigureAwait(false);
            if (beginItf == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.FullyQualifiedBeginInterfaceName));
            }

            var metaLocation = beginItf.Locations.FirstOrDefault(l => l.IsInMetadata);
            if(metaLocation != null) {
                throw new LocationNotFoundException(String.Format(MsgMissingProjectForAssembly0, metaLocation.MetadataModule?.MetadataName));
            }

            var impls = beginItf.DeclaringSyntaxReferences.Select(dsr=>dsr.GetSyntax()).OfType<InterfaceDeclarationSyntax>();

            IList<Location> locs = new List<Location>();
            foreach (var impl in impls) {
                var loc = impl.Identifier.GetLocation();

                var lineSpan = loc.GetLineSpan();
                if (!lineSpan.IsValid) {
                    continue;
                }

                var filePath   = loc.SourceTree?.FilePath;
                var textExtent = loc.SourceSpan.ToTextExtent();
                var lineRange  = lineSpan.ToLineRange();

                locs.Add(new Location(textExtent, lineRange, filePath));
            }

            if (!locs.Any()) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.FullyQualifiedBeginInterfaceName));
            }
            return locs;
        } , cancellationToken );

        return task;
    }


    #endregion
    
    #region FindTaskDeclarationLocationsAsync

    /// <summary>
    /// Nav→C#: Löst eine Task auf die <see cref="Location"/>en der generierten WFS-Klassen auf. Da die
    /// konkreten Klassen theoretisch in einem anderen Namespace als die Basisklasse liegen können, steigt die
    /// Suche von der über <see cref="TaskCodeInfo.FullyQualifiedWfsBaseName"/> aufgelösten
    /// <c>{Task}WFSBase</c>-Klasse per Roslyn-<c>SymbolFinder.FindDerivedClassesAsync</c> zu den abgeleiteten
    /// Klassen ab. Generierte Dateien (<c>*.generated.cs</c>) werden ausgeblendet.
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn die Basisklasse fehlt oder keine (nicht-generierte) abgeleitete Klasse gefunden wird.
    /// </exception>
    public static Task<IList<Location>> FindTaskDeclarationLocationsAsync(Project project, TaskCodeInfo codegenInfo, CancellationToken cancellationToken) {

        var task = Task.Run(async () => {

            (var wfsBaseSymbol, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, codegenInfo.FullyQualifiedWfsBaseName, cancellationToken).ConfigureAwait(false);
            if (wfsBaseSymbol == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.FullyQualifiedWfsBaseName));
            }

            // Wir kennen de facto nur den Basisklassen Namespace + Namen, da die abgeleiteten Klassen theoretisch in einem
            // anderen Namespace liegen können. Deshalb steigen wir von der Basisklasse zu den abgeleiteten Klassen ab.
            var derived = await SymbolFinder.FindDerivedClassesAsync(wfsBaseSymbol, project.Solution, ToImmutableSet(project), cancellationToken);

            var derivedSyntaxes = derived.SelectMany(d => d.DeclaringSyntaxReferences)
                                         .Select(dsr => dsr.GetSyntax())
                                         .OfType<TypeDeclarationSyntax>();

            IList<Location> locs = new List<Location>();
            foreach (var ds in derivedSyntaxes) {
                    
                var loc = ds.Identifier.GetLocation();

                var filePath = loc.SourceTree?.FilePath;
                // TODO Evtl. Option um .generated files auch anzuzeigen
                if (filePath?.EndsWith("generated.cs") == true) {
                    continue;
                }

                var lineSpan = loc.GetLineSpan();
                if (!lineSpan.IsValid) {
                    continue;
                }

                var textExtent = loc.SourceSpan.ToTextExtent();
                var lineRange  = lineSpan.ToLineRange();
                    
                locs.Add(new Location(textExtent, lineRange, filePath));
            }

            if(!locs.Any()) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindAnyClassesDerivedFrom0, codegenInfo.FullyQualifiedWfsBaseName));
            }
            return locs;

        }, cancellationToken);

        return task;
    }

    #endregion

    #region FindTriggerDeclarationLocationsAsync

    /// <summary>
    /// Nav→C#: Löst einen Signal-Trigger auf die <see cref="Location"/> der zugehörigen generierten
    /// <c>TriggerLogic</c>-Methode auf (Methodenname aus <see cref="SignalTriggerCodeInfo.TriggerLogicMethodName"/>,
    /// gesucht über <see cref="FindTriggerMethodSymbol"/>).
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn die Methode bzw. deren Location nicht gefunden wird.</exception>
    public static Task<Location> FindTriggerDeclarationLocationsAsync(Project project, SignalTriggerCodeInfo codegenInfo, CancellationToken cancellationToken) {

        var task = Task.Run(async ()  =>  {

            var memberSymbol   = await FindTriggerMethodSymbol(project, codegenInfo, cancellationToken);               
            var memberLocation = memberSymbol?.Locations.FirstOrDefault();
            var location       = ToLocation(memberLocation);

            if (location == null) {
                throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
            }

            return location;

        }, cancellationToken);

        return task;
    }

    /// <summary>
    /// Steigt von der über <see cref="TaskCodeInfo.FullyQualifiedWfsBaseName"/> aufgelösten WFS-Basisklasse zu
    /// den abgeleiteten Klassen ab und sucht dort das Member mit
    /// <see cref="SignalTriggerCodeInfo.TriggerLogicMethodName"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">Wenn die WFS-Basisklasse nicht auflösbar ist.</exception>
    static async Task<Microsoft.CodeAnalysis.ISymbol> FindTriggerMethodSymbol(Project project, SignalTriggerCodeInfo codegenInfo, CancellationToken cancellationToken) {

        (var wfsBaseSymbol, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName, cancellationToken).ConfigureAwait(false);
        if (wfsBaseSymbol == null) {
            throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName));
        }

        // Wir kennen de facto nur den Basisklassen Namespace + Namen, da die abgeleiteten Klassen theoretisch in einem
        // anderen Namespace liegen können. Deshalb steigen wir von der Basisklasse zu den abgeleiteten Klassen ab.
        var derived      = await SymbolFinder.FindDerivedClassesAsync(wfsBaseSymbol, project.Solution, ToImmutableSet(project), cancellationToken);
        var memberSymbol = derived.SelectMany(d => d.GetMembers(codegenInfo.TriggerLogicMethodName)).FirstOrDefault();

        return memberSymbol;
    }

    #endregion

    #region FindTaskBeginDeclarationLocationAsync

    /// <summary>
    /// Nav→C#: Löst einen <c>init</c>-Knoten auf die <see cref="Location"/> der zugehörigen generierten
    /// <c>BeginLogic</c>-Methode auf. Da mehrere <c>BeginLogic</c>-Überladungen existieren können, wird die
    /// richtige über die aus dem generierten Code gelesene <see cref="NavInitAnnotation"/> identifiziert
    /// (<see cref="NavInitAnnotation.InitName"/> muss <see cref="TaskInitCodeInfo.InitName"/> entsprechen).
    /// Genuiner Nav→C#-Pfad: der Methodenname stammt versionsrichtig aus
    /// <see cref="TaskInitCodeInfo.BeginLogicMethodName"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn Basisklasse, Task-Annotation oder eine passende <c>BeginLogic</c>-Methode fehlen.
    /// </exception>
    public static Task<Location> FindTaskBeginDeclarationLocationAsync(Project project, TaskInitCodeInfo codegenInfo, CancellationToken cancellationToken) {
        var task = Task.Run(async () => {

            (var wfsBaseSymbol, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName, cancellationToken).ConfigureAwait(false);
            if (wfsBaseSymbol == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName));
            }

            var taskAnnotation = wfsBaseSymbol.DeclaringSyntaxReferences
                                              .Select(sr => sr.GetSyntax())
                                              .OfType<ClassDeclarationSyntax>()
                                              .Select(cd => AnnotationReader.ReadNavTaskAnnotation(cd, wfsBaseSymbol))
                                              .FirstOrDefault();

            if (taskAnnotation == null) {
                throw new LocationNotFoundException(MsgUnableToFindTheTaskAnnotation);
            }

            var derived = await SymbolFinder.FindDerivedClassesAsync(wfsBaseSymbol, project.Solution, ToImmutableSet(project), cancellationToken);

            var beginLogics = derived.SelectMany(d=> d.GetMembers(codegenInfo.BeginLogicMethodName).OfType<IMethodSymbol>());

            foreach(var beginLogic in beginLogics) {

                var methodDeclaration = beginLogic.DeclaringSyntaxReferences
                                                  .Select(sr => sr.GetSyntax())
                                                  .OfType<MethodDeclarationSyntax>()
                                                  .FirstOrDefault();

                if (methodDeclaration == null) {
                    continue;
                }
                
                var initAnnotation = AnnotationReader.ReadNavInitAnnotation(taskAnnotation, methodDeclaration, beginLogic);
                if (initAnnotation?.InitName != codegenInfo.InitName) {
                    continue;
                }

                var location = ToLocation(methodDeclaration.Identifier.GetLocation());
                if (location == null) {
                    throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
                }

                return location;                    
            }

            throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.BeginLogicMethodName));

        }, cancellationToken);

        return task;
    }

    #endregion

    #region FindTaskExitDeclarationLocationAsync

    /// <summary>
    /// Nav→C#: Löst einen <c>exit</c>-Verbindungspunkt auf die <see cref="Location"/> der zugehörigen
    /// generierten <c>AfterLogic</c>-Methode auf (Methodenname aus
    /// <see cref="TaskExitCodeInfo.AfterLogicMethodName"/>, gefunden über den Abstieg von der WFS-Basisklasse
    /// zu den abgeleiteten Klassen).
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn die WFS-Basisklasse oder die Ziel-Location nicht gefunden wird.
    /// </exception>
    public static Task<Location> FindTaskExitDeclarationLocationAsync(Project project, TaskExitCodeInfo codegenInfo, CancellationToken cancellationToken) {

        var task = Task.Run(async () => {

            (var wfsBaseSymbol, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName, cancellationToken).ConfigureAwait(false);
            if (wfsBaseSymbol == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, codegenInfo.ContainingTask.FullyQualifiedWfsBaseName));
            }

            // Wir kennen de facto nur den Basisklassen Namespace + Namen, da die abgeleiteten Klassen theoretisch in einem
            // anderen Namespace liegen können. Deshalb steigen wir von der Basisklasse zu den abgeleiteten Klassen ab.
            var derived        = await SymbolFinder.FindDerivedClassesAsync(wfsBaseSymbol, project.Solution, ToImmutableSet(project), cancellationToken);
            var memberSymbol   = derived.SelectMany(d => d.GetMembers(codegenInfo.AfterLogicMethodName)).FirstOrDefault();
            var memberLocation = memberSymbol?.Locations.FirstOrDefault();
                
            var location = ToLocation(memberLocation);
            if (location == null) {
                throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
            }

            return location;

        }, cancellationToken);

        return task;
    }

    #endregion

    #region FindChoiceLogicDeclarationLocationAsync

    /// <summary>
    /// Genuiner Nav→C#-Pfad (Choice-Knoten → <c>{Choice}Logic</c>): der Logic-Name kommt versionsrichtig aus
    /// <see cref="ChoiceCodeInfo.ChoiceLogicMethodName"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<Location> FindChoiceLogicDeclarationLocationAsync(Project project, ChoiceCodeInfo codegenInfo, CancellationToken cancellationToken) {
        return FindChoiceLogicDeclarationLocationAsync(
            project              : project,
            wfsBaseFqn           : codegenInfo.ContainingTask.FullyQualifiedWfsBaseName,
            choiceLogicMethodName: codegenInfo.ChoiceLogicMethodName,
            cancellationToken    : cancellationToken);
    }

    /// <summary>
    /// Annotationsgetriebener C#→C#-Pfad (Aufrufstelle <c>next.{Choice}(…)</c> → <c>{Choice}Logic</c>): startet an
    /// einer <c>&lt;NavChoiceCall&gt;</c>-Annotation ohne Nav-Symbol, der Logic-Name läuft daher — wie
    /// <see cref="DefaultBeginLogicMethodName"/> — auf der Default-Generation.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    public static Task<Location> FindCallChoiceLogicDeclarationLocationAsync(Project project, NavChoiceCallAnnotation choiceCallAnnotation, CancellationToken cancellationToken) {
        var choiceLogicMethodName = $"{choiceCallAnnotation.ChoiceName.ToPascalcase()}{DefaultLogicMethodSuffix}";
        return FindChoiceLogicDeclarationLocationAsync(
            project              : project,
            wfsBaseFqn           : choiceCallAnnotation.WfsBaseFullyQualifiedName,
            choiceLogicMethodName: choiceLogicMethodName,
            cancellationToken    : cancellationToken);
    }

    /// <summary>
    /// Geteilte Implementierung beider <c>{Choice}Logic</c>-Auflösungen (genuiner Nav→C#-Pfad und
    /// annotationsgetriebener Call-Site-Pfad): löst die WFS-Basisklasse über <paramref name="wfsBaseFqn"/>
    /// auf, steigt zu den abgeleiteten Klassen ab und liefert die <see cref="Location"/> des Members
    /// <paramref name="choiceLogicMethodName"/>.
    /// </summary>
    /// <exception cref="LocationNotFoundException">
    /// Wenn die Basisklasse oder die Ziel-Location nicht gefunden wird.
    /// </exception>
    static Task<Location> FindChoiceLogicDeclarationLocationAsync(Project project, string wfsBaseFqn, string choiceLogicMethodName, CancellationToken cancellationToken) {

        var task = Task.Run(async () => {

            (var wfsBaseSymbol, project) = await GetTypeByMetadataNameWithSharedProjectsAsync(project, wfsBaseFqn, cancellationToken).ConfigureAwait(false);
            if (wfsBaseSymbol == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFind0, wfsBaseFqn));
            }

            // Wir kennen de facto nur den Basisklassen Namespace + Namen, da die abgeleiteten Klassen theoretisch in einem
            // anderen Namespace liegen können. Deshalb steigen wir von der Basisklasse zu den abgeleiteten Klassen ab.
            var derived        = await SymbolFinder.FindDerivedClassesAsync(wfsBaseSymbol, project.Solution, ToImmutableSet(project), cancellationToken);
            var memberSymbol   = derived.SelectMany(d => d.GetMembers(choiceLogicMethodName)).FirstOrDefault();
            var memberLocation = memberSymbol?.Locations.FirstOrDefault();

            var location = ToLocation(memberLocation);
            if (location == null) {
                throw new LocationNotFoundException(MsgUnableToGetMemberLoation);
            }

            return location;

        }, cancellationToken);

        return task;
    }

    #endregion

    /// <summary>
    /// Löst einen Typ über seinen voll qualifizierten Metadaten-Namen in der Compilation des
    /// <paramref name="project"/> auf. Findet er sich dort nicht und ist das Projekt ein
    /// <c>.Shared</c>-Projekt (Shared-/Client-Aufteilung), wird zusätzlich im gleichnamigen Projekt ohne das
    /// <c>.Shared</c>-Suffix gesucht.
    /// </summary>
    /// <returns>
    /// Der gefundene Typ samt dem <see cref="Project"/>, dessen Compilation ihn enthält, oder
    /// <c>(null, null)</c>, wenn er nirgends gefunden wird.
    /// </returns>
    static async Task<(INamedTypeSymbol Symbol, Project Project)> GetTypeByMetadataNameWithSharedProjectsAsync(Project project, string fullyQualifiedMetadataName, CancellationToken cancellationToken) {

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        var symbol = compilation?.GetTypeByMetadataName(fullyQualifiedMetadataName);
        if (symbol != null) {
            return (symbol, project);
        }

        // Alternative Suche für Shared/Client Style
        const string sharedSuffix = ".Shared";
        if (project.AssemblyName.EndsWith(sharedSuffix)) {

            var newLength               = project.AssemblyName.Length - sharedSuffix.Length;
            var alternativeAssemblyName = project.AssemblyName.Substring(0, newLength);

            foreach (var proj in project.Solution.Projects.Where(p => p.AssemblyName == alternativeAssemblyName)) {

                compilation = await proj.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                symbol      = compilation?.GetTypeByMetadataName(fullyQualifiedMetadataName);

                if (symbol != null) {
                    return (symbol, proj);
                }
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Übersetzt eine Roslyn-<see cref="Microsoft.CodeAnalysis.Location"/> in die neutrale Nav-<see cref="Location"/>
    /// (Datei, zeichenbasierter Extent, Zeilenbereich) — der Rand, an dem die Brücke Roslyn-Positionen verlässt.
    /// </summary>
    /// <returns>
    /// Die Nav-<see cref="Location"/> oder <c>null</c>, wenn <paramref name="memberLocation"/> fehlt oder
    /// keinen gültigen Zeilenbereich hat (z.B. reine Metadaten-Location).
    /// </returns>
    [CanBeNull]
    public static Location ToLocation([CanBeNull] Microsoft.CodeAnalysis.Location memberLocation) {

        if (memberLocation == null) {
            Logger.Debug($"{nameof(ToLocation)}: {nameof(memberLocation)} is null");
            return null;
        }

        var lineSpan = memberLocation.GetLineSpan();
        if (!lineSpan.IsValid) {
            Logger.Debug($"{nameof(ToLocation)}: lineSpan is not valid");
            return null;
        }

        var textExtent = memberLocation.SourceSpan.ToTextExtent();
        var lineRange  = lineSpan.ToLineRange();
        var filePath   = memberLocation.SourceTree?.FilePath;

        var loc = new Location(textExtent, lineRange, filePath);

        return loc;
    }

    /// <summary>Hebt einen Einzelwert in eine einelementige <see cref="IEnumerable{T}"/>.</summary>
    static IEnumerable<T> ToEnumerable<T>(T value) {
        return new[] { value };
    }

    /// <summary>
    /// Verpackt ein einzelnes Element als <see cref="IImmutableSet{T}"/> — für die auf ein Projekt
    /// eingegrenzte Roslyn-Symbolsuche (<c>SymbolFinder.FindDerivedClassesAsync</c>).
    /// </summary>
    static IImmutableSet<T> ToImmutableSet<T>(T item) {
        return new[] { item }.ToImmutableHashSet();
    }       
}