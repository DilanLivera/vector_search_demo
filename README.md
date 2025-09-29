# Vector Search Demo

This is a demo application for vector search.

## Create Google authentication client for authentication

1. **Go to [Google Cloud Console](https://console.cloud.google.com/):**

2. **Create or Select a Project:**
    - Click the **Select a project** dropdown.
    - Click **New Project** (or select an existing project).
    - Give it a name and click **Create**.

3. **Enable the OAuth API:**
    - Navigate to **APIs & Services** > **Library**.
    - Search for **"Google Identity Services"** or **"OAuth 2.0"**.
    - Enable it.

4. **Create OAuth 2.0 Credentials:**
    - Go to **APIs & Services** > **Credentials**.
    - Click **Create Credentials** > **OAuth Client ID**.
    - Choose **Application Type**: Select **Web Application**.
    - Under **Authorized JavaScript Origins**, add your web app’s domain (e.g., `https://yourwebsite.com`
      or `http://localhost:3000` for development).
    - Under **Authorized Redirect URIs**, add the callback URL where Google will redirect after authentication (
      e.g., `https://yourwebsite.com/auth/callback` or `http://localhost:3000/auth/callback`).
    - Click **Create**.

5. **Copy the Client ID and Client Secret**
    - After creation, you’ll see the **Client ID** and **Client Secret**. Save these for later use.

## How to run the application

### Prerequisites

- [Qdrant vector database](https://qdrant.tech/)
- Ollama (docker compose)
- Ollama Embedding model([Embedding models](https://ollama.com/blog/embedding-models))

qdrant dashboard: http://localhost:6333/dashboard
aspire dashboard: http://localhost:18888

1. Start Qdrant and Ollama docker containers using the `docker-compose up qdrant ollama` command.
2. If the model does not exist, use the `'docker exec -it ollama ollama pull mxbai-embed-large:335m'` command to pull the model.
3. Run the application. Please note that the application may take a few seconds to start, as the collection is created and seeded during startup.
4. Enter a text value to search.

## Resources

- [Add Auth0 Authentication to Blazor Web Apps](https://auth0.com/blog/auth0-authentication-blazor-web-apps/)
- [ASP.NET Core Blazor authentication state](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/authentication-state)
- [How to Get Started with Qdrant Locally](https://qdrant.tech/documentation/quickstart)
- [Qdrant .NET SDK](https://github.com/qdrant/qdrant-dotnet)
