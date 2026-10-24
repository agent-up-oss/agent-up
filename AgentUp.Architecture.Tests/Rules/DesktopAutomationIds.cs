using System.Xml;
using System.Xml.Linq;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class DesktopAutomationIds
{
    private static readonly string[] InteractiveElements =
    [
        "Button",
        "TextBox",
        "ListBox",
        "ComboBox",
        "CheckBox",
        "ToggleSwitch",
        "ToggleButton",
        "HyperlinkButton",
        "TabItem",
        "RadioButton",
        "MenuItem",
        "RepeatButton",
        "SplitButton",
        "NumericUpDown",
        "AutoCompleteBox",
        "Slider"
    ];

    [Test]
    public void Desktop_views_give_interactive_controls_an_AutomationId()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var views = Directory.EnumerateFiles(
                Path.Join(root, "AgentUp.Desktop"),
                "*.axaml",
                SearchOption.AllDirectories)
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}App.axaml", StringComparison.Ordinal))
            .ToArray();

        var violations = views
            .SelectMany(path => FindMissingIds(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Interactive Desktop controls need AutomationProperties.AutomationId so Desktop-surface flows can address them by testId.");
    }

    private static IEnumerable<string> FindMissingIds(string root, string path)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(path, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            return [$"{ArchitectureFixture.Relative(root, path)}: could not parse AXAML ({exception.Message})"];
        }

        return document.Descendants()
            .Select(element => (Element: element, Name: LocalName(element)))
            .Where(item => !item.Name.Contains('.', StringComparison.Ordinal))
            .Where(item => InteractiveElements.Contains(item.Name, StringComparer.Ordinal))
            .Where(item => !HasAutomationId(item.Element))
            .Select(item =>
            {
                var line = item.Element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
                return $"{ArchitectureFixture.Relative(root, path)}:{line}: <{item.Name}> is missing AutomationProperties.AutomationId";
            });
    }

    private static bool HasAutomationId(XElement element)
        => element.Attributes().Any(attribute =>
            string.Equals(LocalName(attribute), "AutomationId", StringComparison.Ordinal)
            || string.Equals(attribute.Name.LocalName, "AutomationProperties.AutomationId", StringComparison.Ordinal));

    private static string LocalName(XElement element)
        => element.Name.LocalName;

    private static string LocalName(XAttribute attribute)
        => attribute.Name.LocalName;
}
