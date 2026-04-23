using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using TestConsole.Infra;

namespace TestConsole.Tests.NewFoundryAgents;
    
public class ImageAnalysisToolTest : BaseTest
{
    protected override string TestName => "RunImageAnalysisToolTestAsync";

    public ImageAnalysisToolTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        string projectEndpoint = Configuration["AIFoundryEndpoint"];
        var modelName = Configuration["ModelDeployement"];
        var filePath = Configuration["LocalFilePath_PNG"];
        var fileType = "image/png";

        string agentName = "TestImageAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        LogInfo($"Starting test for {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        // Get file as BinaryData for upload
        var fileData = BinaryData.FromStream(File.OpenRead(filePath), fileType);

        // Create the agent
        var agentDefinition = new PromptAgentDefinition(model: modelName)
        {
            Instructions = @"You are a helpful assistant that can answer questions about the content of an image provided 
                by the user. Answer the user's question based on that image information."
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

        LogInfo($"Image size: {fileData.ToMemory().Length} bytes");

        ResponseResult response = await responsesClient.CreateResponseAsync(
            new CreateResponseOptions
            {
                InputItems =
                {
                    ResponseItem.CreateUserMessageItem(new[]
                    {
                        ResponseContentPart.CreateInputTextPart("Please analyze and summarize the attached image."),
                        ResponseContentPart.CreateInputImagePart(fileData, ResponseImageDetailLevel.High)
                    })
                }
            }
        );

        LogInfo(response.GetOutputText());

        // Cleanup
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        LogInfo($"Deleted agent with name: {result.Value.Name}");
    }
}
