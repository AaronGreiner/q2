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
            .WithSummary("Returns one thread with its newest page, and marks it as read.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/messages", GetOlderMessages)
            .WithName("GetOlderChatMessages")
            .WithSummary("Returns the page of a thread before a cursor the thread handed out.")
            .Produces<ChatPageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/messages", SendMessage)
            .WithName("SendMessage")
            .WithSummary("Writes a message into a conversation.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}/messages/{messageId:guid}", GetMessage)
            .WithName("GetChatMessage")
            .WithSummary("One message as it now is, wherever in the thread it lies.")
            .Produces<ChatMessageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/messages/{messageId:guid}", EditMessage)
            .WithName("EditChatMessage")
            .WithSummary("Corrects the words of your own message, within fifteen minutes of sending it.")
            .Produces<ChatMessageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/messages/{messageId:guid}", DeleteMessage)
            .WithName("DeleteChatMessage")
            .WithSummary("Takes back your own message for everybody; its place in the thread stays.")
            .Produces<ChatMessageResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/messages/{messageId:guid}/reactions", ToggleReaction)
            .WithName("ToggleMessageReaction")
            .WithSummary("Adds a reaction to a message, or takes it back, and returns the message.")
            .Produces<ChatMessageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}/pin", PinChat)
            .WithName("PinChat")
            .WithSummary("Pins a conversation to the top of its section of the reader's list, or unpins it. At most three.")
            .Produces<ChatDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/photos", GetPhotos)
            .WithName("GetChatPhotos")
            .WithSummary("The photographs of a conversation, newest first: those sent as messages and a goal's proofs.")
            .Produces<IReadOnlyList<ChatPhotoResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/mute", MuteChat)
            .WithName("MuteChat")
            .WithSummary("Stops a conversation from ringing on your devices, or lets it ring again.")
            .Produces<ChatDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Ok<ChatDetailResponse>> PinChat(
        ChatService chats,
        Guid id,
        PinChatRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chats.SetPinnedAsync(id, request, cancellationToken));

    private static async Task<Ok<IReadOnlyList<ChatPhotoResponse>>> GetPhotos(
        ChatService chats,
        Guid id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chats.GetPhotosAsync(id, cancellationToken));

    private static async Task<Ok<ChatDetailResponse>> MuteChat(
        ChatService chats,
        Guid id,
        MuteChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await chats.SetMutedAsync(id, request, cancellationToken);
        return TypedResults.Ok(result);
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

    private static async Task<Ok<ChatPageResponse>> GetOlderMessages(
        ChatService chats,
        Guid id,
        [FromQuery] string? before,
        CancellationToken cancellationToken)
    {
        var result = await chats.GetOlderAsync(id, before, cancellationToken);
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

    private static async Task<Ok<ChatMessageResponse>> GetMessage(
        ChatService chats,
        Guid id,
        Guid messageId,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chats.GetMessageAsync(id, messageId, cancellationToken));

    private static async Task<Ok<ChatMessageResponse>> EditMessage(
        ChatService chats,
        Guid id,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chats.EditMessageAsync(id, messageId, request, cancellationToken));

    private static async Task<Ok<ChatMessageResponse>> DeleteMessage(
        ChatService chats,
        Guid id,
        Guid messageId,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chats.DeleteMessageAsync(id, messageId, cancellationToken));

    private static async Task<Ok<ChatMessageResponse>> ToggleReaction(
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
