# Сборка
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Справочники нужны на этапе сборки: они вшиваются в библиотеку как EmbeddedResource.
COPY data/ data/
COPY src/Domeo.Calculators/ src/Domeo.Calculators/
COPY src/Domeo.Calculators.Web/ src/Domeo.Calculators.Web/

RUN dotnet publish src/Domeo.Calculators.Web/Domeo.Calculators.Web.csproj \
    -c Release -o /app/publish

# Рантайм
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "Domeo.Calculators.Web.dll"]
