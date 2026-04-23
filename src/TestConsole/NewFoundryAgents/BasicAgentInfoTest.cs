using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using TestConsole.Infra;

namespace TestConsole.NewFoundryAgents;

public class BasicAgentInfoTest : BaseTest
{
    protected override string TestName => "RunBasicAgentInfoTestAsync";

    public BasicAgentInfoTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        string projectEndpoint = Configuration["AIFoundryEndpoint"];
        string agentName = Configuration["AgentName"];

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        var agentRecord = await projectClient.Agents.GetAgentAsync(agentName);
        var agentDefinition = (PromptAgentDefinition)agentRecord.Value.GetLatestVersion().Definition;

        // Log all fields of agentDefinition
        LogInfo($"Agent Instructions: {agentDefinition.Instructions}");
        LogInfo($"Agent Model: {agentDefinition.Model}");
        LogInfo($"Agent Temperature: {agentDefinition.Temperature}");
        LogInfo($"Agent TopP: {agentDefinition.TopP}");
    }
}
