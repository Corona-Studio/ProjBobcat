using Microsoft.Extensions.DependencyInjection;
using ProjBobcat.Handler;

namespace ProjBobcat.Class.Helper
{
    public static class ServiceHelper
    {
        public static IServiceCollection ServiceCollection { get; private set; }
        public static ServiceProvider ServiceProvider { get; private set; }

        public static void Init()
        {
            ServiceCollection = new ServiceCollection();

            ServiceCollection.AddTransient<RetryHandler>();
            ServiceCollection.AddTransient<RedirectHandler>();

            ServiceProvider = ServiceCollection.BuildServiceProvider();
        }

        public static void UpdateServiceProvider()
        {
            ServiceProvider = ServiceCollection.BuildServiceProvider();
        }
    }
}