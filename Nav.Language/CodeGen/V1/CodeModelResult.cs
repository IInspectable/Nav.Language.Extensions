#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Bündel aller dateibezogenen CodeModels <b>eines Tasks</b> — das Zwischenergebnis der ersten
/// V1-Generierungsstufe (<see cref="CodeGeneratorV1"/>): Aus dem Task-Symbol baut der Generator dieses
/// Result, im zweiten Schritt rendert er je enthaltenes Modell über den passenden Emitter eine
/// <c>CodeGenerationSpec</c>. Welche Modelle gesetzt sind, steuern die <see cref="GenerationOptions"/>
/// (<c>GenerateWflClasses</c>/<c>GenerateIwflClasses</c>/<c>GenerateToClasses</c>) — nicht erzeugte
/// Artefakte bleiben <c>null</c> bzw. leer.
/// </summary>
sealed class CodeModelResult {

    public CodeModelResult(
        ITaskDefinitionSymbol taskDefinition,
        IBeginWfsCodeModel? beginWfsCodeModel,
        IWfsCodeModel? iwfsCodeModel,
        WfsBaseCodeModel? wfsBaseCodeModel,
        WfsCodeModel? wfsCodeModel,
        IEnumerable<TOCodeModel>? toCodeModels) {

        TaskDefinition     = taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition));
        IBeginWfsCodeModel = beginWfsCodeModel;
        IWfsCodeModel      = iwfsCodeModel;
        WfsBaseCodeModel   = wfsBaseCodeModel;
        WfsCodeModel       = wfsCodeModel;
        TOCodeModels       = (toCodeModels ?? Enumerable.Empty<TOCodeModel>()).ToImmutableList();
    }

    /// <summary>Das Task-Symbol, für das die Modelle gebaut wurden (landet als Träger in der <c>CodeGenerationResult</c>).</summary>
    public ITaskDefinitionSymbol TaskDefinition { get; }

    /// <summary>Das Modell der <c>IBegin{Task}WFS</c>-Datei — <c>null</c>, wenn keine WFL-Klassen erzeugt werden.</summary>
    public IBeginWfsCodeModel? IBeginWfsCodeModel { get; }

    /// <summary>Das Modell der <c>I{Task}WFS</c>-Datei — <c>null</c>, wenn keine IWFL-Klassen erzeugt werden.</summary>
    public IWfsCodeModel? IWfsCodeModel { get; }

    /// <summary>Das Modell der generierten <c>{Task}WFSBase</c>/<c>{Task}WFS</c>-Datei — <c>null</c>, wenn keine WFL-Klassen erzeugt werden.</summary>
    public WfsBaseCodeModel? WfsBaseCodeModel { get; }

    /// <summary>Das Modell der einmalig angelegten Benutzer-Datei <c>{Task}WFS</c> — <c>null</c>, wenn keine WFL-Klassen erzeugt werden.</summary>
    public WfsCodeModel? WfsCodeModel { get; }

    /// <summary>Die Modelle der <c>{View}TO</c>-Stub-Dateien — leer, wenn keine TO-Klassen erzeugt werden.</summary>
    public ImmutableList<TOCodeModel> TOCodeModels { get; }

}