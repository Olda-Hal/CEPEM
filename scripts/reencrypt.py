"""
Re-encrypts all patient photos and documents onto a new AES key, writing the
new "CEPM1" format (magic header + per-file random IV + AES-CBC ciphertext)
that PhotoEncryptionService / DocumentEncryptionService read.

Usage:
    python reencrypt.py \
        --old-key "<current key from .env>" \
        --old-iv  "<current IV from .env, for legacy-format files>" \
        --new-key "<newly generated key>" \
        --photos-dir  /path/to/patient-photos \
        --docs-dir    /path/to/patient-documents

Legacy-format files (no CEPM1 header) are decrypted with --old-iv; files
already in CEPM1 format are decrypted with their embedded IV. Everything is
re-encrypted with the new key and a fresh random IV per file.

The script processes files in-place, writing a .tmp file first and only
replacing the original on success, so a crash leaves the originals intact.
"""

import argparse
import os
import secrets
import sys
from pathlib import Path
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend

MAGIC = b"CEPM1"


def _pad_key(key: str, length: int) -> bytes:
    # Must match C#: Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32))
    return (key.encode("utf-8") + b" " * length)[:length]


def decrypt(data: bytes, key: bytes, iv: bytes) -> bytes:
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    decryptor = cipher.decryptor()
    padded = decryptor.update(data) + decryptor.finalize()
    pad_len = padded[-1]
    if pad_len < 1 or pad_len > 16:
        raise ValueError(f"Invalid PKCS#7 padding length: {pad_len}")
    if padded[-pad_len:] != bytes([pad_len]) * pad_len:
        raise ValueError("Invalid PKCS#7 padding bytes")
    return padded[:-pad_len]


def encrypt(data: bytes, key: bytes, iv: bytes) -> bytes:
    pad_len = 16 - (len(data) % 16)
    padded = data + bytes([pad_len] * pad_len)
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
    encryptor = cipher.encryptor()
    return encryptor.update(padded) + encryptor.finalize()


def split_encrypted(data: bytes) -> tuple[bytes, bytes] | None:
    """Return (iv, ciphertext) for CEPM1-format data, or None for legacy format."""
    if len(data) > len(MAGIC) + 16 and data[: len(MAGIC)] == MAGIC:
        iv = data[len(MAGIC) : len(MAGIC) + 16]
        return iv, data[len(MAGIC) + 16 :]
    return None


def reencrypt_directory(directory: Path, old_key: bytes, old_iv: bytes | None, new_key: bytes) -> tuple[int, int]:
    if not directory.exists():
        print(f"  ⚠️  Složka neexistuje, přeskakuji: {directory}")
        return 0, 0

    files = list(directory.glob("*.enc"))
    if not files:
        print(f"  ℹ️  Žádné .enc soubory v {directory}")
        return 0, 0

    ok = 0
    failed = 0
    for file in files:
        tmp = file.with_suffix(".tmp")
        try:
            encrypted = file.read_bytes()

            parts = split_encrypted(encrypted)
            if parts is not None:
                iv, ciphertext = parts
            else:
                if old_iv is None:
                    raise ValueError("legacy-format file but --old-iv was not provided")
                iv, ciphertext = old_iv, encrypted

            plain = decrypt(ciphertext, old_key, iv)
            new_iv = secrets.token_bytes(16)
            re_encrypted = MAGIC + new_iv + encrypt(plain, new_key, new_iv)

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
    parser = argparse.ArgumentParser(description="Re-encrypt patient files onto a new key, in CEPM1 per-file-IV format.")
    parser.add_argument("--old-key", required=True, help="Current encryption key (from .env)")
    parser.add_argument("--old-iv", help="Current static IV (from .env) - only needed if legacy-format files remain")
    parser.add_argument("--new-key", required=True, help="New encryption key (generate with: openssl rand -hex 32)")
    parser.add_argument("--photos-dir", default="../data/patient-photos")
    parser.add_argument("--docs-dir", default="../data/patient-documents")
    args = parser.parse_args()

    old_key = _pad_key(args.old_key, 32)
    old_iv = _pad_key(args.old_iv, 16) if args.old_iv else None
    new_key = _pad_key(args.new_key, 32)

    if old_key == new_key:
        print("Starý a nový klíč jsou stejné, není co dělat.")
        sys.exit(0)

    photos_dir = Path(args.photos_dir)
    docs_dir = Path(args.docs_dir)

    total_ok = total_failed = 0

    print(f"\n📁 Fotografie pacientů ({photos_dir}):")
    ok, failed = reencrypt_directory(photos_dir, old_key, old_iv, new_key)
    total_ok += ok
    total_failed += failed

    print(f"\n📁 Dokumenty pacientů ({docs_dir}):")
    ok, failed = reencrypt_directory(docs_dir, old_key, old_iv, new_key)
    total_ok += ok
    total_failed += failed

    print(f"\n{'✅' if total_failed == 0 else '⚠️ '} Hotovo: {total_ok} přešifrováno, {total_failed} selhalo")

    if total_failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
