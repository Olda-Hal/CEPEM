#!/bin/sh
set -e

PHOTO_PATH="${PhotoStorage__Path:-${PHOTO_STORAGE_PATH:-/app/patient-photos}}"
DOCUMENT_PATH="${DocumentStorage__Path:-${DOCUMENT_STORAGE_PATH:-/app/patient-documents}}"

mkdir -p "$PHOTO_PATH" "$DOCUMENT_PATH"
chown -R app:app "$PHOTO_PATH" "$DOCUMENT_PATH"

# wait-for-it runs as root; drop to the unprivileged app user for the API.
exec gosu app /wait-for-it.sh mysql:3306 --timeout=60 -- dotnet DatabaseAPI.dll
