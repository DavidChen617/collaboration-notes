namespace Todo.Application;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSendr();
            services.AddSendrNotification();

            services
                    .AddRequestHandler<CreateTodoCommand, Result<CreateTodoDto>, CreateTodoCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                    .AddRequestHandler<DeleteTodoCommand, Result, DeleteTodoCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                    .AddRequestHandler<CompleteTodoCommand, Result, CompleteTodoCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                    .AddRequestHandler<GetTodoQuery, Result<GetTodoDto>, GetTodoQueryHandler>()
                    .AddRequestHandler<ListTodosQuery, Result<ListTodosDto>, ListTodosQueryHandler>();

            services.AddNotificationHandler<TodoCreatedDomainEvent>(o =>
                        o.Handler.Sequence.With<TodoCreatedDomainEventHandler>());
            services.AddNotificationHandler<TodoDeletedDomainEvent>(o =>
                        o.Handler.Sequence.With<TodoDeletedDomainEventHandler>());
            services.AddNotificationHandler<TodoCompletedDomainEvent>(o =>
                        o.Handler.Sequence.With<TodoCompletedDomainEventHandler>());

            return services;
        }
    }
}

