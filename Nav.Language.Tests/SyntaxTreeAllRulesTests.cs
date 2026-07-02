using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

// ReSharper disable PossibleNullReferenceException => Dann soll es so sein. Test wird dann eh rot

namespace Nav.Language.Tests; 

[TestFixture]
public class SyntaxTreeTests {

    [Test]
    public void TestAllSyntaxesPresent() {

        var src = Resources.AllRules;

        var cgu = Syntax.ParseCodeGenerationUnit(src);

        var nodeTypes = typeof(Syntax).Assembly
                                      .GetTypes().Where(
                                           t => typeof(SyntaxNode).IsAssignableFrom(t) &&
                                                !t.IsAbstract)
                                      .ToList();

        // Die Anzahl kann/darf sich über die Zeit auch ändern.
        // Blöd wäre nur, wenn hier keine Syntaxen gefunden würden ;-)
        Assert.That(nodeTypes.Count, Is.EqualTo(48));

        // Direktiven sind strukturierte Trivia (keine Kindknoten) und werden daher über die Trivia erreicht:
        // AllRules trägt die wirksame #version (VersionDirectiveSyntax); eine unbekannte Direktive
        // (BadDirectiveTriviaSyntax) kommt aus einem eigenen Schnipsel, da AllRules bewusst fehlerfrei bleibt.
        var presentTypes = new HashSet<Type>(cgu.DescendantNodesAndSelf().Select(node => node.GetType()));
        presentTypes.UnionWith(cgu.SyntaxTree.Directives().Select(directive => directive.GetType()));
        presentTypes.UnionWith(SyntaxTree.ParseText("#unknown\r\ntask A{}").Directives().Select(directive => directive.GetType()));

        foreach (var nodeType in nodeTypes) {

            var message = $"Es fehlt die Syntax {nodeType.Name}.";
            var sample  = SampleSyntax.Of(nodeType);
            if (!String.IsNullOrEmpty(sample)) {
                message += $" Beispiel: '{sample}'";
            }
            Assert.That(
                presentTypes.Contains(nodeType),
                Is.True, message);
        }
    }

    [Test]
    public void TestEmptyText() {
        var syntaxTree = SyntaxTree.ParseText(String.Empty);
        Assert.That(syntaxTree,      Is.Not.Null);
        Assert.That(syntaxTree.Root, Is.Not.Null);
    }

    [Test]
    public void TestParseEmptyCodeDeclaration() {
        var syntax = Syntax.ParseCodeDeclaration(String.Empty);
        Assert.That(syntax, Is.Not.Null);
    }
       
    [Test]
    public void TestParentedNodesAndTokens() {
        var syntaxTree = SyntaxTree.ParseText(Resources.AllRules);

        Assert.That(syntaxTree.Tokens.Count(token => token.Parent               == null), Is.EqualTo(0));
        Assert.That(syntaxTree.Root.DescendantNodes().Count(node => node.Parent == null), Is.EqualTo(0));
        Assert.That(syntaxTree.Root.Parent,                                               Is.Null);
    }

    [Test]
    public void TestCommentTokens() {
        var syntaxTree = SyntaxTree.ParseText(Resources.AllRules);

        // Kommentare liegen seit Schritt 5.4 nicht mehr im flachen Token-Strom, sondern als angehängte
        // Trivia — Zugriff über die Trivia-Sicht am Baum.
        var comments = syntaxTree.Comments().ToList();
        Assert.That(comments.Count, Is.EqualTo(2));

        var firstComment = comments.First();
        Assert.That(firstComment.Type,      Is.EqualTo(SyntaxTokenType.SingleLineComment));
        Assert.That(firstComment.IsComment, Is.True);
    }
        
    [Test]
    public void TestEndOfFile() {
        var syntaxRoot = SyntaxTree.ParseText(Resources.AllRules).Root;

        Assert.That(syntaxRoot.ChildTokens().Last().Type, Is.EqualTo(SyntaxTokenType.EndOfFile));
    }
        
