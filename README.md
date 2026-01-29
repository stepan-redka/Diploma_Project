# RAG Web Demo

A lightweight Retrieval-Augmented Generation (RAG) system built with ASP.NET MVC and Microsoft Semantic Kernel.

## Architecture

This is a **hybrid RAG system** that uses:

- **Qdrant** (Docker) - Vector database for storing document embeddings
- **Ollama** (local) - Free embedding generation using `nomic-embed-text` model
- **Google Gemini API** - Answer synthesis and generation

## Prerequisites

1. **Docker** - For running Qdrant vector database
2. **Ollama** - Local LLM runtime for embeddings
3. **Google Gemini API Key** - For answer generation

## Setup Instructions

### 1. Start Qdrant (Vector Database)

```bash
docker-compose up -d
```

This starts Qdrant on:
- REST API: http://localhost:6333
- gRPC API: http://localhost:6334 (used by the app)

### 2. Install Ollama and Pull Embedding Model

```bash
# Install Ollama (if not already installed)
curl -fsSL https://ollama.com/install.sh | sh

# Pull the embedding model
ollama pull nomic-embed-text

# Verify Ollama is running
ollama list
```

### 3. Configure Gemini API Key

Edit `appsettings.json` and replace `YOUR_GEMINI_API_KEY_HERE` with your actual API key:

```json
"Gemini": {
  "ApiKey": "your-actual-api-key",
  "ModelId": "gemini-2.0-flash"
}
```

Get a free API key at: https://makersuite.google.com/app/apikey

### 4. Run the Application

```bash
dotnet run
```

Navigate to https://localhost:5001 (or the port shown in console)

## Usage

### Ingest Documents

1. Enter a document name (optional)
2. Paste your text content
3. Click "Ingest Document"

The system will:
- Chunk the text into overlapping segments
- Generate embeddings using Ollama (nomic-embed-text)
- Store vectors in Qdrant

### Query the Knowledge Base

1. Enter your question
2. Adjust the number of sources (1-10)
3. Click "Ask"

The system will:
- Generate embedding for your question
- Search Qdrant for similar chunks
- Send context + question to Gemini
- Return the synthesized answer with sources

## Project Structure

```
RagWebDemo/
├── Controllers/
│   └── HomeController.cs      # API endpoints for ingest/query
├── Models/
│   ├── RagConfiguration.cs    # Configuration classes
│   └── RagModels.cs           # Request/Response models
├── Services/
│   └── RagService.cs          # Core RAG logic (chunking, embedding, search)
├── Views/
│   └── Home/
│       └── Index.cshtml       # UI for document upload and querying
├── Program.cs                  # DI setup for Semantic Kernel
├── appsettings.json           # Configuration
└── docker-compose.yml         # Qdrant container setup
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Qdrant.Host` | Qdrant server host | `localhost` |
| `Qdrant.Port` | Qdrant gRPC port | `6334` |
| `Qdrant.CollectionName` | Vector collection name | `documents` |
| `Qdrant.VectorSize` | Embedding dimension | `768` |
| `Ollama.Endpoint` | Ollama API endpoint | `http://localhost:11434` |
| `Ollama.EmbeddingModel` | Embedding model name | `nomic-embed-text` |
| `Gemini.ModelId` | Gemini model to use | `gemini-2.0-flash` |
| `Chunking.MaxChunkSize` | Max characters per chunk | `500` |
| `Chunking.ChunkOverlap` | Overlap between chunks | `100` |

## Troubleshooting

### "Connection refused" errors
- Ensure Qdrant is running: `docker-compose ps`
- Ensure Ollama is running: `ollama list`

### Slow embedding generation
- First run downloads the model (~300MB)
- Subsequent requests are faster

### Empty search results
- Ingest documents first
- Try lowering the relevance threshold in `RagService.cs`

## License

MIT
# Diploma_Project
