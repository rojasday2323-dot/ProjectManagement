using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class DocumentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadFile_ValidTxt_ReturnsExtractedText()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "projectId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Texto de la minuta"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "minuta.txt");

        var response = await _client.PostAsync("/api/documents/files", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(document);
        Assert.Equal(1, document.ProjectId);
        Assert.Equal("TXT", document.DocumentType);
        Assert.Equal("Prepared", document.ProcessingStatus);
        Assert.Equal("Texto de la minuta", document.ExtractedText);
        Assert.Equal(Encoding.UTF8.GetByteCount("Texto de la minuta"), document.FileSizeBytes);
        Assert.True(document.HasOriginalFile);

        var listed = await _client.GetFromJsonAsync<List<DocumentSummaryResponse>>(
            "/api/documents/projects/1");
        Assert.NotNull(listed);
        Assert.Contains(listed, item => item.Id == document.Id);
        Assert.Equal("Portal Banca Digital v2", listed.Single(item => item.Id == document.Id).ProjectName);

        var downloaded = await _client.GetByteArrayAsync($"/api/documents/{document.Id}/file");
        Assert.Equal(Encoding.UTF8.GetBytes("Texto de la minuta"), downloaded);

        try
        {
            var detail = await _client.GetFromJsonAsync<DocumentResponse>(
                $"/api/documents/{document.Id}");
            Assert.NotNull(detail);
            Assert.Equal(document.ExtractedText, detail.ExtractedText);
        }
        finally
        {
            await DeleteDocumentAsync(document.Id);
        }
    }

    [Fact]
    public async Task UploadFile_UnsupportedFormat_ReturnsBadRequest()
    {
        using var form = CreateUpload("script.exe", "application/octet-stream", [1, 2, 3]);

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_EmptyFile_ReturnsBadRequest()
    {
        using var form = CreateUpload("empty.txt", "text/plain", []);

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_DamagedDocx_ReturnsUnprocessableEntity()
    {
        using var form = CreateUpload(
            "damaged.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Encoding.UTF8.GetBytes("not a Word document"));

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ValidDocxAndPdf_PersistsOriginalAndExtractedText()
    {
        var files = new[]
        {
            (Name: "valid.docx", Type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Bytes: CreateDocxBytes("Documento DOCX válido")),
            (Name: "valid.pdf", Type: "application/pdf", Bytes: CreatePdfBytes("Documento PDF valido"))
        };

        foreach (var item in files)
        {
            using var form = CreateUpload(item.Name, item.Type, item.Bytes);
            var response = await _client.PostAsync("/api/documents/files", form);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var document = await response.Content.ReadFromJsonAsync<DocumentResponse>();
            Assert.NotNull(document);
            Assert.Contains(item.Name.EndsWith(".pdf", StringComparison.Ordinal) ? "PDF" : "DOCX", document.DocumentType);
            Assert.Contains(item.Name.EndsWith(".pdf", StringComparison.Ordinal) ? "PDF valido" : "DOCX válido", document.ExtractedText);
            Assert.Equal(item.Bytes.LongLength, document.FileSizeBytes);
            Assert.True(document.HasOriginalFile);
            Assert.Equal(item.Bytes, await _client.GetByteArrayAsync($"/api/documents/{document.Id}/file"));
            await DeleteDocumentAsync(document.Id);
        }
    }

    [Fact]
    public async Task UploadFile_DamagedPdf_ReturnsUnprocessableEntity()
    {
        using var form = CreateUpload("damaged.pdf", "application/pdf", Encoding.UTF8.GetBytes("not a PDF"));

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ContentTypeDoesNotMatchExtension_ReturnsBadRequest()
    {
        using var form = CreateUpload("minutes.txt", "application/pdf", Encoding.UTF8.GetBytes("plain text"));

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ExceedsMaximumSize_ReturnsPayloadTooLarge()
    {
        using var form = CreateUpload("large.txt", "text/plain", new byte[10 * 1024 * 1024 + 1]);

        var response = await _client.PostAsync("/api/documents/files", form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task CreateNotes_ValidText_CanBeListedAndRead()
    {
        var title = $"Notas de prueba {Guid.NewGuid():N}";
        var request = new CreateMeetingNotesRequest(1, title, "Decisiones y siguientes pasos.");

        var response = await _client.PostAsJsonAsync("/api/documents/notes", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(document);
        Assert.Equal(title, document.Title);
        Assert.Equal(1, document.ProjectId);
        Assert.Equal("MeetingNote", document.DocumentType);
        Assert.Equal("Decisiones y siguientes pasos.", document.ExtractedText);

        var listed = await _client.GetFromJsonAsync<List<DocumentSummaryResponse>>(
            "/api/documents/projects/1");
        Assert.NotNull(listed);
        Assert.Contains(listed, item => item.Id == document.Id);
        var detail = await _client.GetFromJsonAsync<DocumentResponse>(
            $"/api/documents/{document.Id}");
        Assert.NotNull(detail);
        Assert.Equal(document.ExtractedText, detail.ExtractedText);

        await DeleteDocumentAsync(document.Id);
    }

    [Fact]
    public async Task CreateNotes_WithoutProject_RemainsUnassociated()
    {
        var request = new CreateMeetingNotesRequest(null, "Nota general", "Texto de nota general.");

        var response = await _client.PostAsJsonAsync("/api/documents/notes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(document);
        Assert.Null(document.ProjectId);
        Assert.Equal("Texto de nota general.", document.ExtractedText);
        await DeleteDocumentAsync(document.Id);
    }

    [Fact]
    public async Task Notes_CanBeEditedListedAndDeleted()
    {
        var createdResponse = await _client.PostAsJsonAsync(
            "/api/documents/notes",
            new CreateMeetingNotesRequest(1, $"Nota {Guid.NewGuid():N}", "Contenido inicial."));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(created);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/documents/{created.Id}/notes",
            new UpdateMeetingNotesRequest(null, "Nota editada", "Contenido modificado."));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(updated);
        Assert.Null(updated.ProjectId);
        Assert.Equal("Contenido modificado.", updated.ExtractedText);
        Assert.NotNull(updated.UpdatedAt);

        var listed = await _client.GetFromJsonAsync<List<DocumentSummaryResponse>>(
            "/api/documents?search=Nota%20editada&type=MeetingNote&unassignedOnly=true");
        Assert.Contains(listed!, item => item.Id == created.Id);

        var deleteResponse = await _client.DeleteAsync($"/api/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<DeleteDocumentResponse>();
        Assert.NotNull(deleteResult);
        Assert.True(deleteResult.Deleted);
        Assert.True(deleteResult.FileRemoved);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/documents/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteDocument_PreservesAnalysisTraceAndProposalHistory()
    {
        var createdResponse = await _client.PostAsJsonAsync(
            "/api/documents/notes",
            new CreateMeetingNotesRequest(1, $"Nota con análisis {Guid.NewGuid():N}", "Texto con propuestas."));
        var created = await createdResponse.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(created);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AIExtractions.Add(new ProjectManagement.Api.Models.AIExtraction
            {
                ProjectId = created.ProjectId,
                SourceType = "Meeting",
                SourceReference = created.Id.ToString(),
                ExtractedData = "{}",
                Status = "PendingReview"
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retainedAnalysis = await verifyDb.AIExtractions.SingleAsync(item => item.SourceReference == created.Id.ToString());
        var deletedDocument = await verifyDb.ProjectDocuments.SingleAsync(item => item.Id == created.Id);
        Assert.Equal(created.Id.ToString(), retainedAnalysis.SourceReference);
        Assert.True(deletedDocument.IsDeleted);
        Assert.Null(deletedDocument.ExtractedText);
    }

    [Fact]
    public async Task CreateNotes_EmptyText_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/documents/notes",
            new CreateMeetingNotesRequest(1, "Vacío", "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNotes_UnknownProject_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/documents/notes",
            new CreateMeetingNotesRequest(999, "Nota", "Contenido."));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectDocuments_UnknownProject_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/documents/projects/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static MultipartFormDataContent CreateUpload(string fileName, string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "projectId");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private async Task DeleteDocumentAsync(int id)
    {
        await _client.DeleteAsync($"/api/documents/{id}");
    }

    private static byte[] CreateDocxBytes(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            part.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] CreatePdfBytes(string text)
    {
        var streamText = $"BT /F1 12 Tf 50 700 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(streamText)} >>\nstream\n{streamText}\nendstream"
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
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
