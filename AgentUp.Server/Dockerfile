FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends xvfb libx11-6 libxtst6 xfonts-base \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /var/lib/agent-up && chown -R $APP_UID /var/lib/agent-up
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["AgentUp.Server/AgentUp.Server.csproj", "AgentUp.Server/"]
COPY ["AgentUp.Browser.Streaming/AgentUp.Browser.Streaming.csproj", "AgentUp.Browser.Streaming/"]
COPY ["AgentUp.CommitPolicy/AgentUp.CommitPolicy.csproj", "AgentUp.CommitPolicy/"]
COPY ["AgentUp.Capabilities.Abstractions/AgentUp.Capabilities.Abstractions.csproj", "AgentUp.Capabilities.Abstractions/"]
COPY ["AgentUp.Capabilities.Common/AgentUp.Capabilities.Common.csproj", "AgentUp.Capabilities.Common/"]
COPY ["AgentUp.Capabilities.Dotnet/AgentUp.Capabilities.Dotnet.csproj", "AgentUp.Capabilities.Dotnet/"]
COPY ["AgentUp.Capabilities.Docker/AgentUp.Capabilities.Docker.csproj", "AgentUp.Capabilities.Docker/"]
COPY ["AgentUp.InstallerConfig/AgentUp.InstallerConfig.csproj", "AgentUp.InstallerConfig/"]
RUN dotnet restore "AgentUp.Server/AgentUp.Server.csproj"
COPY . .
WORKDIR "/src/AgentUp.Server"
RUN dotnet build "AgentUp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AgentUp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV Storage__DataDirectory=/var/lib/agent-up
ENV DOTNET_CONTENTROOT=/app
ENV ASPNETCORE_CONTENTROOT=/app
ENTRYPOINT ["dotnet", "AgentUp.Server.dll"]
