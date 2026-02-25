# Обновление форка ShokoServer на новый upstream-релиз

## Общая схема

```
upstream (ShokoAnime/ShokoServer)    ваш форк (bm12/ShokoServer)
─────────────────────────────────    ──────────────────────────────

tag v5.2.5 ────────────────────────► release-5.2.5
     │                                    │
     │                                    ├── 900ac98  FakeHash API
     │                                    ├── cd30af6  FakeHash fix
     │                                    ├── 9333b40  Документация
     │                                    └── 2e47aef  Dockerfile.noproxy
     │
tag v5.3.1 ────────────────────────► release-5.3.1 (новая ветка)
                                          │
                                          └── cherry-pick ваших коммитов
```

**Суть:** вы создаёте новую ветку от нового тега upstream, затем переносите
свои кастомные коммиты через `cherry-pick`.

---

## Пошаговая инструкция (вручную)

### 1. Добавить upstream (один раз)

```bash
git remote add upstream https://github.com/ShokoAnime/ShokoServer.git
```

Проверить:
```bash
git remote -v
# origin    https://github.com/bm12/ShokoServer.git
# upstream  https://github.com/ShokoAnime/ShokoServer.git
```

### 2. Подтянуть теги и ветки upstream

```bash
git fetch upstream --tags
```

### 3. Узнать, какие теги/релизы доступны

```bash
git tag --sort=-v:refname | head -10
```

### 4. Создать новую ветку от нужного тега

```bash
git checkout -b release-X.Y.Z vX.Y.Z
```

Пример:
```bash
git checkout -b release-5.4.0 v5.4.0
```

### 5. Найти ваши кастомные коммиты на старой ветке

```bash
# Показать коммиты, которые есть в старой ветке, но НЕТ в upstream
git log --oneline release-5.3.1 --not upstream/main
```

Или найти коммиты между общим предком и вершиной вашей ветки:
```bash
git log --oneline $(git merge-base release-5.3.1 upstream/main)..release-5.3.1
```

### 6. Cherry-pick коммитов (без merge-коммитов)

```bash
git cherry-pick <hash1> <hash2> <hash3> ...
```

**Важно:** пропускайте merge-коммиты (типа `Merge pull request #N`).
Они не несут собственных изменений — все изменения уже есть в обычных коммитах.

### 7. Разрешение конфликтов (если возникнут)

Если cherry-pick остановится с конфликтом:

```bash
# Посмотреть, какие файлы конфликтуют
git status

# Открыть файлы, разрешить конфликты (маркеры <<<<<<< / ======= / >>>>>>>)

# После разрешения
git add <файлы>
git cherry-pick --continue

# Или отменить cherry-pick и разобраться
git cherry-pick --abort
```

### 8. Запушить новую ветку

```bash
git push -u origin release-X.Y.Z
```

---

## Скрипт-автоматизация

Скрипт `upgrade-fork.sh` (лежит в корне репо) автоматизирует шаги 2–6.

Использование:
```bash
# Обновить форк с release-5.3.1 на тег v5.4.0
./upgrade-fork.sh release-5.3.1 v5.4.0

# С указанием имени новой ветки (по умолчанию = тег без "v")
./upgrade-fork.sh release-5.3.1 v5.4.0 release-5.4.0
```

---

## Как понять, какие коммиты — ваши?

Ваши коммиты обычно можно найти по автору:
```bash
git log --oneline --author="makhmud" release-5.3.1
```

Или по разнице с upstream:
```bash
git log --oneline release-5.3.1 --not upstream/main
```

---

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Конфликт в `README.md` | Upstream обновил тот же файл — разрешите вручную |
| `FakeHashController.cs` не компилируется | API репозиториев изменился — см. `FAKEHASH_IMPLEMENTATION.md` раздел "Восстановление логики" |
| Merge-коммит мешает cherry-pick | Пропустите его, он не нужен |
| `upstream` remote не найден | `git remote add upstream https://github.com/ShokoAnime/ShokoServer.git` |
