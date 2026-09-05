namespace CoNotes.Application.AppUsers.Commands.Upsert;

public sealed record UpsertAppUserCommand(string KeycloakSub) : ICommand<Result<UpsertAppUserDto>>;

public sealed record UpsertAppUserDto(Guid AppUserId);
