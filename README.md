<div align="center">

# Velocity - Download Manager

**Лёгкий portable download manager для Windows 11**

[![Release](https://img.shields.io/github/v/release/NEONARCHY/velocity-download?style=flat-square&color=0078d4)](https://github.com/NEONARCHY/velocity-download/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/NEONARCHY/velocity-download/build.yml?style=flat-square&label=build)](https://github.com/NEONARCHY/velocity-download/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/NEONARCHY/velocity-download?style=flat-square)](LICENSE)

Несколько загрузок одновременно, до 16 соединений на файл, пауза и продолжение — в аккуратном Fluent-интерфейсе.

[Скачать portable EXE](https://github.com/NEONARCHY/velocity-download/releases/latest) · [Сообщить об ошибке](https://github.com/NEONARCHY/velocity-download/issues/new?template=bug_report.yml)

</div>

![Velocity Download — интерфейс Windows 11](docs/screenshot.png)

## Возможности

- Одновременная загрузка нескольких файлов.
- 1, 4, 8 или 16 соединений на файл; в режиме «Авто» используется 8.
- Пауза и продолжение загрузки, в том числе после перезапуска приложения.
- Автоматический переход на один поток, если сервер не поддерживает загрузку частями.
- Диагностика текущей и пиковой скорости, загрузки интернет-тарифа и вероятного лимита сервера.
- Временные части хранятся отдельно от папки загрузок — Проводник не дёргается во время записи файла.
- Кэш автоматически удаляется после завершения или удаления незаконченной загрузки.
- Один автономный `.exe`: установка и отдельный .NET Runtime не нужны.
- Интерфейс в стиле Windows 11 Fluent с Mica, системной типографикой и поддержкой DPI.

## Быстрый старт

1. Скачайте `VelocityDownload-v2.4-portable.exe` из [последнего релиза](https://github.com/NEONARCHY/velocity-download/releases/latest).
2. Запустите файл, вставьте одну или несколько прямых HTTP/HTTPS-ссылок.
3. Выберите папку и нажмите **«Добавить»**.

> Приложению нужна именно прямая ссылка на файл. Страницы сайтов, ссылки с обязательной авторизацией, cookies или CAPTCHA могут не работать.

## Скорость и ограничения серверов

Velocity Download использует доступную пропускную способность соединения и многопоточную загрузку, когда сервер её разрешает. Приложение не обходит ограничения скорости, установленные сервером, аккаунтом, провайдером или Wi‑Fi. Диагностическая строка помогает понять, где вероятнее всего находится ограничение.

## Кэш и приватность

Незавершённые данные находятся в `%LOCALAPPDATA%\VelocityDownload\Cache`. После успешной загрузки временные файлы удаляются автоматически. Кнопка `×` удаляет выбранную задачу вместе только с её кэшем; обычная пауза сохраняет данные для продолжения.

Приложение не содержит телеметрии и не отправляет историю ссылок сторонним сервисам.

## Сборка из исходников

Требуются Windows и [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Готовый файл появится в `bin/Release/net10.0-windows/win-x64/publish/`.

## Проверка файла

Для официального portable EXE версии 2.4:

```text
SHA-256: 9C7716B1A2AEEF6AF465566FDEF587FEEFA0AAD3A2AD5A6A8F63FB2311069230
```

```powershell
Get-FileHash .\VelocityDownload-v2.4-portable.exe -Algorithm SHA256
```

## Лицензия

Исходный код распространяется по лицензии [MIT](LICENSE).

