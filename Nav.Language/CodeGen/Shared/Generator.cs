#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Gemeinsame Basis aller Codegeneratoren: hält die <see cref="GenerationOptions"/> und den
/// <see cref="IDisposable"/>-Vertrag, den jeder Generator erfüllt. Abgeleitet sind der versions­spezifische
/// <see cref="CodeGeneratorV1"/> und <c>CodeGeneratorV2</c> (beide zusätzlich <c>ICodeGenerator</c>) sowie
/// der dateischreibende <see cref="FileGenerator"/>. Die Basis selbst kennt kein Erzeugungs-Verhalten —
/// sie stellt nur die geteilte Konfiguration und Lebenszyklus-Klammer bereit.
/// </summary>
public abstract class Generator: IDisposable {

    /// <summary>
    /// Erzeugt die Basis mit den <paramref name="options"/>; <c>null</c> wird auf
    /// <see cref="GenerationOptions.Default"/> normalisiert, sodass <see cref="Options"/> nie <c>null</c> ist.
    /// </summary>
    protected Generator(GenerationOptions? options) {
        Options = options ?? GenerationOptions.Default;
    }

    /// <summary>Die für diesen Generator geltenden Erzeugungsoptionen (Kodierung, Nullable-Kontext, TO-Erzeugung …).</summary>
    public GenerationOptions Options { get; }

    /// <summary>
    /// Gibt die vom Generator gehaltenen Ressourcen frei. Die Basis tut nichts; Ableitungen überschreiben
    /// bei Bedarf. Vorhanden, damit Generatoren einheitlich in einem <c>using</c> verwendet werden können.
    /// </summary>
    public virtual void Dispose() {
    }

}