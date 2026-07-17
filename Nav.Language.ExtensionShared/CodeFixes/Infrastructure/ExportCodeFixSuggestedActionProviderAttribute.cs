#region Using Directives

using System;
using System.ComponentModel.Composition;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Exportiert eine Klasse per MEF als <see cref="ICodeFixSuggestedActionProvider"/> — die deklarative Art,
/// einen Fix-Provider bei der <see cref="CodeFixSuggestedActionProviderService"/>-Aggregation anzumelden.
/// Der mitgegebene Name dient als Metadatum zur Identifikation des Providers.
/// </summary>
[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
class ExportCodeFixSuggestedActionProviderAttribute : ExportAttribute {
    
    /// <summary>Exportiert unter dem <see cref="ICodeFixSuggestedActionProvider"/>-Vertrag mit dem angegebenen Namen.</summary>
    /// <param name="name">Der Bezeichner des Providers (MEF-Metadatum).</param>
    public ExportCodeFixSuggestedActionProviderAttribute(string name): base(typeof(ICodeFixSuggestedActionProvider)) {
        Name = name;
    }

    /// <summary>Der Bezeichner des exportierten Providers.</summary>
    public string Name { get; }
}