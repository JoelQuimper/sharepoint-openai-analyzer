using Microsoft.Extensions.Configuration;
using TestConsole.AnalyzerApi;
using TestConsole.NewFoundryAgents;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
IConfigurationRoot configuration = builder.Build();

await new APITest(configuration).RunAsync();

// await TestAgentClassic.RunAIFoundryTestVectorStoreAsync(configuration);
// await TestAgentClassic.RunAIFoundryTestCodeInterpreterAsync(configuration);
// await TestAgentClassic.CleanUpAIFoundryProjectAsync(configuration);

// await new FileSearchToolTest(configuration).RunAsync();
// await new ImageAnalysisToolTest(configuration).RunAsync();
// await new FileAnalysisToolTest(configuration).RunAsync();
// await new BasicAgentInfoTest(configuration).RunAsync();
