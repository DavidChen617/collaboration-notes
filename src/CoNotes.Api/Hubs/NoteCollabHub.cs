using CoNotes.Application.Abstractions;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoNotes.Api.Hubs;

/// <summary>
/// 即時協作編輯的傳輸層 - 只負責存取檢查、轉發 Yjs update 給同一篇筆記的其他連線、
/// 附加寫入更新紀錄。完全不理解 Yjs binary 的合併語義(design.md decision 1)。
///
/// 不能沿用 REST API 那邊的 <see cref="IUserContext"/> - 它是靠 IHttpContextAccessor
/// 讀目前 request 的 HttpContext, 但 SignalR 在非 WebSocket 傳輸(例如 long polling)下,
/// Hub 方法執行當下不保證有對應的 HttpContext 可拿。這裡改用 SignalR 自己每個連線都會
/// 正確帶著走的 <see cref="Hub.Context"/>.User。
/// </summary>
[Authorize]
internal sealed class NoteCollabHub(
    IAppUserRepository appUserRepository,
    INoteRepository noteRepository,
    INoteEditHistoryStore editHistoryStore
) : Hub
{
    public async Task JoinNoteAsync(Guid noteId)
    {
        var note = await noteRepository.GetByIdAsync(noteId, Context.ConnectionAborted);
        var appUserId = await GetAppUserIdAsync();

        if (note is null || !note.IsAccessibleBy(appUserId))
            throw new HubException("使用者沒有權限存取這篇筆記!");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(noteId), Context.ConnectionAborted);
    }

    private async Task<Guid> GetAppUserIdAsync()
    {
        var keycloakSub = Context.User?.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("No authenticated user on this connection.");

        var appUser = await appUserRepository.FindByKeycloakSubAsync(keycloakSub, Context.ConnectionAborted)
            ?? throw new InvalidOperationException($"No AppUser found for Keycloak sub '{keycloakSub}'.");

        return appUser.Id;
    }

    public async Task SendUpdateAsync(Guid noteId, byte[] update)
    {
        await Clients.OthersInGroup(GroupName(noteId)).SendAsync("ReceiveUpdate", update, Context.ConnectionAborted);

        var needsSnapshot = await editHistoryStore.AppendUpdateAsync(noteId, update, Context.ConnectionAborted);

        if (needsSnapshot)
            await Clients.Caller.SendAsync("SnapshotRequested", noteId, Context.ConnectionAborted);
    }

    public async Task SaveSnapshotAsync(Guid noteId, byte[] snapshot)
    {
        await editHistoryStore.SaveSnapshotAsync(noteId, snapshot, Context.ConnectionAborted);
    }

    private static string GroupName(Guid noteId) => $"note:{noteId}";
}