    [Test]
    public void TestCodeNamespaceDeclaration() {
        // [namespaceprefix NS.1]
        var syntaxRoot               = SyntaxTree.ParseText(Resources.AllRules).Root;
        var codeNamespaceDeclaration = syntaxRoot.DescendantNodes<CodeNamespaceDeclarationSyntax>().First();

        Assert.That(codeNamespaceDeclaration.NamespaceprefixKeyword.ToString(),     Is.EqualTo("namespaceprefix"));
        Assert.That(codeNamespaceDeclaration.NamespaceprefixKeyword.Type,           Is.EqualTo(SyntaxTokenType.NamespaceprefixKeyword));
        Assert.That(codeNamespaceDeclaration.NamespaceprefixKeyword.Classification, Is.EqualTo(TextClassification.Keyword));

        Assert.That(codeNamespaceDeclaration.Namespace.Text, Is.EqualTo("NS.1"));

        Assert.That(codeNamespaceDeclaration.ChildTokens().Count(),           Is.EqualTo(3));
        Assert.That(codeNamespaceDeclaration.Namespace.ChildTokens().Count(), Is.EqualTo(1));

        Write(codeNamespaceDeclaration.ChildTokens().ToList());
    }

    void Write(IEnumerable<SyntaxToken> list) {
        // ReSharper disable once UnusedVariable
        foreach (var value in list) {
            //Console.WriteLine(value.ToDebuggerDisplayString());
        }
    }

    [Test]
    public void TestCodeUsingDirective() {
        // [using U1]
        var syntaxRoot         = SyntaxTree.ParseText(Resources.AllRules).Root;
        var codeUsingDirective = syntaxRoot.DescendantNodes<CodeUsingDeclarationSyntax>().First();

        Assert.That(codeUsingDirective.UsingKeyword.ToString(),     Is.EqualTo("using"));
        Assert.That(codeUsingDirective.UsingKeyword.Type,           Is.EqualTo(SyntaxTokenType.UsingKeyword));
        Assert.That(codeUsingDirective.UsingKeyword.Classification, Is.EqualTo(TextClassification.Keyword));


        Assert.That(codeUsingDirective.Namespace.Text, Is.EqualTo("U1"));

        Assert.That(codeUsingDirective.ChildTokens().Count(),           Is.EqualTo(3));
        Assert.That(codeUsingDirective.Namespace.ChildTokens().Count(), Is.EqualTo(1));

        Write(codeUsingDirective.ChildTokens().ToList());
    }

    [Test]
    public void TestTaskIncludeDirective() {
        // taskref "F1";

        var syntaxRoot       = SyntaxTree.ParseText(Resources.AllRules).Root;
        var includeDirective = syntaxRoot.DescendantNodes<IncludeDirectiveSyntax>().First();

        Assert.That(includeDirective.TaskrefKeyword.ToString(),     Is.EqualTo("taskref"));
        Assert.That(includeDirective.TaskrefKeyword.Type,           Is.EqualTo(SyntaxTokenType.TaskrefKeyword));
        Assert.That(includeDirective.TaskrefKeyword.Classification, Is.EqualTo(TextClassification.Keyword));

        Assert.That(includeDirective.StringLiteral.ToString(),     Is.EqualTo("\"F1\""));
        Assert.That(includeDirective.StringLiteral.Type,           Is.EqualTo(SyntaxTokenType.StringLiteral));
        Assert.That(includeDirective.StringLiteral.Classification, Is.EqualTo(TextClassification.StringLiteral));

        Assert.That(includeDirective.Semicolon.ToString(), Is.EqualTo(";"));
        Assert.That(includeDirective.Semicolon.Type,       Is.EqualTo(SyntaxTokenType.Semicolon));

        Assert.That(includeDirective.ChildTokens().Count(), Is.EqualTo(3));

        Write(includeDirective.ChildTokens().ToList());
    }

