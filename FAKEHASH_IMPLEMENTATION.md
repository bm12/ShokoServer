# 📋 Отчёт о реализации FakeHash API

## 📦 Общая информация о коммитах

| Коммит | Дата | Описание |
|--------|------|----------|
| `900ac981b` | 28 Jan 2026 | feat: add FakeHash API endpoint for storing pre-computed file hashes and metadata |
| `cd30af627` | 28 Jan 2026 | fix: create fake hash file location when missing |

---

## 🎯 Цель изменений

Добавить минимально инвазивный публичный admin-API, который позволяет подложить фейковые хэши и метаданные файла в Shoko (ED2K/CRC32/MD5/SHA1 + FileSize + даты), **не меняя существующую логику хэширования/сканирования**, чтобы форк легко обновлялся с upstream.

**Use case:** Файлы добавляются в папку, но ещё не прохэшированы (хэширование отключено через `settings-server.json` → `LimitedConcurrencyOverrides.HashFileJob = 1`). Внешний скрипт/инструмент вычисляет хэши и передаёт их в Shoko через этот API.

---

## 📁 Добавленные/изменённые файлы

### 1. `Shoko.Server/API/v3/Controllers/FakeHashController.cs` (НОВЫЙ ФАЙЛ — 193 строки)

**Назначение:** Основной контроллер API endpoint'а.

**Endpoint:** `POST /api/v3/FakeHash`

**Авторизация:** `[Authorize("admin")]` — только для админов.

**Ключевые методы:**

| Метод | Назначение |
|-------|------------|
| `AddFakeHashes()` | Основной метод обработки запроса |
| `TryGetRelativePath()` | Преобразует путь файла в относительный путь внутри import folder |
| `NormalizeHash()` | Нормализует хэши (trim + uppercase) |
| `SaveFileNameHash()` | Сохраняет FileNameHash запись (копия логики из HashFileJob) |

**Алгоритм работы `AddFakeHashes()`:**

1. **Валидация входных данных:**
   - Проверка наличия body
   - Проверка: либо `fileID`, либо `importFolderID + filePath`
   - Проверка наличия hashes
   - Валидация ModelState

2. **Поиск/создание VideoLocal:**
   - Если передан `fileID` → ищем существующий `VideoLocal` по ID
   - Если найден → берём связанный `VideoLocal_Place`

3. **Поиск/создание VideoLocal_Place (если не найден через fileID):**
   - Проверяем наличие `importFolderID + filePath`
   - Находим ImportFolder по ID
   - Преобразуем путь в относительный через `TryGetRelativePath()`
   - Ищем существующий `VideoLocal_Place` по пути и ImportFolder ID
   - Если найден — используем связанный VideoLocal

4. **Создание нового VideoLocal (если не найден):**

```csharp
vlocal = new SVR_VideoLocal
{
    DateTimeCreated = body.DateCreated ?? now,
    DateTimeUpdated = body.DateUpdated ?? now,
    DateTimeImported = body.DateImported,
    FileName = Path.GetFileName(relativePath),
    Hash = string.Empty,
    CRC32 = string.Empty,
    MD5 = string.Empty,
    SHA1 = string.Empty,
    IsIgnored = false,
    IsVariation = false
};
```

5. **Запись хэшей и метаданных в VideoLocal:**

```csharp
vlocal.Hash = NormalizeHash(body.Hashes.ED2K);
vlocal.CRC32 = NormalizeHash(body.Hashes.CRC32);
vlocal.MD5 = NormalizeHash(body.Hashes.MD5);
vlocal.SHA1 = NormalizeHash(body.Hashes.SHA1);
vlocal.HashSource = (int)body.HashSource;
vlocal.FileSize = body.FileSize;
vlocal.DateTimeUpdated = body.DateUpdated ?? now;
vlocal.DateTimeCreated = body.DateCreated ?? vlocal.DateTimeCreated;
vlocal.DateTimeImported = body.DateImported ?? vlocal.DateTimeImported;
```

6. **Сохранение VideoLocal:**

```csharp
RepoFactory.VideoLocal.Save(vlocal, true);
```

7. **Создание VideoLocal_Place (если не существует) — добавлено в коммите `cd30af627`:**

```csharp
if (place == null)
{
    place = new SVR_VideoLocal_Place
    {
        FilePath = relativePath,
        ImportFolderID = importFolder.ImportFolderID,
        ImportFolderType = importFolder.ImportFolderType,
        VideoLocalID = vlocal.VideoLocalID
    };
}
```

