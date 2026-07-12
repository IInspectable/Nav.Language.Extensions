using System;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests;

[TestFixture]
public class ExtentTests {

    [Test]
    public void TestMissingExtent() {
        var missing = TextExtent.Missing;

        Assert.That(missing.IsMissing,        Is.True);
        Assert.That(missing.IsEmpty,          Is.True);
        Assert.That(missing.IsEmptyOrMissing, Is.True);

        Assert.That(missing.Start,  Is.EqualTo(-1));
        Assert.That(missing.End,    Is.EqualTo(-1));
        Assert.That(missing.Length, Is.EqualTo(0));

        Assert.That(missing.ToString(), Is.EqualTo("<missing>"));
    }

    [Test]
    public void TestEmptyExtent() {
        var empty = TextExtent.Empty;

        Assert.That(empty.IsMissing,        Is.False);
        Assert.That(empty.IsEmpty,          Is.True);
        Assert.That(empty.IsEmptyOrMissing, Is.True);

        Assert.That(empty.Start,  Is.EqualTo(0));
        Assert.That(empty.End,    Is.EqualTo(0));
        Assert.That(empty.Length, Is.EqualTo(0));
    }

    [Test]
    public void TestBoundConstruction() {
        var extent = TextExtent.FromBounds(1, 9);

        Assert.That(extent.IsMissing,        Is.False);
        Assert.That(extent.IsEmpty,          Is.False);
        Assert.That(extent.IsEmptyOrMissing, Is.False);

        Assert.That(extent.Start,  Is.EqualTo(1));
        Assert.That(extent.End,    Is.EqualTo(9));
        Assert.That(extent.Length, Is.EqualTo(8));
    }

