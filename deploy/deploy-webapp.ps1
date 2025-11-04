# deploy app
param(    
    [Parameter(Mandatory = $true)]
    [string]$resourceGroup,
    
    [Parameter(Mandatory = $true)]
    [string]$webAppName,

    [Parameter(Mandatory = $true)]
    [string]$solutionPath
)

"Starting deployment to Azure Web App: $webAppName in Resource Group: $resourceGroup"
$package = "$solutionPath\AnalyzerWebApi.zip"

"Building and publishing the web application..."
dotnet build $solutionPath\AnalyzerWebApi\AnalyzerWebApi.csproj
dotnet publish $solutionPath\AnalyzerWebApi\AnalyzerWebApi.csproj -c Release

$releasePath = "$solutionPath\AnalyzerWebApi\bin\Release\net8.0\publish"

# Remove appsettings.Development.json if it exists in the publish folder
"Verifying presence of appsettings.Development.json for removal..."
$devSettingsPath = "$releasePath\appsettings.Development.json"
if (Test-Path $devSettingsPath) {
    Remove-Item $devSettingsPath -Force
    "Removed appsettings.Development.json from publish output"
}

"Creating deployment package at $package ..."
Compress-Archive -Path $releasePath\* -DestinationPath $package

"Logging into Azure..."
az login
az webapp deploy --resource-group $resourceGroup --name $webAppName --src-path $package
"Deployment completed."

if (Test-Path $package) {
    Remove-Item $package -Force
    "Removed temporary package file"
}