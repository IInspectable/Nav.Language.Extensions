﻿#region Using Directives

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
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.Common;
using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

public static class LocationFinder {

    static readonly Logger Logger = Logger.Create(typeof(LocationFinder));

    const string MsgUnableToFind0                            = "Unable to find {0}";
    const string MsgUnableToFindTask0InFile1                 = "Unable to find task '{0}' in file '{1}'";
    const string MsgUnableToFindSignalTrigger0InTask1        = "Unable to find signal trigger '{0}' in task '{1}'";
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

    const string BeginLogicMethodName = CodeGenFacts.BeginMethodPrefix +CodeGenFacts.LogicMethodSuffix;

    #region FindNavLocationsAsync

    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavTaskAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetTaskLocations, cancellationToken);
    }

    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavInitAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetInitLocations, cancellationToken);
    }

    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<AmbiguousLocation>> FindNavLocationsAsync(string sourceText, NavExitAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetExitLocations, cancellationToken);
    }

    /// <exception cref="LocationNotFoundException"/>
    public static Task<IEnumerable<Location>> FindNavLocationsAsync(string sourceText, NavTriggerAnnotation annotation, CancellationToken cancellationToken) {
        return FindNavLocationsAsync(sourceText, annotation, GetTriggerLocations, cancellationToken);
    }

    // TODO Hier sollte bereits eine CodeGenerationUnit an Stelle des Source Texts rein. Alternativ eine "echte "SourceText" Implementierung
    /// <exception cref="LocationNotFoundException"/>
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

    static IEnumerable<Location> GetTaskLocations(ITaskDefinitionSymbol task, NavTaskAnnotation nav) {

        return ToEnumerable(task.Syntax.Identifier.GetLocation());
    }

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

    #region FindCallBeginLogicDeclarationLocationsAsync

    /// <summary>
    /// Findet die entsprechende BeginXYLogic Implementierung.
    /// </summary>
    /// <exception cref="LocationNotFoundException"/>
    /// <returns></returns>
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
                                            .Where(m => m.Name == BeginLogicMethodName);

            // Der erste Parameter ist immer das IBegin---WFS interface.
            var beginParameter = initCallAnnotation.Parameter.Skip(1).ToList();
            var beginMethod    = FindBestBeginLogicOverload(beginParameter, beginLogicMethods);

            if (beginMethod == null) {
                throw new LocationNotFoundException(String.Format(MsgUnableToFindMatchingOverloadForMethod0, BeginLogicMethodName));
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

    #region FindTaskIBeginInterfaceDeclarationLocations

    /// <exception cref="LocationNotFoundException"/>
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

    /// <exception cref="LocationNotFoundException"/>
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

    /// <exception cref="LocationNotFoundException"/>
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

    /// <exception cref="LocationNotFoundException"/>
    public static async Task<Microsoft.CodeAnalysis.ISymbol> FindTriggerMethodSymbol(Project project, SignalTriggerCodeInfo codegenInfo, CancellationToken cancellationToken) {

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

    /// <exception cref="LocationNotFoundException"/>
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

            throw new LocationNotFoundException(String.Format(MsgUnableToFind0, BeginLogicMethodName));

        }, cancellationToken);

        return task;
    }

    #endregion

    #region FindTaskExitDeclarationLocationAsync

    /// <exception cref="LocationNotFoundException"/>
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

    static IEnumerable<T> ToEnumerable<T>(T value) {
        return new[] { value };
    }

    static IImmutableSet<T> ToImmutableSet<T>(T item) {
        return new[] { item }.ToImmutableHashSet();
    }       
}