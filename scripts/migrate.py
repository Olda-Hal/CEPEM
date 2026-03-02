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
            # Připoj se ke zdrojové databázi
            self.source_db.connect()
            
            # Přečti data ze zdroje
            activities = self.source_db.execute(
                "SELECT * FROM _cinnosti",
                fetch=True
            )
            
            
            if activities:
                # Připoj se k cílové databázi
                self.target_db.connect()
                
                # Vlož data do cíle
                for activity in activities:
                    values = tuple(activity.values())
                    self.target_db.execute(
                        "INSERT IGNORE INTO ExaminationTypes (Id, Name) VALUES (%s, %s)",
                        (values[0], values[1])
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
