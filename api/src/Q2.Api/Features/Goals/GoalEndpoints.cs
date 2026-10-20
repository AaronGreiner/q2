using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.Goals;

/// <summary>
/// HTTP surface for goals and what is due today.
/// </summary>
/// <remarks>
/// Endpoints stay thin: bind, validate, delegate, map. Anything that looks
/// like a decision belongs in <see cref="GoalService"/> or in
/// <see cref="Goal"/> itself.
///
/// Delivering a proof is not here. It is mapped by
/// <see cref="Proofs.ProofEndpoints"/> onto <c>/api/goals/{id}/proof</c>, so
/// the route reads the way people think about it while the code sits with the
/// photograph and the vote it belongs to.
/// </remarks>
public static class GoalEndpoints
{
    public static IEndpointRouteBuilder MapGoalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var goals = endpoints.MapGroup("/api/goals").WithTags("Goals").RequireAuthorization();

        goals.MapGet("/", ListGoals)
            .WithName("ListGoals")
            .WithSummary("Lists goals, newest first.")
            .Produces<IReadOnlyList<GoalResponse>>();

        // Before the id route only for readability: "archive" is not a GUID,
        // so the constraint already keeps the two apart.
        goals.MapGet("/archive", ListArchive)
            .WithName("ListArchivedGoals")
            .WithSummary("Lists the goals that have stopped, most recently stopped first.")
            .Produces<IReadOnlyList<GoalResponse>>();

        // The Do somebody without friends has waiting (docs/adr/0039). Not a
        // goal, so not under an id: there is at most one, and it is the
        // caller's.
        goals.MapGet("/draft", GetDraft)
            .WithName("GetGoalDraft")
            .WithSummary("Returns your waiting Do, or null when there is none.")
            .Produces<GoalDraftStateResponse>();

        goals.MapPut("/draft", SaveDraft)
            .WithName("SaveGoalDraft")
            .WithSummary("Writes your waiting Do, which starts by itself when your first friendship does.")
            .Produces<GoalDraftResponse>()
            .ProducesValidationProblem();

        goals.MapDelete("/draft", DiscardDraft)
            .WithName("DiscardGoalDraft")
            .WithSummary("Throws your waiting Do away.")
            .Produces(StatusCodes.Status204NoContent);

