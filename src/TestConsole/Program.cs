using Microsoft.Extensions.Configuration;
using TestConsole;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
IConfigurationRoot configuration = builder.Build();

await TestAPI.RunCallApiAsync(configuration);

// await TestAgentClassic.RunAIFoundryTestVectorStoreAsync(configuration);
// await TestAgentClassic.RunAIFoundryTestCodeInterpreterAsync(configuration);
// await TestAgentClassic.CleanUpAIFoundryProjectAsync(configuration);

// await TestAgentNew.RunFileSearchToolTestAsync(configuration);
// await TestAgentNew.RunImageAnalysisToolTestAsync(configuration);
// await TestAgentNew.RunFileAnalysisToolTestAsync(configuration);
// await TestAgentNew.RunBasicAgentInfoTestAsync(configuration);
