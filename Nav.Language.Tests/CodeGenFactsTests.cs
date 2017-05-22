﻿#region Using Directives

using NUnit.Framework;
using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Nav.Language.Tests {
    [TestFixture]
    public class CodeGenFactsTests {

        readonly PathProviderTests _pathProviderTests = new PathProviderTests();

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
    }
}