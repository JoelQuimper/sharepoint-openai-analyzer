using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using TestConsole.Infra;

namespace TestConsole.Tests.NewFoundryAgents;

public class FileAnalysisToolTest : BaseTest
{
    protected override string TestName => "RunFileAnalysisToolTestAsync";

    public FileAnalysisToolTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        string projectEndpoint = Configuration["AIFoundryEndpoint"];
        var modelName = Configuration["ModelDeployement"];
        var filePath = Configuration["LocalFilePath_PDF"];
        // In my test, this was only working using pdf. Maybe other models/config would behave differently,
        // but I am hardcoding this for now to make sure the test runs. We can make this more dynamic later.
        var fileType = "application/pdf";

        string agentName = "TestFileAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        LogInfo($"Starting test for {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        // Get file as BinaryData for upload
        var fileData = BinaryData.FromStream(File.OpenRead(filePath), fileType);

        // Create the agent
        var agentDefinition = new PromptAgentDefinition(model: modelName)
        {
            Instructions = @"You are a helpful assistant that can answer questions about the content of the file provided 
                by the user. Answer the user's question based on that file information."
        };

        var result = await projectClient.Agents.CreateAgentVersionAsync(
            agentName: $"{agentName}-{uniqueId}",
            options: new AgentVersionCreationOptions(agentDefinition)
        );
        LogInfo($"Created agent with name: {result.Value.Name} and version: {result.Value.Version}");

        // Create a conversation
        ProjectConversation conversation = await projectClient.OpenAI.GetProjectConversationsClient().CreateProjectConversationAsync();

        ProjectResponsesClient responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
            defaultAgent: result.Value.Name,
            defaultConversationId: conversation.Id);

        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart("Please analyze and summarize the attached file available."),
                    ResponseContentPart.CreateInputFilePart(fileData, fileType, Path.GetFileName(filePath))
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await responsesClient.CreateResponseAsync(options);
        LogInfo(response.GetOutputText());

        // Cleanup
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        LogInfo($"Deleted agent with name: {result.Value.Name}");
    }
}
