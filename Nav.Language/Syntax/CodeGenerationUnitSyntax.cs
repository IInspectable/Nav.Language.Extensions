#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("")]
public partial class CodeGenerationUnitSyntax: SyntaxNode {

    internal CodeGenerationUnitSyntax(
        TextExtent extent,
        VersionDirectiveSyntax languageVersionDirective,
        CodeNamespaceDeclarationSyntax codeNamespaceDeclaration,
        IReadOnlyList<CodeUsingDeclarationSyntax> codeUsingDeclarations,
        IReadOnlyList<MemberDeclarationSyntax> memberDeclarations
    )
        : base(extent) {

        // Die (optionale) Versions-Direktive steht am Dateikopf — als erster Kindknoten, damit die
        // Kinder in Quelltext-Reihenfolge bleiben.
        AddChildNode(LanguageVersionDirective = languageVersionDirective);
        AddChildNode(CodeNamespace            = codeNamespaceDeclaration);
        AddChildNodes(CodeUsings              = codeUsingDeclarations);
        AddChildNodes(Members                 = memberDeclarations);
    }

    /// <summary>
    /// Die (optionale) Sprach-Versions-Direktive <c>#pragma version</c> am Dateikopf, oder <c>null</c>,
    /// wenn die Datei keine trägt.
    /// </summary>
    [CanBeNull]
    public VersionDirectiveSyntax LanguageVersionDirective { get; }

    /// <summary>
    /// Die Sprach-Version dieser Datei: der von <see cref="LanguageVersionDirective"/> festgelegte Wert,
    /// sonst <see cref="NavLanguageVersion.Default"/> (das historische Verhalten ohne Pragma).
    /// </summary>
    public NavLanguageVersion LanguageVersion => LanguageVersionDirective?.Version ?? NavLanguageVersion.Default;

    [CanBeNull]
    public CodeNamespaceDeclarationSyntax CodeNamespace { get; }

    [NotNull]
    public IReadOnlyList<CodeUsingDeclarationSyntax> CodeUsings { get; }

    [NotNull]
    public IReadOnlyList<MemberDeclarationSyntax> Members { get; }

    [NotNull]
    public IReadOnlyList<IncludeDirectiveSyntax> Includes => Members.OfType<IncludeDirectiveSyntax>().ToList();

    [NotNull]
    public IReadOnlyList<TaskDeclarationSyntax> TaskDeclarations => Members.OfType<TaskDeclarationSyntax>().ToList();

    [NotNull]
    public IReadOnlyList<TaskDefinitionSyntax> TaskDefinitions => Members.OfType<TaskDefinitionSyntax>().ToList();

}