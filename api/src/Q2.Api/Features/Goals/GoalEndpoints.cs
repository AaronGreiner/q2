using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.Goals;

/// <summary>
/// HTTP surface for goals.
/// </summary>
/// <remarks>
/// Endpoints stay thin: bind, validate, delegate, map. Anything that looks
/// like a decision belongs in <see cref="GoalService"/> or in
/// <see cref="Goal"/> itself.
/// </remarks>
public static class GoalEndpoints
{
    public static IEndpointRouteBuilder MapGoalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/goals").WithTags("Goals");

        group.MapGet("/", ListGoals)
            .WithName("ListGoals")
            .WithSummary("Lists goals, newest first.")
            .Produces<IReadOnlyList<GoalResponse>>();

        group.MapGet("/{id:guid}", GetGoal)
            .WithName("GetGoal")
            .WithSummary("Returns a single goal.")
            .Produces<GoalResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateGoal)
            .WithName("CreateGoal")
            .WithSummary("Creates a goal.")
            .Produces<GoalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

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

    private static async Task<Ok<GoalResponse>> GetGoal(
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
}
