using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ProjectManagement.Api.Services.Documents;

namespace ProjectManagement.Api.Tests.Services;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    [Fact]
    public void Extract_Txt_ReturnsUtf8Text()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Notas de seguimiento"));

        var text = _extractor.Extract(".txt", content);

        Assert.Equal("Notas de seguimiento", text);
    }

    [Fact]
    public void Extract_Docx_ReturnsParagraphText()
    {
        using var content = CreateDocx("Minuta DOCX de prueba");

        var text = _extractor.Extract(".docx", content);

        Assert.Contains("Minuta DOCX de prueba", text);
    }

    [Fact]
    public void Extract_Pdf_ReturnsPageText()
    {
        using var content = CreatePdf("Minuta PDF de prueba");

        var text = _extractor.Extract(".pdf", content);

        Assert.Contains("Minuta PDF de prueba", text);
    }

    [Fact]
    public void Extract_UnsupportedExtension_ThrowsValidationError()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("No se debe abrir"));

        var error = Assert.Throws<DocumentUploadValidationException>(
            () => _extractor.Extract(".exe", content));

        Assert.Contains("PDF, DOCX o TXT", error.Message);
    }

    [Fact]
    public void Extract_EmptyText_ReportsUnreadableDocument()
    {
        using var content = new MemoryStream();

        Assert.Throws<DocumentExtractionException>(() => _extractor.Extract(".txt", content));
    }

    [Fact]
    public void Extract_DamagedDocx_ReportsUnreadableDocument()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("not a docx"));

        Assert.Throws<DocumentExtractionException>(() => _extractor.Extract(".docx", content));
    }

    [Fact]
    public void Extract_DamagedPdf_ReportsUnreadableDocument()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("not a pdf"));

        Assert.Throws<DocumentExtractionException>(() => _extractor.Extract(".pdf", content));
    }

    private static MemoryStream CreateDocx(string text)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            mainPart.Document.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePdf(string text)
    {
        var contentStream = $"BT /F1 12 Tf 50 700 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}\nendstream"
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return new MemoryStream(Encoding.ASCII.GetBytes(builder.ToString()));
    }
}
