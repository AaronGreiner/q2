using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.Goals;

/// <summary>
/// HTTP surface for goals and the tasks under them.
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
        var goals = endpoints.MapGroup("/api/goals").WithTags("Goals").RequireAuthorization();

        goals.MapGet("/", ListGoals)
            .WithName("ListGoals")
            .WithSummary("Lists goals, newest first.")
            .Produces<IReadOnlyList<GoalResponse>>();

        goals.MapGet("/{id:guid}", GetGoal)
            .WithName("GetGoal")
            .WithSummary("Returns one goal with its team and its tasks.")
            .Produces<GoalDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        goals.MapPost("/", CreateGoal)
            .WithName("CreateGoal")
            .WithSummary("Creates a goal.")
            .Produces<GoalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        goals.MapPost("/{id:guid}/contribute", Contribute)
            .WithName("ContributeToGoal")
            .WithSummary("Records one step of progress towards a goal.")
            .Produces<GoalResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var tasks = endpoints.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        tasks.MapGet("/", ListTasks)
            .WithName("ListTasks")
            .WithSummary("Lists the tasks scheduled for today, or all of them.")
            .Produces<IReadOnlyList<GoalTaskResponse>>();

        tasks.MapPost("/", CreateTask)
            .WithName("CreateTask")
            .WithSummary("Creates a task.")
            .Produces<GoalTaskResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks.MapPost("/{id:guid}/toggle", ToggleTask)
            .WithName("ToggleTask")
            .WithSummary("Ticks a task off for today, or takes it back.")
            .Produces<GoalTaskResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static async Task<Ok<GoalResponse>> Contribute(
        GoalService goals,
        Guid id,
        CancellationToken cancellationToken)
    {
        var goal = await goals.ContributeAsync(id, cancellationToken);
        return TypedResults.Ok(goal);
    }

    private static async Task<Ok<IReadOnlyList<GoalTaskResponse>>> ListTasks(
        GoalTaskService tasks,
        [FromQuery] bool? all,
        CancellationToken cancellationToken)
    {
        var result = await tasks.ListAsync(scheduledOnly: all is not true, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<GoalTaskResponse>, ValidationProblem>> CreateTask(
        GoalTaskService tasks,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var errors = CreateTaskRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var created = await tasks.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/tasks/{created.Id}", created);
    }

    private static async Task<Ok<GoalTaskResponse>> ToggleTask(
        GoalTaskService tasks,
        Guid id,
        CancellationToken cancellationToken)
    {
        var task = await tasks.ToggleAsync(id, cancellationToken);
        return TypedResults.Ok(task);
    }
}
