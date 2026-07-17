using System;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Factory für die versionierbaren Codegen-Fakten (<see cref="ICodeGenFacts"/>): liefert zu einer
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
    static readonly ICodeGenFacts V2 = new CodeGenFactsV2();

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

        if (version == NavLanguageVersion.Version2) {
            return V2;
        }

        throw new NotSupportedException(
            $"Für die Nav-Sprachversion '{version}' sind keine Codegen-Fakten implementiert.");
    }

    /// <summary>
    /// Version 1 — die historischen Werte. Sie delegiert an die handgeschriebenen
    /// <see cref="CodeGenFacts"/>-Konstanten (bis zur Ablösung von StringTemplate aus <c>.stg</c>
    /// exportiert), unter denen der Bestand byte-identisch übersetzt.
    /// </summary>
    sealed class CodeGenFactsV1: ICodeGenFacts {

        public string BeginMethodPrefix  => CodeGenFacts.BeginMethodPrefix;
        public string ExitMethodPrefix   => CodeGenFacts.ExitMethodPrefix;
        public string LogicMethodSuffix  => CodeGenFacts.LogicMethodSuffix;
        public string WfsClassSuffix     => CodeGenFacts.WfsClassSuffix;
        public string WfsBaseClassSuffix => CodeGenFacts.WfsBaseClassSuffix;
        public string WflNamespaceSuffix => CodeGenFacts.WflNamespaceSuffix;

    }

    /// <summary>
    /// Version 2 — der CallContext-Codegen. Die versionierbare <b>Namensalgebra</b> ist bewusst
    /// identisch zu Version 1: die aus Nav-Knotennamen abgeleiteten Member (<c>Begin{Node}</c>,
    /// <c>After{Node}</c>) und die Klassen-/Namespace-Suffixe (<c>WFS</c>/<c>WFSBase</c>/<c>WFL</c>)
    /// müssen die V1-Schreibweise behalten, damit die invarianten <c>IBegin{Task}WFS</c>-Schnittstellen
    /// über Sprachversionen hinweg konsumierbar bleiben (Cross-Version-<c>taskref</c>). V2 unterscheidet
    /// sich von V1 nicht in den <i>Namen</i>, sondern in der erzeugten <i>Gestalt</i> (CallContext statt
    /// Switch) — und die steckt allein im V2-Emitter, nicht in diesen Fakten.
    /// </summary>
    sealed class CodeGenFactsV2: ICodeGenFacts {

        public string BeginMethodPrefix  => CodeGenFacts.BeginMethodPrefix;
        public string ExitMethodPrefix   => CodeGenFacts.ExitMethodPrefix;
        public string LogicMethodSuffix  => CodeGenFacts.LogicMethodSuffix;
        public string WfsClassSuffix     => CodeGenFacts.WfsClassSuffix;
        public string WfsBaseClassSuffix => CodeGenFacts.WfsBaseClassSuffix;
        public string WflNamespaceSuffix => CodeGenFacts.WflNamespaceSuffix;

    }

}
