namespace Todo.Application.Absctractions;

public interface IUserContext
{
    Guid UserId { get; }
    string Name { get; }
    string Email { get; }
}

