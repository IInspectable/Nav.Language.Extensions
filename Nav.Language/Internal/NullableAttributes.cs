#nullable enable

// Polyfill der Nullable-Flussanalyse-Attribute für netstandard2.0 — dort fehlen sie in der BCL. Der
// Compiler erkennt sie allein an Namespace und Gestalt (exakt das BCL-Original); enthalten ist nur, was
// im Projekt tatsächlich genutzt wird.

namespace System.Diagnostics.CodeAnalysis;

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
