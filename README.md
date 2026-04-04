# AI RAG Search

This project is a demonstration of a Retrieval-Augmented Generation (RAG) pipeline using .NET, Elasticsearch, and OpenAI.

## Features

- Indexes a list of movies into an Elasticsearch index.
- Generates vector embeddings for movie overviews using OpenAI.
- Performs a hybrid search (k-NN and keyword) to find relevant movies.
- Uses a RAG pipeline to answer questions about the movies.

## Prerequisites

- .NET 8 SDK
- Docker
- An OpenAI API key

## Getting Started

1. **Clone the repository:**
   ```bash
   git clone https://github.com/nawedx/ai-rag-search.git
   ```
2. **Set the OpenAI API key:**
   Set the `OPENAI_API_KEY` environment variable to your OpenAI API key.
3. **Start the services:**
   ```bash
   docker-compose up -d
   ```
4. **Run the indexer:**
   ```bash
   dotnet run --project SearchSprint.Indexer/SearchSprint.Indexer.csproj
   ```
