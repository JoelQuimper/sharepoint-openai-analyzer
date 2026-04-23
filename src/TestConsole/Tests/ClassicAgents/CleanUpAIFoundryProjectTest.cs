using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using TestConsole.Infra;

namespace TestConsole.Tests.ClassicAgents;

public class CleanUpAIFoundryProjectTest : BaseTest
{
    protected override string TestName => "CleanUpAIFoundryProjectAsync";

    public CleanUpAIFoundryProjectTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        var endpoint = Configuration["AIFoundryEndpoint"];
        LogInfo($"Starting cleanup of AI Foundry project resources in {endpoint}...");

        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var agentsClient = projectClient.GetPersistentAgentsClient();

        await foreach (var agent in agentsClient.Administration.GetAgentsAsync())
        {
            LogInfo($"Deleting Agent: {agent.Id}, Name: {agent.Name}");
            await agentsClient.Administration.DeleteAgentAsync(agent.Id);
        }

        await foreach (var vectorStore in agentsClient.VectorStores.GetVectorStoresAsync())
        {
            LogInfo($"Deleting Vector Store: {vectorStore.Id}, Name: {vectorStore.Name}");
            await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Id);
        }

        var files = await agentsClient.Files.GetFilesAsync();
        foreach (var file in files.Value)
        {
            LogInfo($"Deleting File: {file.Id}, Filename: {file.Filename}");
            await agentsClient.Files.DeleteFileAsync(file.Id);
        }

        LogInfo("Cleanup completed.");
    }
}
