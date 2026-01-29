using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using RagWebDemo.Models;
using RagWebDemo.Services;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0070

var builder = WebApplication.CreateBuilder(args);

// Load local secrets if available (not committed to git)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

// Register Ollama embedding generator using Microsoft.Extensions.AI
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    return new OllamaEmbeddingGenerator(
        new Uri(ragConfig.Ollama.Endpoint), 
        ragConfig.Ollama.EmbeddingModel);
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
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
