#!/usr/bin/env python3

"""
Download INFRM documents recorded in ExaminationDocuments, decrypt them,
and store plaintext files to a target directory.

Defaults:
- Reads DB connection from DATABASE_API_CONNECTION_STRING or project .env
- Reads document encryption key/iv from DOCUMENT_ENCRYPTION_KEY/IV or .env
- Reads encrypted source files from DOCUMENT_STORAGE_PATH or ./data/patient-documents
- Writes decrypted files to ~/infrm_download
"""

import argparse
import importlib
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes


@dataclass
class DocumentRow:
	doc_id: int
	examination_id: int
	file_name: str
	original_file_name: str
	encrypted_path: str
	is_deleted: int


def parse_args() -> argparse.Namespace:
	parser = argparse.ArgumentParser(
		description="Download + decrypt INFRM documents from ExaminationDocuments."
	)
	parser.add_argument(
		"--output-dir",
		default=str(Path.home() / "infrm_download"),
		help="Directory where decrypted files are saved (default: ~/infrm_download)",
	)
	parser.add_argument(
		"--source-dir",
		default=None,
		help="Directory with encrypted files (*.enc). If omitted, uses env/.env/default.",
	)
	parser.add_argument(
		"--include-deleted",
		action="store_true",
		help="Include records where IsDeleted = 1.",
	)
	parser.add_argument(
		"--all-documents",
		action="store_true",
		help="Process all documents in ExaminationDocuments (not only INFRM matches).",
	)
	return parser.parse_args()


def load_dotenv(dotenv_path: Path) -> Dict[str, str]:
	result: Dict[str, str] = {}
	if not dotenv_path.exists():
		return result

	for raw_line in dotenv_path.read_text(encoding="utf-8").splitlines():
		line = raw_line.strip()
		if not line or line.startswith("#") or "=" not in line:
			continue
		key, value = line.split("=", 1)
		key = key.strip()
		value = value.strip().strip('"').strip("'")
		if key:
			result[key] = value
	return result


def parse_connection_string(value: str) -> Dict[str, str]:
	parts: Dict[str, str] = {}
	for item in value.split(";"):
		piece = item.strip()
		if not piece or "=" not in piece:
			continue
		k, v = piece.split("=", 1)
		parts[k.strip().lower()] = v.strip()
	return parts


def pad_key(value: str, length: int) -> bytes:
	return value.encode("utf-8").ljust(length)[:length]


def decrypt_aes_cbc_pkcs7(data: bytes, key: bytes, iv: bytes) -> bytes:
	cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
	decryptor = cipher.decryptor()
	padded = decryptor.update(data) + decryptor.finalize()

	if not padded:
		raise ValueError("Decrypted output is empty")

	pad_len = padded[-1]
	if pad_len < 1 or pad_len > 16:
		raise ValueError(f"Invalid PKCS7 padding length: {pad_len}")

	if padded[-pad_len:] != bytes([pad_len] * pad_len):
		raise ValueError("Invalid PKCS7 padding bytes")

	return padded[:-pad_len]


def sanitize_file_name(name: str) -> str:
	cleaned = re.sub(r"[^A-Za-z0-9._-]+", "_", name.strip())
	return cleaned or "document.bin"


def pick_db_driver() -> str:
	try:
		importlib.import_module("mysql.connector")

		return "mysql.connector"
	except Exception:
		pass

	try:
		importlib.import_module("pymysql")

		return "pymysql"
	except Exception:
		pass

	raise RuntimeError(
		"No MySQL driver found. Install one of: mysql-connector-python, pymysql"
	)


def fetch_documents(
	conn_info: Dict[str, str],
	include_deleted: bool,
	all_documents: bool,
) -> List[DocumentRow]:
	driver = pick_db_driver()

	where_parts: List[str] = []
	if not include_deleted:
		where_parts.append("IsDeleted = 0")
	if not all_documents:
		where_parts.append(
			"(LOWER(OriginalFileName) LIKE '%infrm%' OR LOWER(FileName) LIKE '%infrm%' OR LOWER(EncryptedPath) LIKE '%infrm%')"
		)

	where_sql = ""
	if where_parts:
		where_sql = " WHERE " + " AND ".join(where_parts)

	sql = (
		"SELECT Id, ExaminationId, FileName, OriginalFileName, EncryptedPath, IsDeleted "
		"FROM ExaminationDocuments"
		f"{where_sql} ORDER BY Id"
	)

	host = conn_info.get("server") or conn_info.get("host") or "127.0.0.1"
	port = int(conn_info.get("port") or "3306")
	database = conn_info.get("database") or "cepem_healthcare"
	user = conn_info.get("user") or conn_info.get("uid") or "root"
	password = conn_info.get("password") or conn_info.get("pwd") or ""

	rows: List[Tuple] = []
	hosts_to_try: List[str] = [host]
	if host == "host.docker.internal":
		hosts_to_try.extend(["127.0.0.1", "localhost", "mysql"])

	last_error: Optional[Exception] = None
	query_executed = False

	for active_host in hosts_to_try:
		try:
			if driver == "mysql.connector":
				mysql_connector = importlib.import_module("mysql.connector")

				conn = mysql_connector.connect(
					host=active_host,
					port=port,
					database=database,
					user=user,
					password=password,
				)
				try:
					cursor = conn.cursor()
					cursor.execute(sql)
					rows = list(cursor.fetchall())
				finally:
					conn.close()
			else:
				pymysql = importlib.import_module("pymysql")

				conn = pymysql.connect(
					host=active_host,
					port=port,
					database=database,
					user=user,
					password=password,
					cursorclass=pymysql.cursors.Cursor,
				)
				try:
					with conn.cursor() as cursor:
						cursor.execute(sql)
						rows = list(cursor.fetchall())
				finally:
					conn.close()

			query_executed = True
			break
		except Exception as exc:
			last_error = exc
			continue

	if not query_executed and last_error is not None:
		raise last_error

	return [
		DocumentRow(
			doc_id=int(r[0]),
			examination_id=int(r[1]),
			file_name=str(r[2]),
			original_file_name=str(r[3]),
			encrypted_path=str(r[4]),
			is_deleted=int(r[5]) if r[5] is not None else 0,
		)
		for r in rows
	]


