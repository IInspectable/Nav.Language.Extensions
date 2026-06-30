#region Using Directives

using System.Linq;

using NUnit.Framework;
using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class PerformanceTests {

    [Explicit("Wall-Clock-Schranke (MaxTime) ist auf geteilter CI-Hardware flaky — lokal/bei Bedarf ausführen."), Test, MaxTime(200)]
    public void TestPerformance() {

        SyntaxTree.ParseText(Resources.LargeNav);

        var syntaxTree = SyntaxTree.ParseText(Resources.LargeNav);

        var lastToken = syntaxTree.Tokens.Last();

        Assert.That(lastToken.End, Is.EqualTo(Resources.LargeNav.Length));
    }
}