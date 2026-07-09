#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

// ReSharper disable once InconsistentNaming
sealed class TOCodeModel : FileGenerationCodeModel {

    TOCodeModel(string? relativeSyntaxFileName,
                TaskCodeInfo taskCodeInfo,
                ImmutableList<string> usingNamespaces,
                string? className,
                string? filePath)
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces = usingNamespaces ?? throw new ArgumentNullException(nameof(usingNamespaces));
        ClassName       = className       ?? String.Empty;
    }

    public string ClassName { get; }

    public ImmutableList<string> UsingNamespaces { get; }

    public string IwflNamespace => Task.IwflNamespace;

    public static IEnumerable<TOCodeModel> FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }
        if (pathProvider == null) {
            throw new ArgumentNullException(nameof(pathProvider));
        }

        var taskCodeInfo = TaskCodeInfo.FromTaskDefinition(taskDefinition);

        foreach(var guiNode in taskDefinition.NodeDeclarations.OfType<IGuiNodeSymbol>().Where(n => n.References.Any())) {

            var viewName = guiNode.Name;

            var toClassName = $"{viewName.ToPascalcase()}{CodeGenFacts.ToClassNameSuffix}";
            var filePath    = pathProvider.GetToFileName(guiNode.Name + CodeGenFacts.ToClassNameSuffix);

            var relativeSyntaxFileName = pathProvider.GetRelativePath(filePath, pathProvider.SyntaxFileName);

            yield return new TOCodeModel(
                relativeSyntaxFileName: relativeSyntaxFileName,
                taskCodeInfo          : taskCodeInfo,
                usingNamespaces       : GetUsingNamespaces().ToImmutableList(),
                className             : toClassName,
                filePath              : filePath);
        }
    }

    static IEnumerable<string> GetUsingNamespaces() {

        var namespaces = new List<string> {
            CodeGenFacts.NavigationEngineIwflNamespace
        };

        return namespaces.ToSortedNamespaces();
    }

}
