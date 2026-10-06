using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// Acompanhamento da fase (docs/30 §9-§11, §13, Fase 3 — ADR-033):
// progresso, check-ins, impedimentos e feedbacks do plano em um único
// GET (o diálogo abre por fase e filtra pelo PhaseId no cliente).
// Leitura bilateral: Admin, dono ou gestor (o gestor precisa ver os
// relatos para responder); 404 sem plano, 403 para estranhos.

public sealed record GetPdiAccompanimentQuery(Guid UserId)
    : IQuery<PdiAccompanimentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GetPdiAccompanimentQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetPdiAccompanimentQuery, PdiAccompanimentDto>
{
    public async Task<PdiAccompanimentDto> Handle(
        GetPdiAccompanimentQuery request, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");
        PdiAccess.EnsureCanView(currentUser, plan);

        var rows = await repository.GetTreeAsync(request.UserId, cancellationToken);
        var phaseIds = rows.Phases.Select(phase => phase.Id).ToList();

        var progresses = await repository.ListProgressesAsync(phaseIds, cancellationToken);
        var checkIns = await repository.ListCheckInsAsync(phaseIds, cancellationToken);
        var impediments = await repository.ListImpedimentsAsync(phaseIds, cancellationToken);
        var feedbacks = await repository.ListFeedbacksAsync(phaseIds, cancellationToken);

        // Nomes resolvidos uma única vez (mesma fonte do GET structure).
        var names = new Dictionary<Guid, string?>();
        async Task<string?> NameOf(Guid? id)
        {
            if (id is not { } key)
                return null;
            if (!names.TryGetValue(key, out var name))
                names[key] = name = await plans.GetUserNameAsync(key, cancellationToken);
            return name;
        }

        var progressDtos = new List<PdiProgressDto>(progresses.Count);
        foreach (var progress in progresses.OrderBy(item => item.CreateDateTime))
            progressDtos.Add(new PdiProgressDto(
                progress.Id, progress.PhaseId, progress.AuthorId,
                await NameOf(progress.AuthorId), progress.Percent, progress.Comment,
                progress.CreateDateTime));

        var checkInDtos = new List<PdiCheckInDto>(checkIns.Count);
        foreach (var checkIn in checkIns
                     .OrderBy(item => item.CheckInDate)
                     .ThenBy(item => item.CreateDateTime))
            checkInDtos.Add(new PdiCheckInDto(
                checkIn.Id, checkIn.PhaseId, checkIn.CheckInDate, checkIn.CreatedBy,
                await NameOf(checkIn.CreatedBy), checkIn.WhatDone, checkIn.WhatLearned,
                checkIn.Difficulty, checkIn.NeedSupport, checkIn.ManagerFeedback,
                checkIn.NextAction, checkIn.RespondedAt,
                await NameOf(checkIn.RespondedById)));

        var impedimentDtos = new List<PdiImpedimentDto>(impediments.Count);
        foreach (var impediment in impediments.OrderBy(item => item.OccurredAt)
                     .ThenBy(item => item.CreateDateTime))
            impedimentDtos.Add(new PdiImpedimentDto(
                impediment.Id, impediment.PhaseId, impediment.Type,
                impediment.Description, impediment.Impact, impediment.OccurredAt,
                impediment.ResponsibleId, await NameOf(impediment.ResponsibleId),
                impediment.ActionTaken, impediment.Status.ToString(),
                impediment.ResolvedAt));

        var feedbackDtos = new List<PdiFeedbackDto>(feedbacks.Count);
        foreach (var feedback in feedbacks
                     .OrderBy(item => item.FeedbackDate)
                     .ThenBy(item => item.CreateDateTime))
            feedbackDtos.Add(new PdiFeedbackDto(
                feedback.Id, feedback.PhaseId, feedback.AuthorId,
                await NameOf(feedback.AuthorId), feedback.RecipientId,
                await NameOf(feedback.RecipientId), feedback.Type, feedback.Content,
                feedback.FeedbackDate));

        return new PdiAccompanimentDto(
            plan.UserId, progressDtos, checkInDtos, impedimentDtos, feedbackDtos);
    }
}