    [Test]
    // ReSharper disable once FunctionComplexityOverflow
    public void TestTaskDeclaration() {
        var syntaxTree      = SyntaxTree.ParseText(Resources.AllRules);
        var taskDeclaration = syntaxTree.Root.DescendantNodes<TaskDeclarationSyntax>().First();

        Assert.That(taskDeclaration.ChildNodes()
                                   .Count, Is.EqualTo(6));

        Assert.That(taskDeclaration.TaskrefKeyword.ToString(), Is.EqualTo("taskref"));
        Assert.That(taskDeclaration.Identifier.ToString(),     Is.EqualTo("TR1"));

        Assert.That(taskDeclaration.OpenBrace.Type,  Is.EqualTo(SyntaxTokenType.OpenBrace));
        Assert.That(taskDeclaration.CloseBrace.Type, Is.EqualTo(SyntaxTokenType.CloseBrace));

        var namespaceDeclaration = taskDeclaration.CodeNamespaceDeclaration;
        Assert.That(namespaceDeclaration.NamespaceprefixKeyword.ToString(), Is.EqualTo("namespaceprefix"));
        Assert.That(namespaceDeclaration.Namespace.Text,                    Is.EqualTo("NS.2"));

        var notImplementedDeclaration =taskDeclaration.CodeNotImplementedDeclaration;
        Assert.That(notImplementedDeclaration.NotimplementedKeyword.ToString(), Is.EqualTo("notimplemented"));
        Assert.That(notImplementedDeclaration.NotimplementedKeyword.Type,       Is.EqualTo(SyntaxTokenType.NotimplementedKeyword));

        // result RT1 r1
        var resultDeclaration =taskDeclaration.CodeResultDeclaration;
        Assert.That(resultDeclaration.ResultKeyword.ToString(), Is.EqualTo("result"));
        Assert.That(resultDeclaration.ResultKeyword.Type,       Is.EqualTo(SyntaxTokenType.ResultKeyword));

        var result = resultDeclaration.Result;
        Assert.That(result.Identifier.ToString(), Is.EqualTo("r1"));
        Assert.That(result.Type,                  Is.InstanceOf<SimpleTypeSyntax>());

        var resultType = (SimpleTypeSyntax) result.Type;
        Assert.That(resultType.Identifier.ToString(), Is.EqualTo("RT1"));
        Assert.That(resultType.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));


        //init I1[abstractmethod] [params T1 param1, T2<T3, T4<T5>> param2, T6[][] param3] do "D1";
        var initNodeDeclaration = taskDeclaration.InitNodes().First();
        Assert.That(initNodeDeclaration.InitKeyword.ToString(), Is.EqualTo("init"));
        Assert.That(initNodeDeclaration.InitKeyword.Type,       Is.EqualTo(SyntaxTokenType.InitKeyword));

