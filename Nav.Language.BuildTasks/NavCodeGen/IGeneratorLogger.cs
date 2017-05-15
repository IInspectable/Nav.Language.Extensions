#region Using Directives

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks {
    public interface IGeneratorLogger {
        void LogError(string message);
        void LogError(Diagnostic diag  , [CanBeNull] FileSpec fileSpec = null);
        void LogWarning(Diagnostic diag, [CanBeNull] FileSpec fileSpec = null);
    }
}