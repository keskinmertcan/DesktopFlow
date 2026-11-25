using System.Diagnostics;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Storage;

public static class StorageCategories
{
    private static readonly Dictionary<string, StorageCategory> ExtraExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".heic"] = StorageCategory.Images,
        [".heif"] = StorageCategory.Images,
        [".psd"] = StorageCategory.Images,
        [".raw"] = StorageCategory.Images,
        [".cr2"] = StorageCategory.Images,
        [".nef"] = StorageCategory.Images,
        [".dng"] = StorageCategory.Images,
        [".csv"] = StorageCategory.Documents,
        [".md"] = StorageCategory.Documents,
        [".epub"] = StorageCategory.Documents,
        [".mobi"] = StorageCategory.Documents,
        [".m4v"] = StorageCategory.Video,
        [".mpg"] = StorageCategory.Video,
        [".mpeg"] = StorageCategory.Video,
        [".flv"] = StorageCategory.Video,
        [".wma"] = StorageCategory.Audio,
        [".opus"] = StorageCategory.Audio,
        [".bz2"] = StorageCategory.Archives,
        [".xz"] = StorageCategory.Archives,
        [".cab"] = StorageCategory.Archives,
        [".iso"] = StorageCategory.DiskImages,
        [".img"] = StorageCategory.DiskImages,
        [".vhd"] = StorageCategory.DiskImages,
        [".vhdx"] = StorageCategory.DiskImages,
        [".vmdk"] = StorageCategory.DiskImages,
        [".wim"] = StorageCategory.DiskImages,
    };

    public static StorageCategory ForFile(string fileName, IFileClassifier classifier, IReadOnlyList<ExtensionMapping> mappings)
    {
        var classified = classifier.Classify(fileName, mappings);
        var category = FromCategoryKey(classified.CategoryKey);
        if (category != StorageCategory.Other)
        {
            return category;
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Length > 0 && ExtraExtensions.TryGetValue(extension, out var extra))
        {
            return extra;
        }

        return StorageCategory.Other;
    }

    public static StorageCategory FromCategoryKey(string categoryKey)
    {
        var head = categoryKey.Split('.', 2)[0];
        return head.ToLowerInvariant() switch
        {
            "images" => StorageCategory.Images,
            "videos" => StorageCategory.Video,
            "audio" => StorageCategory.Audio,
            "documents" => StorageCategory.Documents,
            "archives" => StorageCategory.Archives,
            "installers" => StorageCategory.Installers,
            "development" => StorageCategory.Code,
            "other" => StorageCategory.Other,
            _ => StorageCategory.Other,
        };
    }

    public static string LabelKey(StorageCategory category)
    {
        return category switch
        {
            StorageCategory.Images => "Cat_Images",
            StorageCategory.Video => "Cat_Video",
            StorageCategory.Audio => "Cat_Audio",
            StorageCategory.Documents => "Cat_Documents",
            StorageCategory.Archives => "Cat_Archives",
            StorageCategory.Installers => "Cat_Installers",
            StorageCategory.DiskImages => "Cat_DiskImages",
            StorageCategory.Code => "Cat_Code",
            StorageCategory.Other => "Cat_Other",
            _ => throw new UnreachableException(category.ToString()),
        };
    }
}
