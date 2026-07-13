#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Wurzelknoten einer vollständig geparsten <c>.nav</c>-Datei (Grammatik-Einstiegsregel
/// <c>codeGenerationUnit</c>): die optionale wirksame Sprach-Versions-Direktive, der Datei-Kopf mit seinen
/// Code-Deklarationen (<c>[namespaceprefix …]</c>, <c>[using …]</c>) und die Top-Level-Member
/// (Include-Direktiven, Task-Deklarationen, Task-Definitionen — siehe <see cref="MemberDeclarationSyntax"/>).
/// <see cref="SyntaxTree.ParseText"/> liefert einen Baum mit diesem Knoten als <see cref="SyntaxTree.Root"/>.
/// </summary>
[Serializable]
[SampleSyntax("")]
public partial class CodeGenerationUnitSyntax: SyntaxNode {

    internal CodeGenerationUnitSyntax(
        TextExtent extent,
        VersionDirectiveSyntax? languageVersionDirective,
        CodeNamespaceDeclarationSyntax? codeNamespaceDeclaration,
        IReadOnlyList<CodeUsingDeclarationSyntax> codeUsingDeclarations,
        IReadOnlyList<MemberDeclarationSyntax> memberDeclarations
    )
        : base(extent) {

        LanguageVersionDirective = languageVersionDirective;
        AddChildNode(CodeNamespace = codeNamespaceDeclaration);
        AddChildNodes(CodeUsings   = codeUsingDeclarations);
        AddChildNodes(Members      = memberDeclarations);
    }

    /// <summary>
    /// Die (optionale) wirksame Sprach-Versions-Direktive <c>#version</c> am Dateikopf, oder
    /// <c>null</c>, wenn die Datei keine wirksame trägt. Welche Direktive wirksam ist (nur ganz oben, nicht
    /// doppelt), bestimmt der Parser beim Aufbau; deplatzierte oder wiederholte Versions-Direktiven bleiben
    /// als Knoten in den <see cref="SyntaxTree.Directives"/> erhalten, sind aber nicht wirksam.
    /// </summary>
    public VersionDirectiveSyntax? LanguageVersionDirective { get; }

    /// <summary>
    /// Die Sprach-Version dieser Datei: der von <see cref="LanguageVersionDirective"/> festgelegte Wert,
    /// sonst <see cref="NavLanguageVersion.Default"/> (das historische Verhalten ohne Pragma).
    /// </summary>
    public NavLanguageVersion LanguageVersion => LanguageVersionDirective?.Version ?? NavLanguageVersion.Default;

    /// <summary>
    /// Die optionale <c>[namespaceprefix …]</c>-Deklaration am Datei-Kopf
    /// (<see cref="CodeNamespaceDeclarationSyntax"/>), oder <c>null</c>.
    /// </summary>
    public CodeNamespaceDeclarationSyntax? CodeNamespace { get; }

    /// <summary>
    /// Die <c>[using …]</c>-Deklarationen am Datei-Kopf (<see cref="CodeUsingDeclarationSyntax"/>) in
    /// Quelltext-Reihenfolge — als einzige Code-Deklaration wiederholbar; ggf. leer.
    /// </summary>
    public IReadOnlyList<CodeUsingDeclarationSyntax> CodeUsings { get; }

    /// <summary>
    /// Alle Top-Level-Member der Datei in Quelltext-Reihenfolge: Include-Direktiven, Task-Deklarationen und
    /// Task-Definitionen (siehe <see cref="MemberDeclarationSyntax"/>). Die typgefilterten Sichten liefern
    /// <see cref="Includes"/>, <see cref="TaskDeclarations"/> und <see cref="TaskDefinitions"/>.
    /// </summary>
    public IReadOnlyList<MemberDeclarationSyntax> Members { get; }

    /// <summary>
    /// Die Include-Direktiven (<c>taskref "datei.nav";</c>, <see cref="IncludeDirectiveSyntax"/>) unter den
    /// <see cref="Members"/> — bei jedem Zugriff neu gefiltert und materialisiert.
    /// </summary>
    public IReadOnlyList<IncludeDirectiveSyntax> Includes => Members.OfType<IncludeDirectiveSyntax>().ToList();

    /// <summary>
    /// Die Task-Deklarationen (<c>taskref Name { … }</c>, <see cref="TaskDeclarationSyntax"/>) unter den
    /// <see cref="Members"/> — bei jedem Zugriff neu gefiltert und materialisiert.
    /// </summary>
    public IReadOnlyList<TaskDeclarationSyntax> TaskDeclarations => Members.OfType<TaskDeclarationSyntax>().ToList();

    /// <summary>
    /// Die Task-Definitionen (<c>task Name { … }</c>, <see cref="TaskDefinitionSyntax"/>) unter den
    /// <see cref="Members"/> — bei jedem Zugriff neu gefiltert und materialisiert.
    /// </summary>
    public IReadOnlyList<TaskDefinitionSyntax> TaskDefinitions => Members.OfType<TaskDefinitionSyntax>().ToList();

}