    [Test]
    public void TestInvalidConstructionMinus1StartNonZeroLength() {

        // ReSharper disable ObjectCreationAsStatement
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtent(-1, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtent(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtent(-1, -1));
        // ReSharper restore ObjectCreationAsStatement
    }

    [Test]
    public void TestInvalidConstructionMinus1StartNegativeLength() {
        // ReSharper disable ObjectCreationAsStatement
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtent(-1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtent(-1, -2));
        // ReSharper restore ObjectCreationAsStatement
    }

    [Test]
    public void TestManualEmpty() {

        var extent = TextExtent.FromBounds(0, 0);
        Assert.That(extent.IsEmpty, Is.True);

        extent = new TextExtent(0, 0);
        Assert.That(extent.IsEmpty, Is.True);
    }

    [Test]
    public void TestContains() {
        var extent = TextExtent.FromBounds(1, 10);

        // Selbst
        Assert.That(extent.Contains(extent), Is.True);

        Assert.That(extent.Contains(TextExtent.FromBounds(2,  9)),  Is.True);
        Assert.That(extent.Contains(TextExtent.FromBounds(1,  1)),  Is.True);
        Assert.That(extent.Contains(TextExtent.FromBounds(10, 10)), Is.True);

        Assert.That(extent.Contains(TextExtent.FromBounds(2, 11)), Is.False);
        Assert.That(extent.Contains(TextExtent.FromBounds(0, 9)),  Is.False);
        Assert.That(extent.Contains(TextExtent.FromBounds(0, 11)), Is.False);

    }

    [Test]
    public void TestFindIndexAtOrBeforePosition() {
        var extents = new[] {

            TextExtent.FromBounds(0,  10),
            TextExtent.FromBounds(20, 30),
            TextExtent.FromBounds(40, 50)
        };

        // Position vor dem ersten Start → -1.
        Assert.That(extents.FindIndexAtOrBeforePosition(-1), Is.EqualTo(-1));
        // Position exakt auf einem Start → dessen Index.
        Assert.That(extents.FindIndexAtOrBeforePosition(0),  Is.EqualTo(0));
        Assert.That(extents.FindIndexAtOrBeforePosition(10), Is.EqualTo(0));
        // FindIndexAtOrBeforePosition interessiert nicht das Ende, sondern nur der Start!
        // Solange der Start des nächsten Elements nicht erreicht wurde, wird der Index des
        // vorigen (davorliegenden) Elements zurückgeliefert – auch in Lücken zwischen Elementen.
        Assert.That(extents.FindIndexAtOrBeforePosition(11), Is.EqualTo(0));
        Assert.That(extents.FindIndexAtOrBeforePosition(20), Is.EqualTo(1));
        Assert.That(extents.FindIndexAtOrBeforePosition(30), Is.EqualTo(1));
        Assert.That(extents.FindIndexAtOrBeforePosition(31), Is.EqualTo(1));
        Assert.That(extents.FindIndexAtOrBeforePosition(40), Is.EqualTo(2));
        Assert.That(extents.FindIndexAtOrBeforePosition(50), Is.EqualTo(2));
        // Position hinter dem letzten Start → letzter Index.
        Assert.That(extents.FindIndexAtOrBeforePosition(51), Is.EqualTo(2));
    }

    [Test]
    public void TestFindIndexAtPosition() {
        var extents = new[] {

            TextExtent.FromBounds(0,  10),
            TextExtent.FromBounds(20, 30),
            TextExtent.FromBounds(40, 50)
        };

        Assert.That(extents.FindIndexAtPosition(-1), Is.EqualTo(-1));
        Assert.That(extents.FindIndexAtPosition(0),  Is.EqualTo(0));
        Assert.That(extents.FindIndexAtPosition(10), Is.LessThan(0));
        Assert.That(extents.FindIndexAtPosition(11), Is.LessThan(0));
        Assert.That(extents.FindIndexAtPosition(20), Is.EqualTo(1));
        Assert.That(extents.FindIndexAtPosition(30), Is.LessThan(0));
        Assert.That(extents.FindIndexAtPosition(31), Is.LessThan(0));
        Assert.That(extents.FindIndexAtPosition(40), Is.EqualTo(2));
        Assert.That(extents.FindIndexAtPosition(50), Is.LessThan(0));
        Assert.That(extents.FindIndexAtPosition(51), Is.LessThan(0));
    }

    [Test]
    public void TestFindElementAtPosition() {
        var extents = new[] {

            TextExtent.FromBounds(0,  10),
            TextExtent.FromBounds(20, 30),
            TextExtent.FromBounds(40, 50)
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => extents.FindElementAtPosition(-1));
        Assert.That(ex!.ParamName,                    Is.EqualTo("position"));
        Assert.That(extents.FindElementAtPosition(0), Is.EqualTo(TextExtent.FromBounds(0, 10)));
        Assert.That(extents.FindElementAtPosition(5), Is.EqualTo(TextExtent.FromBounds(0, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => extents.FindElementAtPosition(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => extents.FindElementAtPosition(11));
        Assert.That(extents.FindElementAtPosition(20), Is.EqualTo(TextExtent.FromBounds(20, 30)));

        Assert.That(extents.FindElementAtPosition(40), Is.EqualTo(TextExtent.FromBounds(40, 50)));
        Assert.Throws<ArgumentOutOfRangeException>(() => extents.FindElementAtPosition(50));
        Assert.Throws<ArgumentOutOfRangeException>(() => extents.FindElementAtPosition(51));

    }

    [Test]
    public void TestGetElements() {
        var extents = new[] {

            TextExtent.FromBounds(0,  10),
            TextExtent.FromBounds(20, 30),
            TextExtent.FromBounds(40, 50)
        };

        Assert.That(extents.GetElements(TextExtent.FromBounds(0, 50),
                                        includeOverlapping: false),
                    Is.EqualTo(extents));

        Assert.That(extents.GetElements(TextExtent.FromBounds(5, 45),
                                        includeOverlapping: false),
                    Is.EqualTo(new[] { TextExtent.FromBounds(20, 30) }));

        Assert.That(extents.GetElements(TextExtent.FromBounds(5, 45),
                                        includeOverlapping: true),
                    Is.EqualTo(extents));

        Assert.That(extents.GetElements(TextExtent.FromBounds(50, 50),
                                        includeOverlapping: true),
                    Is.EqualTo(new[] { TextExtent.FromBounds(40, 50) }));
    }

}
