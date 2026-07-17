using System;
using System.Linq;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Komfort-Zugriff auf das per <see cref="SampleSyntaxAttribute"/> an einer Syntax-Knoten-Klasse
/// hinterlegte Nav-Quelltext-Beispiel.
/// </summary>
public static class SampleSyntax {

    /// <summary>
    /// Das Beispiel-Snippet des Knoten-Typs <typeparamref name="T"/> — <c>null</c>, wenn kein
    /// <see cref="SampleSyntaxAttribute"/> hinterlegt ist.
    /// </summary>
    public static string? Of<T>() where T : SyntaxNode {
        return SampleSyntaxAttribute.GetAttribute<T>()?.Syntax;
    }

    /// <summary>
    /// Das Beispiel-Snippet des Knoten-Typs <paramref name="type"/> — <c>null</c>, wenn kein
    /// <see cref="SampleSyntaxAttribute"/> hinterlegt ist.
    /// </summary>
    public static string? Of(Type type) {
        return SampleSyntaxAttribute.GetAttribute(type)?.Syntax;
    }

}

/// <summary>
/// Hinterlegt an einer <see cref="SyntaxNode"/>-Klasse ein repräsentatives Nav-Quelltext-Beispiel des
/// Konstrukts, z.B. <c>[SampleSyntax("taskref \"file.nav\";")]</c> an
/// <see cref="IncludeDirectiveSyntax"/>. Die Tests parsen das Beispiel über den per-Regel-Einstieg
/// des Knotens (<see cref="Syntax"/>) und prüfen daran u.a. die Token-Properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SampleSyntaxAttribute: Attribute {

    /// <summary>Initialisiert das Attribut mit dem Beispiel-Snippet <paramref name="syntax"/>.</summary>
    public SampleSyntaxAttribute(string syntax) {
        Syntax = syntax;
    }

    /// <summary>Das Nav-Quelltext-Beispiel des Konstrukts.</summary>
    public string Syntax { get; }

    /// <summary>
    /// Das am Knoten-Typ <typeparamref name="T"/> hinterlegte Attribut — <c>null</c>, wenn keins
    /// vorhanden ist.
    /// </summary>
    public static SampleSyntaxAttribute? GetAttribute<T>() where T : SyntaxNode {
        return typeof(T).GetCustomAttributes(false).OfType<SampleSyntaxAttribute>().FirstOrDefault();
    }

    /// <summary>
    /// Das am Knoten-Typ <paramref name="t"/> hinterlegte Attribut — <c>null</c>, wenn keins
    /// vorhanden ist.
    /// </summary>
    public static SampleSyntaxAttribute? GetAttribute(Type t) {
        return t.GetCustomAttributes(false).OfType<SampleSyntaxAttribute>().FirstOrDefault();
    }

}