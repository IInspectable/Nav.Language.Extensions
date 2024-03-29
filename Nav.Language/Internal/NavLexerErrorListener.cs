﻿#region Using Directives

using System;
using System.Collections.Immutable;

using Antlr4.Runtime;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal; 

sealed class NavLexerErrorListener: IAntlrErrorListener<int> {

    public NavLexerErrorListener(SourceText sourceText, ImmutableArray<Diagnostic>.Builder diagnostics) {
        SourceText  = sourceText  ?? throw new ArgumentNullException(nameof(sourceText));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public SourceText                         SourceText  { get; }
    public ImmutableArray<Diagnostic>.Builder Diagnostics { get; }

    public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) {

        var diagnostic = SyntaxErrorFactory.CreateDiagnostic(SourceText, line, charPositionInLine, msg);

        Diagnostics.Add(diagnostic);

    }

}