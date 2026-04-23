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
- Running integration tests against the API

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

## Deployment

### Deploy web api to Azure

```powershell
# Using the provided deployment script
./deploy/deploy-webapp.ps1
```

### Prerequisites for Deployment
- Azure subscription with appropriate permissions
- Resource group created
- AppService plan and Web App created for hosting the API

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This project is provided "as-is" without any guarantees or warranty. It is intended for demonstration and educational purposes. Use at your own risk. Always test thoroughly in a development environment before deploying to production.

## Support & Contact

For issues, questions, or contributions, please open an issue on the GitHub repository.

## Acknowledgments

A special thanks to the development team at Centre de services scolaire des Trois-Lacs for their contributions to this project.