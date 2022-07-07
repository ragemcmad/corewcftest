using Microsoft.AspNetCore.Hosting.WindowsServices;
using System.ServiceProcess;


// Extension for WebHostService to enable Suspend/Resume
class SuspendableWebHostService : WebHostService
{
    private readonly IWebHost host;
    private readonly ILogger logger;

    public SuspendableWebHostService(IWebHost host) : base(System.OperatingSystem.IsWindows() ? host : throw new NotSupportedException($"OS not supported"))
    {
        this.host = host;
        logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<SuspendableWebHostService>();
        CanHandlePowerEvent = true;
        CanShutdown = true;
        CanStop = true;
        CanPauseAndContinue = true;
    }

    // TODO test
    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        if (!System.OperatingSystem.IsWindows())
            return false;

        var eventName = Enum.GetName(powerStatus) ?? "unknown";
        LogEvent(eventName, "called");
        logger.LogInformation($"Event '{eventName}' called.");

        var result = false;
        try
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.Suspend:
                    {
                        result = host.StopAsync().Wait(19000); // 20s timeout from windows
                        break;
                    }
                case PowerBroadcastStatus.ResumeSuspend:
                    {
                        result = host.StartAsync().Wait(5000);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            LogEvent(eventName, $"{ex.GetType().FullName} : {ex.Message}");
            logger.LogError(ex, $"in Powerevent, '{eventName}'");
        }

        if (result)
            LogEvent(eventName, $"successfully started or stopped");
        else
        {
            logger.LogError($"Failed to start or stop for event '{eventName}'");
            LogEvent(eventName, $"failed to start or stop");
        }
        return result;
    }

    protected override void OnCustomCommand(int command)
    {
        LogEvent(nameof(OnCustomCommand), $"Command: {command}");
        if (System.OperatingSystem.IsWindows())
            base.OnCustomCommand(command);
    }

    private void LogEvent(string eventType, string eventMessage)
    {
        var msg = $"{eventType}: {eventMessage}";
        if (!Environment.UserInteractive && System.OperatingSystem.IsWindows())
            EventLog.WriteEntry(msg);
        if (Environment.UserInteractive)
            Console.WriteLine(msg);
    }
}
