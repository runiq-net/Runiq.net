namespace Runiq.AI.Rag.Tests.Samples;

using Runiq.AI.Rag.Ingestion;

/// <summary>Verifies the corporate document assistant sample corpus.</summary>
public sealed class ProductSupportAssistantSampleTests
{
    [Fact]
    // Verifies the sample contains the Turkish corporate IT markdown procedures used by startup ingestion.
    public void KnowledgeBase_ShouldContainExpectedCorporateMarkdownDocuments()
    {
        var root = FindSampleRoot();
        var documentsRoot = Path.Combine(root, "SampleDocuments", "corporate");
        var markdownDocuments = Directory.GetFiles(documentsRoot, "*.md", SearchOption.TopDirectoryOnly);

        Assert.Equal(12, markdownDocuments.Length);
        Assert.Contains(markdownDocuments, path => Path.GetFileName(path) == "vpn-baglanti-rehberi.md");
        Assert.Contains(markdownDocuments, path => Path.GetFileName(path) == "parola-guvenligi-politikasi.md");
        Assert.Contains(markdownDocuments, path => Path.GetFileName(path) == "harici-erisim-izni-proseduru.md");
    }

    [Fact]
    // Verifies the directory source reads searchable markdown content from the same corporate folder the sample copies to bin output.
    public async Task KnowledgeBase_ShouldReadCorporateMarkdownDocuments()
    {
        var corporateDirectory = Path.Combine(FindSampleRoot(), "SampleDocuments", "corporate");
        var source = new DirectoryRagDocumentSource(corporateDirectory, searchPattern: "*.md");

        var documents = await source.GetDocumentsAsync();

        Assert.Equal(12, documents.Count);
        Assert.All(documents, document => Assert.Equal("text/markdown", document.ContentType));
        Assert.Contains(documents, document =>
            document.Id == "vpn-baglanti-rehberi.md" &&
            document.Content.Contains("VPN baglantisi calismiyorsa", StringComparison.OrdinalIgnoreCase));
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
