using System.Text;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace ProjectManagement.Api.Services.Documents;

public sealed class DocumentExtractionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class DocumentUploadValidationException(string message, int statusCode = 400)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IDocumentTextExtractor
{
    string Extract(string extension, Stream content);
}

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private const long MaxCharactersInDocxPart = 10_000_000;

    public string Extract(string extension, Stream content)
    {
        try
        {
            var text = extension.ToLowerInvariant() switch
            {
                ".txt" => ExtractText(content),
                ".docx" => ExtractWordDocument(content),
                ".pdf" => ExtractPdf(content),
                _ => throw new DocumentUploadValidationException(
                    "Formato no permitido. Utiliza archivos PDF, DOCX o TXT.")
            };

            if (string.IsNullOrWhiteSpace(text))
                throw new DocumentExtractionException("El documento no contiene texto legible.");

            return text.Trim();
        }
        catch (DocumentUploadValidationException)
        {
            throw;
        }
        catch (DocumentExtractionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or DecoderFallbackException
            or FileFormatException
            or XmlException
            or OpenXmlPackageException
            or PdfDocumentFormatException
            or PdfDocumentEncryptedException)
        {
            throw new DocumentExtractionException(
                "No se pudo leer el documento. Comprueba que no esté dañado o protegido.", exception);
        }
    }

    private static string ExtractText(Stream content)
    {
        using var reader = new StreamReader(
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string ExtractWordDocument(Stream content)
    {
        using var document = WordprocessingDocument.Open(content, false, new OpenSettings
        {
            MaxCharactersInPart = MaxCharactersInDocxPart
        });

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            body.Descendants<Paragraph>()
                .Select(paragraph => paragraph.InnerText)
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)));
    }

    private static string ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        return string.Join(
            Environment.NewLine,
            document.GetPages().Select(page => page.Text));
    }
}
