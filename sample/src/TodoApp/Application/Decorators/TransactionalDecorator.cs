namespace Todo.Application.Decorators;

internal sealed class TransactionalDecorator(
    IUnitOfWork unitOfWork,
    ILogger<TransactionalDecorator> logger
) : IRequestDecorator.WithResponse
{
    public async Task<TResponse> HandleAsync<TRequest, TResponse>(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var result = await next();

            await unitOfWork.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while handling {Request}", typeof(TRequest).Name);

            try
            {
                await unitOfWork.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(rollbackException,
                                "An error occurred while rolling back {Request}",
                                typeof(TRequest).Name);
            }

            throw;
        }
    }
}

