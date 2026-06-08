namespace tool_r1ng.Core;

public interface IWarmUpProvider
{
    Task WarmUpAsync(CancellationToken cancellationToken);
}
