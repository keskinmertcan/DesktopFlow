using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Classification;

public static class DefaultTaxonomy
{
    public static List<ExtensionMapping> CreateDesktopMappings()
    {
        return
        [
            Map(".pdf", "documents.pdf", "Documents/PDF"),
            Map(".doc", "documents.word", "Documents/Word"),
            Map(".docx", "documents.word", "Documents/Word"),
            Map(".txt", "documents.text", "Documents/Text"),
            Map(".rtf", "documents.text", "Documents/Text"),
            Map(".odt", "documents.text", "Documents/Text"),
            Map(".xls", "documents.excel", "Documents/Excel"),
            Map(".xlsx", "documents.excel", "Documents/Excel"),
            Map(".ods", "documents.excel", "Documents/Excel"),
            Map(".ppt", "documents.powerpoint", "Documents/PowerPoint"),
            Map(".pptx", "documents.powerpoint", "Documents/PowerPoint"),
            Map(".png", "images.png", "Images/PNG"),
            Map(".jpg", "images.jpg", "Images/JPG"),
            Map(".jpeg", "images.jpg", "Images/JPG"),
            Map(".svg", "images.svg", "Images/SVG"),
            Map(".webp", "images.webp", "Images/WEBP"),
            Map(".gif", "images.other", "Images/Other"),
            Map(".bmp", "images.other", "Images/Other"),
            Map(".ico", "images.other", "Images/Other"),
            Map(".tif", "images.other", "Images/Other"),
            Map(".tiff", "images.other", "Images/Other"),
            Map(".mp4", "videos", "Videos"),
            Map(".mkv", "videos", "Videos"),
            Map(".avi", "videos", "Videos"),
            Map(".mov", "videos", "Videos"),
            Map(".wmv", "videos", "Videos"),
            Map(".webm", "videos", "Videos"),
            Map(".mp3", "audio", "Audio"),
            Map(".wav", "audio", "Audio"),
            Map(".flac", "audio", "Audio"),
            Map(".aac", "audio", "Audio"),
            Map(".ogg", "audio", "Audio"),
            Map(".m4a", "audio", "Audio"),
            Map(".zip", "archives", "Archives"),
            Map(".rar", "archives", "Archives"),
            Map(".7z", "archives", "Archives"),
            Map(".tar", "archives", "Archives"),
            Map(".gz", "archives", "Archives"),
            Map(".exe", "installers", "Installers"),
            Map(".msi", "installers", "Installers"),
            Map(".msix", "installers", "Installers"),
            Map(".appx", "installers", "Installers"),
            Map(".cs", "development.csharp", "Development/CSharp"),
            Map(".py", "development.python", "Development/Python"),
            Map(".js", "development.javascript", "Development/JavaScript"),
            Map(".ts", "development.javascript", "Development/JavaScript"),
            Map(".html", "development.other", "Development/Other"),
            Map(".css", "development.other", "Development/Other"),
            Map(".json", "development.other", "Development/Other"),
            Map(".xml", "development.other", "Development/Other"),
            Map(".sql", "development.other", "Development/Other"),
            Map(".php", "development.other", "Development/Other"),
            Map(".java", "development.other", "Development/Other"),
            Map(".cpp", "development.other", "Development/Other"),
            Map(".h", "development.other", "Development/Other"),
        ];
    }

    public static List<ExtensionMapping> CreateDownloadsMappings()
    {
        return
        [
            Map(".pdf", "pdf", "PDF"),
            Map(".doc", "documents", "Documents"),
            Map(".docx", "documents", "Documents"),
            Map(".txt", "documents", "Documents"),
            Map(".rtf", "documents", "Documents"),
            Map(".odt", "documents", "Documents"),
            Map(".xls", "documents", "Documents"),
            Map(".xlsx", "documents", "Documents"),
            Map(".ods", "documents", "Documents"),
            Map(".ppt", "documents", "Documents"),
            Map(".pptx", "documents", "Documents"),
            Map(".png", "images", "Images"),
            Map(".jpg", "images", "Images"),
            Map(".jpeg", "images", "Images"),
            Map(".svg", "images", "Images"),
            Map(".webp", "images", "Images"),
            Map(".gif", "images", "Images"),
            Map(".bmp", "images", "Images"),
            Map(".ico", "images", "Images"),
            Map(".tif", "images", "Images"),
            Map(".tiff", "images", "Images"),
            Map(".mp4", "videos", "Videos"),
            Map(".mkv", "videos", "Videos"),
            Map(".avi", "videos", "Videos"),
            Map(".mov", "videos", "Videos"),
            Map(".wmv", "videos", "Videos"),
            Map(".webm", "videos", "Videos"),
            Map(".mp3", "audio", "Audio"),
            Map(".wav", "audio", "Audio"),
            Map(".flac", "audio", "Audio"),
            Map(".aac", "audio", "Audio"),
            Map(".ogg", "audio", "Audio"),
            Map(".m4a", "audio", "Audio"),
            Map(".zip", "archives", "Archives"),
            Map(".rar", "archives", "Archives"),
            Map(".7z", "archives", "Archives"),
            Map(".tar", "archives", "Archives"),
            Map(".gz", "archives", "Archives"),
            Map(".exe", "installers", "Installers"),
            Map(".msi", "installers", "Installers"),
            Map(".msix", "installers", "Installers"),
            Map(".appx", "installers", "Installers"),
            Map(".cs", "development", "Development"),
            Map(".py", "development", "Development"),
            Map(".js", "development", "Development"),
            Map(".ts", "development", "Development"),
            Map(".html", "development", "Development"),
            Map(".css", "development", "Development"),
            Map(".json", "development", "Development"),
            Map(".xml", "development", "Development"),
            Map(".sql", "development", "Development"),
            Map(".php", "development", "Development"),
            Map(".java", "development", "Development"),
            Map(".cpp", "development", "Development"),
            Map(".h", "development", "Development"),
        ];
    }

    public static IReadOnlyList<string> DesktopFolderTree()
    {
        return
        [
            "Documents/PDF",
            "Documents/Word",
            "Documents/Excel",
            "Documents/PowerPoint",
            "Documents/Text",
            "Images/PNG",
            "Images/JPG",
            "Images/SVG",
            "Images/WEBP",
            "Images/Other",
            "Videos",
            "Audio",
            "Archives",
            "Installers",
            "Development/CSharp",
            "Development/Python",
            "Development/JavaScript",
            "Development/Other",
            "Other",
        ];
    }

    private static ExtensionMapping Map(string extension, string category, string relativePath)
    {
        return new ExtensionMapping
        {
            Extension = extension,
            CategoryKey = category,
            RelativePath = relativePath,
        };
    }
}
