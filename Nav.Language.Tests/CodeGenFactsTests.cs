#region Using Directives

using NUnit.Framework;
using Pharmatechnik.Nav.Language.CodeGen;
// ReSharper disable InconsistentNaming

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class CodeGenFactsTests {

    [Test]
    public void TestDefaultIwfsBaseType() {
        Assert.That(CodeGenFacts.DefaultIwfsBaseType, Is.EqualTo("IWFService"), "Wrong DefaultIwfsBaseType");
    }
    [Test]
    public void TestDefaultIBeginWfsBaseType() {
        Assert.That(CodeGenFacts.DefaultIBeginWfsBaseType, Is.EqualTo("IBeginWFService"), "Wrong DefaultIBeginWfsBaseType");
    }
    [Test]
    public void TestLogicMethodSuffix() {
        Assert.That(CodeGenFacts.LogicMethodSuffix, Is.EqualTo("Logic"), "Wrong LogicMethodSuffix");
    }
    [Test]
    public void TestToClassNameSuffix() {
        Assert.That(CodeGenFacts.ToClassNameSuffix, Is.EqualTo("TO"), "Wrong ToClassNameSuffix");
    }
    [Test]
    public void TestWflNamespaceSuffix() {
        Assert.That(CodeGenFacts.WflNamespaceSuffix, Is.EqualTo("WFL"), "Wrong WflNamespaceSuffix");
    }
    [Test]
    public void TestIwflNamespaceSuffix() {
        Assert.That(CodeGenFacts.IwflNamespaceSuffix, Is.EqualTo("IWFL"), "Wrong IwflNamespaceSuffix");
    }
    [Test]
    public void TestWfsBaseClassSuffix() {
        Assert.That(CodeGenFacts.WfsBaseClassSuffix, Is.EqualTo("WFSBase"), "Wrong WfsBaseClassSuffix");
    }
    [Test]
    public void TestWfsClassSuffix() {
        Assert.That(CodeGenFacts.WfsClassSuffix, Is.EqualTo("WFS"), "Wrong WfsClassSuffix");
    }
    [Test]
    public void TestBeginMethodPrefix() {
        Assert.That(CodeGenFacts.BeginMethodPrefix, Is.EqualTo("Begin"), "Wrong BeginMethodPrefix");
    }
    [Test]
    public void TestExitMethodPrefix() {
        Assert.That(CodeGenFacts.ExitMethodPrefix, Is.EqualTo("After"), "Wrong ExitMethodPrefix");
    }
    [Test]
    public void TestBeginInterfacePrefix() {
        Assert.That(CodeGenFacts.BeginInterfacePrefix, Is.EqualTo("IBegin"), "Wrong BeginInterfacePrefix");
    }
    [Test]
    public void TestNavigationEngineIwflNamespace() {
        Assert.That(CodeGenFacts.NavigationEngineIwflNamespace, Is.EqualTo("Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL"), "Wrong NavigationEngineIwflNamespace");
    }
    [Test]
    public void TestNavigationEngineWflNamespace() {
        Assert.That(CodeGenFacts.NavigationEngineWflNamespace, Is.EqualTo("Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL"), "Wrong NavigationEngineWflNamespace");
    }
    [Test]
    public void TestUnknownNamespace() {
        Assert.That(CodeGenFacts.UnknownNamespace, Is.EqualTo("__UNKNOWN__NAMESPACE__"), "Wrong UnknownNamespace");
    }
    [Test]
    public void TestDefaultParamterName() {
        Assert.That(CodeGenFacts.DefaultParamterName, Is.EqualTo("par"), "Wrong DefaultParamterName");
    }
    [Test]
    public void TestDefaultTaskResultType() {
        Assert.That(CodeGenFacts.DefaultTaskResultType, Is.EqualTo("bool"), "Wrong DefaultTaskResultType");
    }
    [Test]
    public void TestDefaultWfsBaseClass() {
        Assert.That(CodeGenFacts.DefaultWfsBaseClass, Is.EqualTo("BaseWFService"), "Wrong DefaultWfsBaseClass");
    }

    // -- Invarianten (CodeGenInvariants) — dürfen über keine Generation abweichen ----------------------

    [Test]
    public void InvariantInterfacePrefix() {
        Assert.That(CodeGenInvariants.InterfacePrefix, Is.EqualTo("I"), "Wrong InterfacePrefix");
    }
    [Test]
    public void InvariantBeginInterfacePrefix() {
        Assert.That(CodeGenInvariants.BeginInterfacePrefix, Is.EqualTo("IBegin"), "Wrong BeginInterfacePrefix");
    }
    [Test]
    public void InvariantInterfaceSuffix() {
        Assert.That(CodeGenInvariants.InterfaceSuffix, Is.EqualTo("WFS"), "Wrong InterfaceSuffix");
    }
    [Test]
    public void InvariantIwflNamespaceSuffix() {
        Assert.That(CodeGenInvariants.IwflNamespaceSuffix, Is.EqualTo("IWFL"), "Wrong IwflNamespaceSuffix");
    }
    [Test]
    public void InvariantToClassNameSuffix() {
        Assert.That(CodeGenInvariants.ToClassNameSuffix, Is.EqualTo("TO"), "Wrong ToClassNameSuffix");
    }
    [Test]
    public void InvariantAnnotationTagPrefix() {
        Assert.That(CodeGenInvariants.AnnotationTagPrefix, Is.EqualTo("Nav"), "Wrong AnnotationTagPrefix");
    }
    [Test]
    public void InvariantAnnotationTagNavFile() {
        Assert.That(CodeGenInvariants.AnnotationTagNavFile, Is.EqualTo("NavFile"), "Wrong AnnotationTagNavFile");
    }
    [Test]
    public void InvariantAnnotationTagNavTask() {
        Assert.That(CodeGenInvariants.AnnotationTagNavTask, Is.EqualTo("NavTask"), "Wrong AnnotationTagNavTask");
    }
    [Test]
    public void InvariantAnnotationTagNavTrigger() {
        Assert.That(CodeGenInvariants.AnnotationTagNavTrigger, Is.EqualTo("NavTrigger"), "Wrong AnnotationTagNavTrigger");
    }
    [Test]
    public void InvariantAnnotationTagNavInit() {
        Assert.That(CodeGenInvariants.AnnotationTagNavInit, Is.EqualTo("NavInit"), "Wrong AnnotationTagNavInit");
    }
    [Test]
    public void InvariantAnnotationTagNavExit() {
        Assert.That(CodeGenInvariants.AnnotationTagNavExit, Is.EqualTo("NavExit"), "Wrong AnnotationTagNavExit");
    }
    [Test]
    public void InvariantAnnotationTagNavInitCall() {
        Assert.That(CodeGenInvariants.AnnotationTagNavInitCall, Is.EqualTo("NavInitCall"), "Wrong AnnotationTagNavInitCall");
    }

    [Test]
    public void CombineQualifiedNameeA() {
        var actual = CodeGenFacts.BuildQualifiedName("", "A");
        Assert.That(actual, Is.EqualTo("A"), "Wrong QualifiedName");
    }

    [Test]
    public void CombineQualifiedNameAB() {
        var actual = CodeGenFacts.BuildQualifiedName("A", "B");
        Assert.That(actual, Is.EqualTo("A.B"), "Wrong QualifiedName");
    }

    [Test]
    public void CombineQualifiedNameABC() {
        var actual = CodeGenFacts.BuildQualifiedName("A", "B", "C");
        Assert.That(actual, Is.EqualTo("A.B.C"), "Wrong QualifiedName");
    }

    [Test]
    public void CombineQualifiedNameAeC() {
        var actual = CodeGenFacts.BuildQualifiedName("A", "", "C");
        Assert.That(actual, Is.EqualTo("A.C"), "Wrong QualifiedName");
    }

    [Test]
    public void CombineQualifiedNameA0C() {
        var actual = CodeGenFacts.BuildQualifiedName("A", null, "C");
        Assert.That(actual, Is.EqualTo("A.C"), "Wrong QualifiedName");
    }
}