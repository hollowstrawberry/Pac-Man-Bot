#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["PacManBot.csproj", "."]
RUN dotnet restore "./PacManBot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "PacManBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PacManBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY contents.json .
COPY config.json .

# This was to get the old data from Pacman
#COPY games ./games/
#COPY database.sqlite .

ENTRYPOINT ["dotnet", "Pacman.dll"]
