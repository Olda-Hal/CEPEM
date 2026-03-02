import json
import os
import re
import uuid
import mysql.connector
from mysql.connector import Error


class MySQLDatabase:
    def __init__(self, host="127.0.0.1", port=3306,
                 user="root", password="", database=None):
        self.config = {
            "host": host,
            "port": port,
            "user": user,
            "password": password,
            "database": database,
            "autocommit": False
        }
        self.conn = None

    def connect(self):
        try:
            self.conn = mysql.connector.connect(**self.config)
            if self.conn.is_connected():
                print("Connected to MySQL")
        except Error as e:
            raise RuntimeError(f"Connection error: {e}")

    def close(self):
        if self.conn and self.conn.is_connected():
            self.conn.close()
            print("Connection closed")

    def execute(self, query, params=None, fetch=False):
        """
        Univerzální metoda:
        - INSERT/UPDATE/DELETE → fetch=False
        - SELECT → fetch=True
        """
        if not self.conn or not self.conn.is_connected():
            raise RuntimeError("Not connected to database")

        cursor = self.conn.cursor(dictionary=True)

        try:
            cursor.execute(query, params or ())
            
            if fetch:
                result = cursor.fetchall()
                return result
            else:
                return cursor.rowcount

        except Error as e:
            self.conn.rollback()
            raise RuntimeError(f"Query failed: {e}")

        finally:
            cursor.close()

    def commit(self):
        if self.conn:
            self.conn.commit()

    def rollback(self):
        if self.conn:
            self.conn.rollback()

# ============================================================================
# Migrations pro CEPEM Healthcare
# ============================================================================

