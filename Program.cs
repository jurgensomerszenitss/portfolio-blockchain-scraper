using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using RomeScraper.Config;
using RomeScraper.Scraper;

namespace RomeScraper
{
    class Program
    {
        static readonly Container _container;

        static Program()
        {
            _container = new Container();

            RegisterLogger(_container);
            RegisterConfiguration(_container);

            _container.RegisterSingleton<IScraper,Scraper.Scraper>();

            _container.Verify();
        }

        static void Main(string[] args)
        {
            
            var scraper = _container.GetInstance<IScraper>();
            scraper.Verify().Wait();

            //scraper.TestSubscriptions().Wait();
            //scraper.TestTxHistory().Wait();
            scraper.TestGetTx().GetAwaiter().GetResult();
            scraper.StartAsync().GetAwaiter().GetResult();
            Console.WriteLine("...end");
            Console.ReadKey();
        }

        private static void RegisterConfiguration(Container container)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .Build();            

            var ethSettings = new EthSettings();
            config.Bind("eth", ethSettings);
            _container.RegisterInstance(ethSettings);
        }
 
        private static void RegisterLogger(Container container)
        {
            var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddSimpleConsole(options =>  {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    }));

            container.RegisterInstance<ILogger>(loggerFactory.CreateLogger<Program>());
        }
    }
}
