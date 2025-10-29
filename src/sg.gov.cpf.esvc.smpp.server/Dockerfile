# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG FEED_ACCESSTOKEN
RUN wget -qO- https://raw.githubusercontent.com/Microsoft/artifacts-credprovider/master/helpers/installcredprovider.sh | bash
ENV VSS_NUGET_EXTERNAL_FEED_ENDPOINTS="{\"endpointCredentials\": [{\"endpoint\":\"https://pkgs.dev.azure.com/CPFB/Website/_packaging/Website/nuget/v3/index.json\", \"username\":\"docker\", \"password\":\"$FEED_ACCESSTOKEN\"}]}"

WORKDIR /
COPY ["nuget.config", "src/sg.gov.cpf.esvc.smpp.server/"]
COPY ["sg.gov.cpf.esvc.smpp.server.csproj", "src/sg.gov.cpf.esvc.smpp.server/"]

RUN dotnet restore -s "https://pkgs.dev.azure.com/CPFB/Website/_packaging/Website/nuget/v3/index.json" "src/sg.gov.cpf.esvc.smpp.server/sg.gov.cpf.esvc.smpp.server.csproj"
COPY . /src/sg.gov.cpf.esvc.smpp.server/
WORKDIR "/src/sg.gov.cpf.esvc.smpp.server/"
RUN dotnet build "sg.gov.cpf.esvc.smpp.server.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "sg.gov.cpf.esvc.smpp.server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/sg.gov.cpf.esvc.smpp.server"

ENV ASPNETCORE_URLS=http://+:8080
ENV COMPlus_EnableDiagnostics=0
COPY ["Certs/", "/usr/local/share/ca-certificates/"]
RUN chmod 644 /usr/local/share/ca-certificates/ && /usr/sbin/update-ca-certificates

WORKDIR /app

RUN set -eux; \
    chown -R app /app
USER app  

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 2775

ENTRYPOINT ["dotnet", "sg.gov.cpf.esvc.smpp.server.dll"]