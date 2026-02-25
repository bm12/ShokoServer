#!/usr/bin/env bash
set -euo pipefail

# ─────────────────────────────────────────────────────────────────────────────
# upgrade-fork.sh — перенос кастомных коммитов форка на новый upstream-релиз
#
# Использование:
#   ./upgrade-fork.sh <старая-ветка> <новый-тег> [новая-ветка]
#
# Примеры:
#   ./upgrade-fork.sh release-5.3.1 v5.4.0
#   ./upgrade-fork.sh release-5.3.1 v5.4.0 release-5.4.0
# ─────────────────────────────────────────────────────────────────────────────

UPSTREAM_REMOTE="upstream"
UPSTREAM_URL="https://github.com/ShokoAnime/ShokoServer.git"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

die()  { echo -e "${RED}ОШИБКА: $*${NC}" >&2; exit 1; }
info() { echo -e "${CYAN}>>> $*${NC}"; }
ok()   { echo -e "${GREEN}✓ $*${NC}"; }
warn() { echo -e "${YELLOW}⚠ $*${NC}"; }

if [[ $# -lt 2 ]]; then
    echo "Использование: $0 <старая-ветка> <новый-тег> [новая-ветка]"
    echo ""
    echo "  старая-ветка   Ветка с вашими кастомными коммитами (напр. release-5.3.1)"
    echo "  новый-тег      Тег upstream-релиза (напр. v5.4.0)"
    echo "  новая-ветка    Имя новой ветки (по умолчанию: release-X.Y.Z из тега)"
    exit 1
fi

OLD_BRANCH="$1"
NEW_TAG="$2"
NEW_BRANCH="${3:-release-${NEW_TAG#v}}"

# ─── Проверки ────────────────────────────────────────────────────────────────

git rev-parse --is-inside-work-tree &>/dev/null \
    || die "Запускайте из git-репозитория"

[[ -z "$(git status --porcelain)" ]] \
    || die "Рабочая директория не чистая. Закоммитьте или stash'ните изменения."

git rev-parse --verify "$OLD_BRANCH" &>/dev/null \
    || die "Ветка '$OLD_BRANCH' не найдена"

# ─── Настройка upstream remote ───────────────────────────────────────────────

if ! git remote get-url "$UPSTREAM_REMOTE" &>/dev/null; then
    info "Добавляю remote '$UPSTREAM_REMOTE' → $UPSTREAM_URL"
    git remote add "$UPSTREAM_REMOTE" "$UPSTREAM_URL"
fi

info "Подтягиваю upstream теги и ветки..."
git fetch "$UPSTREAM_REMOTE" --tags --quiet

git rev-parse --verify "$NEW_TAG" &>/dev/null \
    || die "Тег '$NEW_TAG' не найден. Доступные теги:\n$(git tag --sort=-v:refname | head -10)"

# ─── Поиск кастомных коммитов ────────────────────────────────────────────────

info "Ищу кастомные коммиты в '$OLD_BRANCH' (отсутствующие в upstream)..."

MERGE_BASE=$(git merge-base "$OLD_BRANCH" "$UPSTREAM_REMOTE/main" 2>/dev/null) \
    || die "Не удалось найти общего предка между '$OLD_BRANCH' и '$UPSTREAM_REMOTE/main'"

COMMITS=()
while IFS= read -r line; do
    hash="${line%% *}"
    parents=$(git rev-list --parents -1 "$hash" | wc -w)
    # parents > 2 означает merge-коммит (у него 3+ слов: сам хэш + 2 родителя)
    if [[ "$parents" -le 2 ]]; then
        COMMITS+=("$line")
    else
        warn "Пропускаю merge-коммит: $line"
    fi
done < <(git log --oneline --reverse "$MERGE_BASE..$OLD_BRANCH")

if [[ ${#COMMITS[@]} -eq 0 ]]; then
    die "Кастомных коммитов не найдено в '$OLD_BRANCH'"
fi

echo ""
echo -e "${GREEN}Найдено ${#COMMITS[@]} коммит(ов) для переноса:${NC}"
echo "────────────────────────────────────────────────"
for c in "${COMMITS[@]}"; do
    echo "  $c"
done
echo "────────────────────────────────────────────────"
echo ""

# ─── Подтверждение ───────────────────────────────────────────────────────────

echo -e "Новая ветка: ${CYAN}$NEW_BRANCH${NC} (от тега ${CYAN}$NEW_TAG${NC})"
echo ""
read -rp "Продолжить? (y/N) " confirm
[[ "$confirm" =~ ^[yYдД]$ ]] || { echo "Отменено."; exit 0; }

# ─── Создание ветки и cherry-pick ────────────────────────────────────────────

if git rev-parse --verify "$NEW_BRANCH" &>/dev/null; then
    warn "Ветка '$NEW_BRANCH' уже существует, переключаюсь на неё"
    git checkout "$NEW_BRANCH"
else
    info "Создаю ветку '$NEW_BRANCH' от '$NEW_TAG'..."
    git checkout -b "$NEW_BRANCH" "$NEW_TAG"
fi

HASHES=()
for c in "${COMMITS[@]}"; do
    HASHES+=("${c%% *}")
done

info "Выполняю cherry-pick ${#HASHES[@]} коммитов..."
echo ""

if git cherry-pick "${HASHES[@]}"; then
    echo ""
    ok "Все коммиты успешно перенесены!"
else
    echo ""
    warn "Cherry-pick остановился из-за конфликта."
    echo ""
    echo "Что делать:"
    echo "  1. Разрешите конфликты (git status покажет файлы)"
    echo "  2. git add <файлы>"
    echo "  3. git cherry-pick --continue"
    echo ""
    echo "  Или отменить: git cherry-pick --abort"
    exit 1
fi

# ─── Итог ────────────────────────────────────────────────────────────────────

echo ""
echo "════════════════════════════════════════════════"
echo -e "${GREEN}Готово!${NC} Ветка ${CYAN}$NEW_BRANCH${NC} содержит:"
echo ""
git log --oneline "$NEW_TAG..$NEW_BRANCH"
echo ""
echo "════════════════════════════════════════════════"
echo ""
echo "Следующие шаги:"
echo "  git push -u origin $NEW_BRANCH    # запушить в GitHub"
echo "  docker build ...                  # пересобрать образ"
