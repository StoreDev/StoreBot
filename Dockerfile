# https://hub.docker.com/_/microsoft-dotnet-core
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY *.csproj .
COPY nuget.config .
RUN dotnet restore --configfile nuget.config

# copy everything else and build app
COPY . .
WORKDIR /source
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:5.0	
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "StoreBot.dll"]
