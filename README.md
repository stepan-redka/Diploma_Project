# RAG Web Demo

A Retrieval-Augmented Generation (RAG) system built with ASP.NET MVC and Microsoft Semantic Kernel. Fully local implementation with no external API dependencies.

## Architecture

**Stack:**
- Qdrant (Docker) - Vector database
- Ollama - Embeddings (nomic-embed-text) and chat (llama3.2:3b)
- ASP.NET Core MVC - Web interface

**Structure:**
```
Core/              Domain layer (entities, interfaces, models)
Infrastructure/    AI services and business logic
Web/               Controllers, views, static files
```

## Setup

**1. Start Qdrant**
```bash
docker-compose up -d
```

**2. Install Ollama and models**
```bash
curl -fsSL https://ollama.com/install.sh | sh
ollama pull nomic-embed-text
ollama pull llama3.2:3b
```

**3. Run application**
```bash
dotnet run
```

Access at http://localhost:5000

## Usage

**Ingest documents:** Home page - paste text or upload files (.txt, .pdf, .docx, .md, .html)

**Query knowledge base:** Enter question, adjust source count, get answer with citations

**Manage database:** `/Database` endpoint - view, search, delete chunks

## Configuration

Edit `appsettings.json` or `appsettings.Local.json`:

```json
{
  "RagConfiguration": {
    "Qdrant": { "Host": "localhost", "Port": 6334 },
    "Ollama": { 
      "Endpoint": "http://localhost:11434",
      "EmbeddingModel": "nomic-embed-text",
      "ChatModel": "llama3.2:3b"
    },
    "Chunking": { "MaxChunkSize": 500, "ChunkOverlap": 100 }
  }
}
```

## Troubleshooting

**Qdrant connection errors:** Verify container is running with `docker ps`

**Ollama connection errors:** Check service with `ollama list`

**Empty search results:** Ingest documents first, check at `/Database`

**Views not found:** Run from project root, rebuild if needed

## License

MIT
