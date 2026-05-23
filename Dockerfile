FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HAGSS.Contracts/HAGSS.Contracts.csproj", "HAGSS.Contracts/"]
COPY ["HAGSS/HAGSS.csproj", "HAGSS/"]
RUN dotnet restore "HAGSS/HAGSS.csproj"
COPY HAGSS.Contracts/ HAGSS.Contracts/
COPY HAGSS/ HAGSS/
# Ensure no local build artifacts (e.g. Rider "bin\Debug") pollute the Linux publish
RUN rm -rf HAGSS/bin HAGSS/obj HAGSS/bin\\Debug HAGSS/bin/Debug
RUN dotnet publish "HAGSS/HAGSS.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --retries=5 CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "HAGSS.dll"]
