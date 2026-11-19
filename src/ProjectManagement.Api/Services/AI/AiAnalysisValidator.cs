using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AI;

public sealed class AiAnalysisValidationException(string message) : Exception(message);

public sealed class AiAnalysisValidator
{
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "Commitment", "Activity", "Milestone", "Deliverable", "Dependency", "Risk"
    };

    public AiAnalysisContent Validate(
        AiAnalysisContent content,
        string? expectedProjectName,
        IReadOnlyCollection<string> knownDeveloperNames,
        IReadOnlyCollection<string> knownProductOwnerNames,
        string sourceText)
    {
        if (content.Proposals is null || content.UncertainInformation is null)
            throw new AiAnalysisValidationException("La respuesta del modelo no contiene todas las secciones requeridas.");
        if (content.MeetingSummary is null)
            throw new AiAnalysisValidationException("La respuesta del modelo no contiene el resumen inteligente requerido.");
        if (content.Proposals.Count > 100 || content.UncertainInformation.Count > 100)
            throw new AiAnalysisValidationException("La respuesta del modelo contiene demasiados elementos.");
        if ((content.ProjectName?.Length ?? 0) > 300 || (content.Summary?.Length ?? 0) > 4000)
            throw new AiAnalysisValidationException("El proyecto o resumen de la respuesta supera la longitud permitida.");

        var knownDevelopers = new HashSet<string>(knownDeveloperNames, StringComparer.OrdinalIgnoreCase);
        var knownProductOwners = new HashSet<string>(knownProductOwnerNames, StringComparer.OrdinalIgnoreCase);
        var knownPeople = new HashSet<string>(knownDeveloperNames.Concat(knownProductOwnerNames), StringComparer.OrdinalIgnoreCase);
        var meetingSummary = ValidateMeetingSummary(content.MeetingSummary, sourceText, knownPeople);
        var proposals = new List<AiAnalysisProposal>(content.Proposals.Count);
        foreach (var proposal in content.Proposals)
        {
            if (!Categories.Contains(proposal.Category)
                || string.IsNullOrWhiteSpace(proposal.Title)
                || proposal.Title.Length > 300
                || (proposal.Description?.Length ?? 0) > 4000
                || (proposal.Evidence?.Length ?? 0) > 1000
                || string.IsNullOrWhiteSpace(proposal.Evidence)
                || !double.IsFinite(proposal.Confidence)
                || proposal.Confidence is < 0 or > 1)
                throw new AiAnalysisValidationException("Una propuesta contiene campos obligatorios o valores no válidos.");

            if (proposal.StartDate is not null
                && proposal.EndDate is not null
                && proposal.EndDate < proposal.StartDate)
                throw new AiAnalysisValidationException("Una propuesta tiene fecha final anterior a su fecha inicial.");

            ValidateList(proposal.Dependencies);
            ValidateList(proposal.Risks);
            var knownDeveloper = IsKnown(proposal.DeveloperName, knownDevelopers);
            var knownProductOwner = IsKnown(proposal.ProductOwnerName, knownProductOwners);
            var evidenceVerified = ContainsEvidence(sourceText, proposal.Evidence);
            var projectMismatch = !string.IsNullOrWhiteSpace(content.ProjectName)
                && !string.IsNullOrWhiteSpace(expectedProjectName)
                && !string.Equals(content.ProjectName.Trim(), expectedProjectName.Trim(), StringComparison.OrdinalIgnoreCase);

            proposals.Add(proposal with
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = proposal.Title.Trim(),
                Description = Normalize(proposal.Description),
                DeveloperName = Normalize(proposal.DeveloperName),
                ProductOwnerName = Normalize(proposal.ProductOwnerName),
                Dependencies = proposal.Dependencies.Select(value => value.Trim()).ToArray(),
                Risks = proposal.Risks.Select(value => value.Trim()).ToArray(),
                Evidence = proposal.Evidence.Trim(),
                ConfirmationRequired = proposal.ConfirmationRequired
                    || proposal.Confidence < 0.75
                    || (proposal.DeveloperName is not null && !knownDeveloper)
                    || (proposal.ProductOwnerName is not null && !knownProductOwner)
                    || !evidenceVerified
                    || projectMismatch,
                Decision = "Pending"
            });
        }

        if (content.UncertainInformation.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000))
            throw new AiAnalysisValidationException("La respuesta del modelo contiene información de incertidumbre no válida.");

        var uncertainties = content.UncertainInformation.Select(value => value.Trim()).ToList();
        var contentProjectMismatch = !string.IsNullOrWhiteSpace(content.ProjectName)
            && !string.IsNullOrWhiteSpace(expectedProjectName)
            && !string.Equals(content.ProjectName.Trim(), expectedProjectName.Trim(), StringComparison.OrdinalIgnoreCase);
        if (contentProjectMismatch)
            uncertainties.Add("El nombre de proyecto detectado no coincide con el proyecto seleccionado.");

        return content with
        {
            ProjectName = Normalize(content.ProjectName),
            Summary = Normalize(content.Summary),
            Proposals = proposals,
            UncertainInformation = uncertainties,
            MeetingSummary = meetingSummary
        };
    }

    private static AiMeetingSummary ValidateMeetingSummary(
        AiMeetingSummary summary,
        string sourceText,
        HashSet<string> knownPeople)
    {
        if (string.IsNullOrWhiteSpace(summary.Title)
            || summary.Title.Length > 300
            || string.IsNullOrWhiteSpace(summary.GeneralSummary)
            || summary.GeneralSummary.Length > 4000)
            throw new AiAnalysisValidationException("El título o resumen general no es válido.");

        var topics = ValidateFacts(summary.Topics, sourceText);
        var agreements = ValidateFacts(summary.Agreements, sourceText);
        var decisions = ValidateFacts(summary.Decisions, sourceText);
        var pendingItems = ValidateFacts(summary.PendingItems, sourceText);
        var commitments = ValidateFacts(summary.Commitments, sourceText);
        var deliverables = ValidateFacts(summary.Deliverables, sourceText);
        var risksAndObservations = ValidateFacts(summary.RisksAndObservations, sourceText);
        var nextSteps = ValidateFacts(summary.NextSteps, sourceText);

        if (summary.ResponsiblePeople is null || summary.ResponsiblePeople.Count > 50
            || summary.ImportantDates is null || summary.ImportantDates.Count > 50)
            throw new AiAnalysisValidationException("El resumen contiene demasiadas personas o fechas.");

        var responsiblePeople = summary.ResponsiblePeople.Select(person =>
        {
            if (string.IsNullOrWhiteSpace(person.Responsibility)
                || person.Responsibility.Length > 1000
                || !HasEvidence(sourceText, person.Evidence)
                || (person.Name?.Length ?? 0) > 200)
                throw new AiAnalysisValidationException("Una persona responsable no tiene datos o evidencia válidos.");

            var name = Normalize(person.Name);
            return person with
            {
                Name = name,
                Responsibility = person.Responsibility.Trim(),
                Evidence = person.Evidence.Trim(),
                NeedsConfirmation = person.NeedsConfirmation
                    || name is null
                    || !knownPeople.Contains(name)
            };
        }).ToArray();

        var importantDates = summary.ImportantDates.Select(item =>
        {
            if (string.IsNullOrWhiteSpace(item.Description)
                || item.Description.Length > 1000
                || (item.ResponsiblePerson?.Length ?? 0) > 200
                || !HasEvidence(sourceText, item.Evidence))
                throw new AiAnalysisValidationException("Una fecha importante no tiene datos o evidencia válidos.");

            var responsiblePerson = Normalize(item.ResponsiblePerson);
            return item with
            {
                Description = item.Description.Trim(),
                ResponsiblePerson = responsiblePerson,
                Evidence = item.Evidence.Trim(),
                NeedsConfirmation = item.NeedsConfirmation
                    || item.Date is null
                    || (responsiblePerson is not null && !knownPeople.Contains(responsiblePerson))
            };
        }).ToArray();

        return summary with
        {
            Title = summary.Title.Trim(),
            GeneralSummary = summary.GeneralSummary.Trim(),
            Topics = topics,
            Agreements = agreements,
            Decisions = decisions,
            PendingItems = pendingItems,
            Commitments = commitments,
            Deliverables = deliverables,
            ResponsiblePeople = responsiblePeople,
            ImportantDates = importantDates,
            RisksAndObservations = risksAndObservations,
            NextSteps = nextSteps
        };
    }

    private static IReadOnlyList<AiSummaryFact> ValidateFacts(
        IReadOnlyList<AiSummaryFact>? facts,
        string sourceText)
    {
        if (facts is null || facts.Count > 50)
            throw new AiAnalysisValidationException("Una sección del resumen contiene demasiados elementos o no es válida.");

        return facts.Select(fact =>
        {
            if (string.IsNullOrWhiteSpace(fact.Text)
                || fact.Text.Length > 1000
                || !HasEvidence(sourceText, fact.Evidence))
                throw new AiAnalysisValidationException("Una afirmación del resumen no está respaldada por el documento.");
            return fact with { Text = fact.Text.Trim(), Evidence = fact.Evidence.Trim() };
        }).ToArray();
    }

    public static void ValidateCorrectedProposal(AiAnalysisProposal proposal)
    {
        if (!Categories.Contains(proposal.Category)
            || string.IsNullOrWhiteSpace(proposal.Title)
            || proposal.Title.Length > 300
            || (proposal.Description?.Length ?? 0) > 4000
            || string.IsNullOrWhiteSpace(proposal.Evidence)
            || proposal.Evidence.Length > 1000
            || !double.IsFinite(proposal.Confidence)
            || proposal.Confidence is < 0 or > 1
            || (proposal.StartDate is not null && proposal.EndDate is not null && proposal.EndDate < proposal.StartDate))
            throw new AiAnalysisValidationException("Los cambios de la propuesta contienen valores o fechas no válidos.");
        ValidateList(proposal.Dependencies);
        ValidateList(proposal.Risks);
    }

    private static bool IsKnown(string? name, HashSet<string> knownNames) =>
        string.IsNullOrWhiteSpace(name) || knownNames.Contains(name.Trim());

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ContainsEvidence(string sourceText, string evidence)
    {
        static string NormalizeWhitespace(string value) =>
            string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return NormalizeWhitespace(sourceText).Contains(
            NormalizeWhitespace(evidence),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEvidence(string sourceText, string? evidence) =>
        !string.IsNullOrWhiteSpace(evidence) && ContainsEvidence(sourceText, evidence);

    private static void ValidateList(IReadOnlyList<string> values)
    {
        if (values is null || values.Count > 50 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000))
            throw new AiAnalysisValidationException("Una lista de dependencias o riesgos contiene valores no válidos.");
    }
}
