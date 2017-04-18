﻿#region Using Directives

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using JetBrains.Annotations;
using Pharmatechnik.Nav.Language.Internal;

#endregion

namespace Pharmatechnik.Nav.Language {

    [Serializable]
    public sealed class SyntaxTokenList: IReadOnlyList<SyntaxToken> {

        static readonly IReadOnlyList<SyntaxToken> EmptyTokens= new List<SyntaxToken>(Enumerable.Empty<SyntaxToken>()).AsReadOnly();
        readonly IReadOnlyList<SyntaxToken> _tokens;

        public SyntaxTokenList(): this(null) {                
        }

        public SyntaxTokenList(List<SyntaxToken> tokens) {

            if(tokens?.Count != 0) {
                var tokenList = new List<SyntaxToken>(tokens ?? Enumerable.Empty<SyntaxToken>());
                tokenList.Sort((x, y) => x.Start - y.Start);

                _tokens = tokenList;
            } else {
                _tokens = EmptyTokens;
            }      
        }

        public IEnumerator<SyntaxToken> GetEnumerator() {
            return _tokens.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public int Count { get { return _tokens.Count; } }

        public SyntaxToken this[int index] {
            get { return _tokens[index]; }
        }

        [NotNull]
        public IEnumerable<SyntaxToken> this[TextExtent extent, bool includeOverlapping = false] {
            get { return _tokens.GetElements(extent, includeOverlapping); }
        }

        public SyntaxToken FindAtPosition(int position) {
            if(position < 0) {
                return SyntaxToken.Missing;
            }
            return _tokens.FindElementAtPosition(position);
        }

        internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, SyntaxTokenType type, bool nextToken) {
            SyntaxToken token = currentToken;
            while (!(token = NextOrPrevious(node, token, nextToken)).IsMissing) {
                if (token.Type == type) {
                    return token;
                }
            }
            return SyntaxToken.Missing;
        }

        internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, SyntaxTokenClassification tokenClassification, bool nextToken) {
            SyntaxToken token = currentToken;
            while (!(token = NextOrPrevious(node, token, nextToken)).IsMissing) {
                if (token.Classification == tokenClassification) {
                    return token;
                }
            }
            return SyntaxToken.Missing;
        }

        internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, bool nextToken) {
            return _tokens.NextOrPreviousElement(node, currentToken, nextToken, SyntaxToken.Missing);
        }
    }
}
