#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Internal;

#endregion

// ReSharper disable InconsistentNaming Ist bei den Tests unerheblich
// ReSharper disable PossibleNullReferenceException Wenns knallt wird der Test ohnehin rot
namespace Nav.Language.Tests; 

[TestFixture]
public class CodeSanityTests {

    class CodeSyntaxTester : SyntaxNodeWalker {
        public class BaseFail {
            public BaseFail(Type syntaxType, Type actualBaseType, Type expectedBaseType) {
                SyntaxType       = syntaxType;
                ActualBaseType   = actualBaseType;
                ExpectedBaseType = expectedBaseType;
            }

            public Type SyntaxType       { get; }
            public Type ExpectedBaseType { get; }
            public Type ActualBaseType   { get; }
        }

        public readonly List<BaseFail> BaseFails = new();
        public readonly List<Type>     NameFails = new();

        public override bool DefaultWalk(SyntaxNode node) {
            if (node is ArrayRankSpecifierSyntax) {
                return true;
            }

            if (node.ChildTokens().OfType(SyntaxTokenType.OpenBracket).Any() && 
                node.ChildTokens().OfType(SyntaxTokenType.CloseBracket).Any()) {

                var syntaxType       = node.GetType();
                var actualBaseType   = syntaxType.BaseType;
                var expectedBaseType = typeof (CodeSyntax);
                if (expectedBaseType != actualBaseType) {
                    BaseFails.Add(new BaseFail(syntaxType, actualBaseType, expectedBaseType));
                }

                if (!syntaxType.Name.StartsWith("Code")) {
                    NameFails.Add(syntaxType);
                }
            }
            return true;
        }
    }

    [Test]
    [Description("Mit diesem Test stellen wir sicher, dass alle 'Code' Konstrukte von CodeSyntax ableiten: \r\n")]
    public void EnsureCodeSyntaxBase() {

        var syntax = SyntaxTree.ParseText(Resources.AllRules);
        var walker = new CodeSyntaxTester();
        walker.Walk(syntax.Root);
        foreach (var fail in walker.BaseFails) {
            Assert.That(fail.ActualBaseType, Is.EqualTo(fail.ExpectedBaseType), "Base von {0} sollte {1} sein.", fail.SyntaxType.Name, fail.ExpectedBaseType.Name);
        }
    }

    [Test]
    [Description("Mit diesem Test stellen wir sicher, dass alle 'Code' Konstrukte mt Code im Klassennamen beginnen: \r\n")]
    public void EnsureCodeClassName() {

        var syntax = SyntaxTree.ParseText(Resources.AllRules);
        var walker = new CodeSyntaxTester();
        walker.Walk(syntax.Root);
        foreach (var fail in walker.NameFails) {
            Assert.Fail("Name von {0} sollte mit 'Code' beginnen.", fail.Name);
        }
    }

    [Test]
    [Description("Wenn ein Member ein IEnumerable<T> zurückliefert, sollte es immer eine Methode und kein Property sein: \r\n")]
    public void IEnumerablePropertyShouldBeMethod() {

        var nodeTypes =FindAllDerivedTypesAndSelf<SyntaxNode>();

        foreach(var nodeType in nodeTypes) {

            var enumerableProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                               .Where(m => 
                                                          m.PropertyType.IsGenericType && 
                                                          m.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            foreach (var candidate in enumerableProperties) {

                Assert.Fail( "{0}.{1}() sollte eine Methode sein!", candidate.DeclaringType.Name, candidate.Name);
            }
        }
    }

    [Test]
    [Description("Methoden sollen IReadOnlyList<T> anstelle von IReadOnlyCollection<T> zurückliefern: \r\n")]
    public void DoPreferIReadOnlyListOverIReadOnlyCollection() {

        var nodeTypes = FindAllDerivedTypesAndSelf<SyntaxNode>();

        foreach (var nodeType in nodeTypes) {

            var collectionMethods = nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(m => 
                                                       m.ReturnType.IsGenericType && 
                                                       m.ReturnType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>));

            foreach (var candidate in collectionMethods) {
                Assert.That(candidate.ReturnType, Is.InstanceOf(typeof(IReadOnlyList<>)),
                            "{0}.{1} sollte IReadOnlyList<> verwenden!", candidate.DeclaringType.Name, candidate.Name);
            }               
        }
    }

