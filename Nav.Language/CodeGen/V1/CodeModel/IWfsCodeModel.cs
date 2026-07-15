#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Trägt die Datei mit dem <b>öffentlichen Task-Interface <c>I{Task}WFS</c></b> (Konsument:
/// <c>IWfsEmitter</c>). Das Interface bildet den invarianten Schnittstellen-Vertrag (Name/Namespace aus
/// <see cref="CodeGenInvariants"/>, nicht versionierbar) und deklariert je Trigger-Transition
/// (<see cref="TriggerTransitions"/>) die <c>On{Trigger}(…)</c>-Signatur; als Basis dient
/// <see cref="BaseInterfaceName"/> (aus <c>base</c>-Deklaration oder Default). Regression-Beispiel:
/// <c>ISimpleTaskWFS.generated.expected.cs</c> (leeres Interface) bzw.
/// <c>ITestWFS.generated.expected.cs</c>.
/// </summary>
sealed class IWfsCodeModel : FileGenerationCodeModel {

    IWfsCodeModel(TaskCodeInfo taskCodeInfo, 
                  string relativeSyntaxFileName, 
                  string filePath, 
                  ImmutableList<string> usingNamespaces, 
                  string baseInterfaceName, 
                  ImmutableList<TriggerTransitionCodeModel> triggerTransitions) 

        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {
        UsingNamespaces    = usingNamespaces    ?? throw new ArgumentNullException(nameof(usingNamespaces));
        BaseInterfaceName  = baseInterfaceName  ?? throw new ArgumentNullException(nameof(baseInterfaceName));
        TriggerTransitions = triggerTransitions ?? throw new ArgumentNullException(nameof(triggerTransitions));
    }

    /// <summary>Die sortierten <c>using</c>-Namespaces im Kopf der Datei.</summary>
    public ImmutableList<string>                     UsingNamespaces    { get; }        
    /// <summary>Der (invariante) Interface-Namespace (<c>{ns}.IWFL</c>); siehe <see cref="TaskCodeInfo.IwflNamespace"/>.</summary>
    public string                                    Namespace          => Task.IwflNamespace;
    /// <summary>Der Interface-Name (<c>I{Task}WFS</c>); siehe <see cref="TaskCodeInfo.IWfsTypeName"/>.</summary>
    public string                                    InterfaceName      => Task.IWfsTypeName;
    /// <summary>Der Basis-Interface-Name (<c>I{Task}WFS: <b>{Base}</b></c>) — aus der <c>base</c>-Deklaration (<c>IwfsBaseType</c>) oder <see cref="CodeGenFacts.DefaultIwfsBaseType"/>.</summary>
    public string                                    BaseInterfaceName  { get; }
    /// <summary>Die Trigger-Transitionen → <c>On{Trigger}(…)</c>-Interface-Methoden; siehe <see cref="CodeModelBuilder.GetTriggerTransitions"/>.</summary>
    public ImmutableList<TriggerTransitionCodeModel> TriggerTransitions { get; }

    /// <summary>
    /// Fabrik: baut das <see cref="IWfsCodeModel"/> aus dem Task-Symbol. Der Basis-Interface-Name kommt
    /// aus der <c>base</c>-Deklaration (<c>IwfsBaseType</c>) bzw. dem Default; die <c>using</c>-Liste
    /// hängt vom <see cref="GenerationOptions.Strict"/>-Modus ab.
    /// </summary>
    public static IWfsCodeModel FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }
        if (pathProvider == null) {
            throw new ArgumentNullException(nameof(pathProvider));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.IWfsFileName, pathProvider.SyntaxFileName);

        var namespaces         = GetUsingNamespaces(taskDefinition, taskCodeInfo, options);
        var triggerTransitions = CodeModelBuilder.GetTriggerTransitions(taskDefinition, taskCodeInfo);
            
        return new IWfsCodeModel(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.IWfsFileName, 
            usingNamespaces       : namespaces.ToImmutableList(), 
            baseInterfaceName     : taskDefinition.Syntax.CodeBaseDeclaration?.IwfsBaseType?.ToString() ?? CodeGenFacts.DefaultIwfsBaseType, 
            triggerTransitions    : triggerTransitions.ToImmutableList());
    }

    /// <summary>
    /// Stellt die <c>using</c>-Liste des Interfaces zusammen. Im <see cref="GenerationOptions.Strict"/>-Modus
    /// werden nur der Engine-IWFL-Namespace und die auf <see cref="CodeGenFacts.IwflNamespaceSuffix"/>
    /// endenden Nav-<c>using</c>s aufgenommen (das Interface soll ausschließlich gegen Interface-Namespaces
    /// binden); sonst zusätzlich der eigene Interface-Namespace und alle Nav-<c>using</c>s. Ergebnis sortiert.
    /// </summary>
    private static IEnumerable<string> GetUsingNamespaces(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo, GenerationOptions options) {

        var namespaces = new List<string>();
        if (options.Strict) {
            namespaces.Add(CodeGenFacts.NavigationEngineIwflNamespace);
            namespaces.AddRange(taskDefinition.CodeGenerationUnit
                                              .GetCodeUsingNamespaces()
                                              .Where(ns => ns.EndsWith(CodeGenFacts.IwflNamespaceSuffix)));
        } else {
            namespaces.Add(taskCodeInfo.IwflNamespace);
            namespaces.Add(CodeGenFacts.NavigationEngineIwflNamespace);
            namespaces.AddRange(taskDefinition.CodeGenerationUnit.GetCodeUsingNamespaces());
        }

        return namespaces.ToSortedNamespaces();
    }

}