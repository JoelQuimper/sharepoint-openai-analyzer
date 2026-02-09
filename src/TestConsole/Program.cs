using Microsoft.Extensions.Configuration;
using TestConsole;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
IConfigurationRoot configuration = builder.Build();

var tests = new Tests();
await tests.RunCallApiAsync(configuration);
//await tests.RunAIFoundryTestVectorStoreAsync(configuration);
//await tests.RunAIFoundryTestCodeInterpreterAsync(configuration);
//await tests.RunAIFoundryTestVisionAsync(configuration);
//await tests.CleanUpAIFoundryProjectAsync(configuration);

