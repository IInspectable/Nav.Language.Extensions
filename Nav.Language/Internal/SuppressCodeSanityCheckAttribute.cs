using System;
using System.Linq;

namespace Pharmatechnik.Nav.Language.Internal;

/// <summary>
/// Markiert einen Typ oder ein Member als bewusste Ausnahme von den reflexionsbasierten
/// Code-Konventions-Prüfungen der Test-Suite (<c>CodeSanityTests</c>) — etwa der Regel, dass alle
/// <c>ISymbol</c>-Ableitungen <c>sealed</c> sein müssen, oder den Namenskonventionen der Syntax-Tokens.
/// Die Prüfungen überspringen jedes so markierte Element; die verpflichtende <see cref="Reason"/>
/// dokumentiert im Quelltext, warum die Konvention hier absichtlich nicht gilt (z.B. der bewusst
/// unversiegelte <c>NodeReferenceSymbol</c>).
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
sealed class SuppressCodeSanityCheckAttribute : Attribute {

    /// <summary>Erzeugt das Attribut mit der verpflichtenden Begründung <paramref name="reason"/>.</summary>
    public SuppressCodeSanityCheckAttribute(string reason) {
        Reason = reason;
    }

    /// <summary>Die Begründung, warum die betroffene Konvention hier bewusst ausgesetzt wird.</summary>
    public string Reason { get; }

    /// <summary>
    /// Liefert das an <typeparamref name="T"/> gesetzte <see cref="SuppressCodeSanityCheckAttribute"/> oder
    /// <c>null</c>, wenn keines vorhanden ist.
    /// </summary>
    public static SuppressCodeSanityCheckAttribute? GetAttribute<T>() {
        return typeof(T).GetCustomAttributes(false).OfType<SuppressCodeSanityCheckAttribute>().FirstOrDefault();
    }

    /// <summary>
    /// Liefert das am Typ <paramref name="t"/> gesetzte <see cref="SuppressCodeSanityCheckAttribute"/> oder
    /// <c>null</c>, wenn keines vorhanden ist.
    /// </summary>
    public static SuppressCodeSanityCheckAttribute? GetAttribute(Type t) {
        return t.GetCustomAttributes(false).OfType<SuppressCodeSanityCheckAttribute>().FirstOrDefault();
    }
}