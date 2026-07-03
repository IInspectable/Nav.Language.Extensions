#nullable enable

namespace Pharmatechnik.Nav.Language.Generator;

public interface ILogger {
    void LogVerbose(string message);
    void LogInfo(string message);
    void LogWarning(Diagnostic diag);
    void LogError(string message);
    void LogError(Diagnostic diag);
}