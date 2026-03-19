#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${PROJECT_ROOT}/.env"
DATA_DIR="${PROJECT_ROOT}/data"
BACKUP_ROOT="${PROJECT_ROOT}/backups"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-cepem_mysql}"
KEEP_BACKUPS="${KEEP_BACKUPS:-7}"

if [[ -f "${ENV_FILE}" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "${ENV_FILE}"
    set +a
fi

if [[ ! -d "${DATA_DIR}" ]]; then
    echo "Error: data directory not found at ${DATA_DIR}" >&2
    exit 1
fi

if [[ -z "${MYSQL_ROOT_PASSWORD:-}" ]]; then
    echo "Error: MYSQL_ROOT_PASSWORD is not set. Check ${ENV_FILE}." >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: docker is required." >&2
    exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -qx "${MYSQL_CONTAINER}"; then
    echo "Error: MySQL container '${MYSQL_CONTAINER}' is not running." >&2
    exit 1
fi

timestamp="$(date '+%Y%m%d_%H%M%S')"
backup_dir="${BACKUP_ROOT}/${timestamp}"
mkdir -p "${backup_dir}"

data_archive="${backup_dir}/data_${timestamp}.tar.gz"
sql_dump="${backup_dir}/mysql_full_${timestamp}.sql.gz"

tar -czf "${data_archive}" -C "${PROJECT_ROOT}" data

docker exec "${MYSQL_CONTAINER}" sh -c "exec mysqldump -uroot -p\"${MYSQL_ROOT_PASSWORD}\" --all-databases --single-transaction --routines --events --triggers" \
    | gzip -9 > "${sql_dump}"

find "${BACKUP_ROOT}" -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((KEEP_BACKUPS + 1)) | xargs -r rm -rf

echo "Backup created: ${backup_dir}"