using Microsoft.Extensions.Configuration;
using TestConsole.Tests.AnalyzerApi;
using TestConsole.Tests.ClassicAgents;
using TestConsole.Tests.NewFoundryAgents;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
IConfigurationRoot configuration = builder.Build();

// await new APITest(configuration).RunAsync();

await new VectorStoreAgentTest(configuration).RunAsync();
await new CodeInterpreterAgentTest(configuration).RunAsync();
// await new CleanUpAIFoundryProjectTest(configuration).RunAsync();

// await new FileSearchToolTest(configuration).RunAsync();
// await new ImageAnalysisToolTest(configuration).RunAsync();
// await new FileAnalysisToolTest(configuration).RunAsync();
// await new BasicAgentInfoTest(configuration).RunAsync();
