using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Qdrant.Client;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;
using RagWebDemo.Infrastructure.Services;
using RagWebDemo.Infrastructure.AI;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0070

var builder = WebApplication.CreateBuilder(args);

// Load local secrets if available (not committed to git)
builder.Configuration.AddJsonFile(Path.Combine("Web", "appsettings.Local.json"), optional: true, reloadOnChange: true);

// Add services to the container with Razor view location configuration
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        // Configure Razor to look for views in Web/Views
        options.ViewLocationFormats.Clear();
        options.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
        
        options.AreaViewLocationFormats.Clear();
        options.AreaViewLocationFormats.Add("/Web/Areas/{2}/Views/{1}/{0}.cshtml");
        options.AreaViewLocationFormats.Add("/Web/Areas/{2}/Views/Shared/{0}.cshtml");
        options.AreaViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    });

// Bind RAG configuration from appsettings.json
builder.Services.Configure<RagConfiguration>(
    builder.Configuration.GetSection("RagConfiguration"));

var ragConfig = builder.Configuration.GetSection("RagConfiguration").Get<RagConfiguration>() 
    ?? new RagConfiguration();

// Register HttpClientFactory for Ollama
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ragConfig.Ollama.Endpoint);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Build Semantic Kernel with Google Gemini for chat completion
var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070
kernelBuilder.AddGoogleAIGeminiChatCompletion(
    modelId: ragConfig.Gemini.ModelId,
    apiKey: ragConfig.Gemini.ApiKey
);
#pragma warning restore SKEXP0070

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);

// Register Qdrant client
builder.Services.AddSingleton<QdrantClient>(sp => 
    new QdrantClient(ragConfig.Qdrant.Host, ragConfig.Qdrant.Port));

// Register HttpClient for Ollama
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ragConfig.Ollama.Endpoint);
});

// Register Ollama embedding generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    return new OllamaEmbeddingService(httpClient, ragConfig.Ollama.EmbeddingModel);
});

// Register Ollama chat service
builder.Services.AddScoped<IOllamaChatService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    var logger = sp.GetRequiredService<ILogger<OllamaChatService>>();
    return new OllamaChatService(httpClient, ragConfig.Ollama.ChatModel, logger);
});

// Register Document Parser Service
builder.Services.AddScoped<IDocumentParserService, DocumentParserService>();

// Register RagService
builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files to use Web/wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Web", "wwwroot")),
    RequestPath = ""
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
