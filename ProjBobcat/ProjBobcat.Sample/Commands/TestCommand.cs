using Cocona;
using Microsoft.Extensions.Logging;

namespace ProjBobcat.Sample.Commands;

public class TestCommand(ILogger<TestCommand> logger)
{
    readonly ILogger _logger = logger;

    [Command("hello")]
    public void Hello()
    {
        _logger.LogInformation("Hello from Cocona.");
    }
}