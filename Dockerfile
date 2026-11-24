# Vinex HR API - container image (used by Railway). The WPF desktop client is not part of this image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer cache): copy only project files.
COPY Directory.Build.props ./
COPY src/Vinex.HR.Domain/Vinex.HR.Domain.csproj src/Vinex.HR.Domain/
COPY src/Vinex.HR.Contracts/Vinex.HR.Contracts.csproj src/Vinex.HR.Contracts/
COPY src/Vinex.HR.Application/Vinex.HR.Application.csproj src/Vinex.HR.Application/
COPY src/Vinex.HR.Infrastructure/Vinex.HR.Infrastructure.csproj src/Vinex.HR.Infrastructure/
COPY src/Vinex.HR.API/Vinex.HR.API.csproj src/Vinex.HR.API/
RUN dotnet restore src/Vinex.HR.API/Vinex.HR.API.csproj

COPY src/ src/
RUN dotnet publish src/Vinex.HR.API/Vinex.HR.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vinex.HR.API.dll"]
