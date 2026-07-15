#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Trägt die Datei mit dem <b>Begin-Interface <c>IBegin{Task}WFS</c></b> (Konsument:
/// <c>IBeginWfsEmitter</c>) — die Schnittstelle, über die ein aufrufender Task diesen Task startet. Sie
/// deklariert je Init-Transition (<see cref="InitTransitions"/>) eine <c>Begin(…)</c>-Signatur (liefert
/// <c>IINIT_TASK</c>), erbt von <see cref="BaseInterfaceName"/> und nimmt die wörtlich durchgereichten
/// <c>code</c>-Deklarationen (<see cref="CodeDeclarations"/>) auf. Regression-Beispiel:
/// <c>IBeginSimpleTaskWFS.generated.expected.cs</c>.
/// </summary>
// ReSharper disable once InconsistentNaming
sealed class IBeginWfsCodeModel : FileGenerationCodeModel {

    IBeginWfsCodeModel(TaskCodeInfo taskCodeInfo, 
                       string relativeSyntaxFileName, 
                       string filePath, 
                       ImmutableList<string> usingNamespaces, 
                       ImmutableList<InitTransitionCodeModel> initTransitions,
                       ImmutableList<string> codeDeclarations) 
        :base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces  = usingNamespaces  ?? throw new ArgumentNullException(nameof(usingNamespaces));
        InitTransitions  = initTransitions  ?? throw new ArgumentNullException(nameof(initTransitions));
        CodeDeclarations = codeDeclarations ?? throw new ArgumentNullException(nameof(codeDeclarations));
    }

    /// <summary>Der Namespace des Begin-Interfaces (<c>{ns}.WFL</c>); siehe <see cref="TaskCodeInfo.WflNamespace"/>.</summary>
    public string Namespace         => Task.WflNamespace;
    /// <summary>Der Basis-Interface-Name (<c>IBegin{Task}WFS: <b>{Base}</b></c>) — aus <c>base</c>-Deklaration oder Default; siehe <see cref="TaskCodeInfo.IBeginWfsBaseTypeName"/>.</summary>
    public string BaseInterfaceName => Task.IBeginWfsBaseTypeName;

    /// <summary>Die sortierten <c>using</c>-Namespaces im Kopf der Datei.</summary>
    public ImmutableList<string>                  UsingNamespaces  { get; }
    /// <summary>Die Init-Transitionen → <c>Begin(…)</c>-Interface-Methoden; siehe <see cref="CodeModelBuilder.GetInitTransitions"/>.</summary>
    public ImmutableList<InitTransitionCodeModel> InitTransitions  { get; }
    /// <summary>Die wörtlich in das Interface durchgereichten <c>code</c>-Deklarationen; siehe <see cref="CodeModelBuilder.GetCodeDeclarations"/>.</summary>
    public ImmutableList<string>                  CodeDeclarations { get; }
        
    /// <summary>
    /// Fabrik: baut das <see cref="IBeginWfsCodeModel"/> aus dem Task-Symbol (Namespaces,
    /// <c>code</c>-Deklarationen, Init-Transitionen, Datei-/Syntaxpfad über den
    /// <paramref name="pathProvider"/>).
    /// </summary>
    public static IBeginWfsCodeModel FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }
        if (pathProvider == null) {
            throw new ArgumentNullException(nameof(pathProvider));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.IBeginWfsFileName, pathProvider.SyntaxFileName);

        var namespaces       = GetUsingNamespaces(taskDefinition, taskCodeInfo);
        var codeDeclarations = CodeModelBuilder.GetCodeDeclarations(taskDefinition);
        var initTransitions  = CodeModelBuilder.GetInitTransitions(taskDefinition, taskCodeInfo);
            
        return new IBeginWfsCodeModel(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.IBeginWfsFileName,
            usingNamespaces       : namespaces.ToImmutableList(),
            initTransitions       : initTransitions.ToImmutableList(),
            codeDeclarations      : codeDeclarations.ToImmutableList());
    }

    /// <summary>
    /// Stellt die <c>using</c>-Liste des Begin-Interfaces zusammen: Interface-Namespace des Tasks, die
    /// Navigation-Engine-Namespaces und die im Nav-Code deklarierten <c>using</c>s, sortiert.
    /// </summary>
    private static IEnumerable<string> GetUsingNamespaces(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        // ReSharper disable once UseObjectOrCollectionInitializer
        var namespaces = new List<string>();

        namespaces.Add(taskCodeInfo.IwflNamespace);
        namespaces.Add(CodeGenFacts.NavigationEngineIwflNamespace);
        namespaces.Add(CodeGenFacts.NavigationEngineWflNamespace);
        namespaces.AddRange(taskDefinition.CodeGenerationUnit.GetCodeUsingNamespaces());

        return namespaces.ToSortedNamespaces();
    }
}