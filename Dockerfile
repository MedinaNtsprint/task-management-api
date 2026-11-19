FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TaskManagement.slnx ./
COPY src/TaskManagement.Api/TaskManagement.Api.csproj src/TaskManagement.Api/packages.lock.json src/TaskManagement.Api/
COPY tests/TaskManagement.Tests/TaskManagement.Tests.csproj tests/TaskManagement.Tests/packages.lock.json tests/TaskManagement.Tests/
RUN dotnet restore TaskManagement.slnx --locked-mode
COPY src/TaskManagement.Api/ src/TaskManagement.Api/
RUN dotnet publish src/TaskManagement.Api/TaskManagement.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskManagement.Api.dll"]
