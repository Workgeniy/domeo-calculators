# Сборка
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Веб-проект снова зависит от библиотеки (нормировочная модель),
# а её справочники вшиваются из data/ как EmbeddedResource.
COPY data/ data/
COPY src/Domeo.Calculators/ src/Domeo.Calculators/
COPY src/Domeo.Calculators.Web/ src/Domeo.Calculators.Web/

RUN dotnet publish src/Domeo.Calculators.Web/Domeo.Calculators.Web.csproj \
    -c Release -o /app/publish

# Рантайм
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Отчёт — это снимок самой страницы, поэтому нужен браузер.
# Шрифты отдельно: в образе .NET нет ни одного с кириллицей,
# а без noto-color-emoji иконки комнат печатаются квадратами.
RUN apt-get update \
 && apt-get install -y --no-install-recommends chromium fonts-dejavu-core fonts-noto-color-emoji \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "Domeo.Calculators.Web.dll"]
