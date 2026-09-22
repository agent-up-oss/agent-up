FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
USER root
COPY ["AgentUp.Server/install-runtime-tools.sh", "/tmp/install-runtime-tools.sh"]
COPY ["AgentUp.Server/runtime-tools/", "/tmp/runtime-tools/"]
RUN chmod +x /tmp/install-runtime-tools.sh \
    && /tmp/install-runtime-tools.sh /tmp/runtime-tools \
    && rm /tmp/install-runtime-tools.sh \
    && rm -rf /tmp/runtime-tools \
    && apt-get update \
    && apt-get install -y --no-install-recommends xvfb libx11-6 libxtst6 xfonts-base xz-utils \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/agent-up /nix /home/app/.config/nix \
    && chown -R $APP_UID:$APP_UID /var/lib/agent-up /nix /home/app
ENV HOME=/home/app
ENV USER=app
ENV PATH="/home/app/.nix-profile/bin:/nix/var/nix/profiles/default/bin:/opt/agent-up/bin:${PATH}"
ENV NIX_SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt
ENV AGENTUP_CAPABILITY_REGISTRY_PATH=/var/lib/agent-up/capability-registry
ENV AGENTUP_CAPABILITY_ENABLED_PATH=/var/lib/agent-up/capabilities/enabled.json
USER $APP_UID
RUN curl -L https://nixos.org/nix/install | sh -s -- --no-daemon \
    && printf 'filter-syscalls = false\nsandbox = false\n' > /home/app/.config/nix/nix.conf

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["AgentUp.Server/AgentUp.Server.csproj", "AgentUp.Server/"]
COPY ["AgentUp.Browser.Streaming/AgentUp.Browser.Streaming.csproj", "AgentUp.Browser.Streaming/"]
COPY ["AgentUp.CommitPolicy/AgentUp.CommitPolicy.csproj", "AgentUp.CommitPolicy/"]
COPY ["AgentUp.Verification/AgentUp.Verification.csproj", "AgentUp.Verification/"]
COPY ["AgentUp.Capabilities.Abstractions/AgentUp.Capabilities.Abstractions.csproj", "AgentUp.Capabilities.Abstractions/"]
COPY ["AgentUp.Capabilities.Common/AgentUp.Capabilities.Common.csproj", "AgentUp.Capabilities.Common/"]
COPY ["AgentUp.Registry/AgentUp.Registry.csproj", "AgentUp.Registry/"]
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
