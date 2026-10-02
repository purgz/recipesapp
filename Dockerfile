# --- Angular build ---
FROM node:22-alpine AS web
WORKDIR /web
COPY src/app/package*.json ./
RUN npm ci
COPY src/app/ ./
RUN npm run build

# --- API build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/api/*.csproj ./
RUN dotnet restore
COPY src/api/ ./
RUN dotnet publish -c Release -o /out

# --- Runtime (single process: API + Angular from wwwroot) ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /web/dist/app/browser ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "RecipeApp.Api.dll"]
