#nullable enable

using System;
using System.Linq;

namespace Pharmatechnik.Nav.Language;

public static class SampleSyntax {

    public static string? Of<T>() where T : SyntaxNode {
        return SampleSyntaxAttribute.GetAttribute<T>()?.Syntax;
    }

    public static string? Of(Type type) {
        return SampleSyntaxAttribute.GetAttribute(type)?.Syntax;
    }

}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SampleSyntaxAttribute: Attribute {

    public SampleSyntaxAttribute(string syntax) {
        Syntax = syntax;
    }

    public string Syntax { get; }

    public static SampleSyntaxAttribute? GetAttribute<T>() where T : SyntaxNode {
        return typeof(T).GetCustomAttributes(false).OfType<SampleSyntaxAttribute>().FirstOrDefault();
    }

    public static SampleSyntaxAttribute? GetAttribute(Type t) {
        return t.GetCustomAttributes(false).OfType<SampleSyntaxAttribute>().FirstOrDefault();
    }

}