FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HAGSS/HAGSS.csproj", "HAGSS/"]
RUN dotnet restore "HAGSS/HAGSS.csproj"
COPY HAGSS/ HAGSS/
# Ensure no local build artifacts (e.g. Rider "bin\Debug") pollute the Linux publish
RUN rm -rf HAGSS/bin HAGSS/obj HAGSS/bin\\Debug HAGSS/bin/Debug
RUN dotnet publish "HAGSS/HAGSS.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HAGSS.dll"]
