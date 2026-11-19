using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Models;
using System.Text;

namespace ProjectManagement.Api.Services.Documents;

public sealed class DocumentService(
    AppDbContext db,
    IDocumentTextExtractor textExtractor,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DocumentService> logger) : IDocumentService
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const string DefaultStorageDirectory = "App_Data/Documents";

    private static readonly IReadOnlyDictionary<string, (string Type, string[] ContentTypes)> FileTypes =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("PDF", ["application/pdf", "application/octet-stream"]),
            [".docx"] = ("DOCX", [
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/octet-stream"
            ]),
            [".txt"] = ("TXT", ["text/plain", "application/octet-stream"])
        };

    public async Task<DocumentResponse> UploadFileAsync(
        int? projectId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new DocumentUploadValidationException("El archivo está vacío.");
        if (file.Length > MaxFileSizeBytes)
            throw new DocumentUploadValidationException("El tamaño máximo permitido es 10 MB.", StatusCodes.Status413PayloadTooLarge);

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        if (originalFileName.Length > 300)
            throw new DocumentUploadValidationException("El nombre del archivo no puede superar los 300 caracteres.");
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(originalFileName)))
            throw new DocumentUploadValidationException("El archivo debe tener un nombre válido.");
        if (!FileTypes.TryGetValue(extension, out var fileType))
            throw new DocumentUploadValidationException("Formato no permitido. Utiliza archivos PDF, DOCX o TXT.");
        if (!fileType.ContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new DocumentUploadValidationException("El tipo de contenido no coincide con la extensión del archivo.");

        var projectName = await GetProjectNameAsync(projectId, cancellationToken);
        await using var input = new MemoryStream(checked((int)file.Length));
        await file.CopyToAsync(input, cancellationToken);
        input.Position = 0;
        var extractedText = textExtractor.Extract(extension, input);

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var absoluteFilePath = Path.Combine(GetStorageDirectory(), storedFileName);
        Directory.CreateDirectory(GetStorageDirectory());

        var document = new ProjectDocument
        {
            ProjectId = projectId,
            Title = Path.GetFileNameWithoutExtension(originalFileName),
            DocumentType = fileType.Type,
            OriginalFileName = originalFileName,
            FileSizeBytes = file.Length,
            ProcessingStatus = "Prepared",
            ExtractedText = extractedText,
            FilePath = storedFileName,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await using (var output = new FileStream(
                absoluteFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                input.Position = 0;
                await input.CopyToAsync(output, cancellationToken);
            }

            db.ProjectDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (File.Exists(absoluteFilePath))
                File.Delete(absoluteFilePath);
            throw;
        }

        logger.LogInformation("Prepared uploaded document {DocumentId} ({DocumentType})", document.Id, document.DocumentType);
        return ToResponse(document, projectName);
    }

    public async Task<DocumentResponse> CreateNotesAsync(
        CreateMeetingNotesRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new DocumentUploadValidationException("El texto de las notas es obligatorio.");
        ValidateProjectId(request.ProjectId);

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DocumentUploadValidationException("El título de la nota es obligatorio.");
        var title = request.Title.Trim();
        ValidateNote(title, request.Text);

        var projectName = await GetProjectNameAsync(request.ProjectId, cancellationToken);
        var document = new ProjectDocument
        {
            ProjectId = request.ProjectId,
            Title = title,
            DocumentType = "MeetingNote",
            ProcessingStatus = "Prepared",
            ExtractedText = request.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.ProjectDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Prepared meeting notes {DocumentId}", document.Id);
        return ToResponse(document, projectName);
    }

    public async Task<DocumentResponse?> UpdateNotesAsync(
        int id,
        UpdateMeetingNotesRequest request,
        CancellationToken cancellationToken)
    {
        var document = await db.ProjectDocuments
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null)
            return null;
        if (document.DocumentType != "MeetingNote")
            throw new DocumentUploadValidationException("Solo se pueden editar registros de notas.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DocumentUploadValidationException("El título de la nota es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new DocumentUploadValidationException("El texto de las notas es obligatorio.");

        var title = request.Title.Trim();
        ValidateProjectId(request.ProjectId);
        ValidateNote(title, request.Text);

        var projectName = await GetProjectNameAsync(request.ProjectId, cancellationToken);
        document.ProjectId = request.ProjectId;
        document.Project = request.ProjectId is null ? null : document.Project;
        document.Title = title;
        document.ExtractedText = request.Text.Trim();
        document.UpdatedAt = DateTime.UtcNow;
        document.ProcessingStatus = "Prepared";
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(document, projectName);
    }

    public async Task<IReadOnlyList<DocumentSummaryResponse>> GetAllAsync(
        string? search,
        int? projectId,
        string? type,
        bool unassignedOnly,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectDocuments
            .AsNoTracking()
            .Where(document => !document.IsDeleted)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(document => document.ProjectId == projectId.Value);
        if (unassignedOnly)
            query = query.Where(document => document.ProjectId == null);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(document => document.DocumentType == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(document =>
                document.Title.Contains(term)
                || (document.OriginalFileName != null && document.OriginalFileName.Contains(term)));
        }

        return await query
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new DocumentSummaryResponse(
                document.Id,
                document.ProjectId,
                document.Project == null ? null : document.Project.Name,
                document.Title,
                document.DocumentType,
                document.OriginalFileName,
                document.FileSizeBytes,
                document.FilePath != null,
                document.ProcessingStatus,
                document.CreatedAt,
                document.UpdatedAt,
                document.SourceDocumentId,
                document.SourceAnalysisId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSummaryResponse>?> GetProjectDocumentsAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(project => project.Id == projectId, cancellationToken))
            return null;

        return await GetAllAsync(null, projectId, null, false, cancellationToken);
    }

    public async Task<DocumentResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var document = await db.ProjectDocuments
            .AsNoTracking()
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return document is null ? null : ToResponse(document, document.Project?.Name);
    }

    public async Task<DocumentFile?> DownloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await db.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (document is null)
            return null;
        if (string.IsNullOrWhiteSpace(document.FilePath) || string.IsNullOrWhiteSpace(document.OriginalFileName))
            throw new DocumentUploadValidationException("Este registro no tiene un archivo original para descargar.", StatusCodes.Status409Conflict);

        var absolutePath = ResolveStoredFilePath(document.FilePath);
        if (!File.Exists(absolutePath))
            throw new DocumentUploadValidationException("El archivo original ya no está disponible en el almacenamiento.", StatusCodes.Status410Gone);

        var contentType = document.DocumentType switch
        {
            "PDF" => "application/pdf",
            "DOCX" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "text/plain"
        };
        return new DocumentFile(File.OpenRead(absolutePath), contentType, document.OriginalFileName);
    }

    public async Task<DeleteDocumentResponse?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var document = await db.ProjectDocuments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return null;

        document.IsDeleted = true;
        document.ProcessingStatus = "Deleted";
        document.ExtractedText = null;
        document.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(document.FilePath))
            return new DeleteDocumentResponse(true, true, null);

        var path = ResolveStoredFilePath(document.FilePath);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            document.FilePath = null;
            await db.SaveChangesAsync(cancellationToken);
            return new DeleteDocumentResponse(true, true, null);
        }
        catch (IOException)
        {
            logger.LogError("Could not remove stored file for deleted document {DocumentId}", id);
            return new DeleteDocumentResponse(
                true,
                false,
                "El registro quedó eliminado, pero no se pudo liberar el archivo. Reintenta la eliminación más tarde.");
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogError("Storage permissions prevented removal for deleted document {DocumentId}", id);
            return new DeleteDocumentResponse(
                true,
                false,
                "El registro quedó eliminado, pero el servidor no tiene permiso para borrar el archivo.");
        }
    }

    private string GetStorageDirectory()
    {
        var configuredDirectory = configuration["DocumentStorage:Directory"] ?? DefaultStorageDirectory;
        return Path.GetFullPath(Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(environment.ContentRootPath, configuredDirectory));
    }

    private string ResolveStoredFilePath(string storedPath)
    {
        var storageRoot = GetStorageDirectory();
        string candidate;
        if (Path.IsPathRooted(storedPath))
        {
            candidate = Path.GetFullPath(storedPath);
        }
        else if (storedPath.Contains(Path.DirectorySeparatorChar)
            || storedPath.Contains(Path.AltDirectorySeparatorChar))
        {
            candidate = Path.GetFullPath(Path.Combine(environment.ContentRootPath, storedPath));
        }
        else
        {
            candidate = Path.GetFullPath(Path.Combine(storageRoot, storedPath));
        }

        var rootWithSeparator = storageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La ruta almacenada del documento está fuera del directorio configurado.");

        return candidate;
    }

    private static void ValidateProjectId(int? projectId)
    {
        if (projectId is <= 0)
            throw new DocumentUploadValidationException("El identificador del proyecto debe ser mayor que cero.");
    }

    private static void ValidateNote(string title, string text)
    {
        if (title.Length > 300)
            throw new DocumentUploadValidationException("El título no puede superar los 300 caracteres.");
        if (Encoding.UTF8.GetByteCount(text) > MaxFileSizeBytes)
            throw new DocumentUploadValidationException("El texto supera el tamaño máximo permitido de 10 MB.", StatusCodes.Status413PayloadTooLarge);
    }

    private async Task<string?> GetProjectNameAsync(int? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
            return null;

        var projectName = await db.Projects
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (projectName is null)
            throw new DocumentUploadValidationException($"Proyecto con ID {projectId} no encontrado.", StatusCodes.Status404NotFound);
        return projectName;
    }

    private static DocumentResponse ToResponse(ProjectDocument document, string? projectName) =>
        new(
            document.Id,
            document.ProjectId,
            projectName,
            document.Title,
            document.DocumentType,
            document.OriginalFileName,
            document.FileSizeBytes,
            document.FilePath is not null,
            document.ProcessingStatus,
            document.ExtractedText ?? string.Empty,
            document.CreatedAt,
            document.UpdatedAt,
            document.SourceDocumentId,
            document.SourceAnalysisId);
}
