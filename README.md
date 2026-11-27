# Algoref Landing

Бекенд конструктора лендингов на C#, ASP.NET Core 10, EF Core и PostgreSQL 17. Партнёр сохраняет черновик, публикует ревизию и получает адрес API, по которому фронтенд может отрисовать страницу.

## Локальный запуск через Docker

Запустите Docker Desktop. Если API работает через локальный скрипт, остановите его нажатием Ctrl+C для освобождения порта 5180. Из корня проекта выполните:

    docker compose up -d --build
    docker compose run --rm seed

Compose запускает PostgreSQL, применяет миграции отдельным контейнером и запускает API. Postman использует прежний адрес http://127.0.0.1:5180. PostgreSQL доступен с компьютера на порту 55432, из контейнеров — по адресу postgres:5432. Данные Docker хранятся в томе landing_data. У прежнего запуска Windows своя база.

Команда docker compose run --rm seed перечитывает db/seed.sql с диска. Она восстанавливает черновики и активные публикации трёх демостраниц, сохраняя их ID и увеличивая ревизии. Страницы, созданные отдельно в Postman, сохраняются. Если зарезервированный slug уже занят другой страницей, транзакция завершается ошибкой и не меняет данные.

| Slug | ID | Пример |
| --- | --- | --- |
| seed-course | f5e40c57-d0ad-495a-b534-4aa374ec59a1 | Публикация партнёра A. Гость видит заголовок, local-member видит также предложение. |
| seed-draft | f5e40c57-d0ad-495a-b534-4aa374ec59a2 | Черновик партнёра A. Публичный запрос возвращает 404. |
| seed-partner-b | f5e40c57-d0ad-495a-b534-4aa374ec59a3 | Публикация партнёра B. Запрос черновика от local-a возвращает 404. |

В Postman можно отправить GET http://127.0.0.1:5180/api/public/landings/seed-course. Для изменения черновика сначала получите текущую revision через GET /api/landings/{id} с токеном владельца. OpenAPI доступен на /openapi/v1.json. Команда docker compose down останавливает контейнеры и сохраняет том.

## Проверка работающего API

Оставьте API запущенным. Во втором терминале PowerShell из корня проекта выполните:

```powershell
.\scripts\Check-Api.ps1
```

Скрипт проверяет описание OpenAPI, создаёт один демонстрационный лендинг с уникальным slug и проверяет доступ владельца и другого партнёра. Затем он публикует страницу, сравнивает выдачу гостя и участника, меняет черновик и проверяет конфликт ревизий и повторную публикацию. В конце появляется строка `PASS` с адресом опубликованного JSON. Проверочный лендинг остаётся в локальной базе. Если API запущен на другом порту, передайте `-Port 5181`, подставив свой порт.

## Первый запрос

В другом окне PowerShell выполните:

```powershell
$api = 'http://127.0.0.1:5180/api'
$partner = @{ Authorization = 'Bearer local-a' }
$document = @{
    name = 'Курс программирования'
    type = 'course'
    theme = @{ accent = '#7C3AED' }
    blocks = @(
        @{ id = 'intro'; type = 'heading'; props = @{ title = 'Программа курса' } }
        @{ id = 'offer'; type = 'promo'; segments = @('member'); props = @{ title = 'Для участников' } }
    )
}
$slug = "programming-$([guid]::NewGuid().ToString('N').Substring(0,8))"
$body = @{ slug = $slug; content = $document } | ConvertTo-Json -Depth 10
$draft = Invoke-RestMethod "$api/landings" -Method Post -Headers $partner -ContentType 'application/json; charset=utf-8' -Body $body
$publish = @{ revision = $draft.revision } | ConvertTo-Json
Invoke-RestMethod "$api/landings/$($draft.id)/publish" -Method Post -Headers $partner -ContentType application/json -Body $publish
Invoke-RestMethod "$api/public/landings/$slug"
Invoke-RestMethod "$api/public/landings/$slug" -Headers @{ Authorization = 'Bearer local-member' }
```

## Контракт первой версии

| Маршрут | Поведение |
| --- | --- |
| POST /api/landings | Создаёт черновик, возвращает ID и ревизию. Адрес slug уникален. |
| GET /api/landings/{id} | Возвращает владельцу полный черновик и номер опубликованной ревизии. |
| PUT /api/landings/{id} | Принимает `{ revision, content }`, заменяет черновик и увеличивает ревизию. |
| POST /api/landings/{id}/publish | Принимает `{ revision }`, сохраняет снимок и назначает его активным в одной транзакции. |
| GET /api/public/landings/{slug} | Возвращает JSON активной публикации с видимыми блоками в заданном партнёром порядке. |

Идентификаторы блоков стабильны в пределах страницы. Первый набор включает `heading`, `hero`, `coursegrid` и `promo`. Пустой черновик можно сохранить. Для публикации требуется хотя бы один нескрытый блок. Поле `segments` может содержать `guest` или `member`. Пустой список означает показ всем, `hidden` скрывает блок для любой аудитории. Устаревшая ревизия возвращает HTTP 409. Сохранение черновика после публикации не меняет публичную выдачу до следующего вызова publish.

## Что подключить дальше

Локальные идентификаторы `local-a`, `local-b` и `local-member` имитируют двух партнёров и посетителя. Схема предназначена для запуска на localhost в Development. Перед развёртыванием нужны проверка учётных данных продукта и привязка его групп к сегментам. Затем фронтенд сможет подключить редактор и маршрут сайта `/promo/{slug}`. По API заказчика предстоит подключить каталог и обработку регистрации.

В [бэклоге второго разработчика](docs/backend-backlog.md) указаны порядок работы, зависимости и критерии приёмки семи задач.
