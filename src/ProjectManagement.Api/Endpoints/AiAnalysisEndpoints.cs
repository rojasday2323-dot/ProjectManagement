using ProjectManagement.Api.DTOs;
using ProjectManagement.Api.Services.AI;

namespace ProjectManagement.Api.Endpoints;

public static class AiAnalysisEndpoints
{
    public static void MapAiAnalysisEndpoints(this WebApplication app)
    {
        var documents = app.MapGroup("/api/documents").WithTags("AI Analysis");

        app.MapGet("/api/ai/status", (IAiAnalysisService analysisService) =>
                Results.Ok(analysisService.GetConfigurationStatus()))
            .WithTags("AI Analysis")
            .WithSummary("Check Azure AI Foundry configuration without exposing credentials")
            .Produces<AiAnalysisConfigurationResponse>(StatusCodes.Status200OK);

        documents.MapPost("/{documentId:int}/analyze", AnalyzeDocument)
            .WithSummary("Analyze a document with Azure AI Foundry")
            .WithDescription("Extracts reviewable project proposals from the prepared text. The document is treated as untrusted input; records are created only when each supported proposal is explicitly accepted.")
            .Produces<AiAnalysisResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ApiErrorResponse>(StatusCodes.Status504GatewayTimeout);

        documents.MapGet("/{documentId:int}/analyses", GetDocumentAnalyses)
            .WithSummary("List analysis attempts and results for a document")
            .Produces<IReadOnlyList<AiAnalysisResponse>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/api/analyses/{analysisId:int}/proposals/{proposalId}", ReviewProposal)
            .WithTags("AI Analysis")
            .WithSummary("Accept or reject an analysis proposal")
            .WithDescription("Saves optional user corrections and, on explicit acceptance, registers supported proposals in the source project. Repeating a registered acceptance is idempotent.")
            .Produces<AiAnalysisResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapPost("/api/analyses/{analysisId:int}/summary-note", SaveSummaryAsNote)
            .WithTags("AI Analysis")
            .WithSummary("Save the user-reviewed AI summary as a project note")
            .WithDescription("Persists the edited summary as a MeetingNote linked to its source document and analysis. Repeated requests return the existing note and never create project commitments.")
            .Produces<AiAnalysisResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> AnalyzeDocument(
        int documentId,
        IAiAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await analysisService.AnalyzeDocumentAsync(documentId, cancellationToken));
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }

    private static async Task<IResult> GetDocumentAnalyses(
        int documentId,
        IAiAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        var analyses = await analysisService.GetDocumentAnalysesAsync(documentId, cancellationToken);
        return analyses is null
            ? Results.NotFound(ApiErrorResponse.Error($"Documento con ID {documentId} no encontrado."))
            : Results.Ok(analyses);
    }

    private static async Task<IResult> ReviewProposal(
        int analysisId,
        string proposalId,
        ReviewAiProposalRequest request,
        IAiAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await analysisService.ReviewProposalAsync(
                analysisId, proposalId, request, cancellationToken);
            return result is null
                ? Results.NotFound(ApiErrorResponse.Error("No se encontró la propuesta solicitada."))
                : Results.Ok(result);
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
        catch (AiAnalysisValidationException exception)
        {
            return Results.BadRequest(ApiErrorResponse.Error(exception.Message));
        }
    }

    private static async Task<IResult> SaveSummaryAsNote(
        int analysisId,
        SaveAiSummaryNoteRequest request,
        IAiAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await analysisService.SaveSummaryAsNoteAsync(analysisId, request, cancellationToken);
            return result is null
                ? Results.NotFound(ApiErrorResponse.Error("No se encontró el análisis solicitado."))
                : Results.Ok(result);
        }
        catch (AiAnalysisProviderException exception)
        {
            return Results.Json(ApiErrorResponse.Error(exception.Message), statusCode: exception.StatusCode);
        }
    }
}
