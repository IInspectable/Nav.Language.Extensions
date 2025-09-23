lexer grammar NavTokens;

channels {
    TriviaChannel,
    PreprocessorChannel
}

TaskKeyword            : 'task';
TaskrefKeyword         : 'taskref';
InitKeyword            : 'init';
EndKeyword             : 'end';
ChoiceKeyword          : 'choice';
DialogKeyword          : 'dialog';
ViewKeyword            : 'view';
ExitKeyword            : 'exit';
OnKeyword              : 'on';
IfKeyword              : 'if';
ElseKeyword            : 'else';
SpontaneousKeyword     : 'spontaneous';
SpontKeyword           : 'spont';
DoKeyword              : 'do';
ResultKeyword          : 'result';
ParamsKeyword          : 'params';
BaseKeyword            : 'base';
NamespaceprefixKeyword : 'namespaceprefix';
UsingKeyword           : 'using';
CodeKeyword            : 'code';
GeneratetoKeyword      : 'generateto';
NotimplementedKeyword  : 'notimplemented';
AbstractmethodKeyword  : 'abstractmethod';
DonotinjectKeyword     : 'donotinject';
GoToEdgeKeyword        : '-->';
ModalEdgeKeyword       : 'o->';
NonModalEdgeKeyword    : '==>';

//------------------
// DEFAULT_MODE

HashToken                  
    : '#'  
    -> channel(PreprocessorChannel), mode(PreprocessorMode)
    ;

Whitespace
    : WS+
    -> channel(TriviaChannel)
    ;

SingleLineComment
    :   '//' .*? (NL | EOF)
    -> channel(TriviaChannel)
    ;

MultiLineComment
    :   '/*' .*? '*/'
    -> channel(TriviaChannel)
    ;

NewLine
    : NL
    -> channel(TriviaChannel)
    ;

Identifier
    :   IdentifierCharacter+
    ;

OpenBrace
    :   '{'
    ;

CloseBrace
    :   '}'
    ;

OpenParen
    :  '('
    ;

CloseParen
    :   ')'
    ;

OpenBracket
    :   '['
    ;

CloseBracket
    :   ']'
    ;

LessThan
    :   '<'
    ;

GreaterThan
    :   '>'
    ;

Questionmark
    :   '?'
    ;

Semicolon
    :   ';'
    ;

Comma
    :   ','
    ;

Colon
    :   ':'
    ;

StringLiteral
    :   '\"' (StringLiteralCharacter)* '\"'
    ;

Unknown
    :  .
    -> channel(TriviaChannel)
    ;

//------------------
// PreprocessorMode

mode PreprocessorMode;

PreprocessorKeyword
    :   LetterCharacter+   
    -> channel(PreprocessorChannel), mode(PreprocessorTextMode)
    ;

PreprocessorNewLine
    :   NL    
    -> channel(PreprocessorChannel), mode(DEFAULT_MODE)
    ;

//------------------
// PreprocessorTextMode
//
mode PreprocessorTextMode;

PreprocessorText
    :   ~[NL]             
    -> channel(PreprocessorChannel)
    ;

PreprocessorTextNewline
    :   NL     
    -> channel(PreprocessorChannel), type(PreprocessorNewLine), mode(DEFAULT_MODE)
    ;

//------------------
// Fragments
//
fragment NL
    :   '\r\n' | '\r' | '\n'
    | '\u0085'      // <Next Line CHARACTER (U+0085)>'
    | '\u2028'      // '<Line Separator CHARACTER (U+2028)>'
    | '\u2029'      // '<Paragraph Separator CHARACTER (U+2029)>'
    ;

fragment WS
    : UnicodeClassZS //'<Any Character With Unicode Class Zs>'
    | '\u0009'       //'<Horizontal Tab Character (U+0009)>'
    | '\u000B'       //'<Vertical Tab Character (U+000B)>'
    | '\u000C'       //'<Form Feed Character (U+000C)>'
    ;

fragment UnicodeClassZS
    : '\u0020'      // SPACE
    | '\u00A0'      // NO_BREAK SPACE
    | '\u1680'      // OGHAM SPACE MARK
    | '\u180E'      // MONGOLIAN VOWEL SEPARATOR
    | '\u2000'      // EN QUAD
    | '\u2001'      // EM QUAD
    | '\u2002'      // EN SPACE
    | '\u2003'      // EM SPACE
    | '\u2004'      // THREE_PER_EM SPACE
    | '\u2005'      // FOUR_PER_EM SPACE
    | '\u2006'      // SIX_PER_EM SPACE
    | '\u2008'      // PUNCTUATION SPACE
    | '\u2009'      // THIN SPACE
    | '\u200A'      // HAIR SPACE
    | '\u202F'      // NARROW NO_BREAK SPACE
    | '\u3000'      // IDEOGRAPHIC SPACE
    | '\u205F'      // MEDIUM MATHEMATICAL SPACE
    ;

fragment IdentifierCharacter
    :   LetterCharacter
    |   '_'
    |   DecimalDigitCharacter
    |   '.'
    ;

fragment LetterCharacter
    :   'a'..'z'
    |   'A'..'Z'
    |   'Ä'|'Ö'|'Ü'|'ä'|'ö'|'ü'|'ß'
    ;

fragment DecimalDigitCharacter
    :   '0'..'9' 
    ;

fragment StringLiteralCharacter
    :   ~( '\"' | '\u000D' | '\u000A' | '\u2028' | '\u2029')
    ;