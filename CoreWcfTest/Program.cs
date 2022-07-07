using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Diagnostics;
using System.Net;

namespace CoreWcfTest;

public static class Program
{
    static Program()
    {

        ServicePointManager.DefaultConnectionLimit = 5000;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        var integrationLoggerFactory = LoggerFactory.Create(loggingbuilder =>
        {
            if (Environment.UserInteractive)
                loggingbuilder.AddConsole();
            else
                loggingbuilder.AddEventLog();
        });

        var unhandledExceptionLogger = integrationLoggerFactory.CreateLogger<UnhandledExceptionEventHandler>();
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => { unhandledExceptionLogger.LogError(e.ExceptionObject as Exception, "unhandled exception"); };
    }

    public static void Main(string[] args)
    {

        var hostBuilder = new WebHostBuilder()
            .ConfigureAppConfiguration((WebHostBuilderContext ctx, IConfigurationBuilder cfg) =>
            {
                AddAppsettings(ctx, cfg);
            })
            .Configure((WebHostBuilderContext ctx, IApplicationBuilder app) =>
            {
                ConfigureWcfCore(app);
            })
            .ConfigureServices((WebHostBuilderContext ctx, IServiceCollection services) =>
            {
                AddLogging(services);
                AddWCFServices(services);
                AddApplicationServices(services);
            })
            .UseKestrel((WebHostBuilderContext ctx, KestrelServerOptions options) =>
            {
                var cfg = ctx.Configuration.GetRequiredSection("Kestrel");
                var port = cfg.GetValue<int>("Port");

                var cert = options.ApplicationServices.GetRequiredService<CertificateService>().Get();

                options.AllowSynchronousIO = true;
                options.ListenAnyIP(port, ipcfg =>
                {
                    ipcfg.UseHttps(cert);
                });
            })
            .UseDefaultServiceProvider(sp => sp.ValidateOnBuild = true);

        var host = hostBuilder.Build();

        ScopedControllerBase.RootContainer = host.Services;

        if (Environment.UserInteractive || !System.OperatingSystem.IsWindows())
        {
            host.Start();

            var port = host.Services
                .GetRequiredService<IConfiguration>()
                .GetRequiredSection("Kestrel")
                .GetValue<int>("Port")
                ;
            var psi = new ProcessStartInfo()
            {
                FileName = "firefox",
                Arguments = "https://localhost:8080/api/test",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            host.WaitForShutdown();
        }
        else
        {
            // TODO check if service-events suspend+resume are handled
            host.RunAsService();
            //var svc = new SuspendableWebHostService(host);
            //ServiceBase.Run(svc);
        }
    }

    private static void AddAppsettings(WebHostBuilderContext ctx, IConfigurationBuilder cfg)
    {
        var fileName = ctx.HostingEnvironment.IsDevelopment()
                            ? $"appsettings.Development.json"
                            : $"appsettings.json";
        cfg.AddJsonFile(fileName);
    }

    private static void AddWCFServices(IServiceCollection services)
    {
        services
            .AddServiceModelWebServices()
            .AddServiceModelServices()
            .AddServiceModelMetadata()
            ;
    }

    private static void AddApplicationServices(IServiceCollection services)
    {
        services
            .AddSingleton<CertificateService>()
            .AddScoped<IDummyAuthenticationService, DummyAuthenticationService>()
            ;
    }

    private static void AddLogging(IServiceCollection services)
    {
        services.AddLogging(lb =>
        {
            if (Environment.UserInteractive || !System.OperatingSystem.IsWindows())
                lb.AddConsole();
            else
                ; // TODO add file-logging
        });
    }

    private static void ConfigureWcfCore(IApplicationBuilder app)
    {
        app.UseServiceModel(builder =>
        {
            builder
                .AddService<DummyController>()
                .AddServiceWebEndpoint<DummyController, IDummyService>(new WebHttpBinding(WebHttpSecurityMode.Transport), "api")
                .ConfigureServiceHostBase<DummyController>(
                    hostbase => hostbase.Description.Endpoints.First().EndpointBehaviors.Add(
                        new DummyBehavior(
                            new DummyAuthenticationDispatcher(),
                            new OriginDispatcher(new string[] { "192.168.", "localhost" })
                        )
                    )
                 );
        });
    }
}
