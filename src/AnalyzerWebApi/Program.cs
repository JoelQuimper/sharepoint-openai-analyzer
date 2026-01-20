using AnalyzerWebApi.Service;
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
    
builder.Services.AddScoped<IDocumentService, DocumentService>(
    sp =>
        new DocumentService(
            builder.Configuration["MicrosoftFoundry:Endpoint"] ?? throw new InvalidOperationException("MicrosoftFoundry:Endpoint is not configured."),
            builder.Configuration["MicrosoftFoundry:DeploymentName" ] ?? throw new InvalidOperationException("MicrosoftFoundry:DeploymentName is not configured."),
            sp.GetRequiredService<ILogger<DocumentService>>()
        )
);

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
