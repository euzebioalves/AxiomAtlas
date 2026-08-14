# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.9-noble AS build
WORKDIR /src
COPY ["Axiom.Atlas.Migrator/Axiom.Atlas.Migrator.csproj", "Axiom.Atlas.Migrator/"]
COPY ["Axiom.Atlas.Persistence/Axiom.Atlas.Persistence.csproj", "Axiom.Atlas.Persistence/"]
COPY ["Axiom.Atlas.Domain/Axiom.Atlas.Domain.csproj", "Axiom.Atlas.Domain/"]
RUN dotnet restore "Axiom.Atlas.Migrator/Axiom.Atlas.Migrator.csproj"
COPY . .
ARG VERSION=0.0.0
ARG REVISION=unknown
RUN dotnet publish "Axiom.Atlas.Migrator/Axiom.Atlas.Migrator.csproj" --configuration Release --no-restore --output /app/publish \
    /p:Version=$VERSION /p:InformationalVersion=$VERSION+$REVISION /p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/runtime:10.0.9-noble AS final
WORKDIR /app
RUN mkdir -p /tmp && chown -R app:app /app /tmp
COPY --from=build --chown=app:app /app/publish .
USER app
ENTRYPOINT ["dotnet", "Axiom.Atlas.Migrator.dll"]
