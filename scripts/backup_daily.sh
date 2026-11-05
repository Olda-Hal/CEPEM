#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${PROJECT_ROOT}/.env"
DATA_DIR="${PROJECT_ROOT}/data"
BACKUP_ROOT="${BACKUP_ROOT:-${PROJECT_ROOT}/backups}"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-cepem_mysql}"
KEEP_BACKUPS="${KEEP_BACKUPS:-7}"

# Optional: GPG encryption. Set one of these in .env to protect backups at rest.
BACKUP_ENCRYPTION_PASSPHRASE="${BACKUP_ENCRYPTION_PASSPHRASE:-}"
BACKUP_GPG_RECIPIENT="${BACKUP_GPG_RECIPIENT:-}"
# Optional offsite copy, e.g. "remote:cepem-backups".
BACKUP_REMOTE="${BACKUP_REMOTE:-}"
# Include .env (contains encryption keys) only when the archive is encrypted.
BACKUP_INCLUDE_ENV="${BACKUP_INCLUDE_ENV:-true}"
# Verify archive integrity after writing.
BACKUP_VERIFY="${BACKUP_VERIFY:-true}"
# Restore the SQL dump into a temporary database to prove it is usable.
BACKUP_RESTORE_TEST="${BACKUP_RESTORE_TEST:-false}"

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

ENCRYPTED=false
if [[ -n "${BACKUP_ENCRYPTION_PASSPHRASE}" || -n "${BACKUP_GPG_RECIPIENT}" ]]; then
    if ! command -v gpg >/dev/null 2>&1; then
        echo "Error: BACKUP encryption is configured but gpg is not installed." >&2
        exit 1
    fi
    ENCRYPTED=true
else
    echo "WARNING: BACKUP_ENCRYPTION_PASSPHRASE / BACKUP_GPG_RECIPIENT is not set." >&2
    echo "WARNING: backups will be written unencrypted and .env will be omitted." >&2
fi

if [[ -n "${BACKUP_REMOTE}" ]] && ! command -v rclone >/dev/null 2>&1; then
    echo "Error: BACKUP_REMOTE is set but rclone is not installed." >&2
    exit 1
fi

timestamp="$(date '+%Y%m%d_%H%M%S')"
backup_dir="${BACKUP_ROOT}/${timestamp}"
mkdir -p "${backup_dir}"

data_archive="${backup_dir}/data_${timestamp}.tar.gz"
sql_dump="${backup_dir}/mysql_full_${timestamp}.sql.gz"

encrypt_file() {
    local source="$1"
    local target="$2"
    if [[ -n "${BACKUP_GPG_RECIPIENT}" ]]; then
        gpg --batch --yes --encrypt --recipient "${BACKUP_GPG_RECIPIENT}" \
            --output "${target}" "${source}"
    else
        gpg --batch --yes --symmetric --cipher-algo AES256 \
            --passphrase "${BACKUP_ENCRYPTION_PASSPHRASE}" \
            --output "${target}" "${source}"
    fi
}

# Patient files. The .env is included only inside an encrypted archive.
data_inputs=("data")
if [[ "${BACKUP_INCLUDE_ENV}" == "true" && "${ENCRYPTED}" == "true" && -f "${ENV_FILE}" ]]; then
    data_inputs+=(".env")
fi
tar -czf "${data_archive}" -C "${PROJECT_ROOT}" "${data_inputs[@]}"

docker exec "${MYSQL_CONTAINER}" sh -c \
    "exec mysqldump -uroot -p\"${MYSQL_ROOT_PASSWORD}\" --all-databases --single-transaction --routines --events --triggers" \
    | gzip -9 > "${sql_dump}"

if [[ "${ENCRYPTED}" == "true" ]]; then
    encrypt_file "${data_archive}" "${data_archive}.gpg"
    encrypt_file "${sql_dump}" "${sql_dump}.gpg"
    rm -f "${data_archive}" "${sql_dump}"
    data_archive="${data_archive}.gpg"
    sql_dump="${sql_dump}.gpg"
fi

verify_archive() {
    local archive="$1"
    local kind="$2"
    local temp
    temp="$(mktemp)"
    if [[ "${ENCRYPTED}" == "true" ]]; then
        if [[ -n "${BACKUP_GPG_RECIPIENT}" ]]; then
            gpg --batch --yes --decrypt --output "${temp}" "${archive}"
        else
            gpg --batch --yes --decrypt \
                --passphrase "${BACKUP_ENCRYPTION_PASSPHRASE}" \
                --output "${temp}" "${archive}"
        fi
    else
        cp "${archive}" "${temp}"
    fi
    if [[ "${kind}" == "tar" ]]; then
        tar -tzf "${temp}" >/dev/null
    else
        gzip -t "${temp}"
    fi
    rm -f "${temp}"
}

if [[ "${BACKUP_VERIFY}" == "true" ]]; then
    verify_archive "${data_archive}" tar
    verify_archive "${sql_dump}" sql
    echo "Backup integrity verified."
fi

if [[ "${BACKUP_RESTORE_TEST}" == "true" ]]; then
    restore_db="cepem_restore_test_${timestamp}"
    temp_sql="$(mktemp)"
    if [[ "${ENCRYPTED}" == "true" ]]; then
        if [[ -n "${BACKUP_GPG_RECIPIENT}" ]]; then
            gpg --batch --yes --decrypt --output "${temp_sql}.gz" "${sql_dump}"
        else
            gpg --batch --yes --decrypt \
                --passphrase "${BACKUP_ENCRYPTION_PASSPHRASE}" \
                --output "${temp_sql}.gz" "${sql_dump}"
        fi
        gzip -dc "${temp_sql}.gz" > "${temp_sql}"
    else
        gzip -dc "${sql_dump}" > "${temp_sql}"
    fi
    docker exec "${MYSQL_CONTAINER}" sh -c \
        "mysql -uroot -p\"${MYSQL_ROOT_PASSWORD}\" -e \"DROP DATABASE IF EXISTS \\\`${restore_db}\\\`; CREATE DATABASE \\\`${restore_db}\\\`;\""
    docker exec -i "${MYSQL_CONTAINER}" sh -c \
        "mysql -uroot -p\"${MYSQL_ROOT_PASSWORD}\" ${restore_db}" < "${temp_sql}"
    docker exec "${MYSQL_CONTAINER}" sh -c \
        "mysql -uroot -p\"${MYSQL_ROOT_PASSWORD}\" -e \"DROP DATABASE \\\`${restore_db}\\\`;\""
    rm -f "${temp_sql}" "${temp_sql}.gz"
    echo "Restore test passed (temporary database ${restore_db})."
fi

if [[ -n "${BACKUP_REMOTE}" ]]; then
    rclone copy "${backup_dir}" "${BACKUP_REMOTE}/${timestamp}" --checksum
    echo "Backup copied to ${BACKUP_REMOTE}/${timestamp}"
fi

find "${BACKUP_ROOT}" -mindepth 1 -maxdepth 1 -type d | sort -r \
    | tail -n +$((KEEP_BACKUPS + 1)) | xargs -r rm -rf

echo "Backup created: ${backup_dir}"
