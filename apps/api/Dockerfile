FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/PoolPredict.Api.csproj apps/api/
RUN dotnet restore apps/api/PoolPredict.Api.csproj
COPY apps/api apps/api
RUN dotnet publish apps/api/PoolPredict.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PoolPredict.Api.dll"]
