using CoNotes.Application.ChatMessages.Commands.Send;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoNotes.Api.Hubs;

[Authorize]
internal sealed class ChatHub(
    IAppUserRepository appUserRepository,
    INoteRepository noteRepository
) : Hub
{
    public async Task JoinNoteAsync(Guid noteId)
    {
        var note = await noteRepository.GetByIdAsync(noteId, Context.ConnectionAborted);
        var appUserId = await GetAppUserIdAsync();

        if (note is null || !note.IsAccessibleBy(appUserId))
            throw new HubException("使用者沒有權限存取這篇筆記的聊天室!");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(noteId),
            Context.ConnectionAborted
        );
    }

    public async Task<SendChatMessageDto> SendMessageAsync(Guid noteId, string content)
    {
        var appUserId = await GetAppUserIdAsync();
        var result = await Context.GetHttpContext()!
            .RequestServices
            .GetRequiredService<ISender>()
            .SendAsync(
                new SendChatMessageCommand(noteId, content, appUserId),
                Context.ConnectionAborted
            );

        if (!result.IsSuccess)
            throw new HubException(result.Error.Code);

        return result.Value;
    }

    private async Task<Guid> GetAppUserIdAsync()
    {
        var keycloakSub = Context.User?.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("No authenticated user on this connection.");
        var appUser = await appUserRepository.FindByKeycloakSubAsync(
            keycloakSub,
            Context.ConnectionAborted
        ) ?? throw new InvalidOperationException(
            $"No AppUser found for Keycloak sub '{keycloakSub}'."
        );

        return appUser.Id;
    }

    internal static string GroupName(Guid noteId) => $"chat:{noteId}";
}
