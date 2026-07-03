#nullable enable

#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

abstract class FileGenerationCodeModel : CodeModel {

    protected FileGenerationCodeModel(TaskCodeInfo taskCodeInfo, string? relativeSyntaxFileName, string? filePath) {
        RelativeSyntaxFileName = relativeSyntaxFileName ?? String.Empty;
        Task                   = taskCodeInfo           ?? throw new ArgumentNullException(nameof(taskCodeInfo));
        FilePath               = filePath               ?? String.Empty;
    }

    public TaskCodeInfo Task                   { get; }
    public string       RelativeSyntaxFileName { get; }
    public string       FilePath               { get; }               
}