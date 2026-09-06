using CoNotes.Application.Billing.Commands.Issue;
using CoNotes.Application.Billing.Commands.Redeem;
using CoNotes.Application.Billing.Commands.Revoke;
using CoNotes.Application.ChatMessages.Queries.GetHistory;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Join;
using CoNotes.Application.Notes.Commands.Share;
using CoNotes.Application.Notes.Queries.Get;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing;
using CoNotes.Domain.ChatMessages;
using Davish.Sendr;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class LicenseCodeRedemptionTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenRedeemedCode_WhenAppUserReloadedFromDb_ThenPlanTierWasUpdatedByTheCrossContextEventHandler()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var appUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(appUser, CancellationToken.None);
        userContext.AppUserId = appUser.Id;

        var issueResult = await sender.SendAsync(
            new IssueLicenseCodeCommand(PlanTier.ProMax, "PAYPAL-ORDER-1"),
            CancellationToken.None
        );
        Assert.True(issueResult.IsSuccess);

        // 這裡是關鍵驗證:UnitOfWork 在 commit 之後才發布 Domain Event, 而
        // LicenseCodeRedeemedDomainEventHandler 會再透過 IAppUserRepository.UpdateAsync 寫 DB——
        // 如果 commit 完 appDbContext.Transaction 沒有先清空、事件處理者用了同一個(已經 commit
        // 過)的 transaction 物件, Npgsql 會直接丟例外, RedeemLicenseCodeCommand 這裡就會失敗。
        var redeemResult = await sender.SendAsync(
            new RedeemLicenseCodeCommand(issueResult.Value.Code),
            CancellationToken.None
        );

        Assert.True(redeemResult.IsSuccess);
        Assert.Equal(PlanTier.ProMax, redeemResult.Value.PlanTier);

        var reloadedAppUser = await appUserRepository.FindByIdAsync(appUser.Id, CancellationToken.None);
        Assert.NotNull(reloadedAppUser);
        Assert.Equal(PlanTier.ProMax, reloadedAppUser.PlanTier);
    }

    [Fact]
    public async Task GivenPersistedCode_WhenRedeemedConcurrentlyByTwoRequests_ThenOnlyOneSucceeds()
    {
        // 用兩個各自獨立的 DI scope(各自獨立的 AppDbContext/DbConnection), 才是真的模擬
        // 兩個併發的 HTTP request——同一個 scope 共用同一條連線, 沒辦法真的並行送出兩個指令。
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var licenseCodeRepositoryForSetup = scopeA.ServiceProvider.GetRequiredService<ILicenseCodeRepository>();
        var appUserRepository = scopeA.ServiceProvider.GetRequiredService<IAppUserRepository>();

        var appUserA = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var appUserB = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(appUserA, CancellationToken.None);
        await appUserRepository.AddAsync(appUserB, CancellationToken.None);

        var licenseCode = LicenseCode.Issue("CONCURRENT1", PlanTier.Pro, "PAYPAL-ORDER-2", DateTime.UtcNow);
        await licenseCodeRepositoryForSetup.AddAsync(licenseCode, CancellationToken.None);

        var licenseCodeRepositoryA = scopeA.ServiceProvider.GetRequiredService<ILicenseCodeRepository>();
        var licenseCodeRepositoryB = scopeB.ServiceProvider.GetRequiredService<ILicenseCodeRepository>();
        var codeForA = (await licenseCodeRepositoryA.GetByCodeAsync("CONCURRENT1", CancellationToken.None))!;
        var codeForB = (await licenseCodeRepositoryB.GetByCodeAsync("CONCURRENT1", CancellationToken.None))!;
        codeForA.Redeem(appUserA.Id, DateTime.UtcNow);
        codeForB.Redeem(appUserB.Id, DateTime.UtcNow);

        var results = await Task.WhenAll(
            licenseCodeRepositoryA.TryPersistRedemptionAsync(codeForA, CancellationToken.None),
            licenseCodeRepositoryB.TryPersistRedemptionAsync(codeForB, CancellationToken.None)
        );

        Assert.Single(results, success => success);

        var finalState = await licenseCodeRepositoryForSetup.GetByCodeAsync("CONCURRENT1", CancellationToken.None);
        Assert.NotNull(finalState);
        Assert.True(finalState.RedeemedByAppUserId == appUserA.Id || finalState.RedeemedByAppUserId == appUserB.Id);
    }

    [Fact]
    public async Task GivenAdminRevokesUser_WhenReloadedFromDb_ThenPlanTierIsFree()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var targetAppUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        targetAppUser.ApplyRedeemedPlanTier(PlanTier.Pro);
        await appUserRepository.AddAsync(targetAppUser, CancellationToken.None);

        userContext.IsAdminUser = true;
        var revokeResult = await sender.SendAsync(
            new RevokeAppUserPlanTierCommand(targetAppUser.Id),
            CancellationToken.None
        );

        Assert.True(revokeResult.IsSuccess);
        var reloaded = await appUserRepository.FindByIdAsync(targetAppUser.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(PlanTier.Free, reloaded.PlanTier);
    }

    [Fact]
    public async Task GivenOwnerPlanTierRevoked_WhenQueryingExistingCollaboratorsOrChatHistory_ThenExistingDataUnaffected()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        owner.ApplyRedeemedPlanTier(PlanTier.ProMax);
        var collaborator = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(collaborator, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var createResult = await sender.SendAsync(new CreateNoteCommand("Downgrade note", "Content"), CancellationToken.None);
        var noteId = createResult.Value.NoteId;
        var shareResult = await sender.SendAsync(new GenerateShareLinkCommand(noteId), CancellationToken.None);

        userContext.AppUserId = collaborator.Id;
        await sender.SendAsync(new JoinNoteViaShareLinkCommand(shareResult.Value.ShareToken), CancellationToken.None);

        await chatMessageRepository.AddAsync(
            ChatMessage.Create(noteId, owner.Id, "Before downgrade", DateTime.UtcNow),
            CancellationToken.None
        );

        // 擁有者的等級被撤銷回 Free(軟性降級, 見 design.md 決定 7)
        userContext.IsAdminUser = true;
        await sender.SendAsync(new RevokeAppUserPlanTierCommand(owner.Id), CancellationToken.None);
        userContext.IsAdminUser = false;

        userContext.AppUserId = collaborator.Id;
        var getNoteResult = await sender.SendAsync(new GetNoteQuery(noteId), CancellationToken.None);
        Assert.True(getNoteResult.IsSuccess);

        var historyResult = await sender.SendAsync(new GetChatHistoryQuery(noteId), CancellationToken.None);
        Assert.True(historyResult.IsSuccess);
        Assert.Equal("Before downgrade", Assert.Single(historyResult.Value.Messages).Content);
    }
}
