using Akka.Actor;
using Akka.Configuration;
using Akka.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Akka.Configuration;
using Microsoft.Extensions.Logging;
using Marten;

namespace Akka_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost host;

        public App()
        {
            var logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File("log.txt")
                    .WriteTo.Debug()
                    .MinimumLevel.Debug()
                    .CreateBootstrapLogger();

            Log.Logger = logger;

            host = Host.CreateDefaultBuilder()
                   .ConfigureHostConfiguration(configHost =>
                   {
                   })
                   .ConfigureServices((context, services) =>
                   {
                       ConfigureServices(context.Configuration, services);
                   })
                   .UseSerilog()
                   .Build();
        }

        private void ConfigureServices(IConfiguration configuration,
            IServiceCollection services)
        {
            services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));

            services.AddDbContext<DatabaseContext>(
                options => options.UseNpgsql(@"Server=127.0.0.1;Port=5432;Database=PADE;User Id=postgres;Password=Welcome123$!;"));

            services.AddScoped<ISampleService, SampleService>();

            services.AddSingleton<IAkkaService, AkkaService>();

            // creates instance of IPublicHashingService that can be accessed by ASP.NET
            services.AddHostedService<AkkaService>(sp => (AkkaService)sp.GetRequiredService<IAkkaService>());

            services.AddSingleton<MainWindow>();

            services.AddMarten(options =>
            {
                options.Connection(@"Server=127.0.0.1;Port=5432;Database=PADE;User Id=postgres;Password=Welcome123$!;");
            });
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await host.StartAsync();

            var mainWindow = host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (host)
            {
                await host.StopAsync(TimeSpan.FromSeconds(5));
            }

            base.OnExit(e);
        }
    }

    public interface IAkkaService
    {
        ActorSystem System { get; set; }
        IActorRef Create(Props props,string name);
    }

    public sealed class AkkaService : IHostedService, IAkkaService
    {
        private IActorRef _apiMaster;
        public ActorSystem System { get; set; }
        private IActorRef _downloadMaster;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;


        private readonly IServiceProvider _serviceProvider;

        public AkkaService(IServiceProvider sp, IHostApplicationLifetime appLifetime, ILogger<AkkaService> logger)
        {
            _serviceProvider = sp;
            _appLifetime = appLifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnStarted);
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            var config = ConfigurationFactory.ParseString(File.ReadAllText("wpf.hocon"));
            var bootstrap = BootstrapSetup.Create()
                .WithConfig(config);
               //.WithConfig(config.ApplyOpsConfig());// injects environment variables into HOCON
               //.WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

            // N.B. `WithActorRefProvider` isn't actually needed here 
            // the HOCON file already specifies Akka.Cluster

            // enable DI support inside this ActorSystem, if needed
            var diSetup = DependencyResolverSetup.Create(_serviceProvider);

            // merge this setup (and any others) together into ActorSystemSetup
            var actorSystemSetup = bootstrap.And(diSetup);
            // start ActorSystem
            System = ActorSystem.Create("webcrawler", actorSystemSetup);

            //ClusterSystem.StartPbm(); // start Petabridge.Cmd (https://cmd.petabridge.com/)

            // instantiate actors
            //_apiMaster = ClusterSystem.ActorOf(Props.Create(() => new ApiMaster()), "api");
            //_downloadMaster = ClusterSystem.ActorOf(Props.Create(() => new DownloadsMaster()), "downloads");

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // strictly speaking this may not be necessary - terminating the ActorSystem would also work
            // but this call guarantees that the shutdown of the cluster is graceful regardless
            await CoordinatedShutdown.Get(System).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }

        private void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called.");

            // Perform post-startup activities here
        }

        private void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called.");

            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");

            // Perform post-stopped activities here
        }

        public IActorRef Create(Props props, string name)
        {
            return this.System.ActorOf(props, name);
        }
    }



    public class AppSettings
    {
        public string StringSetting { get; set; }

        public int IntegerSetting { get; set; }

        public bool BooleanSetting { get; set; }
    }

    public interface ISampleService
    {
        string GetCurrentDate();
    }

    public class SampleService : ISampleService
    {
        public string GetCurrentDate() => DateTime.Now.ToLongDateString();
    }
}
