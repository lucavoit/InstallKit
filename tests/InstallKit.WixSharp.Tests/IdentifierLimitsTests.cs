using System.Linq;
using System.Xml.Linq;
using InstallKit.WixSharp;
using Xunit;

namespace InstallKit.WixSharp.Tests;

public class IdentifierLimitsTests
{
    static readonly XNamespace Ns = "http://wixtoolset.org/schemas/v4/wxs";

    const string LongFile = "Microsoft.Extensions.Configuration.EnvironmentVariables.dll_1712862982";
    static readonly string LongComponent = "Component." + LongFile;

    static XDocument Document(params string[] longFiles) =>
        new XDocument(new XElement(Ns + "Wix",
            new XElement(Ns + "Package",
                new XElement(Ns + "Directory", new XAttribute("Id", "INSTALLDIR"),
                    new XElement(Ns + "Component", new XAttribute("Id", "Component.App.exe_1"), new XElement(Ns + "File", new XAttribute("Id", "App.exe_1"))),
                    longFiles.Select(f => new XElement(Ns + "Component", new XAttribute("Id", "Component." + f),
                        new XElement(Ns + "File", new XAttribute("Id", f), new XAttribute("Source", f)),
                        new XElement(Ns + "XmlFile", new XAttribute("File", $"[#{f}]"), new XAttribute("ElementPath", "/a")),
                        new XElement(Ns + "Shortcut", new XAttribute("Target", $"[#{f}]\\sub")))).Cast<object>().ToArray()),
                new XElement(Ns + "Feature", new XAttribute("Id", "Complete"),
                    new XElement(Ns + "ComponentRef", new XAttribute("Id", "Component.App.exe_1")),
                    longFiles.Select(f => new XElement(Ns + "ComponentRef", new XAttribute("Id", "Component." + f))).Cast<object>().ToArray()))));

    static string[] Ids(XDocument document, string element) =>
        document.Descendants().Where(e => e.Name.LocalName == element).Select(e => (string)e.Attribute("Id")!).ToArray();

    [Fact]
    public void Short_ids_are_left_alone()
    {
        var document = Document();
        var before = document.ToString();

        Assert.Equal(0, IdentifierLimits.Apply(document));
        Assert.Equal(before, document.ToString());
    }

    [Fact]
    public void A_long_component_id_is_shortened_to_72_characters_and_its_references_follow()
    {
        var document = Document(LongFile);

        var changed = IdentifierLimits.Apply(document);

        Assert.True(LongComponent.Length > 72);
        Assert.Equal(1, changed);                                       // only the component: the file id is 70 characters
        var component = Ids(document, "Component").Single(id => id != "Component.App.exe_1");
        Assert.Equal(72, component.Length);
        Assert.StartsWith(LongComponent.Substring(0, 63), component);
        Assert.Contains(component, Ids(document, "ComponentRef"));       // the feature still points at it
        Assert.DoesNotContain(LongComponent, Ids(document, "ComponentRef"));
    }

    [Fact]
    public void Formatted_references_to_a_renamed_file_follow_it()
    {
        var document = Document(LongComponent);   // a file id that is itself too long

        IdentifierLimits.Apply(document);

        var fileId = Ids(document, "File").Single(id => id != "App.exe_1");
        Assert.True(fileId.Length <= 72);
        Assert.Equal($"[#{fileId}]", (string)document.Descendants().Single(e => e.Name.LocalName == "XmlFile").Attribute("File")!);
        Assert.Equal($"[#{fileId}]\\sub", (string)document.Descendants().Single(e => e.Name.LocalName == "Shortcut").Attribute("Target")!);
    }

    [Fact]
    public void The_new_id_is_stable_and_different_ids_stay_different_even_with_the_same_start()
    {
        var a = LongFile + "A";
        var b = LongFile + "B";

        Assert.Equal(IdentifierLimits.Shorten(a), IdentifierLimits.Shorten(a));
        Assert.NotEqual(IdentifierLimits.Shorten(a), IdentifierLimits.Shorten(b));
        Assert.All(new[] { a, b }, id => Assert.Equal(72, IdentifierLimits.Shorten(id).Length));

        var document = Document(a, b);
        IdentifierLimits.Apply(document);
        var components = Ids(document, "Component");
        Assert.Equal(components.Length, components.Distinct().Count());
    }

    [Fact]
    public void Applying_it_twice_changes_nothing_more()
    {
        var document = Document(LongFile);
        IdentifierLimits.Apply(document);
        var once = document.ToString();

        Assert.Equal(0, IdentifierLimits.Apply(document));
        Assert.Equal(once, document.ToString());
    }

    [Fact]
    public void Every_reference_still_resolves()
    {
        var document = Document(LongFile, LongFile + "X", "short.dll_1");

        IdentifierLimits.Apply(document);

        var components = Ids(document, "Component").ToHashSet();
        Assert.All(Ids(document, "ComponentRef"), id => Assert.Contains(id, components));
    }
}
