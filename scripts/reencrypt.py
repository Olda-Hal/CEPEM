"""
Re-encrypts all patient photos and documents onto a new key.

The current on-disk format is "CEPM2": magic + random nonce + AES-GCM tag +
ciphertext, with the key derived by HKDF-SHA256 from the configured secret and
context-bound associated data. This script writes CEPM2 and can read all older
formats:

  - CEPM2 (magic + nonce + tag + ciphertext)
  - CEPM1 (magic + per-file random IV + AES-CBC ciphertext)
  - raw legacy AES-CBC ciphertext with the configured static IV

Usage:
    python reencrypt.py \
        --old-key "<current key from .env>" \
        --old-iv  "<current IV from .env, for legacy-format files>" \
        --new-key "<newly generated key>" \
        --photos-dir  /path/to/patient-photos \
        --docs-dir    /path/to/patient-documents

The script processes files in-place, writing a .tmp file first and only
replacing the original on success, so a crash leaves the originals intact.
"""

import argparse
import secrets
import sys
from pathlib import Path

from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.hkdf import HKDF

MAGIC_V1 = b"CEPM1"
MAGIC_V2 = b"CEPM2"
NONCE_SIZE = 12
TAG_SIZE = 16
IV_SIZE = 16
KEY_SIZE = 32


def _legacy_key(key: str) -> bytes:
    # Must match the pre-CEPM2 C#: PadRight(32).Substring(0, 32) as UTF-8.
    return (key.encode("utf-8") + b" " * KEY_SIZE)[:KEY_SIZE]


def _v2_key(key: str, context: str) -> bytes:
    return HKDF(
        algorithm=hashes.SHA256(),
        length=KEY_SIZE,
        salt=b"",
        info=f"CEPEM2:{context}".encode("utf-8"),
        backend=default_backend(),
    ).derive(key.encode("utf-8"))


def _aad(context: str) -> bytes:
    return f"CEPM2:{context}".encode("utf-8")


def decrypt_legacy_cbc(data: bytes, key: bytes, iv: bytes) -> bytes:
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    decryptor = cipher.decryptor()
    padded = decryptor.update(data) + decryptor.finalize()
    pad_len = padded[-1]
    if pad_len < 1 or pad_len > 16:
        raise ValueError(f"Invalid PKCS#7 padding length: {pad_len}")
    if padded[-pad_len:] != bytes([pad_len]) * pad_len:
        raise ValueError("Invalid PKCS#7 padding bytes")
    return padded[:-pad_len]


def encrypt_v2(data: bytes, key: bytes, context: str) -> bytes:
    nonce = secrets.token_bytes(NONCE_SIZE)
    sealed = AESGCM(key).encrypt(nonce, data, _aad(context))
    ciphertext, tag = sealed[:-TAG_SIZE], sealed[-TAG_SIZE:]
    return MAGIC_V2 + nonce + tag + ciphertext


def decrypt_v2(data: bytes, key: bytes, context: str) -> bytes:
    nonce = data[len(MAGIC_V2) : len(MAGIC_V2) + NONCE_SIZE]
    tag = data[
        len(MAGIC_V2) + NONCE_SIZE : len(MAGIC_V2) + NONCE_SIZE + TAG_SIZE
    ]
    ciphertext = data[len(MAGIC_V2) + NONCE_SIZE + TAG_SIZE :]
    return AESGCM(key).decrypt(nonce, ciphertext + tag, _aad(context))


def decrypt_any(data: bytes, old_key: str, old_iv: bytes | None, context: str) -> bytes:
    if data.startswith(MAGIC_V2):
        return decrypt_v2(data, _v2_key(old_key, context), context)

    if len(data) > len(MAGIC_V1) + IV_SIZE and data.startswith(MAGIC_V1):
        iv = data[len(MAGIC_V1) : len(MAGIC_V1) + IV_SIZE]
        return decrypt_legacy_cbc(
            data[len(MAGIC_V1) + IV_SIZE :], _legacy_key(old_key), iv
        )

    if old_iv is None:
        raise ValueError("legacy-format file but --old-iv was not provided")
    return decrypt_legacy_cbc(data, _legacy_key(old_key), old_iv)


def reencrypt_directory(
    directory: Path,
    context: str,
    old_key: str,
    old_iv: bytes | None,
    new_key: str,
) -> tuple[int, int]:
    if not directory.exists():
        print(f"  ⚠️  Složka neexistuje, přeskakuji: {directory}")
        return 0, 0

    files = list(directory.glob("*.enc"))
    if not files:
        print(f"  ℹ️  Žádné .enc soubory v {directory}")
        return 0, 0

    new_key_bytes = _v2_key(new_key, context)
    ok = 0
    failed = 0
    for file in files:
        tmp = file.with_suffix(".tmp")
        try:
            encrypted = file.read_bytes()
            plain = decrypt_any(encrypted, old_key, old_iv, context)
            re_encrypted = encrypt_v2(plain, new_key_bytes, context)

            tmp.write_bytes(re_encrypted)
            tmp.replace(file)
            ok += 1
            if ok % 100 == 0:
                print(f"  … {ok} souborů hotovo")
        except Exception as e:
            print(f"  ❌ {file.name}: {e}")
            if tmp.exists():
                tmp.unlink()
            failed += 1

    return ok, failed


def main():
    parser = argparse.ArgumentParser(
        description="Re-encrypt patient files onto a new key in authenticated CEPM2 format."
    )
    parser.add_argument("--old-key", required=True, help="Current encryption key (from .env)")
    parser.add_argument(
        "--old-iv",
        help="Current static IV (from .env) - only needed if legacy-format files remain",
    )
    parser.add_argument(
        "--new-key", required=True, help="New encryption key (generate with: openssl rand -hex 32)"
    )
    parser.add_argument("--photos-dir", default="../data/patient-photos")
    parser.add_argument("--docs-dir", default="../data/patient-documents")
    args = parser.parse_args()

    old_iv = _legacy_key(args.old_iv)[:IV_SIZE] if args.old_iv else None

    if args.old_key == args.new_key:
        print("Starý a nový klíč jsou stejné, není co dělat.")
        sys.exit(0)

    photos_dir = Path(args.photos_dir)
    docs_dir = Path(args.docs_dir)

    total_ok = total_failed = 0

    print(f"\n📁 Fotografie pacientů ({photos_dir}):")
    ok, failed = reencrypt_directory(photos_dir, "photo", args.old_key, old_iv, args.new_key)
    total_ok += ok
    total_failed += failed

    print(f"\n📁 Dokumenty pacientů ({docs_dir}):")
    ok, failed = reencrypt_directory(docs_dir, "document", args.old_key, old_iv, args.new_key)
    total_ok += ok
    total_failed += failed

    print(
        f"\n{'✅' if total_failed == 0 else '⚠️ '} Hotovo: {total_ok} přešifrováno, {total_failed} selhalo"
    )

    if total_failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
