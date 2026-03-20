# SharePoint OpenAI Analyzer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET Version](https://img.shields.io/badge/.NET-v10.0-blue)
![Status](https://img.shields.io/badge/Status-Active-brightgreen)

A powerful tool to analyze and extract insights from SharePoint content using Azure AI Foundry language models. This project enables users to process documents, generate summaries, and produce intelligent reports from data stored in SharePoint with minimal configuration.

## Features

- **Document Analysis**: Automatically process and analyze documents from SharePoint
- **AI-Powered Insights**: Leverage Azure AI Foundry language models for intelligent document comprehension
- **Report Generation**: Create detailed analysis reports from SharePoint content
- **Secure Integration**: Uses Microsoft Graph API and Azure AD authentication for secure SharePoint access
- **RESTful API**: Built with ASP.NET Core for easy integration
- **Modern Stack**: Built on .NET 10 with latest Azure services

## Architecture

The project consists of two main components:

### AnalyzerWebApi
A RESTful API service that:
- Authenticates with SharePoint via Microsoft Graph API
- Integrates with Azure AI Foundry for language model operations
- Exposes document analysis endpoints
- Manages AI service configurations and prompts

### TestConsole
A console application for:
- Testing document analysis functionality
- Running quick manual tests
- Debugging and development

## Prerequisites

- **.NET 10 Runtime** or higher
- **Azure Subscription** with the following services:
  - Azure AI Foundry (with configured agents)
  - Microsoft Entra ID (for authentication)
  - Microsoft Graph API access
- **SharePoint Online** tenant with document access
- **Entra Application Registration** with appropriate permissions

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/JoelQuimper/sharepoint-openai-analyzer.git
cd sharepoint-openai-analyzer
```

### 2. Configure Azure Resources

#### Create Entra Application
```powershell
# Use the provided infrastructure scripts to set up the application
./infra/Start-Devtunnel.ps1  # Set up dev tunnel for local testing
```

#### Configure Application Settings
Update `appsettings.Development.json` with your Azure configuration:

```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  },
  "MicrosoftFoundry": {
    "Endpoint": "https://your-foundry-endpoint",
    "AgentName": "your-agent-name"
  },
  "GraphInfo": {
    "Scopes": ["https://graph.microsoft.com/.default"]
  }
}
```

### 3. Build and Run

#### Using .NET CLI
```bash
cd src
dotnet build
dotnet run --project AnalyzerWebApi/AnalyzerWebApi.csproj
```

#### Using Visual Studio
```bash
# Open the solution
open src/Analyzer.sln

## Configuration Details

### Environment Variables
Key configuration values that can be set as environment variables:

| Variable | Description | Required |
|----------|-------------|----------|
| `AzureAd:TenantId` | Microsoft Entra ID tenant ID | Yes |
| `AzureAd:ClientId` | Azure AD application client ID | Yes |
| `AzureAd:ClientSecret` | Azure AD application secret | Yes |
| `MicrosoftFoundry:Endpoint` | Azure AI Foundry endpoint URL | Yes |
| `MicrosoftFoundry:AgentName` | AI agent name to use for analysis | Yes |

## Deployment

### Deploy to Azure

```powershell
# Using the provided deployment script
./deploy/deploy-webapp.ps1
```

### Prerequisites for Deployment
- Azure subscription with appropriate permissions
- Resource group created
- AppService plan and Web App created for hosting the API

## Testing

### Using Test Console App

```bash
cd src/TestConsole
dotnet run
```

The test console provides options to:
- Test document analysis with different documents
- Verify Azure Foundry connectivity
- Test Microsoft Graph integration

## Development

### Setting Up Development Environment

1. Install .NET 10 SDK
2. Clone the repository
3. Create `appsettings.Development.json` with your local configuration
4. Run `dotnet build` from the `src` directory

### Code Structure

- **Controllers**: Handle HTTP requests and route them to services
- **Services**: Contain business logic for document analysis and AI integration
- **Models**: Define data structures for API requests/responses

## Contributing

Contributions are welcome! To contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Security Considerations

⚠️ **Important Security Notes:**

- Never commit `appsettings.json` or secrets to version control
- Use Azure Key Vault for storing sensitive configuration in production
- Ensure proper RBAC is configured for SharePoint access
- Regularly rotate Azure AD client secrets
- Monitor API usage and authentication logs

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This project is provided "as-is" without any guarantees or warranty. It is intended for demonstration and educational purposes. Use at your own risk. Always test thoroughly in a development environment before deploying to production.

## Support & Contact

For issues, questions, or contributions, please open an issue on the GitHub repository.

## Acknowledgments

A special thanks to the development team at Centre de services scolaire des Trois-Lacs for their contributions to this project.