    [Test]
    [Description("Methoden sollen IReadOnlyList<T> anstelle von List<T> zurückliefern: \r\n")]
    public void DoPreferIReadOnlyListOverList() {

        var nodeTypes = FindAllDerivedTypesAndSelf<SyntaxNode>();

        foreach (var nodeType in nodeTypes) {

            var collectionMethods = nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(m =>
                                                       m.ReturnType.IsGenericType &&
                                                       m.ReturnType.GetGenericTypeDefinition() == typeof(List<>));

            foreach (var candidate in collectionMethods) {
                Assert.That(candidate.ReturnType, Is.InstanceOf(typeof(IReadOnlyList<>)),
                            "{0}.{1} sollte IReadOnlyList<> verwenden!", candidate.DeclaringType.Name, candidate.Name);
            }
        }
    }

    [Test]
    [Description("Properties mit einem Rückgabewert von IReadOnlyList<T> dürfen keinen Setter haben: \r\n")]
    public void IReadOnlyListPropertyShouldNotHaveASetter() {

        var nodeTypes = FindAllDerivedTypesAndSelf<SyntaxNode>();

        foreach (var nodeType in nodeTypes) {              

            var readOnlyListProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                 .Where(m => 
                                                            m.PropertyType.IsGenericType && 
                                                            m.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));

            foreach (var candidate in readOnlyListProperties) {

                Assert.That(candidate.CanWrite, Is.False, "{0}.{1} sollte keinen Setter haben!", candidate.DeclaringType.Name, candidate.Name);
            }
        }
    }

    [Test]
    [Description("Konstruktoren sollten internal sein: \r\n")]
    public void CtorShouldBeInternal() {

        var nodeTypes = FindAllDerivedTypesAndSelf<SyntaxNode>().Where(n=>!n.IsAbstract);

        foreach (var nodeType in nodeTypes) {

            var publicCtors = nodeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var candidate in publicCtors) {

                Assert.That(candidate.IsPublic, Is.False, "{0}.{1} sollte internal sein!", candidate.DeclaringType.Name, candidate.Name);
            }
        }
    }

    [Test]
    [Description("ISymbol interfaces sollten keine Setter haben: \r\n")]
    public void ISymbolInterfaceShouldBeReadOnly() {

        var symbolInterfaces = FindAllDerivedTypesAndSelf<ISymbol>().Where(n => n.IsInterface);

        foreach (var itf in symbolInterfaces) {

            var properties = itf.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var candidate in properties) {

                Assert.That(candidate.CanWrite, Is.False, "{0}.{1} sollte keinen Setter haben!", candidate.DeclaringType.Name, candidate.Name);
            }
        }
    }

    [Test]
    [Description("ISymbol interfaces sollten auf 'Symbol' enden: \r\n")]
    public void ISymbolInterfaceShouldEndWithSymbol() {

        var symbolInterfaces = FindAllDerivedTypesAndSelf<ISymbol>().Where(n => n.IsInterface && n.IsPublic);

        foreach (var candidate in symbolInterfaces) {

            // Der Name von Generischen Klassen endet mit ` + Anzahl an generischen Paramtern
            var name = candidate.Name;
            if (candidate.IsGenericType) {
                name = candidate.Name.Substring(0, candidate.Name.IndexOf('`'));
            }
            Assert.That(name.EndsWith("Symbol"), Is.True, "Der name der Schnittstelle {0} sollte auf 'Symbol' enden", candidate.FullName);
        }
    }

    [Test]
    [Description("Symbol Derivate sollten versiegelt sein: \r\n")]
    public void SymbolClassesShouldBeSealed() {

        var symbolTypes = FindAllDerivedTypesAndSelf<ISymbol>().Where(n => !n.IsInterface && !n.IsAbstract);

        foreach (var candidate in symbolTypes.Where(c=> !Attribute.IsDefined(c, typeof(SuppressCodeSanityCheckAttribute)))) {

            Assert.That(candidate.IsSealed, Is.True, "Die Klasse {0} sollte versiegelt sein", candidate.FullName);
        }
    }

    [Test]
    [Ignore("Muss noch geklärt werden")]
    [Description("ISymbol Schnittstellen sollten eine Syntax Eigenschaft haben: \r\n")]
    public void SymbolInterfacesShouldHaveASyntaxProperty() {

        var symbolTypes = FindAllDerivedTypesAndSelf<ISymbol>().Where(n => !n.IsInterface && !n.IsAbstract);
            
        foreach (var candidate in symbolTypes) {

            var symbolItf =candidate.GetInterfaces().First(itf=> itf.UnderlyingSystemType != typeof(ISymbol) && typeof(ISymbol).IsAssignableFrom(itf.UnderlyingSystemType));

            var syntaxProp = symbolItf.GetProperty("Syntax");
            var present    = syntaxProp !=null && typeof(SyntaxNode).IsAssignableFrom(syntaxProp.PropertyType);
                                    
            Assert.That(present, Is.True, "Die Klasse {0} respektive deren {1} Interface sollte eine Syntax Eigenschaft vom Typ SyntaxNode haben", candidate.FullName, symbolItf.FullName);
        }
    }
        
    [Test]
    [Description("DignosticDescriptor Ids sollen eindeutig sein.")]
    public void DignosticDescriptorIdMustBeUnique() {

        var diagCanditates = typeof(DiagnosticDescriptors).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                          .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        var deadCodeCanditates = typeof(DiagnosticDescriptors.DeadCode).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                       .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        var semanticCanditates = typeof(DiagnosticDescriptors.Semantic).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                       .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        DignosticDescriptorIdMustBeUnique(diagCanditates.Union(deadCodeCanditates)
                                                        .Union(semanticCanditates));
    }

    static void DignosticDescriptorIdMustBeUnique(IEnumerable<FieldInfo> canditates) {

        var mapedIds = new Dictionary<string, FieldInfo>();
        foreach(var field in canditates) {
            var instance = field.GetValue(null) as DiagnosticDescriptor;
            if(mapedIds.ContainsKey(instance.Id)) {
                Assert.Fail($"{field.DeclaringType.FullName}.{field.Name}: Die Id '{instance.Id}' wurde bereits von {mapedIds[instance.Id].DeclaringType.FullName}.{mapedIds[instance.Id].Name} vergeben");
            } else {
                mapedIds[instance.Id] = field;
            }
        }
    }

    [Test]
    [Description("Der Feldname des DignosticDescriptor sollte mit der Id beginnen.")]
    public void DignosticDescriptorFieldNameShoulsStartWithId() {

        var diagsCandidates = typeof(DiagnosticDescriptors).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                           .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        var deadCodecanditates = typeof(DiagnosticDescriptors.DeadCode).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                       .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        var semanticCodecanditates = typeof(DiagnosticDescriptors.Semantic).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                           .Where(f => f.FieldType == typeof(DiagnosticDescriptor));

        DignosticDescriptorFieldNameShoulsStartWithId(diagsCandidates.Union(deadCodecanditates)
                                                                     .Union(semanticCodecanditates));
    }

     
    static void DignosticDescriptorFieldNameShoulsStartWithId(IEnumerable<FieldInfo> canditates) {

        foreach(var field in canditates) {
            var instance = field.GetValue(null) as DiagnosticDescriptor;
            var id       = instance.Id;

            Assert.That(field.Name.StartsWith(id), Is.True, $"Der Name {field.DeclaringType.FullName}.{field.Name} sollte mit '{id}' beginnen");
        }
    }

    [Test]
    [Description("DignosticDescriptor Ids sollen eindeutig sein.")]
    public void NavErrorIdsMustBeUnique() {

        var canditates = typeof(DiagnosticId).GetFields(BindingFlags.Public | BindingFlags.Static)
                                             .Where(f => f.FieldType == typeof(string));

        var mapedIds = new Dictionary<string, FieldInfo>();
        foreach (var field in canditates) {
            var id = (String) field.GetValue(null);
            if (mapedIds.ContainsKey(id)) {
                Assert.Fail($"{typeof(DiagnosticDescriptors).FullName}.{field.Name}: Die Id '{id}' wurde bereits von {typeof(DiagnosticDescriptors).FullName}.{mapedIds[id].Name} vergeben");
            } else {
                mapedIds[id] = field;
            }
        }
    }

    static List<Type> FindAllDerivedTypesAndSelf<T>() {
        return FindAllDerivedTypesAndSelf<T>(Assembly.GetAssembly(typeof(T)));
    }

    static List<Type> FindAllDerivedTypesAndSelf<T>(Assembly assembly) {
        var derivedType = typeof(T);
        return assembly.GetTypes()
                       .Where(t => derivedType.IsAssignableFrom(t))
                       .ToList();

    }
}