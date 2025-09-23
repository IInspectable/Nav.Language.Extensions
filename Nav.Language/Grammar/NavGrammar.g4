/*
Grammar for Nav Language
*/

parser grammar NavGrammar;

options
{ tokenVocab = NavTokens; }

codeGenerationUnit
    :   (codeNamespaceDeclaration
         codeUsingDeclaration*
        )?
        memberDeclaration*
        EOF
    ;

memberDeclaration
    :   includeDirective
    |   taskDeclaration
    |   taskDefinition
    ;


includeDirective
    :   TaskrefKeyword StringLiteral Semicolon
    ;

taskDeclaration
    :   TaskrefKeyword Identifier
                codeNamespaceDeclaration       ?
                codeNotImplementedDeclaration  ?
                codeResultDeclaration          ?
        OpenBrace
            connectionPointNodeDeclaration*
        CloseBrace
    ;

taskDefinition: TaskKeyword Identifier
        codeDeclaration           ?
        codeBaseDeclaration       ?
        codeGenerateToDeclaration ?
        codeParamsDeclaration     ?
        codeResultDeclaration     ?
        OpenBrace
            nodeDeclarationBlock
            transitionDefinitionBlock
        CloseBrace
    ;

nodeDeclarationBlock
    :   nodeDeclaration*
    ;

nodeDeclaration
    :   connectionPointNodeDeclaration
    |   taskNodeDeclaration
    |   choiceNodeDeclaration
    |   dialogNodeDeclaration
    |   viewNodeDeclaration
    ;

connectionPointNodeDeclaration
    :   initNodeDeclaration
    |   exitNodeDeclaration
    |   endNodeDeclaration
    ;

initNodeDeclaration
    :   InitKeyword Identifier? codeAbstractMethodDeclaration? codeParamsDeclaration? doClause? Semicolon
    ;

exitNodeDeclaration
    :   ExitKeyword Identifier Semicolon
    ;

endNodeDeclaration
    :   EndKeyword Semicolon
    ;

taskNodeDeclaration
    :   TaskKeyword Identifier Identifier? codeDoNotInjectDeclaration? codeAbstractMethodDeclaration? Semicolon
    ;

choiceNodeDeclaration
    :   ChoiceKeyword Identifier Semicolon
    ;

dialogNodeDeclaration
    :   DialogKeyword Identifier Semicolon
    ;

viewNodeDeclaration
    :   ViewKeyword Identifier Semicolon
    ;

transitionDefinitionBlock
    :   (transitionDefinition | exitTransitionDefinition)*
    ;

transitionDefinition
    :   sourceNode      { NotifyErrorListeners("missing edge"); }
    |   sourceNode edge { NotifyErrorListeners("missing target node"); }
    |   sourceNode edge targetNode trigger? conditionClause? doClause? Semicolon
    ;

exitTransitionDefinition
    :   identifierSourceNode Colon Identifier edge targetNode conditionClause? doClause? Semicolon
    ;

sourceNode
    :   initSourceNode
    |   identifierSourceNode
    ;

initSourceNode
    :   InitKeyword
    ;

identifierSourceNode
    :   Identifier
    ;

edge:   goToEdge
    |   modalEdge
    |   nonModalEdge
    ;

goToEdge
    :   GoToEdgeKeyword
    ;

modalEdge
    :   ModalEdgeKeyword
    ;

nonModalEdge
    :   NonModalEdgeKeyword
    ;

targetNode
    :   endTargetNode
    |   identifierTargetNode
    ;

endTargetNode
    :   EndKeyword
    ;

identifierTargetNode
    :   Identifier
    ;

conditionClause
    :   ifConditionClause
    |   elseIfConditionClause
    |   elseConditionClause
    ;

ifConditionClause
    :   IfKeyword identifierOrString
    ;

elseIfConditionClause
    :   elseConditionClause ifConditionClause
    ;

elseConditionClause
    :   ElseKeyword
    ;

doClause
    :   DoKeyword identifierOrString
    ;

trigger
    :   signalTrigger
    |   spontaneousTrigger
    ;

spontaneousTrigger
    :   SpontaneousKeyword
    |   SpontKeyword
    ;

signalTrigger
    :   OnKeyword identifier
    ;

identifierOrString
    :   identifier
    |   stringLiteral
    ;

identifier
    :   Identifier
    ;

stringLiteral
    :   StringLiteral
    ;

// ==================
// Code Syntaxen
//
codeNamespaceDeclaration
    :   OpenBracket NamespaceprefixKeyword identifierOrString CloseBracket
    ;

codeUsingDeclaration
    :   OpenBracket UsingKeyword identifierOrString CloseBracket
    ;

codeParamsDeclaration
    :   OpenBracket ParamsKeyword parameterList?  CloseBracket
    ;

codeResultDeclaration
    :   OpenBracket ResultKeyword parameter CloseBracket
    ;

codeBaseDeclaration
    :   OpenBracket BaseKeyword codeType (Colon codeType (Comma codeType)? )? CloseBracket
    ;

codeDeclaration
    :   OpenBracket CodeKeyword StringLiteral* CloseBracket
    ;

codeGenerateToDeclaration
    :   OpenBracket GeneratetoKeyword StringLiteral CloseBracket
    ;

codeNotImplementedDeclaration
    :   OpenBracket NotimplementedKeyword CloseBracket
    ;

codeAbstractMethodDeclaration
    :   OpenBracket AbstractmethodKeyword CloseBracket
    ;

codeDoNotInjectDeclaration
    :   OpenBracket DonotinjectKeyword CloseBracket;

//
parameterList
    :   parameter (Comma parameter)*
    ;

parameter
    :   codeType Identifier?
    ;

codeType
    :   simpleType
    |   genericType
    |   arrayType
    ;

simpleType
    :   Identifier Questionmark?
    ;

genericType
    :   Identifier LessThan codeType (Comma codeType)* GreaterThan
    ;

arrayType
    :   (simpleType | genericType) arrayRankSpecifier+
    ;

arrayRankSpecifier
    :   OpenBracket CloseBracket
    ;