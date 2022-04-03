FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["TwitchSrBot/TwitchSrBot.csproj", "TwitchSrBot/"]
RUN dotnet restore "TwitchSrBot/TwitchSrBot.csproj"
COPY . .
WORKDIR "/src/TwitchSrBot"
RUN dotnet build "TwitchSrBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TwitchSrBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TwitchSrBot.dll"]
