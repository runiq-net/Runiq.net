using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Models.Ingestion;
using UglyToad.PdfPig;
using System.Security.Cryptography;
using System.Text;

namespace Runiq.AI.Rag.Ingestion;

/// <summary>Discovers supported files below a directory in ordinal path order.</summary>
public sealed class DirectoryRagDocumentSource : IRagDocumentSource
{
    private readonly string rootPath;
    private readonly HashSet<string> extensions;
    private readonly string searchPattern;
    private readonly SearchOption searchOption;
    private readonly int maximumDocuments;
    private readonly long maximumFileBytes;
    private readonly long maximumTotalBytes;

    /// <summary>Initializes a directory source with optional file selection settings.</summary>
    /// <param name="rootPath">The directory to scan when discovery is requested.</param>
    /// <param name="extensions">Allowed extensions; defaults to text, Markdown and JSON.</param>
    /// <param name="searchPattern">The file-name search pattern.</param>
    /// <param name="recursive">Whether discovery includes subdirectories.</param>
    /// <param name="maximumDocuments">Maximum number of matching files that may be materialized.</param>
    /// <param name="maximumFileBytes">Maximum size of an individual file before it is read.</param>
    /// <param name="maximumTotalBytes">Maximum combined size of matching files before their contents are read.</param>
    public DirectoryRagDocumentSource(
        string rootPath,
        IEnumerable<string>? extensions = null,
        string searchPattern = "*",
        bool recursive = true,
        int maximumDocuments = 10_000,
        long maximumFileBytes = 8 * 1024 * 1024,
        long maximumTotalBytes = 64 * 1024 * 1024)
    {
        this.rootPath = string.IsNullOrWhiteSpace(rootPath) ? throw new ArgumentException("A root path is required.", nameof(rootPath)) : Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        if (string.IsNullOrWhiteSpace(searchPattern) || searchPattern.IndexOf('\0') >= 0 || Path.GetFileName(searchPattern) != searchPattern)
        {
            throw new ArgumentException("A non-empty file-name search pattern is required.", nameof(searchPattern));
        }

        this.searchPattern = searchPattern;
        if (maximumDocuments <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDocuments));
        if (maximumFileBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
        if (maximumTotalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTotalBytes));
        this.maximumDocuments = maximumDocuments;
        this.maximumFileBytes = maximumFileBytes;
        this.maximumTotalBytes = maximumTotalBytes;
        searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        this.extensions = new HashSet<string>(extensions ?? [".txt", ".md", ".markdown", ".json", ".pdf"], StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string Identity => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"directory\n{rootPath}\n{searchPattern}\n{searchOption}")));

    /// <inheritdoc />
    public string SourceType => "Directory";

    /// <inheritdoc />
    public string DisplayValue => Path.GetFileName(rootPath) is { Length: > 0 } name ? name : "directory";

    /// <summary>Gets the normalized absolute root path used during discovery.</summary>
    public string RootPath => rootPath;

    /// <summary>Gets the file-name search pattern used during discovery.</summary>
    public string SearchPattern => searchPattern;

    /// <summary>Gets a value indicating whether discovery includes subdirectories.</summary>
    public bool Recursive => searchOption == SearchOption.AllDirectories;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException(rootPath);
        var files = Directory.EnumerateFiles(rootPath, searchPattern, searchOption)
            .Where(path => extensions.Contains(Path.GetExtension(path))).OrderBy(path => path, StringComparer.Ordinal).Take(maximumDocuments + 1).ToArray();
        if (files.Length > maximumDocuments) throw new InvalidOperationException($"Directory source exceeds the configured limit of {maximumDocuments} documents.");
        var totalBytes = files.Sum(file => new FileInfo(file).Length);
        if (totalBytes > maximumTotalBytes) throw new InvalidOperationException($"Directory source exceeds the configured total size limit of {maximumTotalBytes} bytes.");
        var result = new List<RagSourceDocument>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(file).Length > maximumFileBytes) throw new InvalidOperationException($"File '{Path.GetFileName(file)}' exceeds the configured source file size limit.");
            var type = GetContentType(file);
            var content = type == "application/pdf" ? ExtractPdfText(file) : await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            result.Add(new RagSourceDocument { Id = Path.GetRelativePath(rootPath, file).Replace('\\', '/'), Content = content, ContentType = type, Title = Path.GetFileNameWithoutExtension(file), Source = file, Version = File.GetLastWriteTimeUtc(file).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        return result;
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".md" or ".markdown" => "text/markdown", ".json" => "application/json", ".pdf" => "application/pdf", _ => "text/plain" };
    private static string ExtractPdfText(string path) { using var pdf = PdfDocument.Open(path); return string.Join("\f", pdf.GetPages().Select(page => page.Text)); }
}
