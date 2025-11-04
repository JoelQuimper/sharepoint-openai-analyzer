using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Graph;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Add Graph service
builder.Services.AddScoped(sp =>
{
    var options = new TokenCredentialOptions
    {
        AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
    };

    var clientSecret = builder.Configuration["AzureAd:ClientSecret"];
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];

    var clientSecretCredential = new ClientSecretCredential(
        tenantId, clientId, clientSecret, options);

    var graphScopes = builder.Configuration.GetSection("GraphInfo:Scopes").Get<string[]>();

    return new GraphServiceClient(clientSecretCredential, graphScopes);
});

builder.Services.AddScoped(serviceProvider =>
{
    var endpoint = builder.Configuration["AzureOpenAI:Endpoint"];
    var deployment = builder.Configuration["AzureOpenAI:DeploymentName"];
    var credential = new DefaultAzureCredential();

    var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
    var chatClient = azureClient.GetChatClient(deployment);
    return chatClient;
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
