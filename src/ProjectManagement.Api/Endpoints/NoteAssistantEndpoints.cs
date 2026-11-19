using Microsoft.AspNetCore.Http.Features;
using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.AI;
using ProjectManagement.Api.Services.Documents;

namespace ProjectManagement.Api.Endpoints;

public static class NoteAssistantEndpoints
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private const long MaxAttachmentSizeBytes = DocumentService.MaxFileSizeBytes;
    private const long MaxRequestSizeBytes = MaxAttachmentSizeBytes + 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> ImageTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        };

    public static void MapNoteAssistantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes assistant");

        group.MapPost("/chat", AskAboutNotes)
            .WithSummary("Ask a question about project meeting notes")
            .WithDescription("Answers using only the current draft and saved meeting notes associated with the selected project.")
            .Produces<NoteAssistantAnswerResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ApiErrorResponse>(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/analyze", AnalyzeDraft)
            .WithSummary("Identify evidence-backed fields in an unsaved meeting note")
            .WithDescription("Analyzes draft text using Azure AI Foundry without persisting it or creating project records.")
            .Produces<AiAnalysisContent>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ApiErrorResponse>(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/attachments/extract", ExtractAttachment)
            .WithSummary("Extract text from a transient note attachment")
            .WithDescription("Extracts text from PDF, DOCX, TXT, or text visible in a supported image; does not store the original file.")
            .Produces<NoteAttachmentTextResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ApiErrorResponse>(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> AnalyzeDraft(
        NoteDraftAnalysisRequest request,
        INoteAssistantService assistant,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await assistant.AnalyzeDraftAsync(request, cancellationToken));
        }
        catch (NoteAssistantValidationException exception)
        {
            return Results.BadRequest(ApiErrorResponse.Error(exception.Message));
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> AskAboutNotes(
        NoteAssistantQuestionRequest request,
        INoteAssistantService assistant,
        CancellationToken cancellationToken)
    {
        try
        {
            var answer = await assistant.AskAsync(request, cancellationToken);
            return answer is null
                ? Results.NotFound(ApiErrorResponse.Error("El proyecto seleccionado no existe."))
                : Results.Ok(new NoteAssistantAnswerResponse(answer));
        }
        catch (NoteAssistantValidationException exception)
        {
            return Results.BadRequest(ApiErrorResponse.Error(exception.Message));
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> ExtractAttachment(
        HttpRequest request,
        IDocumentTextExtractor textExtractor,
        IAiAnalysisProvider provider,
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
            return Results.BadRequest(ApiErrorResponse.Error("El archivo está vacío o el formulario supera el límite permitido."));
        }

        if (form.Files.Count != 1)
            return Results.BadRequest(ApiErrorResponse.Error("Adjunta exactamente un archivo a la vez."));

        var file = form.Files[0];
        if (file.Length == 0)
            return Results.BadRequest(ApiErrorResponse.Error("El archivo está vacío."));

        var fileName = Path.GetFileName(file.FileName);
        if (fileName.Length > 300)
            return Results.BadRequest(ApiErrorResponse.Error("El nombre del archivo no puede superar los 300 caracteres."));

        var extension = Path.GetExtension(fileName);
        try
        {
            if (ImageTypes.TryGetValue(extension, out var mediaType))
            {
                if (file.Length > MaxImageSizeBytes)
                    return Results.Json(ApiErrorResponse.Error("El tamaño máximo permitido para una imagen es 5 MB."), statusCode: StatusCodes.Status413PayloadTooLarge);
                if (!string.Equals(file.ContentType, mediaType, StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(ApiErrorResponse.Error("El tipo de contenido de la imagen no coincide con su extensión."));

                await using var imageStream = new MemoryStream(checked((int)file.Length));
                await file.CopyToAsync(imageStream, cancellationToken);
                var bytes = imageStream.ToArray();
                if (!HasValidImageSignature(bytes, mediaType))
                    return Results.BadRequest(ApiErrorResponse.Error("El contenido no coincide con un formato de imagen admitido."));

                var extracted = await provider.ExtractImageTextAsync(
                    mediaType,
                    Convert.ToBase64String(bytes),
                    cancellationToken);
                return string.IsNullOrWhiteSpace(extracted)
                    ? Results.UnprocessableEntity(ApiErrorResponse.Error("La imagen no contiene texto legible."))
                    : Results.Ok(new NoteAttachmentTextResponse(fileName, extracted.Trim()));
            }

            var expectedContentType = extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => null
            };
            if (expectedContentType is null)
                return Results.BadRequest(ApiErrorResponse.Error("Formato no permitido. Utiliza PDF, DOCX, TXT o una imagen PNG, JPG, GIF o WEBP."));
            if (file.Length > MaxAttachmentSizeBytes)
                return Results.Json(ApiErrorResponse.Error("El tamaño máximo permitido es 10 MB."), statusCode: StatusCodes.Status413PayloadTooLarge);
            if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(ApiErrorResponse.Error("El tipo de contenido no coincide con la extensión del archivo."));

            await using var documentStream = new MemoryStream(checked((int)file.Length));
            await file.CopyToAsync(documentStream, cancellationToken);
            documentStream.Position = 0;
            var text = textExtractor.Extract(extension, documentStream);
            return Results.Ok(new NoteAttachmentTextResponse(fileName, text));
        }
        catch (DocumentExtractionException exception)
        {
            return Results.UnprocessableEntity(ApiErrorResponse.Error(exception.Message));
        }
        catch (DocumentUploadValidationException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static bool HasValidImageSignature(byte[] bytes, string mediaType) => mediaType switch
    {
        "image/png" => bytes.Length >= 8
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
        "image/gif" => bytes.Length >= 6
            && (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a"),
        "image/webp" => bytes.Length >= 12
            && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
            && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
        _ => false
    };
}