class CepemHealthcareMigration:
    """Třída pro správu migrací databáze cepem_healthcare"""
    
    def __init__(self, host="127.0.0.1", user="root", password="", port=3306):
        self.source_db = MySQLDatabase(
            host=host,
            user=user,
            password=password,
            database="premedical",
            port=port
        )
        self.target_db = MySQLDatabase(
            host=host,
            user=user,
            password=password,
            database="cepem_healthcare",
            port=port
        )
    
    def migrate_activities(self):
        """Migrace tabulky aktivit (_cinnosti) z premedical do cepem_healthcare"""
        print("\n🔄 Migruju činnosti (_cinnosti)...")
        
        try:
            self.source_db.connect()
            activities = self.source_db.execute("SELECT * FROM _cinnosti", fetch=True)
            
            if activities:
                self.target_db.connect()

                for activity in activities:
                    activity_id = activity['ident']
                    name_cs = activity['name']

                    existing = self.target_db.execute(
                        "SELECT Id FROM ExaminationTypes WHERE Id = %s", (activity_id,), fetch=True
                    )
                    if existing:
                        continue

                    self.target_db.execute(
                        "INSERT INTO Translations (EN, CS) VALUES (%s, %s)",
                        (name_cs, name_cs)
                    )
                    translation_id = self.target_db.execute(
                        "SELECT LAST_INSERT_ID() AS id", fetch=True
                    )[0]['id']

                    self.target_db.execute(
                        "INSERT INTO ExaminationTypes (Id, NameTranslationId) VALUES (%s, %s)",
                        (activity_id, translation_id)
                    )

                self.target_db.commit()
                print(f"  ✅ Migrace činností dokončena ({len(activities)} záznamů)")
        
        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci činností: {e}")
            raise
        
        finally:
            self.source_db.close()
            self.target_db.close()
    
    def migrate_hospitals(self):
        """Migrace tabulky nemocnic z premedical do cepem_healthcare"""
        print("\n🔄 Migruju nemocnice...")

        self.source_db.connect()
        hospitals = self.source_db.execute(
            "SELECT * FROM _strediska",
            fetch=True
        )
        self.source_db.close()

        if not hospitals:
            print("  ⚠️  Žádná data k migraci")
            return

        self.target_db.connect()

        try:
            # První průchod: insert všech hospitalů bez ParentHospitalId
            for hospital in hospitals:
                existing = self.target_db.execute(
                    "SELECT Id FROM Hospitals WHERE Id = %s", (hospital['ident'],), fetch=True
                )
                if existing:
                    continue

                street = f"{hospital['ulice']} {hospital['cislo']}".strip()
                address_id = None
                if street or hospital['mesto'] or hospital['psc'] or hospital['stat']:
                    self.target_db.execute(
                        "INSERT INTO Addresses (Street, City, PostalCode, Country) VALUES (%s, %s, %s, %s)",
                        (street, hospital['mesto'], hospital['psc'], hospital['stat'])
                    )
                    address_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                self.target_db.execute(
                    """INSERT INTO Hospitals (Id, Name, Active, AddressId, CompanyIco, CompanyName)
                       VALUES (%s, %s, 1, %s, %s, %s)""",
                    (
                        hospital['ident'],
                        hospital['alias'],
                        address_id,
                        hospital['ico'] or None,
                        hospital['firmaname'] or None,
                    )
                )

            # Druhý průchod: aktualizace ParentHospitalId
            valid_idents = {h['ident'] for h in hospitals}
            for hospital in hospitals:
                raw_parent = hospital['idparentstredisko']
                parent_id = raw_parent if raw_parent not in (-1, 0) and raw_parent in valid_idents else None
                if parent_id is not None:
                    self.target_db.execute(
                        "UPDATE Hospitals SET ParentHospitalId = %s WHERE Id = %s",
                        (parent_id, hospital['ident'])
                    )

            # Třetí průchod: kontakty
            for hospital in hospitals:
                has_contact = any([hospital['telcmobil'], hospital['telcdrat'], hospital['email']])
                if has_contact:
                    self.target_db.execute("INSERT INTO Contacts () VALUES ()")
                    contact_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                    self.target_db.execute(
                        "INSERT INTO ContactToObjects (ContactId, ObjectId, ObjectType) VALUES (%s, %s, 1)",
                        (contact_id, hospital['ident'])
                    )

                    if hospital['telcmobil']:
                        self.target_db.execute(
                            "INSERT INTO ContactPhoneNumbers (ContactId, PhoneNumber) VALUES (%s, %s)",
                            (contact_id, hospital['telcmobil'])
                        )
                    if hospital['telcdrat']:
                        self.target_db.execute(
                            "INSERT INTO ContactPhoneNumbers (ContactId, PhoneNumber) VALUES (%s, %s)",
                            (contact_id, hospital['telcdrat'])
                        )
                    if hospital['email']:
                        self.target_db.execute(
                            "INSERT INTO ContactEmails (ContactId, Email) VALUES (%s, %s)",
                            (contact_id, hospital['email'])
                        )

            self.target_db.commit()
            print(f"  ✅ Migrace nemocnic dokončena ({len(hospitals)} záznamů)")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci nemocnic: {e}")
            raise

        finally:
            self.target_db.close()

    def migrate_hospital_examination_types(self):
        """Migrace odborností středisek — M:N mezi Hospitals a ExaminationTypes"""
        print("\n🔄 Migruju odbornosti středisek...")

        self.source_db.connect()
        hospitals = self.source_db.execute(
            "SELECT ident, odbornosti FROM _strediska",
            fetch=True
        )
        examination_types = self.source_db.execute(
            "SELECT ident, odbornost FROM _cinnosti",
            fetch=True
        )
        self.source_db.close()

        # Mapa: kód odbornosti → ExaminationType.Id
        code_to_id = {row['odbornost']: row['ident'] for row in examination_types}

        self.target_db.connect()
        try:
            count = 0
            for hospital in hospitals:
                hospital_id = hospital['ident']
                raw = (hospital['odbornosti'] or '').strip()
                if not raw:
                    continue

                codes = [c.strip() for c in raw.split(';') if c.strip()]
                for code in codes:
                    et_id = code_to_id.get(code)
                    if et_id is None:
                        print(f"  ⚠️  Neznámá odbornost '{code}' pro středisko {hospital_id}, přeskočeno")
                        continue

                    existing = self.target_db.execute(
                        "SELECT Id FROM HospitalExaminationTypes WHERE HospitalId = %s AND ExaminationTypeId = %s",
                        (hospital_id, et_id), fetch=True
                    )
                    if existing:
                        continue

                    self.target_db.execute(
                        "INSERT INTO HospitalExaminationTypes (HospitalId, ExaminationTypeId) VALUES (%s, %s)",
                        (hospital_id, et_id)
                    )
                    count += 1

            self.target_db.commit()
            print(f"  ✅ Migrace odborností dokončena ({count} vazeb)")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci odborností: {e}")
            raise

        finally:
            self.target_db.close()

    def migrate_hospital_equipment(self):
        """Migrace přístrojů středisek z pocetpristroju JSON"""
        print("\n🔄 Migruju přístroje středisek...")

        self.source_db.connect()
        hospitals = self.source_db.execute(
            "SELECT ident, pocetpristroju FROM _strediska",
            fetch=True
        )
        self.source_db.close()

        self.target_db.connect()
        try:
            count = 0
            for hospital in hospitals:
                hospital_id = hospital['ident']
                raw = (hospital['pocetpristroju'] or '').strip()
                if not raw:
                    continue

                try:
                    items = json.loads(raw)
                except json.JSONDecodeError:
                    print(f"  ⚠️  Nepodařilo se parsovat JSON pro středisko {hospital_id}, přeskočeno")
                    continue

                for item in items:
                    raw_name = item.get('nameDevice', '')
                    # Source DB stores unicode without backslash (e.g. u017d instead of \u017d)
                    device_name = re.sub(r'u([0-9a-fA-F]{4})', r'\\u\1', raw_name).encode('utf-8').decode('unicode_escape')
                    device_count = int(item.get('count', 0))

                    if not device_name or device_name.upper() in ('ŽÁDNÝ', 'ŽÁDNÉ', 'ŽÁDNÁ'):
                        continue

                    # Upsert Equipment
                    existing_eq = self.target_db.execute(
                        "SELECT Id FROM Equipment WHERE Name = %s",
                        (device_name,), fetch=True
                    )
                    if existing_eq:
                        equipment_id = existing_eq[0]['Id']
                    else:
                        self.target_db.execute(
                            "INSERT INTO Equipment (Name) VALUES (%s)",
                            (device_name,)
                        )
                        equipment_id = self.target_db.execute(
                            "SELECT LAST_INSERT_ID() AS id", fetch=True
                        )[0]['id']

                    # Upsert HospitalEquipment
                    existing_he = self.target_db.execute(
                        "SELECT Id FROM HospitalEquipment WHERE HospitalId = %s AND EquipmentId = %s",
                        (hospital_id, equipment_id), fetch=True
                    )
                    if existing_he:
                        self.target_db.execute(
                            "UPDATE HospitalEquipment SET Count = %s WHERE Id = %s",
                            (device_count, existing_he[0]['Id'])
                        )
                    else:
                        self.target_db.execute(
                            "INSERT INTO HospitalEquipment (HospitalId, EquipmentId, Count) VALUES (%s, %s, %s)",
                            (hospital_id, equipment_id, device_count)
                        )
                    count += 1

            self.target_db.commit()
            print(f"  ✅ Migrace přístrojů dokončena ({count} záznamů)")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci přístrojů: {e}")
            raise

        finally:
            self.target_db.close()

    def migrate_patients(self):
        """Migrace pacientů z _kartoteka do Persons, Patients, Addresses, Contacts"""
        print("\n🔄 Migruju pacienty...")

        self.source_db.connect()
        patients = self.source_db.execute(
            "SELECT poradi, ident, sex, jmeno, prijmeni, titul_pred_jmenem, titul_za_jmenem, "
            "pojistovna, narozeni_date, bydliste_stat, bydliste_mesto, bydliste_ulice, "
            "bydliste_cislo, bydliste_psc, telcmobil, telcdrat, email, umrti_date "
            "FROM _kartoteka",
            fetch=True
        )
        self.source_db.close()

        if not patients:
            print("  ⚠️  Žádná data k migraci")
            return

        self.target_db.connect()
        try:
            count = 0
            for p in patients:
                source_id = p['poradi']

                existing = self.target_db.execute(
                    "SELECT Id FROM Patients WHERE Id = %s", (source_id,), fetch=True
                )
                if existing:
                    continue

                street = f"{p['bydliste_ulice'] or ''} {p['bydliste_cislo'] or ''}".strip()
                address_id = None
                if any([street, p['bydliste_mesto'], p['bydliste_psc'], p['bydliste_stat']]):
                    self.target_db.execute(
                        "INSERT INTO Addresses (Street, City, PostalCode, Country) VALUES (%s, %s, %s, %s)",
                        (street or '', p['bydliste_mesto'] or '', p['bydliste_psc'] or '', p['bydliste_stat'] or '')
                    )
                    address_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                gender = 'M' if p['sex'] == 0 else 'F'
                uid = str(p['ident'])
                self.target_db.execute(
                    "INSERT INTO Persons (FirstName, LastName, TitleBefore, TitleAfter, UID, Active, Gender, CreatedAt, AddressId) "
                    "VALUES (%s, %s, %s, %s, %s, 1, %s, NOW(), %s)",
                    (
                        p['jmeno'] or '',
                        p['prijmeni'] or '',
                        p['titul_pred_jmenem'] or None,
                        p['titul_za_jmenem'] or None,
                        uid,
                        gender,
                        address_id
                    )
                )
                person_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                has_contact = any([p['telcmobil'], p['telcdrat'], p['email']])
                if has_contact:
                    self.target_db.execute("INSERT INTO Contacts () VALUES ()")
                    contact_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                    self.target_db.execute(
                        "INSERT INTO ContactToObjects (ContactId, ObjectId, ObjectType, PersonId) VALUES (%s, %s, 0, %s)",
                        (contact_id, person_id, person_id)
                    )
                    if p['telcmobil']:
                        self.target_db.execute(
                            "INSERT INTO ContactPhoneNumbers (ContactId, PhoneNumber) VALUES (%s, %s)",
                            (contact_id, p['telcmobil'])
                        )
                    if p['telcdrat']:
                        self.target_db.execute(
                            "INSERT INTO ContactPhoneNumbers (ContactId, PhoneNumber) VALUES (%s, %s)",
                            (contact_id, p['telcdrat'])
                        )
                    if p['email']:
                        self.target_db.execute(
                            "INSERT INTO ContactEmails (ContactId, Email) VALUES (%s, %s)",
                            (contact_id, p['email'])
                        )

                birth_ts = p['narozeni_date']
                from datetime import datetime, timezone
                birth_date = datetime.fromtimestamp(birth_ts, tz=timezone.utc) if birth_ts else datetime(1900, 1, 1)

                pojistovna_raw = (p['pojistovna'] or '').strip()
                insurance_match = re.match(r'^(\d+)', pojistovna_raw)
                insurance_number = int(insurance_match.group(1)) if insurance_match else 0

                alive = 1 if not p['umrti_date'] else 0

                self.target_db.execute(
                    "INSERT INTO Patients (Id, PersonId, BirthDate, InsuranceNumber, Alive) VALUES (%s, %s, %s, %s, %s)",
                    (source_id, person_id, birth_date, insurance_number, alive)
                )
                count += 1

            self.target_db.commit()
            print(f"  ✅ Migrace pacientů dokončena ({count} záznamů)")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci pacientů: {e}")
            raise

        finally:
            self.target_db.close()

    def migrate_employees(self):
        """Migrace zaměstnanců z _zamestnanci do Employees, Persons, Roles, UserRoles, HospitalEmployees"""
        print("\n🔄 Migruju zaměstnance...")

        self.source_db.connect()
        employees = self.source_db.execute(
            "SELECT ident, sex, jmeno, prijmeni, titul_pred_jmenem, titul_za_jmenem, "
            "telc, email, strediskoident, odbornost, password FROM _zamestnanci",
            fetch=True
        )
        self.source_db.close()

        if not employees:
            print("  ⚠️  Žádní zaměstnanci nenalezeni")
            return

        try:
            self.target_db.connect()

            def get_or_create_role(name):
                row = self.target_db.execute(
                    "SELECT r.Id FROM Roles r JOIN Translations t ON t.Id = r.NameTranslationId WHERE t.EN = %s",
                    (name,), fetch=True
                )
                if row:
                    return row[0]['Id']
                self.target_db.execute(
                    "INSERT INTO Translations (EN, CS) VALUES (%s, %s)", (name, name)
                )
                translation_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                self.target_db.execute(
                    "INSERT INTO Roles (NameTranslationId) VALUES (%s)", (translation_id,)
                )
                self.target_db.commit()
                return self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

            import bcrypt, datetime

            count = 0
            for row in employees:
                ident = row['ident']
                sex = row['sex']
                jmeno = row['jmeno']
                prijmeni = row['prijmeni']
                titul_pred = row['titul_pred_jmenem']
                titul_za = row['titul_za_jmenem']
                telc = row['telc']
                email = row['email']
                strediskoident = row['strediskoident']
                odbornost = row['odbornost']
                password = row['password'] or 'changeme'

                uid = f"EMP{ident:04d}"
                gender = 'M' if sex == 0 else 'F'

                existing_employee = self.target_db.execute(
                    "SELECT Id FROM Employees WHERE Id = %s", (ident,), fetch=True
                )
                if existing_employee:
                    print(f"  ⏭️  Zaměstnanec {ident} již existuje, přeskakuji")
                    continue

                existing_person = self.target_db.execute(
                    "SELECT p.Id, e.Id AS EmployeeId FROM Persons p "
                    "LEFT JOIN Employees e ON e.PersonId = p.Id "
                    "WHERE p.FirstName = %s AND p.LastName = %s AND p.Gender = %s",
                    (jmeno, prijmeni, gender), fetch=True
                )
                if existing_person:
                    if existing_person[0]['EmployeeId'] is not None:
                        print(f"  ⏭️  {prijmeni} {jmeno} má již Employee, přeskakuji (ident={ident})")
                        continue
                    person_id = existing_person[0]['Id']
                    print(f"  ♻️  Nalezen existující Person ({jmeno} {prijmeni}), použiju person_id={person_id}")
                else:
                    self.target_db.execute(
                        "INSERT INTO Addresses (Street, City, PostalCode, Country) VALUES ('', '', '', '')"
                    )
                    address_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                    self.target_db.execute(
                        "INSERT INTO Persons (FirstName, LastName, Gender, TitleBefore, TitleAfter, UID, AddressId, Active, CreatedAt) "
                        "VALUES (%s, %s, %s, %s, %s, %s, %s, 1, NOW())",
                        (jmeno, prijmeni, gender, titul_pred or None, titul_za or None, uid, address_id)
                    )
                    person_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                contact_id = None
                if email and email.strip():
                    self.target_db.execute("INSERT INTO Contacts () VALUES ()")
                    contact_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                    self.target_db.execute(
                        "INSERT INTO ContactToObjects (ContactId, ObjectId, ObjectType, PersonId) VALUES (%s, %s, 0, %s)",
                        (contact_id, person_id, person_id)
                    )
                    self.target_db.execute(
                        "INSERT INTO ContactEmails (ContactId, Email) VALUES (%s, %s)",
                        (contact_id, email.strip())
                    )
                if telc and telc.strip():
                    if contact_id is None:
                        self.target_db.execute("INSERT INTO Contacts () VALUES ()")
                        contact_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                        self.target_db.execute(
                            "INSERT INTO ContactToObjects (ContactId, ObjectId, ObjectType, PersonId) VALUES (%s, %s, 0, %s)",
                            (contact_id, person_id, person_id)
                        )
                    self.target_db.execute(
                        "INSERT INTO ContactPhoneNumbers (ContactId, PhoneNumber) VALUES (%s, %s)",
                        (contact_id, telc.strip())
                    )

                password_hash = bcrypt.hashpw(password.encode(), bcrypt.gensalt()).decode()
                expiration = datetime.datetime(2027, 1, 1)
                self.target_db.execute(
                    "INSERT INTO Employees (Id, PersonId, PasswordHash, Salt, PasswordExpiration) VALUES (%s, %s, %s, %s, %s)",
                    (ident, person_id, password_hash, '', expiration)
                )

                if strediskoident:
                    hospital_exists = self.target_db.execute(
                        "SELECT Id FROM Hospitals WHERE Id = %s", (strediskoident,), fetch=True
                    )
                    if hospital_exists:
                        self.target_db.execute(
                            "INSERT INTO HospitalEmployees (HospitalId, EmployeeId) VALUES (%s, %s)",
                            (strediskoident, ident)
                        )

                if odbornost and odbornost.strip():
                    roles = list(dict.fromkeys(r.strip() for r in odbornost.split(';') if r.strip()))
                    for role_name in roles:
                        role_id = get_or_create_role(role_name)
                        already = self.target_db.execute(
                            "SELECT Id FROM UserRoles WHERE UserId = %s AND RoleId = %s",
                            (person_id, role_id), fetch=True
                        )
                        if not already:
                            self.target_db.execute(
                                "INSERT INTO UserRoles (UserId, RoleId) VALUES (%s, %s)",
                                (person_id, role_id)
                            )

                self.target_db.commit()
                count += 1
                print(f"  ✅ {prijmeni} {jmeno} (ident={ident}, roles={odbornost})")

            print(f"\n  ✅ Migrace zaměstnanců dokončena ({count} záznamů)")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci zaměstnanců: {e}")
            raise

        finally:
            self.target_db.close()

    def clear_all_data(self):
        """Smaže všechna migrovaná data z cílové DB pro čistou re-migraci. Zachová seed data (EventTypes, Symptoms atd.) a EF migrační historii."""
        print("\n🗑️  Mažu všechna data z cílové DB...")
        try:
            self.target_db.connect()
            self.target_db.execute("SET FOREIGN_KEY_CHECKS = 0")

            tables = [
                "Vaccines", "Pregnancies", "PatientSymptoms", "Injuries",
                "DrugUses", "Events", "PatientDocuments", "Examinations",
                "Reservations", "Appointments", "Comments",
                "DoctorExaminationRooms", "ExaminationRooms",
                "HospitalExaminationTypes", "HospitalEquipment", "HospitalEmployees",
                "UserRoles", "Employees",
                "ContactEmails", "ContactPhoneNumbers", "ContactToObjects", "Contacts",
                "Patients", "Persons", "Addresses",
                "FirstNameHistories", "LastNameHistories", "PhoneNumberHistories",
                "EmailHistories", "ActivityLogs",
                "Equipment", "HospitalEquipment",
                "Hospitals",
                "Roles",
                "DrugToDrugCategories", "Drugs", "DrugCategories",
                "InjuryTypes", "VaccineTypes", "Symptoms", "EventTypes", "ExaminationTypes",
                "Translations",
            ]

            for table in tables:
                self.target_db.execute(f"DELETE FROM `{table}`")
                self.target_db.execute(f"ALTER TABLE `{table}` AUTO_INCREMENT = 1")
                print(f"  ✅ {table}")

            self.target_db.execute("SET FOREIGN_KEY_CHECKS = 1")
            self.target_db.commit()
            print("\n  ✅ Všechna data smazána")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při mazání dat: {e}")
            raise

        finally:
            self.target_db.execute("SET FOREIGN_KEY_CHECKS = 1")
            self.target_db.close()

    def migrate_examinations(self, files_base_dir: str = os.path.expanduser("~/database_CEPEM/clients/files"),
                             doc_storage_dir: str = "/home/olda/programovani/CEPEM/data/patient-documents"):
        """Migrace vyšetření z client_* tabulek do Events, Examinations, Comments, ExaminationDocuments"""
        import datetime
        from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        from cryptography.hazmat.backends import default_backend

        ENCRYPTION_KEY = b"CEPEMSecureKey1234567890123456\x00\x00"[:32]
        ENCRYPTION_IV  = b"CEPEMInitVector1"[:16]

        def encrypt_file(data: bytes) -> bytes:
            pad_len = 16 - (len(data) % 16)
            padded = data + bytes([pad_len] * pad_len)
            cipher = Cipher(algorithms.AES(ENCRYPTION_KEY), modes.CBC(ENCRYPTION_IV), backend=default_backend())
            enc = cipher.encryptor()
            return enc.update(padded) + enc.finalize()

        os.makedirs(doc_storage_dir, exist_ok=True)

        print("\n🔄 Migruju vyšetření...")

        self.source_db.connect()
        client_tables = self.source_db.execute(
            "SELECT table_name FROM information_schema.tables "
            "WHERE table_schema = 'premedical' AND table_name LIKE 'client_%' "
            "ORDER BY table_name",
            fetch=True
        )
        self.source_db.close()

        if not client_tables:
            print("  ⚠️  Žádné client_* tabulky nenalezeny")
            return

        try:
            self.target_db.connect()

            def get_or_create_event_type(name: str) -> int:
                row = self.target_db.execute(
                    "SELECT et.Id FROM EventTypes et JOIN Translations t ON t.Id = et.NameTranslationId WHERE t.EN = %s",
                    (name,), fetch=True
                )
                if row:
                    return row[0]['Id']
                self.target_db.execute("INSERT INTO Translations (EN, CS) VALUES (%s, %s)", (name, name))
                trans_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                self.target_db.execute("INSERT INTO EventTypes (NameTranslationId) VALUES (%s)", (trans_id,))
                self.target_db.commit()
                return self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

            def get_or_create_examination_type(name: str) -> int:
                row = self.target_db.execute(
                    "SELECT et.Id FROM ExaminationTypes et JOIN Translations t ON t.Id = et.NameTranslationId WHERE t.EN = %s",
                    (name,), fetch=True
                )
                if row:
                    return row[0]['Id']
                self.target_db.execute("INSERT INTO Translations (EN, CS) VALUES (%s, %s)", (name, name))
                trans_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']
                self.target_db.execute("INSERT INTO ExaminationTypes (NameTranslationId) VALUES (%s)", (trans_id,))
                self.target_db.commit()
                return self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

            examination_event_type_id = get_or_create_event_type("Vyšetření")

            total_events = 0
            total_files = 0
            total_skipped = 0

            for tbl_row in client_tables:
                table_name = tbl_row['table_name']
                # kartoteka ident = část za 'client_'
                kartoteka_ident = table_name[len('client_'):]

                patient_row = self.target_db.execute(
                    "SELECT pa.Id FROM Patients pa JOIN Persons pe ON pe.Id = pa.PersonId WHERE pe.UID = %s",
                    (kartoteka_ident,), fetch=True
                )
                if not patient_row:
                    print(f"  ⚠️  Pacient UID={kartoteka_ident} nenalezen, přeskakuji tabulku {table_name}")
                    continue

                patient_id = patient_row[0]['Id']

                self.source_db.connect()
                examinations = self.source_db.execute(
                    f"SELECT ident, iduser, date, druh, typ, popis, poznamka, "
                    f"vaha, vyska, puls, tlak, dechfrekvence FROM `{table_name}`",
                    fetch=True
                )
                self.source_db.close()

                for ex in examinations:
                    ex_ident    = ex['ident']
                    druh        = (ex['druh'] or 'Neuvedeno').strip() or 'Neuvedeno'
                    popis       = ex['popis'] or ''
                    poznamka    = ex['poznamka'] or ''
                    ts          = ex['date'] or 0
                    happened_at = datetime.datetime.fromtimestamp(ts) if ts else datetime.datetime(2000, 1, 1)

                    exam_type_id = get_or_create_examination_type(druh)

                    # Idempotence: Event se stejným pacientem, časem a typem vyšetření
                    existing = self.target_db.execute(
                        "SELECT e.Id FROM Events e "
                        "JOIN Examinations ex ON ex.EventId = e.Id "
                        "WHERE e.PatientId = %s AND e.HappenedAt = %s AND ex.ExaminationTypeId = %s",
                        (patient_id, happened_at, exam_type_id), fetch=True
                    )
                    if existing:
                        total_skipped += 1
                        continue

                    comment_id = None
                    combined_text = '\n\n'.join(filter(None, [popis, poznamka])).strip()
                    if combined_text:
                        self.target_db.execute(
                            "INSERT INTO Comments (Text) VALUES (%s)", (combined_text,)
                        )
                        comment_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                    self.target_db.execute(
                        "INSERT INTO Events (PatientId, EventTypeId, HappenedAt, CommentId) VALUES (%s, %s, %s, %s)",
                        (patient_id, examination_event_type_id, happened_at, comment_id)
                    )
                    event_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                    self.target_db.execute(
                        "INSERT INTO Examinations (ExaminationTypeId, EventId) VALUES (%s, %s)",
                        (exam_type_id, event_id)
                    )
                    examination_id = self.target_db.execute("SELECT LAST_INSERT_ID() AS id", fetch=True)[0]['id']

                    # Soubory pro toto vyšetření
                    vysetreni_dir = os.path.join(
                        files_base_dir,
                        f"client_{kartoteka_ident}",
                        f"vysetreni_{ex_ident:08d}"
                    )
                    if os.path.isdir(vysetreni_dir):
                        for filename in os.listdir(vysetreni_dir):
                            filepath = os.path.join(vysetreni_dir, filename)
                            if not os.path.isfile(filepath):
                                continue
                            try:
                                with open(filepath, 'rb') as f:
                                    raw = f.read()
                                encrypted = encrypt_file(raw)
                                enc_filename = f"examination_{examination_id}_{uuid.uuid4()}.enc"
                                enc_path = os.path.join(doc_storage_dir, enc_filename)
                                with open(enc_path, 'wb') as f:
                                    f.write(encrypted)
                                self.target_db.execute(
                                    "INSERT INTO ExaminationDocuments "
                                    "(ExaminationId, FileName, OriginalFileName, UploadedAt, FileSize, EncryptedPath, IsDeleted) "
                                    "VALUES (%s, %s, %s, %s, %s, %s, 0)",
                                    (examination_id, enc_filename, filename, happened_at, len(raw), enc_filename)
                                )
                                total_files += 1
                            except Exception as fe:
                                print(f"    ⚠️  Soubor {filename}: {fe}")

                    self.target_db.commit()
                    total_events += 1

                print(f"  ✅ {table_name}: {len(examinations)} vyšetření")

            print(f"\n  ✅ Migrace vyšetření dokončena: {total_events} nových, {total_skipped} přeskočeno, {total_files} souborů")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci vyšetření: {e}")
            raise

        finally:
            if self.source_db.conn and self.source_db.conn.is_connected():
                self.source_db.close()
            self.target_db.close()


    def migrate_patient_photos(self, photo_storage_dir="/home/olda/programovani/CEPEM/data/patient-photos"):
        print("\n🔄 Migruju fotky pacientů...")

        ENCRYPTION_KEY = b"CEPEMSecureKey1234567890123456\x00\x00"[:32]
        ENCRYPTION_IV  = b"CEPEMInitVector1"[:16]

        from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        from cryptography.hazmat.primitives import padding
        from cryptography.hazmat.backends import default_backend
        import base64

        def encrypt(data: bytes) -> bytes:
            padder = padding.PKCS7(128).padder()
            padded = padder.update(data) + padder.finalize()
            cipher = Cipher(algorithms.AES(ENCRYPTION_KEY), modes.CBC(ENCRYPTION_IV), backend=default_backend())
            enc = cipher.encryptor()
            return enc.update(padded) + enc.finalize()

        os.makedirs(photo_storage_dir, exist_ok=True)

        try:
            self.source_db.connect()
            rows = self.source_db.execute(
                "SELECT ident, photodata FROM _kartoteka WHERE photodata LIKE 'data:image%'",
                fetch=True
            )
            self.source_db.close()

            self.target_db.connect()

            total_saved = 0
            total_skipped = 0

            for row in rows:
                kartoteka_ident = str(row['ident'])
                photodata = row['photodata']

                patient_row = self.target_db.execute(
                    "SELECT pa.Id, pa.PhotoPath FROM Patients pa "
                    "JOIN Persons pe ON pe.Id = pa.PersonId WHERE pe.UID = %s",
                    (kartoteka_ident,), fetch=True
                )
                if not patient_row:
                    total_skipped += 1
                    continue

                patient_id = patient_row[0]['Id']
                existing_photo = patient_row[0]['PhotoPath']

                if existing_photo:
                    total_skipped += 1
                    continue

                try:
                    # Strip data URL prefix: "data:image/png;base64,..."
                    b64_data = photodata.split(',', 1)[1]
                    raw_bytes = base64.b64decode(b64_data)
                except Exception:
                    print(f"  ⚠️  Pacient UID={kartoteka_ident}: nelze dekódovat base64, přeskakuji")
                    total_skipped += 1
                    continue

                encrypted = encrypt(raw_bytes)
                file_name = f"patient_{patient_id}_{uuid.uuid4()}.enc"
                file_path = os.path.join(photo_storage_dir, file_name)

                with open(file_path, 'wb') as f:
                    f.write(encrypted)

                self.target_db.execute(
                    "UPDATE Patients SET PhotoPath = %s WHERE Id = %s",
                    (file_name, patient_id)
                )
                self.target_db.commit()
                total_saved += 1

            print(f"\n  ✅ Migrace fotek dokončena: {total_saved} uloženo, {total_skipped} přeskočeno")

        except Exception as e:
            self.target_db.rollback()
            print(f"  ❌ Chyba při migraci fotek: {e}")
            raise

        finally:
            if self.source_db.conn and self.source_db.conn.is_connected():
                self.source_db.close()
            self.target_db.close()


if __name__ == "__main__":
    # Inicializuj migraci s tvými credentials
    migration = CepemHealthcareMigration(
        host="127.0.0.1",
        user="root",
        password="oldaolda",
        port=3306
    )
    
    # Spusť všechny migrace
    migration.migrate_activities()
    migration.migrate_hospitals()
    migration.migrate_hospital_examination_types()
    migration.migrate_hospital_equipment()
    migration.migrate_patients()
    migration.migrate_employees()
    migration.migrate_examinations()
    migration.migrate_patient_photos()
