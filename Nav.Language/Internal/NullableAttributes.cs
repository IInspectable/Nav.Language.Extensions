#nullable enable

// Polyfill der Nullable-Flussanalyse-Attribute für netstandard2.0 — dort fehlen sie in der BCL. Der
// Compiler erkennt sie allein an Namespace und Gestalt (exakt das BCL-Original). Enthalten ist der
// vollständige Satz, damit die Nullable-Kampagne jedes Flussattribut nutzen kann, ohne diese Datei
// erneut anzufassen (verhindert einen Merge-Hotspot und die CS0122-Falle). Alle Typen sind internal.

namespace System.Diagnostics.CodeAnalysis;

/// <summary>Gibt an, dass <c>null</c> als Eingabe erlaubt ist, auch wenn der Typ es nicht zulässt.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
sealed class AllowNullAttribute: Attribute { }

/// <summary>Gibt an, dass <c>null</c> als Eingabe unzulässig ist, auch wenn der Typ es zuließe.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
sealed class DisallowNullAttribute: Attribute { }

/// <summary>Gibt an, dass ein Ausgabewert <c>null</c> sein kann, auch wenn der Typ es nicht zulässt.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
sealed class MaybeNullAttribute: Attribute { }

/// <summary>Gibt an, dass ein Ausgabewert nicht <c>null</c> ist, auch wenn der Typ es zuließe.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
sealed class NotNullAttribute: Attribute { }

/// <summary>
/// Gibt an, dass der Parameter <c>null</c> sein kann, wenn die Methode den angegebenen
/// <see cref="ReturnValue"/> zurückgibt.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
sealed class MaybeNullWhenAttribute: Attribute {

    public MaybeNullWhenAttribute(bool returnValue) {
        ReturnValue = returnValue;
    }

    public bool ReturnValue { get; }

}

/// <summary>
/// Gibt an, dass der Parameter nicht <c>null</c> ist, wenn die Methode den angegebenen
/// <see cref="ReturnValue"/> zurückgibt.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
sealed class NotNullWhenAttribute: Attribute {

    public NotNullWhenAttribute(bool returnValue) {
        ReturnValue = returnValue;
    }

    public bool ReturnValue { get; }

}

/// <summary>
/// Gibt an, dass der Ausgabewert nicht <c>null</c> ist, wenn der Wert des benannten Parameters nicht
/// <c>null</c> ist.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false, AllowMultiple = true)]
sealed class NotNullIfNotNullAttribute: Attribute {

    public NotNullIfNotNullAttribute(string parameterName) {
        ParameterName = parameterName;
    }

    public string ParameterName { get; }

}

/// <summary>Gibt an, dass die Methode nie regulär zurückkehrt (z.B. immer wirft).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
sealed class DoesNotReturnAttribute: Attribute { }

/// <summary>
/// Gibt an, dass die Methode nicht zurückkehrt, wenn der zugeordnete <see cref="bool"/>-Parameter den
/// angegebenen <see cref="ParameterValue"/> hat.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
sealed class DoesNotReturnIfAttribute: Attribute {

    public DoesNotReturnIfAttribute(bool parameterValue) {
        ParameterValue = parameterValue;
    }

    public bool ParameterValue { get; }

}

/// <summary>Gibt an, dass die gelisteten Member nach Rückkehr der Methode nicht <c>null</c> sind.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
sealed class MemberNotNullAttribute: Attribute {

    public MemberNotNullAttribute(string member) {
        Members = new[] { member };
    }

    public MemberNotNullAttribute(params string[] members) {
        Members = members;
    }

    public string[] Members { get; }

}

/// <summary>
/// Gibt an, dass die gelisteten Member nicht <c>null</c> sind, wenn die Methode den angegebenen
/// <see cref="ReturnValue"/> zurückgibt.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
sealed class MemberNotNullWhenAttribute: Attribute {

    public MemberNotNullWhenAttribute(bool returnValue, string member) {
        ReturnValue = returnValue;
        Members     = new[] { member };
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members) {
        ReturnValue = returnValue;
        Members     = members;
    }

    public bool     ReturnValue { get; }
    public string[] Members     { get; }

}
