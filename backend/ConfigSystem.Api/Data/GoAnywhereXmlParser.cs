using System.Xml.Linq;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Parses GoAnywhere XML project files and extracts variable definitions.
/// </summary>
public static class GoAnywhereXmlParser
{
    public record ParsedVariable(
        string Name,
        string Value,
        string Description,
        string Type);

    /// <summary>
    /// Extracts all &lt;variable&gt; elements from GoAnywhere XML project content.
    /// </summary>
    public static List<ParsedVariable> ParseVariables(string xmlContent)
    {
        var variables = new List<ParsedVariable>();

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var variableElements = doc.Descendants("variable").ToList();

            foreach (var varElement in variableElements)
            {
                var name = varElement.Attribute("name")?.Value ?? "";
                var value = varElement.Attribute("value")?.Value ?? "";
                var description = varElement.Attribute("description")?.Value ?? "";

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var type = InferType(value);
                variables.Add(new ParsedVariable(name, value, description, type));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing GoAnywhere XML: {ex.Message}");
        }

        return variables;
    }

    /// <summary>
    /// Infers the variable type based on its value.
    /// </summary>
    private static string InferType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "STRING";

        // Check for boolean
        if (value.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("False", StringComparison.OrdinalIgnoreCase))
            return "BOOLEAN";

        // Check for integer
        if (int.TryParse(value, out _))
            return "INTEGER";

        // Check for decimal
        if (decimal.TryParse(value, out _))
            return "INTEGER";

        // Default to string
        return "STRING";
    }

    /// <summary>
    /// Combines multiple XML contents and returns unique variables (by name).
    /// </summary>
    public static List<ParsedVariable> ParseMultipleXmlContents(params string[] xmlContents)
    {
        var allVariables = new List<ParsedVariable>();

        foreach (var content in xmlContents)
        {
            if (string.IsNullOrWhiteSpace(content))
                continue;

            allVariables.AddRange(ParseVariables(content));
        }

        // Return unique variables by name (keep first occurrence)
        return allVariables
            .GroupBy(v => v.Name)
            .Select(g => g.First())
            .ToList();
    }
}
