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
            return TypedResults.ValidationProblem(errors);
        }

        var created = await goals.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/goals/{created.Id}", created);
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
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await goals.PauseAsync(id, request, cancellationToken));
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
