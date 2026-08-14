# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src
COPY ["Axiom.Atlas.Web/Axiom.Atlas.Web.csproj", "Axiom.Atlas.Web/"]
COPY ["Axiom.Atlas.Application/Axiom.Atlas.Application.csproj", "Axiom.Atlas.Application/"]
COPY ["Axiom.Atlas.Domain/Axiom.Atlas.Domain.csproj", "Axiom.Atlas.Domain/"]
RUN dotnet restore "Axiom.Atlas.Web/Axiom.Atlas.Web.csproj"
COPY . .
ARG VERSION=0.0.0
ARG REVISION=unknown
RUN dotnet publish "Axiom.Atlas.Web/Axiom.Atlas.Web.csproj" --configuration Release --no-restore --output /app/publish \
    /p:Version=$VERSION /p:InformationalVersion=$VERSION+$REVISION /p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates fontconfig fonts-dejavu-core libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/axiom-atlas/keys /tmp \
    && chown -R app:app /app /var/lib/axiom-atlas /tmp
COPY --from=build --chown=app:app /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "Axiom.Atlas.Web.dll"]
