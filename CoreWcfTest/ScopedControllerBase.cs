using CoreWCF.Web;

namespace CoreWcfTest;

public class ScopedControllerBase : IDisposable
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static IServiceProvider RootContainer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private readonly IServiceScope serviceScope = RootContainer.CreateScope();
    public IServiceProvider Services
        => serviceScope.ServiceProvider;

    public readonly WebOperationContext Context = WebOperationContext.Current;

    public virtual void Dispose()
        => serviceScope.Dispose();
}