8. **Обновление VideoLocal_Place (если VideoLocalID == 0):**

```csharp
if (place.VideoLocalID == 0)
{
    place.VideoLocalID = vlocal.VideoLocalID;
    place.ImportFolderType = importFolder.ImportFolderType;
}
```

9. **Сохранение VideoLocal_Place:**

```csharp
RepoFactory.VideoLocalPlace.Save(place);
```

10. **Сохранение FileNameHash:**

```csharp
SaveFileNameHash(Path.GetFileName(place.FilePath), vlocal, body.DateUpdated ?? now);
```

11. **Возврат результата:**

```csharp
return Ok(new FakeHash.Result
{
    FileID = vlocal.VideoLocalID,
    FileLocationID = place.VideoLocal_Place_ID
});
```

---

### 2. `Shoko.Server/API/v3/Models/Shoko/FakeHash.cs` (НОВЫЙ ФАЙЛ — 95 строк)

**Назначение:** DTO модели для API.

**Вложенные классы:**

| Класс | Назначение |
|-------|------------|
| `FakeHash.Hashes` | Контейнер для хэшей (ED2K, CRC32, MD5, SHA1) |
| `FakeHash.Body` | Тело запроса |
| `FakeHash.Result` | Ответ API |

**Структура `FakeHash.Hashes`:**

```csharp
public class Hashes
{
    [Required, Length(32, 32)]
    public string ED2K { get; set; }    // 32 hex chars

    [Required, Length(8, 8)]
    public string CRC32 { get; set; }   // 8 hex chars

    [Required, Length(32, 32)]
    public string MD5 { get; set; }     // 32 hex chars

    [Required, Length(40, 40)]
    public string SHA1 { get; set; }    // 40 hex chars
}
```

**Структура `FakeHash.Body`:**

```csharp
public class Body
{
    [Range(1, int.MaxValue)]
    public int? FileID { get; set; }           // Опционально: ID существующего файла

    [Range(1, int.MaxValue)]
    public int? ImportFolderID { get; set; }   // ID папки импорта

    public string FilePath { get; set; }        // Относительный или абсолютный путь

    [Range(1L, long.MaxValue)]
    public long FileSize { get; set; }          // Размер файла в байтах

    [Required]
    public Hashes Hashes { get; set; }          // Хэши

    public HashSource HashSource { get; set; } = HashSource.DirectHash;

    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public DateTime? DateImported { get; set; }
}
```

**Структура `FakeHash.Result`:**

```csharp
public class Result
{
    public int FileID { get; set; }           // VideoLocalID
    public int FileLocationID { get; set; }   // VideoLocal_Place_ID
}
```

---

### 3. `Shoko.Server/API/v3/README.md` (ИЗМЕНЁН)

**Изменение:** Добавлена документация по FakeHash endpoint в конец файла.

**Добавленные строки (46-74):**

```markdown
---
### Admin utility endpoints

#### FakeHash (v3)
**Purpose:** Store pre-computed (or externally provided) hashes and metadata for a file so Shoko treats it as already hashed.

**Warning:** Supplying fake hashes will affect matching, duplicate detection, and external integrations (AniDB/MyList, hash-based lookups). Only use if you understand the implications.

**Example request:**
...
```

---

## 🔧 Второй коммит (`cd30af627`) — исправление бага

**Проблема:** Если `VideoLocal_Place` не был найден в базе, контроллер возвращал ошибку `ValidationProblem("Unable to resolve or create a file location.")`, вместо того чтобы создать новую запись.

**Решение:** Добавлена логика создания нового `SVR_VideoLocal_Place` когда он не найден:

```csharp
// ДО исправления (коммит 900ac981b):
if (place == null)
{
    return ValidationProblem("Unable to resolve or create a file location.");
}

// ПОСЛЕ исправления (коммит cd30af627):
if (place == null)
{
    if (importFolder == null || string.IsNullOrWhiteSpace(relativePath))
        return ValidationProblem("Unable to resolve or create a file location.");

    place = new SVR_VideoLocal_Place
    {
        FilePath = relativePath,
        ImportFolderID = importFolder.ImportFolderID,
        ImportFolderType = importFolder.ImportFolderType,
        VideoLocalID = vlocal.VideoLocalID
    };
}
```

---

## 📊 Используемые модели и репозитории Shoko

