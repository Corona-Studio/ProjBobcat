using Cocona;
using ProjBobcat.Sample.Commands;
using ProjBobcat.Sample.Commands.Game;
using Serilog;

namespace ProjBobcat.Sample
{
    [HasSubCommands(typeof(GameManageCommands), "game", Description = "Game management commands")]
    [HasSubCommands(typeof(SystemMetricsCommands), "usage", Description = "System usage metrics commands")]
    [HasSubCommands(typeof(SystemInfoCommands), "utils", Description = "System information utilities")]
    class Program
    {
        static void Main(string[] args)
        {
            var builder = CoconaApp.CreateBuilder();
            builder
                .Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console());

            var app = builder.Build();

            app.AddCommands<Program>();
            app.AddCommands<TestCommand>();
            app.AddCommands<GameLaunchCommands>();

            app.Run();
        }
    }
}
