using System;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Fabrik für die versionierbaren Codegen-Fakten (<see cref="ICodeGenFacts"/>): liefert zu einer
/// <see cref="NavLanguageVersion"/> die passende Facts-Instanz. So schaltet die Codegen-Namensalgebra
/// <b>pro Datei</b> auf die Werte ihrer Generation um — es gibt keinen prozessweiten „V2-Modus".
/// <para>
/// Derzeit ist ausschließlich <see cref="NavLanguageVersion.Version1"/> implementiert; sie liefert
/// exakt die historischen Werte (bezogen aus <see cref="CodeGenFacts"/>), unter denen der gesamte
/// Bestand byte-identisch übersetzt. Weitere Generationen werden hier als eigene
/// <see cref="ICodeGenFacts"/>-Implementierung ergänzt und in <see cref="For"/> aufgenommen.
/// </para>
/// </summary>
public static class NavCodeGenFacts {

    static readonly ICodeGenFacts V1 = new CodeGenFactsV1();

    /// <summary>
    /// Liefert die versionierbaren Codegen-Fakten der angegebenen Sprach-Version. Für eine (noch)
    /// nicht implementierte Version wirft die Methode <see cref="NotSupportedException"/> — der
    /// Aufrufer reicht stets eine bereits geprüfte Version (<see cref="NavLanguageVersion.IsSupported"/>)
    /// herein.
    /// </summary>
    public static ICodeGenFacts For(NavLanguageVersion version) {

        if (version == NavLanguageVersion.Version1) {
            return V1;
        }

        throw new NotSupportedException(
            $"Für die Nav-Sprachversion '{version}' sind keine Codegen-Fakten implementiert.");
    }

    /// <summary>
    /// Version 1 — die historischen Werte. Sie delegiert an die (noch aus der StringTemplate-Quelle
    /// exportierten) <see cref="CodeGenFacts"/>-Konstanten, damit V1 und die <c>.stg</c>-Templates
    /// dieselbe einzige Wertequelle teilen und der Bestand byte-identisch bleibt.
    /// </summary>
    sealed class CodeGenFactsV1: ICodeGenFacts {

        public string BeginMethodPrefix  => CodeGenFacts.BeginMethodPrefix;
        public string ExitMethodPrefix   => CodeGenFacts.ExitMethodPrefix;
        public string LogicMethodSuffix  => CodeGenFacts.LogicMethodSuffix;
        public string WfsClassSuffix     => CodeGenFacts.WfsClassSuffix;
        public string WfsBaseClassSuffix => CodeGenFacts.WfsBaseClassSuffix;
        public string WflNamespaceSuffix => CodeGenFacts.WflNamespaceSuffix;

    }

}