| Компонент | Использование |
|-----------|--------------|
| `SVR_VideoLocal` | Основная сущность файла с хэшами |
| `SVR_VideoLocal_Place` | Расположение файла (связь с ImportFolder) |
| `SVR_ImportFolder` | Папка импорта |
| `FileNameHash` | Кэш хэшей по имени файла |
| `RepoFactory.VideoLocal` | Репозиторий VideoLocal |
| `RepoFactory.VideoLocalPlace` | Репозиторий VideoLocal_Place |
| `RepoFactory.ImportFolder` | Репозиторий ImportFolder |
| `RepoFactory.FileNameHash` | Репозиторий FileNameHash |
| `HashSource` (enum) | Источник хэша (DirectHash по умолчанию) |

---

## ⚠️ Важные замечания для будущих merge'ей

1. **Изолированность:** Все изменения находятся в **новых файлах**, кроме `README.md`. Конфликты маловероятны, если upstream не добавит:
   - Файл с таким же именем `FakeHashController.cs`
   - Модель `FakeHash.cs`
   - Редактирование конца `API/v3/README.md`

2. **Зависимости:** Контроллер использует только существующие модели и репозитории Shoko:
   - `RepoFactory.*`
   - `SVR_*` модели
   - `HashSource` enum из `Shoko.Server.Server`

3. **Если изменится API репозиториев:**
   - `GetByID()`, `Save()`, `Delete()` — могут потребовать обновления
   - `GetByFilePathAndImportFolderID()` — критичный метод для поиска VideoLocal_Place

4. **Если изменится структура SVR_VideoLocal:**
   - Проверить поля: `Hash`, `CRC32`, `MD5`, `SHA1`, `FileSize`, `HashSource`, `DateTimeCreated`, `DateTimeUpdated`, `DateTimeImported`

5. **Если изменится структура SVR_VideoLocal_Place:**
   - Проверить поля: `FilePath`, `ImportFolderID`, `ImportFolderType`, `VideoLocalID`

---

## 🔄 Восстановление логики (если сломается)

Если функционал FakeHash сломается, основные точки восстановления:

1. **Создание VideoLocal:**
   - Смотреть как создаётся в `HashFileJob` или `DiscoverFileJob`
   - Обязательные поля: все хэши (пустые строки если нет), IsIgnored=false, IsVariation=false

2. **Сохранение VideoLocal:**
   - `RepoFactory.VideoLocal.Save(vlocal, true)` — второй параметр (`updateStats`) = true

3. **Создание/обновление VideoLocal_Place:**
   - Связь VideoLocal ↔ Place через `VideoLocalID`
   - `ImportFolderType` берётся из `ImportFolder`

4. **FileNameHash:**
   - Логика скопирована из `HashFileJob.SaveFileNameHash()`
   - Важно: если найдено > 1 записи с таким именем/размером — удалить все и создать новую

---

## 📝 Пример использования API

```http
POST /api/v3/FakeHash
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "importFolderId": 1,
  "filePath": "Anime/Show/episode01.mkv",
  "fileSize": 123456789,
  "hashes": {
    "ed2k": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
    "crc32": "DEADBEEF",
    "md5": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
    "sha1": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
  },
  "hashSource": "DirectHash",
  "dateCreated": "2024-01-01T00:00:00Z",
  "dateUpdated": "2024-01-01T00:00:00Z",
  "dateImported": "2024-01-01T00:00:00Z"
}
```

**Ответ:**

```json
{
  "FileID": 12345,
  "FileLocationID": 67890
}
```

---

## 📋 Изначальная задача

<details>
### Цель
Добавить минимально инвазивный публичный admin-API, который позволит подложить фейковые хэши и метаданные файла в Shoko (ED2K/CRC32/MD5/SHA1 + FileSize + даты), не меняя существующую логику хэширования/сканирования, чтобы форк легко обновлялся с upstream.

### Основные принципы реализации
- Не менять существующие job/флоу (HashFileJob/DiscoverFileJob)
- Добавить новый контроллер и DTO в отдельных файлах → минимум merge-конфликтов
- Минимально использовать существующие модели/репозитории
- Добавить маленький helper/сервис для записи FileNameHash (копия логики SaveFileNameHash)

### Ответы на вопросы дизайна
1. **Где endpoint?** — Отдельный контроллер `/api/v3/FakeHash`
2. **Создание записей для новых файлов?** — Да, файлы добавляются в папку, но ещё не прохэшированы
3. **Все хэши обязательны?** — Да, чтобы Shoko не захотел пересчитать
4. **Генерация ED2K?** — Передаётся извне
5. **Ограничения безопасности?** — Только admin
6. **Триггерить ProcessFileJob?** — Нет смысла, если все хэши фейковые

</details>
