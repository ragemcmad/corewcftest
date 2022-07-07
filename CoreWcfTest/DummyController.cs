using CoreWCF;
using CoreWCF.Web;

namespace CoreWcfTest;

[ServiceContract]
public interface IDummyService
{
    [OperationContract , WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, UriTemplate = "/test")]
    Task<string> GetTest();
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Single)]
public class DummyController : ScopedControllerBase, IDummyService
{
    protected ILogger logger => Services.GetRequiredService<ILoggerFactory>().CreateLogger<DummyController>();

    public async Task<string> GetTest()
    {
        logger.LogInformation("Hello World! returned");
        return await Task.FromResult("Hello World! "+DateTime.Now);
    }
}
