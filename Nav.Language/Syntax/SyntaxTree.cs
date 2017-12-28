﻿#region Using Directives

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Generated;
using Pharmatechnik.Nav.Language.Internal;

#endregion

namespace Pharmatechnik.Nav.Language {

    [Serializable]
    public class SyntaxTree {

        readonly SyntaxNode                    _root;
        readonly SyntaxTokenList               _tokens;
        readonly IReadOnlyList<Diagnostic>     _diagnostics;
        readonly IReadOnlyList<TextLineExtent> _textLines;
        readonly FileInfo                      _fileInfo;
        readonly string                        _sourceText;

        internal SyntaxTree(string sourceText,
                            SyntaxNode root,
                            SyntaxTokenList tokens,
                            IReadOnlyList<TextLineExtent> textLines,
                            IReadOnlyList<Diagnostic> diagnostics,
                            FileInfo fileInfo) {

            _sourceText  = sourceText ?? String.Empty;
            _root        = root;
            _fileInfo    = fileInfo;
            _diagnostics = diagnostics ?? Enumerable.Empty<Diagnostic>().ToList();
            _tokens      = tokens      ?? SyntaxTokenList.Empty;
            _textLines   = textLines   ?? new List<TextLineExtent>();
        }

        [NotNull]
        public SyntaxTokenList Tokens => _tokens;

        [NotNull]
        public IReadOnlyList<TextLineExtent> TextLines => _textLines;

        [NotNull]
        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public SyntaxNode GetRoot() {
            return _root;
        }

        [CanBeNull]
        public FileInfo FileInfo => _fileInfo;

        [NotNull]
        public string SourceText => _sourceText;

        public Location GetLocation(TextExtent extent) {
            return new Location(extent, GetLinePositionExtent(extent), FileInfo?.FullName);
        }

        LinePositionExtent GetLinePositionExtent(TextExtent extent) {

            var start = GetLinePositionAtPosition(extent.Start);
            var end   = GetLinePositionAtPosition(extent.End);

            return new LinePositionExtent(start, end);
        }

        LinePosition GetLinePositionAtPosition(int position) {
            var lineInformaton = GetTextLineExtentAtPositionCore(position);
            return new LinePosition(lineInformaton.Line, position - lineInformaton.Extent.Start);
        }

        public TextLineExtent GetTextLineExtent(int line) {
            return _textLines[line];
        }

