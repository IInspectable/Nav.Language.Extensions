#nullable enable

using System;
using System.Linq;

namespace Pharmatechnik.Nav.Language.Internal;

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
sealed class SuppressCodeSanityCheckAttribute : Attribute {

    public SuppressCodeSanityCheckAttribute(string reason) {
        Reason = reason;
    }

    public string Reason { get; }

    public static SuppressCodeSanityCheckAttribute? GetAttribute<T>() {
        return typeof(T).GetCustomAttributes(false).OfType<SuppressCodeSanityCheckAttribute>().FirstOrDefault();
    }

    public static SuppressCodeSanityCheckAttribute? GetAttribute(Type t) {
        return t.GetCustomAttributes(false).OfType<SuppressCodeSanityCheckAttribute>().FirstOrDefault();
    }
}