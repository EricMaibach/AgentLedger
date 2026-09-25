# The AgentLedger API as a container image. Multi-stage: compile with the SDK image, run on the smaller
# ASP.NET runtime image, which holds no compilers or source. Build from the repository root:
#   docker build -t agentledger-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only: this layer is reused until a project file changes,
# so ordinary code edits don't re-download packages.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/AgentLedger.Core/AgentLedger.Core.csproj src/AgentLedger.Core/
COPY src/AgentLedger.UseCases/AgentLedger.UseCases.csproj src/AgentLedger.UseCases/
COPY src/AgentLedger.Infrastructure/AgentLedger.Infrastructure.csproj src/AgentLedger.Infrastructure/
COPY src/AgentLedger.ServiceDefaults/AgentLedger.ServiceDefaults.csproj src/AgentLedger.ServiceDefaults/
COPY src/AgentLedger.Web/AgentLedger.Web.csproj src/AgentLedger.Web/
RUN dotnet restore src/AgentLedger.Web/AgentLedger.Web.csproj

COPY src/ src/
# Stamped in by the release workflow; /status reports them as "<version>+<commit>". The defaults mark local builds.
ARG VERSION=0.0.0-local
ARG SOURCE_REVISION=unknown
RUN dotnet publish src/AgentLedger.Web/AgentLedger.Web.csproj --configuration Release --no-restore --output /app \
      -p:Version=$VERSION -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Listens on 8080 (the image's default). The connection string comes from the environment:
#   ConnectionStrings__AgentLedger=Host=...;Database=...;Username=...;Password=...
ENV Database__ApplyMigrationsOnStartup=true

# The non-root user the Microsoft images provide.
USER $APP_UID

ENTRYPOINT ["dotnet", "AgentLedger.Web.dll"]
