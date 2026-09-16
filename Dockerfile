FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["BookShelf.csproj", "./"]
RUN dotnet restore "BookShelf.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src"
RUN dotnet build "BookShelf.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "BookShelf.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway provides the PORT environment variable.
# We set ASPNETCORE_URLS so the app listens on the correct port.
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

ENTRYPOINT ["dotnet", "BookShelf.dll"]