def resolve_source_dir(args_source_dir: Optional[str], env_values: Dict[str, str], project_root: Path) -> Tuple[Path, bool]:
	if args_source_dir:
		return Path(args_source_dir).expanduser().resolve(), False

	env_path_raw = os.getenv("DOCUMENT_STORAGE_PATH") or env_values.get("DOCUMENT_STORAGE_PATH")
	default_host_path = (project_root / "data" / "patient-documents").resolve()

	candidates: List[Path] = []
	used_fallback = False

	if env_path_raw:
		env_path = Path(env_path_raw).expanduser().resolve()
		candidates.append(env_path)

		# Docker path from compose often points to container filesystem.
		if env_path_raw.rstrip("/") == "/app/patient-documents":
			candidates.append(default_host_path)
			used_fallback = True

	if default_host_path not in candidates:
		candidates.append(default_host_path)

	for candidate in candidates:
		if candidate.exists():
			return candidate, used_fallback and candidate == default_host_path

	return candidates[0], used_fallback


def main() -> int:
	args = parse_args()

	script_path = Path(__file__).resolve()
	project_root = script_path.parent.parent
	env_values = load_dotenv(project_root / ".env")

	connection_string = (
		os.getenv("DATABASE_API_CONNECTION_STRING")
		or env_values.get("DATABASE_API_CONNECTION_STRING")
		or "Server=127.0.0.1;Port=3306;Database=cepem_healthcare;User=root;Password=;"
	)
	conn_info = parse_connection_string(connection_string)

	key_string = (
		os.getenv("DOCUMENT_ENCRYPTION_KEY")
		or env_values.get("DOCUMENT_ENCRYPTION_KEY")
		or "3ecd03d810e78227081299f6540681f94e01e210ef56dc6b0491264e5860c349"
	)
	iv_string = (
		os.getenv("DOCUMENT_ENCRYPTION_IV")
		or env_values.get("DOCUMENT_ENCRYPTION_IV")
		or "DefaultIV12345678"
	)

	source_dir, used_fallback = resolve_source_dir(args.source_dir, env_values, project_root)
	output_dir = Path(args.output_dir).expanduser().resolve()
	output_dir.mkdir(parents=True, exist_ok=True)

	if not source_dir.exists():
		print(f"ERROR: Encrypted source directory does not exist: {source_dir}")
		print(
			f"Hint: use --source-dir {project_root / 'data' / 'patient-documents'} or set DOCUMENT_STORAGE_PATH to a host path"
		)
		return 1

	if used_fallback:
		print("INFO: DOCUMENT_STORAGE_PATH points to Docker path, using host path fallback.")

	print(f"Source directory: {source_dir}")
	print(f"Output directory: {output_dir}")

	try:
		documents = fetch_documents(conn_info, args.include_deleted, args.all_documents)
	except Exception as exc:
		print(f"ERROR: Cannot read ExaminationDocuments from database: {exc}")
		return 1

	if not documents:
		print("No matching documents found.")
		return 0

	key = pad_key(key_string, 32)
	iv = pad_key(iv_string, 16)

	success = 0
	failed = 0
	missing = 0

	for row in documents:
		encrypted_file = source_dir / row.encrypted_path

		if not encrypted_file.exists():
			print(f"MISS  doc_id={row.doc_id} file={row.encrypted_path}")
			missing += 1
			continue

		safe_original = sanitize_file_name(row.original_file_name)
		output_name = f"{row.doc_id}_{row.examination_id}_{safe_original}"
		output_file = output_dir / output_name

		try:
			encrypted = encrypted_file.read_bytes()
			decrypted = decrypt_aes_cbc_pkcs7(encrypted, key, iv)
			output_file.write_bytes(decrypted)
			print(f"OK    doc_id={row.doc_id} -> {output_file.name}")
			success += 1
		except Exception as exc:
			print(f"FAIL  doc_id={row.doc_id} file={row.encrypted_path}: {exc}")
			failed += 1

	print(
		f"Done. total={len(documents)} success={success} missing={missing} failed={failed}"
	)
	return 0 if failed == 0 else 2


if __name__ == "__main__":
	sys.exit(main())