        Assert.That(initNodeDeclaration.Identifier.ToString(), Is.EqualTo("I1"));
        Assert.That(initNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(initNodeDeclaration.Semicolon.ToString(), Is.EqualTo(";"));
        Assert.That(initNodeDeclaration.Semicolon.Type,       Is.EqualTo(SyntaxTokenType.Semicolon));

        // [abstractmethod]
        var abstractMethodDeclaration = initNodeDeclaration.CodeAbstractMethodDeclaration;
        Assert.That(abstractMethodDeclaration.AbstractmethodKeyword.ToString(), Is.EqualTo("abstractmethod"));
        Assert.That(abstractMethodDeclaration.AbstractmethodKeyword.Type,       Is.EqualTo(SyntaxTokenType.AbstractmethodKeyword));

        // Parameter Tests            
        Assert.That(initNodeDeclaration.CodeParamsDeclaration.ParameterList.Count, Is.EqualTo(3)); // param1, param2, param3

        //===================================================
        // Parameter 1: T1 param1
        //===================================================
        var parameter1 = initNodeDeclaration.CodeParamsDeclaration.ParameterList[0];
        Assert.That(parameter1.Identifier.ToString(), Is.EqualTo("param1"));
        Assert.That(parameter1.Type,                  Is.InstanceOf<SimpleTypeSyntax>());
        var param1Type = (SimpleTypeSyntax)parameter1.Type;
        Assert.That(param1Type.Identifier.ToString(), Is.EqualTo("T1"));

        //===================================================
        // Parameter 2: T2<T3, T4<T5>> param2
        //===================================================
        var parameter2 = initNodeDeclaration.CodeParamsDeclaration.ParameterList[1];
        Assert.That(parameter2.Identifier.ToString(), Is.EqualTo("param2"));
        Assert.That(parameter2.Type,                  Is.InstanceOf<GenericTypeSyntax>());
        var param2Type = (GenericTypeSyntax)parameter2.Type;
        Assert.That(param2Type.Identifier.ToString(), Is.EqualTo("T2"));
        Assert.That(param2Type.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(param2Type.GenericArguments.Count, Is.EqualTo(2));
        // Generic Argument 1: T3
        var genArg1 = param2Type.GenericArguments[0];
        Assert.That(genArg1, Is.InstanceOf<SimpleTypeSyntax>());
        var genArg1Typed = (SimpleTypeSyntax) genArg1;
        Assert.That(genArg1Typed.Identifier.ToString(), Is.EqualTo("T3"));

        // Generic Argument 1: T4<T5>
        var genArg2 = param2Type.GenericArguments[1];
        Assert.That(genArg2, Is.InstanceOf<GenericTypeSyntax>());
        var genArg2Typed = (GenericTypeSyntax)genArg2;
        Assert.That(genArg2Typed.Identifier.ToString(), Is.EqualTo("T4"));

        Assert.That(genArg2Typed.GenericArguments.Count, Is.EqualTo(1));
        var subgenArg1 = genArg2Typed.GenericArguments[0];
        Assert.That(subgenArg1, Is.InstanceOf<SimpleTypeSyntax>());
        var subgenArg1Typed = (SimpleTypeSyntax)subgenArg1;
        Assert.That(subgenArg1Typed.Identifier.ToString(), Is.EqualTo("T5"));

        //===================================================
        // Parameter 3: T6[][] param3
        //===================================================
        var parameter3 = initNodeDeclaration.CodeParamsDeclaration.ParameterList[2];
        Assert.That(parameter3.Identifier.ToString(), Is.EqualTo("param3"));
        Assert.That(parameter3.Type,                  Is.InstanceOf<ArrayTypeSyntax>());
        var param3Type = (ArrayTypeSyntax)parameter3.Type;

        Assert.That(param3Type.RankSpecifiers.Count,                    Is.EqualTo(2));
        Assert.That(param3Type.RankSpecifiers[0].OpenBracket.IsMissing, Is.False);
        Assert.That(param3Type.RankSpecifiers[0].OpenBracket.Type,      Is.EqualTo(SyntaxTokenType.OpenBracket));

        Assert.That(param3Type.RankSpecifiers[0].CloseBracket.IsMissing, Is.False);
        Assert.That(param3Type.RankSpecifiers[0].CloseBracket.Type,      Is.EqualTo(SyntaxTokenType.CloseBracket));

        Assert.That(param3Type.Rank, Is.EqualTo(2));
        Assert.That(param3Type.Type, Is.InstanceOf<SimpleTypeSyntax>());
        var arrayType = (SimpleTypeSyntax)param3Type.Type;
        Assert.That(arrayType.Identifier.ToString(), Is.EqualTo("T6"));

        // DoClause
        var doClause = initNodeDeclaration.DoClause;
        Assert.That(doClause.DoKeyword.ToString(), Is.EqualTo("do"));
        Assert.That(doClause.DoKeyword.Type,       Is.EqualTo(SyntaxTokenType.DoKeyword));

        Assert.That(doClause.IdentifierOrString.Text, Is.EqualTo("D1"));
        Assert.That(doClause.IdentifierOrString,      Is.InstanceOf<StringLiteralSyntax>());

        // exit E1;  
        var exitNodeDeclaration = taskDeclaration.ExitNodes().First();
        Assert.That(exitNodeDeclaration.ExitKeyword.ToString(), Is.EqualTo("exit"));
        Assert.That(exitNodeDeclaration.ExitKeyword.Type,       Is.EqualTo(SyntaxTokenType.ExitKeyword));

        Assert.That(exitNodeDeclaration.Identifier.ToString(), Is.EqualTo("E1"));
        Assert.That(exitNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // end;
        var endNodeDeclaration = taskDeclaration.EndNodes().First();
        Assert.That(endNodeDeclaration.EndKeyword.ToString(), Is.EqualTo("end"));
        Assert.That(endNodeDeclaration.EndKeyword.Type,       Is.EqualTo(SyntaxTokenType.EndKeyword));

        Assert.That(taskDeclaration.ChildTokens().Count(), Is.EqualTo(4));
    }

    [Test]
    public void TestTaskDefinition() {
        var syntaxRoot = SyntaxTree.ParseText(Resources.AllRules).Root;

        var taskDefinition = syntaxRoot.DescendantNodes<TaskDefinitionSyntax>().First();

        Assert.That(taskDefinition.TaskKeyword.ToString(), Is.EqualTo("task"));
        Assert.That(taskDefinition.TaskKeyword.Type,       Is.EqualTo(SyntaxTokenType.TaskKeyword));

        Assert.That(taskDefinition.Identifier.ToString(), Is.EqualTo("T1"));
        Assert.That(taskDefinition.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // [code "code1"] 
        var codeDeclaration = taskDefinition.CodeDeclaration;
        Assert.That(codeDeclaration.CodeKeyword.ToString(), Is.EqualTo("code"));
        Assert.That(codeDeclaration.CodeKeyword.Type,       Is.EqualTo(SyntaxTokenType.CodeKeyword));

        Assert.That(codeDeclaration.GetGetStringLiterals().First().ToString(), Is.EqualTo("\"code1\""));
        Assert.That(codeDeclaration.GetGetStringLiterals().First().Type,       Is.EqualTo(SyntaxTokenType.StringLiteral));

        // [base B0: B1, B2]
        var baseDeclaration = taskDefinition.CodeBaseDeclaration;
        Assert.That(baseDeclaration.BaseKeyword.ToString(), Is.EqualTo("base"));
        Assert.That(baseDeclaration.BaseTypes.Count,        Is.EqualTo(3));

        Assert.That(((SimpleTypeSyntax)baseDeclaration.BaseTypes[0]).Identifier.ToString(), Is.EqualTo("B0"));
        Assert.That(baseDeclaration.WfsBaseType.ToString(),                                 Is.EqualTo("B0"));
        Assert.That(((SimpleTypeSyntax)baseDeclaration.BaseTypes[1]).Identifier.ToString(), Is.EqualTo("B1"));
        Assert.That(baseDeclaration.IwfsBaseType.ToString(),                                Is.EqualTo("B1"));
        Assert.That(((SimpleTypeSyntax)baseDeclaration.BaseTypes[2]).Identifier.ToString(), Is.EqualTo("B2"));
        Assert.That(baseDeclaration.IBeginWfsBaseType.ToString(),                           Is.EqualTo("B2"));

        // [generateto "g1"]
        var generateToDeclaration = taskDefinition.CodeGenerateToDeclaration;
        Assert.That(generateToDeclaration.GeneratetoKeyword.ToString(), Is.EqualTo("generateto"));
        Assert.That(generateToDeclaration.GeneratetoKeyword.Type,       Is.EqualTo(SyntaxTokenType.GeneratetoKeyword));

        Assert.That(generateToDeclaration.StringLiteral.ToString(), Is.EqualTo("\"g1\""));
        Assert.That(generateToDeclaration.StringLiteral.Type,       Is.EqualTo(SyntaxTokenType.StringLiteral));

        // [params P1 p1, P2 p2]
        var paramsDeclaration = taskDefinition.CodeParamsDeclaration;
        Assert.That(paramsDeclaration.ParamsKeyword.ToString(), Is.EqualTo("params"));
        Assert.That(paramsDeclaration.ParamsKeyword.Type,       Is.EqualTo(SyntaxTokenType.ParamsKeyword));

        Assert.That(paramsDeclaration.ParameterList.Count, Is.EqualTo(2));

        var parameter1 = paramsDeclaration.ParameterList[0];
        Assert.That(parameter1.Type,                                            Is.InstanceOf<SimpleTypeSyntax>());
        Assert.That(((SimpleTypeSyntax) parameter1.Type).Identifier.ToString(), Is.EqualTo("P1"));
        Assert.That(parameter1.Identifier.ToString(),                           Is.EqualTo("p1"));

        var parameter2 = paramsDeclaration.ParameterList[1];
        Assert.That(parameter2.Type,                                           Is.InstanceOf<SimpleTypeSyntax>());
        Assert.That(((SimpleTypeSyntax)parameter2.Type).Identifier.ToString(), Is.EqualTo("P2"));
        Assert.That(parameter2.Identifier.ToString(),                          Is.EqualTo("p2"));
            
        // [result R1 r1]
        var resultDeclaration = taskDefinition.CodeResultDeclaration;
        Assert.That(resultDeclaration.ResultKeyword.ToString(),                              Is.EqualTo("result"));
        Assert.That(((SimpleTypeSyntax)resultDeclaration.Result.Type).Identifier.ToString(), Is.EqualTo("R1"));
        Assert.That(resultDeclaration.Result.Identifier.ToString(),                          Is.EqualTo("r1"));
    }

    [Test]
    public void TestNodeDeclarationBlock() {

        var syntaxRoot = SyntaxTree.ParseText(Resources.AllRules).Root;

        var taskDefinition = syntaxRoot.DescendantNodes<TaskDefinitionSyntax>().First();

        var nodeDeclarationBlock = taskDefinition.NodeDeclarationBlock;

        // init I3;
        var initNodeDeclaration = nodeDeclarationBlock.InitNodes().First();
        Assert.That(initNodeDeclaration.InitKeyword.ToString(), Is.EqualTo("init"));
        Assert.That(initNodeDeclaration.InitKeyword.Type,       Is.EqualTo(SyntaxTokenType.InitKeyword));

        Assert.That(initNodeDeclaration.Identifier.ToString(), Is.EqualTo("I3"));
        Assert.That(initNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // exit E2;
        var exitNodeDeclaration = nodeDeclarationBlock.ExitNodes().First();
        Assert.That(exitNodeDeclaration.ExitKeyword.ToString(), Is.EqualTo("exit"));
        Assert.That(exitNodeDeclaration.ExitKeyword.Type,       Is.EqualTo(SyntaxTokenType.ExitKeyword));

        Assert.That(exitNodeDeclaration.Identifier.ToString(), Is.EqualTo("E2"));
        Assert.That(exitNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // end;
        var endNodeDeclaration = nodeDeclarationBlock.EndNodes().First();
        Assert.That(endNodeDeclaration.EndKeyword.ToString(), Is.EqualTo("end"));
        Assert.That(endNodeDeclaration.EndKeyword.Type,       Is.EqualTo(SyntaxTokenType.EndKeyword));

        // task Tx tx [donotinject] [abstractmethod];
        var taskNodeDeclaration = nodeDeclarationBlock.TaskNodes().First();
        Assert.That(taskNodeDeclaration.TaskKeyword.ToString(), Is.EqualTo("task"));
        Assert.That(taskNodeDeclaration.TaskKeyword.Type,       Is.EqualTo(SyntaxTokenType.TaskKeyword));

        Assert.That(taskNodeDeclaration.Identifier.ToString(), Is.EqualTo("Tx"));
        Assert.That(taskNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(taskNodeDeclaration.IdentifierAlias.ToString(), Is.EqualTo("tx"));
        Assert.That(taskNodeDeclaration.IdentifierAlias.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        var doNotInjectDeclaration = taskNodeDeclaration.CodeDoNotInjectDeclaration;
        Assert.That(doNotInjectDeclaration.DonotinjectKeyword.ToString(), Is.EqualTo("donotinject"));
        Assert.That(doNotInjectDeclaration.DonotinjectKeyword.Type,       Is.EqualTo(SyntaxTokenType.DonotinjectKeyword));

        var abstractMethodDeclaration = taskNodeDeclaration.CodeAbstractMethodDeclaration;
        Assert.That(abstractMethodDeclaration.AbstractmethodKeyword.ToString(), Is.EqualTo("abstractmethod"));
        Assert.That(abstractMethodDeclaration.AbstractmethodKeyword.Type,       Is.EqualTo(SyntaxTokenType.AbstractmethodKeyword));

        // choice C1;
        var choiceNodeDeclaration = nodeDeclarationBlock.ChoiceNodes().First();
        Assert.That(choiceNodeDeclaration.ChoiceKeyword.ToString(), Is.EqualTo("choice"));
        Assert.That(choiceNodeDeclaration.ChoiceKeyword.Type,       Is.EqualTo(SyntaxTokenType.ChoiceKeyword));

        Assert.That(choiceNodeDeclaration.Identifier.ToString(), Is.EqualTo("C1"));
        Assert.That(choiceNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // dialog D1;
        var dialogNodeDeclaration = nodeDeclarationBlock.DialogNodes().First();
        Assert.That(dialogNodeDeclaration.DialogKeyword.ToString(), Is.EqualTo("dialog"));
        Assert.That(dialogNodeDeclaration.DialogKeyword.Type,       Is.EqualTo(SyntaxTokenType.DialogKeyword));

        Assert.That(dialogNodeDeclaration.Identifier.ToString(), Is.EqualTo("D1"));
        Assert.That(dialogNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        // view V1;
        var viewNodeDeclaration = nodeDeclarationBlock.ViewNodes().First();
        Assert.That(viewNodeDeclaration.ViewKeyword.ToString(), Is.EqualTo("view"));
        Assert.That(viewNodeDeclaration.ViewKeyword.Type,       Is.EqualTo(SyntaxTokenType.ViewKeyword));

        Assert.That(viewNodeDeclaration.Identifier.ToString(), Is.EqualTo("V1"));
        Assert.That(viewNodeDeclaration.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));
    }

    [Test]
    public  void TestTransitionDefinitionBlock() {
        var syntaxRoot = SyntaxTree.ParseText(Resources.AllRules).Root;

        var taskDefinition = syntaxRoot.DescendantNodes<TaskDefinitionSyntax>().First();

        var transitionDefinitionBlockSyntax = taskDefinition.TransitionDefinitionBlock;
        // init --> Tx on "Something"  if "Condition" do "Action1";

        var initNodeTransition = transitionDefinitionBlockSyntax.TransitionDefinitions[0];
        // init
        Assert.That(initNodeTransition.SourceNode, Is.InstanceOf<InitSourceNodeSyntax>());
        var sourceNode = (InitSourceNodeSyntax) initNodeTransition.SourceNode;
        Assert.That(sourceNode.InitKeyword.ToString(), Is.EqualTo("init"));

        Assert.That(initNodeTransition.Edge.Keyword.ToString(), Is.EqualTo("-->"));
        Assert.That(initNodeTransition.Edge.Keyword.Type,       Is.EqualTo(SyntaxTokenType.GoToEdgeKeyword));
        Assert.That(initNodeTransition.Edge,                    Is.InstanceOf<GoToEdgeSyntax>());
        var goToEdgeSyntax = (GoToEdgeSyntax) initNodeTransition.Edge;
        Assert.That(goToEdgeSyntax.GoToEdgeKeyword.IsMissing,  Is.False);
        Assert.That(goToEdgeSyntax.GoToEdgeKeyword.ToString(), Is.EqualTo("-->"));

        Assert.That(initNodeTransition.Trigger, Is.InstanceOf<SignalTriggerSyntax>());
        var onClause =(SignalTriggerSyntax)initNodeTransition.Trigger;
        Assert.That(onClause.OnKeyword.Type,                 Is.EqualTo(SyntaxTokenType.OnKeyword));
        Assert.That(onClause.Identifier.Text, Is.EqualTo("Something"));
            
        Assert.That(initNodeTransition.ConditionClause, Is.InstanceOf<IfConditionClauseSyntax>());
        var initIfCondition = (IfConditionClauseSyntax)initNodeTransition.ConditionClause;
        Assert.That(initIfCondition.IfKeyword.ToString(),    Is.EqualTo("if"));
        Assert.That(initIfCondition.IfKeyword.Type,          Is.EqualTo(SyntaxTokenType.IfKeyword));
        Assert.That(initIfCondition.IdentifierOrString.Text, Is.EqualTo("Condition"));
  

        Assert.That(initNodeTransition.Semicolon.ToString(), Is.EqualTo(";"));
            
        // do
        Assert.That(initNodeTransition.DoClause.DoKeyword.ToString(), Is.EqualTo("do"));
        Assert.That(initNodeTransition.DoClause.DoKeyword.Type,       Is.EqualTo(SyntaxTokenType.DoKeyword));

        Assert.That(initNodeTransition.DoClause.IdentifierOrString.Text, Is.EqualTo("Action1"));

        Assert.That(initNodeTransition.Semicolon.Type, Is.EqualTo(SyntaxTokenType.Semicolon));

        // Tx:Exit o-> V1 if "Condition" do "Action2";
        var exitNodeTransition = transitionDefinitionBlockSyntax.ExitTransitionDefinitions[0];

        Assert.That(exitNodeTransition.SourceNode.Identifier.ToString(), Is.EqualTo("Tx"));
        Assert.That(exitNodeTransition.SourceNode.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(exitNodeTransition.ExitIdentifier.ToString(), Is.EqualTo("Exit"));
        Assert.That(exitNodeTransition.ExitIdentifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(exitNodeTransition.Edge.Keyword.ToString(), Is.EqualTo("o->"));

        Assert.That(exitNodeTransition.TargetNode, Is.InstanceOf<IdentifierTargetNodeSyntax>());
        var targetNode = (IdentifierTargetNodeSyntax) exitNodeTransition.TargetNode;
        Assert.That(targetNode.Identifier.ToString(), Is.EqualTo("V1"));
        Assert.That(targetNode.Identifier.Type,       Is.EqualTo(SyntaxTokenType.Identifier));

        Assert.That(exitNodeTransition.ConditionClause, Is.InstanceOf<IfConditionClauseSyntax>());
        var exitIfCondition = (IfConditionClauseSyntax)exitNodeTransition.ConditionClause;
        Assert.That(exitIfCondition.IfKeyword.ToString(),    Is.EqualTo("if"));
        Assert.That(exitIfCondition.IfKeyword.Type,          Is.EqualTo(SyntaxTokenType.IfKeyword));
        Assert.That(exitIfCondition.IdentifierOrString.Text, Is.EqualTo("Condition"));

        Assert.That(exitNodeTransition.DoClause.IdentifierOrString.Text, Is.EqualTo("Action2"));

        Assert.That(exitNodeTransition.Semicolon.ToString(), Is.EqualTo(";"));
        Assert.That(exitNodeTransition.Semicolon.Type,       Is.EqualTo(SyntaxTokenType.Semicolon));
    }
        
}