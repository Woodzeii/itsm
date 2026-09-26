FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY itsm.sln ./
COPY itsm.Domain/itsm.Domain.csproj itsm.Domain/
COPY itsm.Application/itsm.Application.csproj itsm.Application/
COPY itsm.Infrastructure/itsm.Infrastructure.csproj itsm.Infrastructure/
COPY itsm.WebApi/itsm.WebApi.csproj itsm.WebApi/
RUN dotnet restore itsm.WebApi/itsm.WebApi.csproj

COPY . .
RUN dotnet publish itsm.WebApi/itsm.WebApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "itsm.WebApi.dll"]