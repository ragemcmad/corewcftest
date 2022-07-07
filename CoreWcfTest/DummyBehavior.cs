using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWcfTest;

public class DummyBehavior : IEndpointBehavior
{
    private readonly IDispatchMessageInspector[] dispatchers;

    public DummyBehavior(params IDispatchMessageInspector[] dispatchers)
    {
        this.dispatchers = dispatchers;
    }

    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
        foreach (var dispatcher in dispatchers)
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(dispatcher);
    }


    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }
    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime) { }
    public void Validate(ServiceEndpoint endpoint) { }
}

public abstract class BaseHttpInspector<TController> : IDispatchMessageInspector
    where TController : ScopedControllerBase
{
    // can be used as correlationState to signal this Inspector is not responsible and should not run for the request
    protected static readonly object _nullObject = new object();

    public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
    {
        if (!(instanceContext.GetServiceInstance(request) is TController controller))
            return _nullObject;
        if (!(request.Properties["httpRequest"] is HttpRequestMessageProperty requestProperty))
            return _nullObject;
        if (!ApplyWhen(controller, requestProperty))
            return _nullObject;

        AfterReceive(controller, requestProperty);
        return new Tuple<TController, HttpRequestMessageProperty>(controller, requestProperty);
    }

    // custom filter-function
    protected virtual bool ApplyWhen(TController controller, HttpRequestMessageProperty request)
        => true;

    protected virtual void AfterReceive(TController controller, HttpRequestMessageProperty request)
    {
        // noop
    }

    public void BeforeSendReply(ref Message reply, object correlationState)
    {
        if (_nullObject == correlationState)
            return;

        if (!(reply.Properties["httpResponse"] is HttpResponseMessageProperty response))
            return;

        var tuple = (Tuple<TController, HttpRequestMessageProperty>)correlationState;
        var controller = tuple.Item1;
        var request = tuple.Item2;

        BeforeSend(ref reply, controller, request, response);
    }

    protected virtual void BeforeSend(ref Message reply, TController controller, HttpRequestMessageProperty request, HttpResponseMessageProperty response)
    {
        // noop
    }
}

public class OriginDispatcher : BaseHttpInspector<DummyController>
{
    private readonly string[] allowedDomains;

    public OriginDispatcher(string[] allowedDomains)
    {
        this.allowedDomains = allowedDomains;
    }

    protected override bool ApplyWhen(DummyController controller, HttpRequestMessageProperty request)
        => OriginIsAllowedDomain(request);

    private bool OriginIsAllowedDomain(HttpRequestMessageProperty request)
    {
        var originHeader = OriginHeader(request);
        return !string.IsNullOrEmpty(originHeader) && allowedDomains.Any(domain => originHeader.Contains(domain));
    }

    private string? OriginHeader(HttpRequestMessageProperty request) => request.Headers["Origin"];

    protected override void BeforeSend(ref Message reply, DummyController controller, HttpRequestMessageProperty request, HttpResponseMessageProperty response)
    {
        var originHeader = OriginHeader(request);
        response.Headers.Add("Access-Control-Allow-Origin", originHeader);

        if (request.Method == "OPTIONS")
        {
            var accessControlHeaders = request.Headers["access-control-request-headers"];
            response.Headers.Add("Access-Control-Allow-Headers", accessControlHeaders);
        }
    }
}

public class DummyAuthenticationDispatcher : BaseHttpInspector<DummyController>
{
    protected override void AfterReceive(DummyController controller, HttpRequestMessageProperty request)
    {
        var authenticationService = controller.Services.GetRequiredService<IDummyAuthenticationService>();
        var authenticationResult = authenticationService.Authenticate("blabla");
        if (authenticationResult == null)
        {
            var logger = controller.Services.GetRequiredService<ILogger<DummyAuthenticationDispatcher>>();
            logger.LogError($"Auth failed for {"blabla"}");
        }
    }

    protected override bool ApplyWhen(DummyController controller, HttpRequestMessageProperty request)
        => !IsMetaEndpoint(request) && !UsesCustomAuthentication(request);

    private bool UsesCustomAuthentication(HttpRequestMessageProperty request)
    {
        // TODO
        return false;
    }

    private bool IsMetaEndpoint(HttpRequestMessageProperty request)
    {
        // TODO
        return false;
    }
}

public interface IDummyAuthenticationService
{
    object Authenticate(string token);
}

public class DummyAuthenticationService : IDummyAuthenticationService
{
    public object Authenticate(string token)
    {
        return null;
    }
}
