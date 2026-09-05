namespace Runiq.AI.Rag.Tests.Samples;

using Runiq.AI.Rag.Ingestion;

/// <summary>Verifies the mixed-format product-support sample corpus.</summary>
public sealed class ProductSupportAssistantSampleTests
{
    [Fact]
    // Verifies the sample contains exactly five real PDF manuals and two synthetic Markdown notes.
    public void KnowledgeBase_ShouldContainExpectedMixedFormatDocuments()
    {
        var root = FindSampleRoot();
        var documentsRoot = Path.Combine(root, "SampleDocuments");
        var pdfDocuments = Directory.GetFiles(documentsRoot, "*.pdf", SearchOption.AllDirectories);
        var markdownDocuments = Directory.GetFiles(documentsRoot, "*.md", SearchOption.AllDirectories);

        Assert.Equal(5, pdfDocuments.Length);
        Assert.Equal(2, markdownDocuments.Length);
        Assert.All(pdfDocuments, path =>
        {
            using var stream = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[4];
            Assert.Equal(signature.Length, stream.Read(signature));
            Assert.True(signature.SequenceEqual("%PDF"u8));
        });
    }

    [Fact]
    // Verifies the framework extracts searchable text from every real PDF included in the sample corpus.
    public async Task KnowledgeBase_ShouldExtractTextFromRealPdfs()
    {
        var productsDirectory = Path.Combine(FindSampleRoot(), "SampleDocuments", "products");
        var pdfPaths = Directory.GetFiles(productsDirectory, "*.pdf", SearchOption.TopDirectoryOnly);

        foreach (var pdfPath in pdfPaths)
        {
            var source = new DirectoryRagDocumentSource(productsDirectory, searchPattern: Path.GetFileName(pdfPath));
            var document = Assert.Single(await source.GetDocumentsAsync());

            Assert.Equal("application/pdf", document.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(document.Content));
            Assert.Contains('\f', document.Content);
        }
    }

    private static string FindSampleRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "samples", "Runiq.AI.Rag.ProductSupportAssistant");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The Product Support Assistant sample directory was not found.");
    }
}
