#region Using Directives

using NUnit.Framework;
using Pharmatechnik.Nav.Language;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class PathProviderTests {

    [Test]
    public void TestGeneratedFolderName() {
        Assert.That(PathProvider.GeneratedFolderName, Is.EqualTo("generated"), "Wrong GeneratedFolderName");
    }
       
    [Test]
    public void TestGeneratedFileNameSuffix() {
        Assert.That(PathProvider.GeneratedFileNameSuffix, Is.EqualTo("generated"), "Wrong GeneratedFileNameSuffix");
    }

    [Test]
    public void TestCSharpFileExtension() {
        Assert.That(PathProvider.CSharpFileExtension, Is.EqualTo("cs"), "Wrong CSharpFileExtension");
    }

    [Test]
    public void TestGeneratedPaths() {

        var taskName       = "Test";
        var syntaxFileName = @"n:\av\test.nav";

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: null, options: null);

        Assert.That(pathProvider.TaskName              , Is.EqualTo(taskName));
        Assert.That(pathProvider.SyntaxFileName        , Is.EqualTo(syntaxFileName));

        Assert.That(pathProvider.WflDirectory          , Is.EqualTo(@"n:\av\WFL"));            
        Assert.That(pathProvider.WfsFileName           , Is.EqualTo(@"n:\av\WFL\TestWFS.cs"));

        Assert.That(pathProvider.WflGeneratedDirectory , Is.EqualTo(@"n:\av\WFL\generated"));
        Assert.That(pathProvider.WfsBaseFileName       , Is.EqualTo(@"n:\av\WFL\generated\TestWFSBase.generated.cs"));            
        Assert.That(pathProvider.IBeginWfsFileName     , Is.EqualTo(@"n:\av\WFL\generated\IBeginTestWFS.generated.cs"));
            
        Assert.That(pathProvider.IwflGeneratedDirectory, Is.EqualTo(@"n:\av\IWFL\generated"));
        Assert.That(pathProvider.IWfsFileName          , Is.EqualTo(@"n:\av\IWFL\generated\ITestWFS.generated.cs"));

        Assert.That(pathProvider.GetToFileName("MyTo") , Is.EqualTo(@"n:\av\IWFL\generated\MyTo.generated.cs"));
    }

    [Test]
    public void TestGeneratedPathsWithTo() {

        var taskName       = "Test";
        var syntaxFileName = @"n:\av\tets.nav";
        var generateTo     = "generateTo";

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: generateTo, options: null);

        Assert.That(pathProvider.TaskName              , Is.EqualTo(taskName));
        Assert.That(pathProvider.SyntaxFileName        , Is.EqualTo(syntaxFileName));

        Assert.That(pathProvider.WflDirectory          , Is.EqualTo(@"n:\av\WFL\generateTo"));
        Assert.That(pathProvider.WfsFileName           , Is.EqualTo(@"n:\av\WFL\generateTo\TestWFS.cs"));

        Assert.That(pathProvider.WflGeneratedDirectory , Is.EqualTo(@"n:\av\WFL\generateTo\generated"));
        Assert.That(pathProvider.WfsBaseFileName       , Is.EqualTo(@"n:\av\WFL\generateTo\generated\TestWFSBase.generated.cs"));
        Assert.That(pathProvider.IBeginWfsFileName     , Is.EqualTo(@"n:\av\WFL\generateTo\generated\IBeginTestWFS.generated.cs"));

        Assert.That(pathProvider.IwflGeneratedDirectory, Is.EqualTo(@"n:\av\IWFL\generateTo\generated"));
        Assert.That(pathProvider.IWfsFileName          , Is.EqualTo(@"n:\av\IWFL\generateTo\generated\ITestWFS.generated.cs"));

    }

    [Test]
    public void TestGeneratedPathsWithIwflRootDirectory() {

        var taskName       = "Test";
        var syntaxFileName = @"n:\av\feature\test.nav";

        var options = GenerationOptions.Default with {
            IwflRootDirectory = @"c:\shared",
            ProjectRootDirectory = @"n:\av"
        };

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: null, options: options);

        Assert.That(pathProvider.TaskName              , Is.EqualTo(taskName));
        Assert.That(pathProvider.SyntaxFileName        , Is.EqualTo(syntaxFileName));

        Assert.That(pathProvider.WflDirectory          , Is.EqualTo(@"n:\av\feature\WFL"));            
        Assert.That(pathProvider.WfsFileName           , Is.EqualTo(@"n:\av\feature\WFL\TestWFS.cs"));

        Assert.That(pathProvider.WflGeneratedDirectory , Is.EqualTo(@"n:\av\feature\WFL\generated"));
        Assert.That(pathProvider.WfsBaseFileName       , Is.EqualTo(@"n:\av\feature\WFL\generated\TestWFSBase.generated.cs"));            
        Assert.That(pathProvider.IBeginWfsFileName     , Is.EqualTo(@"n:\av\feature\WFL\generated\IBeginTestWFS.generated.cs"));
            
        Assert.That(pathProvider.IwflGeneratedDirectory, Is.EqualTo(@"c:\shared\feature\IWFL\generated"));
        Assert.That(pathProvider.IWfsFileName          , Is.EqualTo(@"c:\shared\feature\IWFL\generated\ITestWFS.generated.cs"));

        Assert.That(pathProvider.GetToFileName("MyTo") , Is.EqualTo(@"c:\shared\feature\IWFL\generated\MyTo.generated.cs"));

    }

    [Test]
    public void TestIwflRootDirectory() {

        var taskName       = "Test";
        var syntaxFileName = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten\OffenePostenDruckauswahl.nav";

        var options = GenerationOptions.Default with {
            IwflRootDirectory = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten.Shared",
            ProjectRootDirectory = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten"
        };

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: null, options: options);

        Assert.That(pathProvider.IwflGeneratedDirectory, Is.EqualTo(@"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten.Shared\IWFL\generated"));
    }


    [Test]
    public void TestGeneratedPathsWithWflRootDirectory() {

        var taskName       = "Test";
        var syntaxFileName = @"n:\av\feature\test.nav";

        var options = GenerationOptions.Default with {
            WflRootDirectory = @"c:\AnyWhere",
            ProjectRootDirectory = @"n:\av"
        };

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: null, options: options);

        Assert.That(pathProvider.TaskName              , Is.EqualTo(taskName));
        Assert.That(pathProvider.SyntaxFileName        , Is.EqualTo(syntaxFileName));

        Assert.That(pathProvider.WflDirectory          , Is.EqualTo(@"c:\AnyWhere\feature\WFL"));            
        Assert.That(pathProvider.WfsFileName           , Is.EqualTo(@"c:\AnyWhere\feature\WFL\TestWFS.cs"));

        Assert.That(pathProvider.WflGeneratedDirectory , Is.EqualTo(@"c:\AnyWhere\feature\WFL\generated"));
        Assert.That(pathProvider.WfsBaseFileName       , Is.EqualTo(@"c:\AnyWhere\feature\WFL\generated\TestWFSBase.generated.cs"));            
        Assert.That(pathProvider.IBeginWfsFileName     , Is.EqualTo(@"c:\AnyWhere\feature\WFL\generated\IBeginTestWFS.generated.cs"));
            
        Assert.That(pathProvider.IwflGeneratedDirectory, Is.EqualTo(@"n:\av\feature\IWFL\generated"));
        Assert.That(pathProvider.IWfsFileName          , Is.EqualTo(@"n:\av\feature\IWFL\generated\ITestWFS.generated.cs"));

        Assert.That(pathProvider.GetToFileName("MyTo") , Is.EqualTo(@"n:\av\feature\IWFL\generated\MyTo.generated.cs"));

    }

    [Test]
    public void TestWflRootDirectory() {

        var taskName       = "Test";
        var syntaxFileName = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten\OffenePostenDruckauswahl.nav";

        var options = GenerationOptions.Default with {
            WflRootDirectory = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten.AnyWhere",
            ProjectRootDirectory = @"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten"
        };

        var pathProvider = new PathProvider(syntaxFileName: syntaxFileName, taskName: taskName, generateTo: null, options: options);

        Assert.That(pathProvider.WflGeneratedDirectory, Is.EqualTo(@"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten.AnyWhere\WFL\generated"));
        Assert.That(pathProvider.WflDirectory, Is.EqualTo(@"C:\ws\xtplus\Main\XTplusApplication\src\XTplus.OffenePosten.AnyWhere\WFL"));
    }

}