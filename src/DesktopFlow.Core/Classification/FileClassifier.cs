using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Classification;

public sealed class FileClassifier : IFileClassifier
{
    public const string OtherCategoryKey = "other";
    public const string OtherRelativePath = "Other";

    public FileClassification Classify(string fileName, IReadOnlyList<ExtensionMapping> mappings)
    {
        var extension = NormalizeExtension(Path.GetExtension(fileName));
        if (extension.Length == 0)
        {
            return Other(mappings);
        }

        var match = mappings.FirstOrDefault(mapping =>
            string.Equals(NormalizeExtension(mapping.Extension), extension, StringComparison.OrdinalIgnoreCase));

        if (match is null || string.IsNullOrWhiteSpace(match.RelativePath))
        {
            return Other(mappings);
        }

        var category = string.IsNullOrWhiteSpace(match.CategoryKey) ? OtherCategoryKey : match.CategoryKey.Trim();
        return new FileClassification(category, match.RelativePath.Trim().Replace('\\', '/'));
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim().ToLowerInvariant();
        return value.StartsWith('.') ? value : "." + value;
    }

    private static FileClassification Other(IReadOnlyList<ExtensionMapping> mappings)
    {
        var other = mappings.FirstOrDefault(mapping =>
            string.Equals(mapping.CategoryKey, OtherCategoryKey, StringComparison.OrdinalIgnoreCase));
        var relative = string.IsNullOrWhiteSpace(other?.RelativePath) ? OtherRelativePath : other.RelativePath.Trim();
        return new FileClassification(OtherCategoryKey, relative.Replace('\\', '/'));
    }
}
