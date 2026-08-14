# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.9-noble AS build
WORKDIR /src
COPY ["Axiom.Atlas.API/Axiom.Atlas.API.csproj", "Axiom.Atlas.API/"]
COPY ["Axiom.Atlas.Application/Axiom.Atlas.Application.csproj", "Axiom.Atlas.Application/"]
COPY ["Axiom.Atlas.Domain/Axiom.Atlas.Domain.csproj", "Axiom.Atlas.Domain/"]
COPY ["Axiom.Atlas.Infrastructure/Axiom.Atlas.Infrastructure.csproj", "Axiom.Atlas.Infrastructure/"]
COPY ["Axiom.Atlas.Persistence/Axiom.Atlas.Persistence.csproj", "Axiom.Atlas.Persistence/"]
RUN dotnet restore "Axiom.Atlas.API/Axiom.Atlas.API.csproj"
COPY . .
ARG VERSION=0.0.0
ARG REVISION=unknown
RUN dotnet publish "Axiom.Atlas.API/Axiom.Atlas.API.csproj" --configuration Release --no-restore --output /app/publish \
    /p:Version=$VERSION /p:InformationalVersion=$VERSION+$REVISION /p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/axiom-atlas/keys /tmp \
    && chown -R app:app /app /var/lib/axiom-atlas /tmp
COPY --from=build --chown=app:app /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "Axiom.Atlas.API.dll"]
