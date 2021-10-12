using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Marten;

namespace Akka_WPF
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ActorSystem ActorSystem { get; set; }
        private IActorRef calculator { get; set; }

        private readonly ISampleService sampleService;
        private readonly AppSettings settings;
        private readonly IAkkaService akkaService;
        private readonly IServiceProvider serviceProvider;

        public MainWindow(ISampleService sampleService, IOptions<AppSettings> settings, IAkkaService akkaService, IServiceProvider serviceProvider, IDocumentSession session)
        {
            InitializeComponent();

            //using (DatabaseContext db = new DatabaseContext())
            //{
            //    var blog = new Blog()
            //    {
            //        Url = "a"
            //    };
            //    db.Add<Blog>(blog);
            //    db.SaveChanges();

            //    var result = db.Blogs;
            //}


            this.sampleService = sampleService;
            this.settings = settings.Value;
            this.akkaService = akkaService;
            this.serviceProvider = serviceProvider;

        

            ActorSystem = ActorSystem.Create("PADE", "akka { loglevel=DEBUG,  loggers=[\"Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog\"]}");
            calculator = this.akkaService.Create(ServiceActor.Props(this.serviceProvider, 0),"myactor");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            calculator.Tell(new ServiceActor.ChangeCount(10));
        }
    }

    public class ServiceActor : UntypedActor
    {
        private int _count;
        private ILoggingAdapter Logger;
        private IServiceProvider _serviceProvider;
        public ServiceActor(IServiceProvider sp, int count)
        {
            _count = count;
            _serviceProvider = sp;
            Logger = Context.GetLogger<SerilogLoggingAdapter>();

        }
        protected override void OnReceive(object message)
        {
            if (message is ChangeCount changeCount)
            {
                var db = _serviceProvider.GetService<DatabaseContext>();
                {
                    var result = db.Blogs;
                }

                _count += changeCount.Value;
            }
        }

        protected override void PreStart()
        {
            Logger.Info("Starting actor");
            base.PreStart();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            base.PreRestart(reason, message);
        }

        protected override void PostRestart(Exception reason)
        {
            base.PostRestart(reason);
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        public static Props Props(IServiceProvider sp, int count)
        {
            return Akka.Actor.Props.Create(() => new ServiceActor(sp,count));
        }

        public class ChangeCount
        {
            public ChangeCount(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }

}
