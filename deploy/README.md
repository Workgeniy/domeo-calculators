# Развёртывание на тестовом стенде

Калькуляторы живут на уже существующей ВМ `domeo-testdesign` внутри compose-проекта
`domeo-stage`, рядом с backend. Отдельной инфраструктуры нет.

**Адрес:** https://testdesign.domeohome.ru/calc/ — открыт, без авторизации.

## Что добавлено на стенде

Сервис в `/home/ubuntu/domeo/stage/docker-compose.yml`:

```yaml
  calc:
    image: ghcr.io/workgeniy/domeo-calculators:${CALC_TAG:-live}
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      ASPNETCORE_URLS: "http://+:8080"
      PathBase: "/calc"
    restart: unless-stopped
```

Без БД и без состояния: считает в памяти, справочники вшиты в образ.

Локейшен в `/home/ubuntu/domeo/stage/nginx.conf`, внутри блока `server` на 443:

```nginx
    location = /calc { return 301 /calc/; }

    location /calc/ {
        resolver 127.0.0.11 valid=10s ipv6=off;
        set $calc_upstream http://calc:8080;
        proxy_pass $calc_upstream$request_uri;
        ...
    }
```

**Почему через resolver, а не `proxy_pass http://calc:8080`.** С литеральным именем nginx
резолвит upstream на старте: если контейнер `calc` не поднялся, nginx не стартует и падает
**весь стенд**, включая backend. Через встроенный DNS docker имя резолвится в рантайме —
при лежащем `calc` отдаётся 502 только на `/calc/`, остальное живёт.

`PathBase` в приложении срезает префикс, страница обращается к API относительными путями,
поэтому один и тот же образ работает и на `/calc/`, и локально на корне.

## Обновить версию

Собираем **на самой ВМ**: в образе теперь Chromium, целиком он весит ~1.3 ГБ, и
пересылать его на каждый выкат через `docker save` стало слишком долго. Исходники
весят ~200 КБ, а слой с Chromium на ВМ закеширован, поэтому пересборка быстрая.

```bash
tar -czf /tmp/calc-src.tgz Dockerfile .dockerignore src/Domeo.Calculators.Web
scp /tmp/calc-src.tgz testvm:~/calc-build/
ssh testvm "cd ~/calc-build && tar -xzf calc-src.tgz && docker build -t ghcr.io/workgeniy/domeo-calculators:live ."
ssh testvm "cd /home/ubuntu/domeo/stage && docker compose up -d --force-recreate calc"
```

Правильное решение — workflow в GitHub Actions: у него есть `GITHUB_TOKEN` с правом на
пакеты, долгоживущий секрет на машине не нужен. Пока не сделано.

## Откат

Бэкапы конфигов лежат рядом с оригиналами (`docker-compose.yml.bak-<дата>`,
`nginx.conf.bak-<дата>`). Полный откат:

```bash
ssh testvm "cd /home/ubuntu/domeo/stage \
  && cp docker-compose.yml.bak-<дата> docker-compose.yml \
  && cp nginx.conf.bak-<дата> nginx.conf \
  && docker compose stop calc && docker compose rm -f calc \
  && docker exec domeo-stage-nginx-1 nginx -t \
  && docker exec domeo-stage-nginx-1 nginx -s reload"
```

Убрать только калькуляторы, не трогая nginx, можно и просто остановив `calc` — тогда
`/calc/` начнёт отдавать 502, а стенд продолжит работать.
