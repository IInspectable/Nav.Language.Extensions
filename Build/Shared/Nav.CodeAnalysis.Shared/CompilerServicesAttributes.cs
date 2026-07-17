// Polyfills, damit netstandard2.0-Generatorprojekte moderne C#-Sprachfeatures (Records mit
// init-Accessoren, required-Member) nutzen können. Diese Typen sind erst ab .NET 5 bzw. .NET 7
// im BCL enthalten.

namespace System.Runtime.CompilerServices {

    using ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    static class IsExternalInit {

    }

}

// `required`-Member (C# 11) brauchen RequiredMemberAttribute + CompilerFeatureRequiredAttribute
// (und SetsRequiredMembersAttribute für Konstruktoren, die alle required-Member setzen). In der
// Runtime erst ab .NET 7 enthalten — auf netstandard2.0 hier als Polyfill, auf neueren TFMs
// ausklammern (CS0436).
#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices {

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute: Attribute {

    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute: Attribute {

        public CompilerFeatureRequiredAttribute(string featureName) {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool   IsOptional  { get; init; }

        public const string RefStructs      = nameof(RefStructs);
        public const string RequiredMembers = nameof(RequiredMembers);

    }

}

namespace System.Diagnostics.CodeAnalysis {

    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute: Attribute {

    }

}
#endif
