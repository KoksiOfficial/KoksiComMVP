FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/KoksiCom.Api/KoksiCom.Api.csproj src/KoksiCom.Api/
RUN dotnet restore src/KoksiCom.Api/KoksiCom.Api.csproj
COPY src/KoksiCom.Api/ src/KoksiCom.Api/
RUN dotnet publish src/KoksiCom.Api/KoksiCom.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# The base image also sets ASPNETCORE_HTTP_PORTS=8080. Clear it so only this URL binds.
ENV ASPNETCORE_HTTP_PORTS=
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "KoksiCom.Api.dll"]
