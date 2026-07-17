using System.Linq;
using NUnit.Framework;
using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests; 

[TestFixture]
public class LocationTests {

    [Test]
    public void TrivialLineNumberTest() {

        var syntax = Syntax.ParseTaskDefinition("task F {}");

        var loc =syntax.GetLocation();
        Assert.That(loc.LineRange.Start.Line, Is.EqualTo(0));
        Assert.That(loc.LineRange.End.Line,   Is.EqualTo(0));
    }

    [Test]
    public void MultiLineNumberTest() {

        var syntax = Syntax.ParseCodeGenerationUnit(
            "task T1 {}\r\n"          + // 0
            "/* Multiline\r\n"        + // 1
            " Comment */\r\n"         + // 2
            "task T2 {}\n"            + // 3
            "task T3 {}\r"            + // 4
            "//Single Line Comment\r" + // 5
            "task T4 {}\r\n"            // 6
        );

        var t1 = syntax.DescendantNodes<TaskDefinitionSyntax>().First(td => td.Identifier.ToString() == "T1");
        Assert.That(t1.GetLocation().LineRange.Start.Line, Is.EqualTo(0));

        var t2 = syntax.DescendantNodes<TaskDefinitionSyntax>().First(td => td.Identifier.ToString() == "T2");
        Assert.That(t2.GetLocation().LineRange.Start.Line, Is.EqualTo(3));

        var t3 = syntax.DescendantNodes<TaskDefinitionSyntax>().First(td => td.Identifier.ToString() == "T3");
        Assert.That(t3.GetLocation().LineRange.Start.Line, Is.EqualTo(4));

        var t4 = syntax.DescendantNodes<TaskDefinitionSyntax>().First(td => td.Identifier.ToString() == "T4");
        Assert.That(t4.GetLocation().LineRange.Start.Line, Is.EqualTo(6));            
    }

    [Test]
    public void MultiLineNumberWithTrailingNewLineTest() {

        var syntax = Syntax.ParseCodeGenerationUnit(
            "task T1 {}\r\n" // 0
        );

        var t1 = syntax.DescendantNodes<TaskDefinitionSyntax>().First(td => td.Identifier.ToString() == "T1");
        Assert.That(t1.GetLocation().LineRange.Start.Line, Is.EqualTo(0));
    }

    [Test]
    public void NormalizedFilePathIsLowercasedFullPath() {

        var location = new Location(@"C:\Foo\BAR.nav");

        Assert.That(location.NormalizedFilePath, Is.EqualTo(@"c:\foo\bar.nav"));
    }

    [Test]
    public void NormalizedFilePathIsCachedPerInstance() {

        var location = new Location(@"C:\Foo\Bar.nav");

        // Normalisierung (Uri-Parse + GetFullPath + ToLowerInvariant) läuft nur einmal pro Instanz
        Assert.That(location.NormalizedFilePath, Is.SameAs(location.NormalizedFilePath));
    }

    [Test]
    public void NormalizedFilePathOfNullFilePathIsNull() {

        var location = new Location(TextExtent.Empty, LinePosition.Empty, filePath: null);

        Assert.That(location.FilePath,           Is.Null);
        Assert.That(location.NormalizedFilePath, Is.Null);
    }

    [Test]
    public void LocationsWithDifferentlyCasedFilePathsAreEqual() {

        var left  = new Location(@"C:\Foo\Bar.nav");
        var right = new Location(@"c:\foo\bar.NAV");

        // Equality vergleicht den normalisierten Pfad, nicht den Roh-Pfad
        Assert.That(left.Equals(right), Is.True);
        Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
    }

    [Test]
    public void LocationsWithDifferentFilePathsAreNotEqual() {

        var left  = new Location(@"C:\Foo\Bar.nav");
        var right = new Location(@"C:\Foo\Baz.nav");

        Assert.That(left.Equals(right), Is.False);
    }

}