        goals.MapGet("/{id:guid}", GetGoal)
            .WithName("GetGoal")
            .WithSummary("Returns one goal with its team and its resolved windows.")
            .Produces<GoalDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/", CreateGoal)
            .WithName("CreateGoal")
            .WithSummary("Creates a goal.")
            .Produces<GoalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        goals.MapPost("/{id:guid}/uses", LogUse)
            .WithName("LogDontUse")
            .WithSummary("Logs one use of your own Don't into the running period. No vote, no notification.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapDelete("/{id:guid}/uses/{useId:guid}", UndoUse)
            .WithName("UndoDontUse")
            .WithSummary("Takes back the use of your own Don't you logged last, within seconds of logging it. Never a relapse.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Before the id routes, so "pauses" is never read as an id.
        goals.MapPost("/pauses", PauseAll)
            .WithName("PauseAllGoals")
            .WithSummary("Sets every running goal of yours aside at once, with one reason and one length.")
            .Produces<PauseAllResponse>()
            .ProducesValidationProblem();

        goals.MapPost("/{id:guid}/pause", PauseGoal)
            .WithName("PauseGoal")
            .WithSummary("Sets a goal aside for whole days, with a reason its friends read.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapDelete("/{id:guid}/pause", EndPause)
            .WithName("EndGoalPause")
            .WithSummary("Ends the running pause early.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/{id:guid}/pause/veto", VetoPause)
            .WithName("VetoGoalPause")
            .WithSummary("Objects to a running pause, or takes the objection back. Anonymous.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/{id:guid}/close", CloseGoal)
            .WithName("CloseGoal")
            .WithSummary("Stops a goal for good and moves it to the archive.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPatch("/{id:guid}", EditGoal)
            .WithName("EditGoal")
            .WithSummary("Edits one of your own running goals. Easier changes wait for the next window and can be objected to.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/{id:guid}/change/preview", PreviewGoalEdit)
            .WithName("PreviewGoalEdit")
            .WithSummary("Says what an edit would do — what applies now, what waits and from when — without saving it.")
            .Produces<GoalEditPreviewResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapDelete("/{id:guid}/change", WithdrawGoalChange)
            .WithName("WithdrawGoalChange")
            .WithSummary("Takes back the change waiting on one of your own goals.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/{id:guid}/change/veto", VetoGoalChange)
            .WithName("VetoGoalChange")
            .WithSummary("Objects to the change waiting on a goal you check, or takes the objection back. Anonymous.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPut("/{id:guid}/reminder", SetReminder)
            .WithName("SetGoalReminder")
            .WithSummary("Moves the daily reminder of one of your own running goals, or removes it with null.")
            .Produces<GoalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapDelete("/{id:guid}", DeleteGoal)
            .WithName("DeleteGoal")
            .WithSummary("Deletes a stopped goal outright, for everybody on it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/today", ListDueToday)
            .RequireAuthorization()
            .WithTags("Goals")
            .WithName("ListDueToday")
            .WithSummary("Lists the goals whose current window covers today.")
            .Produces<IReadOnlyList<GoalResponse>>();

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<GoalResponse>>> ListGoals(
        GoalService goals,
        [FromQuery] GoalStatus? status,
        CancellationToken cancellationToken)
    {
        var result = await goals.ListAsync(status, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<GoalDetailResponse>> GetGoal(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken)
    {
        // A missing goal throws ResourceNotFoundException, which the global
        // handler turns into a 404 Problem Details response.
        var goal = await goals.GetAsync(id, cancellationToken);
        return TypedResults.Ok(goal);
    }

    private static async Task<Results<Created<GoalResponse>, ValidationProblem>> CreateGoal(
        GoalService goals,
        CreateGoalRequest request,
        CancellationToken cancellationToken)
    {
        var errors = CreateGoalRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        var created = await goals.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/goals/{created.Id}", created);
    }

    private static async Task<Ok<GoalDraftStateResponse>> GetDraft(
        GoalDraftService drafts,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await drafts.GetAsync(cancellationToken));

    private static async Task<Results<Ok<GoalDraftResponse>, ValidationProblem>> SaveDraft(
        GoalDraftService drafts,
        SaveGoalDraftRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SaveGoalDraftRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await drafts.SaveAsync(request, cancellationToken));
    }

    private static async Task<NoContent> DiscardDraft(
        GoalDraftService drafts,
        CancellationToken cancellationToken)
    {
        await drafts.DiscardAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<GoalResponse>>> ListArchive(
        GoalService goals,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.ListArchiveAsync(cancellationToken));

    private static async Task<Results<Ok<GoalResponse>, ValidationProblem>> PauseGoal(
        GoalService goals,
        Guid id,
        RequestPauseRequest request,
        CancellationToken cancellationToken)
    {
        var errors = RequestPauseValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await goals.PauseAsync(id, request, cancellationToken));
    }

    private static async Task<Results<Ok<PauseAllResponse>, ValidationProblem>> PauseAll(
        GoalService goals,
        RequestPauseRequest request,
        CancellationToken cancellationToken)
    {
        var errors = RequestPauseValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await goals.PauseAllAsync(request, cancellationToken));
    }

    private static async Task<Ok<GoalResponse>> EndPause(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.EndPauseAsync(id, cancellationToken));

    private static async Task<Ok<GoalResponse>> VetoPause(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.VetoPauseAsync(id, cancellationToken));

    private static async Task<Ok<GoalResponse>> CloseGoal(
        GoalService goals,
        Guid id,
        CloseGoalRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.CloseAsync(id, request, cancellationToken));

    private static async Task<Results<Ok<GoalResponse>, ValidationProblem>> EditGoal(
        GoalService goals,
        Guid id,
        EditGoalRequest request,
        CancellationToken cancellationToken)
    {
        var errors = EditGoalRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await goals.EditAsync(id, request, cancellationToken));
    }

    private static async Task<Results<Ok<GoalEditPreviewResponse>, ValidationProblem>> PreviewGoalEdit(
        GoalService goals,
        Guid id,
        EditGoalRequest request,
        CancellationToken cancellationToken)
    {
        var errors = EditGoalRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return errors.ToProblem();
        }

        return TypedResults.Ok(await goals.PreviewEditAsync(id, request, cancellationToken));
    }

    private static async Task<Ok<GoalResponse>> WithdrawGoalChange(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.WithdrawChangeAsync(id, cancellationToken));

    private static async Task<Ok<GoalResponse>> VetoGoalChange(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.VetoChangeAsync(id, cancellationToken));

    private static async Task<Ok<GoalResponse>> SetReminder(
        GoalService goals,
        Guid id,
        SetReminderRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.SetReminderAsync(id, request, cancellationToken));

    private static async Task<Ok<GoalResponse>> LogUse(
        GoalService goals,
        Guid id,
        LogUseRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.LogUseAsync(id, request, cancellationToken));

    private static async Task<Ok<GoalResponse>> UndoUse(
        GoalService goals,
        Guid id,
        Guid useId,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await goals.UndoUseAsync(id, useId, cancellationToken));

    private static async Task<NoContent> DeleteGoal(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken)
    {
        await goals.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<GoalResponse>>> ListDueToday(
        GoalService goals,
        CancellationToken cancellationToken)
    {
        var result = await goals.ListDueTodayAsync(cancellationToken);
        return TypedResults.Ok(result);
    }
}
