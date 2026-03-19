"""
Re-encrypts all patient photos and documents from an old AES key/IV to a new one.

Usage:
    python reencrypt.py \
        --old-key "CEPEMSecureKey1234567890123456" \
        --old-iv  "CEPEMInitVector1" \
        --new-key "NewSecureKey1234567890123456789" \
        --new-iv  "NewInitVector123" \
        --photos-dir  /path/to/patient-photos \
        --docs-dir    /path/to/patient-documents

The script processes files in-place, writing a .tmp file first and only replacing
the original on success, so a crash leaves the originals intact.
"""

import argparse
import os
import sys
from pathlib import Path
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend


def _pad_key(key: str, length: int) -> bytes:
    return key.encode().ljust(length)[:length]


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


def reencrypt_directory(directory: Path, old_key: bytes, old_iv: bytes, new_key: bytes, new_iv: bytes) -> tuple[int, int]:
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
            plain = decrypt(encrypted, old_key, old_iv)
            re_encrypted = encrypt(plain, new_key, new_iv)
            tmp.write_bytes(re_encrypted)
            tmp.replace(file)
            print(f"  ✅ {file.name}")
            ok += 1
        except Exception as e:
            print(f"  ❌ {file.name}: {e}")
            if tmp.exists():
                tmp.unlink()
            failed += 1

    return ok, failed


def main():
    parser = argparse.ArgumentParser(description="Re-encrypt patient files with a new AES key/IV.")
    parser.add_argument("--old-key", required=True)
    parser.add_argument("--old-iv",  required=True)
    parser.add_argument("--new-key", required=True)
    parser.add_argument("--new-iv",  required=True)
    parser.add_argument("--photos-dir", default="../data/patient-photos")
    parser.add_argument("--docs-dir",   default="../data/patient-documents")
    args = parser.parse_args()

    old_key = _pad_key(args.old_key, 32)
    old_iv  = _pad_key(args.old_iv,  16)
    new_key = _pad_key(args.new_key, 32)
    new_iv  = _pad_key(args.new_iv,  16)

    if old_key == new_key and old_iv == new_iv:
        print("Starý a nový klíč jsou stejné, není co dělat.")
        sys.exit(0)

    photos_dir = Path(args.photos_dir)
    docs_dir   = Path(args.docs_dir)

    total_ok = total_failed = 0

    print(f"\n📁 Fotografie pacientů ({photos_dir}):")
    ok, failed = reencrypt_directory(photos_dir, old_key, old_iv, new_key, new_iv)
    total_ok += ok
    total_failed += failed

    print(f"\n📁 Dokumenty pacientů ({docs_dir}):")
    ok, failed = reencrypt_directory(docs_dir, old_key, old_iv, new_key, new_iv)
    total_ok += ok
    total_failed += failed

    print(f"\n{'✅' if total_failed == 0 else '⚠️ '} Hotovo: {total_ok} přešifrováno, {total_failed} selhalo")

    if total_failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
