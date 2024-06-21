using Agora.Addons.Disqord;
using Emporia.Persistence.Extensions;
using Microsoft.Extensions.Hosting;

try
{
    using var host = Startup.CreateGenericHost(args);
    await host.MigrateDatabaseAsync();
    await host.RunAsync();
}
catch (FormatException)
{
    Console.WriteLine("Add your Discord bot application token to the appsetting.json file!");
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}
