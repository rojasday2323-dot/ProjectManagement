using Microsoft.AspNetCore.Http.Features;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.Documents;

namespace ProjectManagement.Api.Endpoints;

public static class DocumentEndpoints
{
    private const long MaxRequestSizeBytes = DocumentService.MaxFileSizeBytes + 1024 * 1024;

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapGet("/", GetAll)
            .WithSummary("List documents and notes")
            .WithDescription("Lists active document and note records, including unassociated records, with optional search and filters.")
            .Produces<IReadOnlyList<DocumentSummaryResponse>>(StatusCodes.Status200OK);

        group.MapPost("/files", UploadFile)
            .WithSummary("Upload and prepare a document")
            .WithDescription("Accepts PDF, DOCX, and TXT files up to 10 MB, extracts their text, and stores the prepared document.")
            .Produces<DocumentResponse>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/notes", CreateNotes)
            .WithSummary("Save meeting notes")
            .WithDescription("Stores user-provided meeting notes as prepared text without AI analysis.")
            .Produces<DocumentResponse>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:int}", GetProjectDocuments)
            .WithSummary("List project documents and notes")
            .Produces<IReadOnlyList<DocumentSummaryResponse>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:int}/notes", UpdateNotes)
            .WithSummary("Update saved meeting notes")
            .Produces<DocumentResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/file", DownloadFile)
            .WithSummary("Download the original uploaded file")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status410Gone);

        group.MapGet("/{id:int}", GetById)
            .WithSummary("Get a document and its extracted text")
            .Produces<DocumentResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", Delete)
            .WithSummary("Delete a document or note while retaining analysis history")
            .Produces<DeleteDocumentResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAll(
        string? search,
        int? projectId,
        string? type,
        bool? unassignedOnly,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        if (projectId is <= 0)
            return Results.BadRequest(ApiErrorResponse.Error("El identificador del proyecto debe ser mayor que cero."));

        return Results.Ok(await documentService.GetAllAsync(
            search, projectId, type, unassignedOnly == true, cancellationToken));
    }

    private static async Task<IResult> UploadFile(
        HttpRequest request,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var requestSizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
            requestSizeFeature.MaxRequestBodySize = MaxRequestSizeBytes;

        if (request.ContentLength > MaxRequestSizeBytes)
            return Results.Json(ApiErrorResponse.Error("El tamaño máximo permitido es 10 MB."), statusCode: StatusCodes.Status413PayloadTooLarge);

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(ApiErrorResponse.Error("El formulario de carga no es válido o supera el límite permitido."));
        }

        var files = form.Files;
        if (files.Count != 1)
            return Results.BadRequest(ApiErrorResponse.Error("Debes enviar exactamente un archivo."));

        int? projectId = null;
        if (!string.IsNullOrWhiteSpace(form["projectId"]))
        {
            if (!int.TryParse(form["projectId"], out var parsedProjectId) || parsedProjectId <= 0)
                return Results.BadRequest(ApiErrorResponse.Error("El identificador del proyecto no es válido."));
            projectId = parsedProjectId;
        }

        try
        {
            var document = await documentService.UploadFileAsync(projectId, files[0], cancellationToken);
            return Results.Created($"/api/documents/{document.Id}", document);
        }
        catch (DocumentExtractionException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (DocumentUploadValidationException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> CreateNotes(
        CreateMeetingNotesRequest request,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.CreateNotesAsync(request, cancellationToken);
            return Results.Created($"/api/documents/{document.Id}", document);
        }
        catch (DocumentUploadValidationException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> UpdateNotes(
        int id,
        UpdateMeetingNotesRequest request,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.UpdateNotesAsync(id, request, cancellationToken);
            return document is null
                ? Results.NotFound(ApiErrorResponse.Error($"Nota con ID {id} no encontrada."))
                : Results.Ok(document);
        }
        catch (DocumentUploadValidationException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> GetProjectDocuments(
        int projectId,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var documents = await documentService.GetProjectDocumentsAsync(projectId, cancellationToken);
        return documents is null
            ? Results.NotFound(ApiErrorResponse.Error($"Proyecto con ID {projectId} no encontrado."))
            : Results.Ok(documents);
    }

    private static async Task<IResult> GetById(
        int id,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var document = await documentService.GetByIdAsync(id, cancellationToken);
        return document is null
            ? Results.NotFound(ApiErrorResponse.Error($"Documento con ID {id} no encontrado."))
            : Results.Ok(document);
    }

    private static async Task<IResult> DownloadFile(
        int id,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await documentService.DownloadAsync(id, cancellationToken);
            return file is null
                ? Results.NotFound(ApiErrorResponse.Error($"Documento con ID {id} no encontrado."))
                : Results.File(file.Content, file.ContentType, file.DownloadName, enableRangeProcessing: true);
        }
        catch (DocumentUploadValidationException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> Delete(
        int id,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var result = await documentService.DeleteAsync(id, cancellationToken);
        return result is null
            ? Results.NotFound(ApiErrorResponse.Error($"Documento o nota con ID {id} no encontrado."))
            : Results.Ok(result);
    }
}
