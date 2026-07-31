using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Q2.Api.Features.Chats;

/// <summary>HTTP surface for conversations and messages.</summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chats").WithTags("Chats").RequireAuthorization();

        group.MapGet("/", ListChats)
            .WithName("ListChats")
            .WithSummary("Lists your conversations, most recent first.")
            .Produces<IReadOnlyList<ChatSummaryResponse>>();

        group.MapPost("/direct", StartDirectChat)
            .WithName("StartDirectChat")
            .WithSummary("Opens the conversation with somebody, creating it the first time.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/groups", CreateGroupChat)
            .WithName("CreateGroupChat")
            .WithSummary("Creates a group conversation with friends.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/leave", LeaveChat)
            .WithName("LeaveChat")
            .WithSummary("Leaves a group conversation.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetChat)
            .WithName("GetChat")
            .WithSummary("Returns one thread and marks it as read.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/messages", SendMessage)
            .WithName("SendMessage")
            .WithSummary("Writes a message into a conversation.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/messages/{messageId:guid}/reactions", ToggleReaction)
            .WithName("ToggleMessageReaction")
            .WithSummary("Adds a reaction to a message, or takes it back.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<ChatSummaryResponse>>> ListChats(
        ChatService chats,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await chats.ListAsync(search, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ChatDetailResponse>> StartDirectChat(
        ChatService chats,
        StartDirectChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chats.StartDirectAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ChatDetailResponse>> CreateGroupChat(
        ChatService chats,
        CreateGroupChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chats.CreateGroupAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> LeaveChat(
        ChatService chats,
        Guid id,
        CancellationToken cancellationToken)
    {
        await chats.LeaveAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ChatDetailResponse>> GetChat(
        ChatService chats,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await chats.GetAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ChatDetailResponse>> SendMessage(
        ChatService chats,
        Guid id,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        // An empty message throws DomainValidationException from ChatMessage
        // itself, which the global handler turns into a 400. There is no second
        // copy of the rule here: "not empty, at most 2000 characters" is the
        // whole of it, and the domain already says so.
        var result = await chats.SendAsync(id, request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ChatDetailResponse>> ToggleReaction(
        ChatService chats,
        Guid id,
        Guid messageId,
        ToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chats.ToggleReactionAsync(id, messageId, request, cancellationToken);
        return TypedResults.Ok(result);
    }
}