        public TextLineExtent GetTextLineExtentAtPosition(int position) {
            if (position < 0 || position > SourceText.Length) {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            return GetTextLineExtentAtPositionCore(position);
        }

        TextLineExtent GetTextLineExtentAtPositionCore(int position) {
            var lineInformaton = _textLines.FindElementAtPosition(position);
            return lineInformaton;
        }
        
        public static SyntaxTree FromFile(string filePath, CancellationToken cancellationToken = default) {

            var content = File.ReadAllText(filePath);

            return ParseTextCore(sourceText       : content, 
                                 treeCreator      : parser => parser.codeGenerationUnit(), 
                                 filePath         : filePath, 
                                 cancellationToken: cancellationToken);
        }

        public static SyntaxTree ParseText(string text, string filePath=null, CancellationToken cancellationToken = default) {

            return ParseTextCore(sourceText       : text, 
                                 treeCreator      : parser => parser.codeGenerationUnit(), 
                                 filePath         : filePath, 
                                 cancellationToken: cancellationToken);
        }
        
        internal static SyntaxTree ParseTextCore(string sourceText, 
                                                 Func<NavGrammarParser, IParseTree> treeCreator, 
                                                 string filePath, 
                                                 Encoding encoding = null, 
                                                 CancellationToken cancellationToken = default) {

            var fileInfo = String.IsNullOrEmpty(filePath) ? null : new FileInfo(filePath);

            sourceText = sourceText ?? String.Empty;

            var stream        = new AntlrInputStream(sourceText);
            var lexer         = new NavGrammarLexer(stream);
            var cts           = new NavCommonTokenStream(lexer);
            var parser        = new NavGrammarParser(cts);
            var errorListener = new NavErrorListener(filePath);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var tree = treeCreator(parser);

            var visitor     = new NavGrammarVisitor(expectedTokenCount: cts.AllTokens.Count);
            var syntax      = visitor.Visit(tree);
            var tokens      = PostprocessTokens(visitor.Tokens, cts, syntax, cancellationToken);
            var textLines   = GetTextLines(sourceText);
            var diagnostics = errorListener.Diagnostics;

            var syntaxTree = new SyntaxTree(sourceText : sourceText, 
                                            root       : syntax, 
                                            tokens     : tokens, 
                                            textLines  : textLines,
                                            diagnostics: diagnostics,
                                            fileInfo   : fileInfo);

            syntax.FinalConstruct(syntaxTree, null);
            
            return syntaxTree;
        }

        static SyntaxTokenList PostprocessTokens(List<SyntaxToken> tokens, NavCommonTokenStream cts, SyntaxNode syntax, CancellationToken cancellationToken) {

            var finalTokens = new List<SyntaxToken>(cts.AllTokens.Count);
            tokens.Sort(SyntaxTokenComparer.Default);

            // Wir haben bereits die signifikanten Token (T) im GrammarVisitor erstellt.
            // Wir können nicht alle Tokens hier ermitteln, da der Visitor viel mehr Kontextinformationen
            // hat. Somit wird aus einem "Identifier" z.B. je nach Kontext ein Keyword, oder Symbol (=> Classification).
            // Was uns hier jedoch noch fehlt sind vor allem die Whitespaces (w) und "unbekannten" (u),
            // die der Parser nie zu Gesicht bekommt. Der TokenStream liefert uns alle Token
            // (candidates = c):
            // -T---TTT---T----T-- <= bereits im Visitor erfasste Token
            // ccccccccccccccccccc <= alle Tokens (candidates)
            // wTwwwTTTwwwTwwwwTuu <= die Tokens, wie wir sie hier haben wollen
            var index = 0;
            foreach (var candidate in cts.AllTokens) {

                cancellationToken.ThrowIfCancellationRequested();

                if (index < tokens.Count) {
                    var existing = tokens[index];
                    // Das Token wurde bereits im Visitor erfasst (T)
                    if (existing.Start == candidate.StartIndex) {
                        finalTokens.Add(existing);
                        index++;
                        continue;
                    }
                }
                // Das Token existiert noch nicht, da es der Parser/Visitor offensichtlich nicht "erwischt hat" (t, u)
                SyntaxTokenClassification tokenClassification;
                switch (candidate.Channel) {
                    case NavGrammarLexer.TriviaChannel:
                        switch (candidate.Type) {
                            case NavGrammarLexer.NewLine:
                                tokenClassification = SyntaxTokenClassification.Whitespace;
                                break;
                            case NavGrammarLexer.Whitespace:
                                tokenClassification = SyntaxTokenClassification.Whitespace;
                                break;
                            case NavGrammarLexer.SingleLineComment:
                                tokenClassification = SyntaxTokenClassification.Comment;
                                break;
                            case NavGrammarLexer.MultiLineComment:
                                tokenClassification = SyntaxTokenClassification.Comment;
                                break;
                            case NavGrammarLexer.Unknown:
                                tokenClassification = SyntaxTokenClassification.Skiped;
                                break;
                            default:
                                // Wir haben sonst eigentlich nix im Trivia Channel
                                throw new ArgumentException();
                        }
                        break;
                    case Lexer.DefaultTokenChannel:
                        tokenClassification = SyntaxTokenClassification.Skiped;
                        break;
                    default:
                        throw new ArgumentException();
                }
                
                // TODO: hier evtl. den "echten" Parent herausfinden...
                SyntaxNode parent = syntax;

                // Fix Für Single Line Comments, da diese leider immer auch das EOL beinhalten
                if(candidate.Type == NavGrammarLexer.SingleLineComment) {
                    foreach(var token in SplitSingleLineCommenTokens(candidate, parent)) {
                        finalTokens.Add(token);
                    }
                } else {
                    finalTokens.Add(SyntaxTokenFactory.CreateToken(candidate, tokenClassification, parent));
                }                
            }   

            return SyntaxTokenList.AttachSortedTokens(finalTokens);
        }

        static IEnumerable<SyntaxToken> SplitSingleLineCommenTokens(IToken candidate, SyntaxNode parent) {
            int newLineIndex = FindNewLineIndexInSingleLineComment(candidate.Text);
            if(newLineIndex > 0) {
                var tokenExtent = TextExtent.FromBounds(candidate.StartIndex, candidate.StartIndex + newLineIndex);
                var newLineExtent = TextExtent.FromBounds(candidate.StartIndex + newLineIndex, candidate.StopIndex + 1);

                yield return SyntaxTokenFactory.CreateToken(tokenExtent, SyntaxTokenType.SingleLineComment, SyntaxTokenClassification.Comment, parent);
                yield return SyntaxTokenFactory.CreateToken(newLineExtent, SyntaxTokenType.NewLine, SyntaxTokenClassification.Whitespace, parent);
            } else {
                yield return SyntaxTokenFactory.CreateToken(candidate, SyntaxTokenClassification.Comment, parent);
            }
        }

        static int FindNewLineIndexInSingleLineComment(string text) {
            char c1 = text[text.Length - 2];
            char c2 = text[text.Length - 1];
            if (c2 == '\n' || c2== '\r') {
                if (c1 == '\n') {
                    return text.Length - 2;
                }
                return text.Length - 1;
            }
            return -1;
        }
        
        static IReadOnlyList<TextLineExtent> GetTextLines(string text) {

            int index;
            int line      = 0;
            int lineStart = 0;
            var lines     = new List<TextLineExtent>();
            for (index = 0; index < text.Length; index++) {

                char c = text[index];

                bool isNewLine = false;

                if (c == '\n') {
                    isNewLine = true;
                } else if (c == '\r') {
                    isNewLine = true;
                    // => \r\n
                    if (index + 1 < text.Length && text[index + 1] == '\n') {
                        index++;
                    }
                }

                if (isNewLine) {
                    // Achtung: Extent End zeigt immer _hinter_ das letzte Zeichen!
                    var lineEnd = index + 1;
                    lines.Add(new TextLineExtent(line, TextExtent.FromBounds(lineStart, lineEnd)));
                    line++;
                    lineStart = lineEnd;
                }
            }

            // Letzte Zeile nicht vergessen. 
            if (index > lineStart) {
                // Achtung: Extent End zeigt immer _hinter_ das letzte Zeichen!
                var lineEnd = index + 1;
                lines.Add(new TextLineExtent(line, TextExtent.FromBounds(lineStart, lineEnd)));
            }

            return lines;
        }
    }
}