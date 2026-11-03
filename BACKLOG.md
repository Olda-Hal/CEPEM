# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.
Stav k 2026-11-03: všechny dokončené body byly z dokumentu odstraněny — níže
zůstávají pouze otevřené úkoly a nápady.

## Chybí dodělat

- **Pokračování Vyhodnocování** — navazující flow podle specifikace (jednání podle doporučení: kdo, co, kdy). Čeká na zadání.
- **Rezervace → Návštěva (UI)** — backend už zakládá návštěvu z potvrzeného slotu; zbývá karta slotu → „zahájit návštěvu“ a zobrazení zdrojového slotu na detailu návštěvy.
- **Vstupní formulář jako PDF** — preview intake formuláře v návštěvě je teď text z komentáře; zvážit generování/ukládání reálného PDF dokumentu (konzistentní s ostatními přílohami, tisk).
- **Editace návštěvy v UI** — datum návštěvy i odpojení přílohy/smazání návštěvy (PATCH endpoint má, FE zatím umí jen poznámku).
- **E2E suite pro TOP 1 toky** — playwright testy (scoping viditelnost zaměstnanců, vytvoření pacienta s/bez centra, návštěvy vč. backfill sanity) jako trvalé repro.
- **Hlídání „orphan“ příloh** — dokumenty bez návštěvy po novém toku by už neměly vznikat; přidat index/view/report kontrolující, že každý dokument má `VisitDocument` záznam (dev DB má 0, ověřit na produkci).

## Technický dluh a nálezy

- Znovupovolit výběr barevného schématu, až budou opraveny ostatní motivy: teď je natvrdo píchnutá `PINNED_THEME='aurora'` v `ThemeManager.ts` a odebraný `ThemeSelector` z AppShell headeru (komponenta zůstala v repu pro pozdější návrat; rozbité motivy = nebula-blue…crimson-night).
- `theme-overrides.css` postupně rozebrat — je to globální `!important` kladivo držící theming pohromadě; cíl: per-component theme tokeny.
- Audit log: staré záznamy nemají uživatele/metodu/status (v tabulce jen „—“) — nové záznamy uživatele mají (opraveno pořadí middleware + service key), zbývá zvážit backfill starých řádků nebo lepší render.
- DB hygiena: v tabulkách zaměstnanců jsou záznamy s UUID UID („Admin User“) a ve správě rolí bezjmenná role; skript na sjednocení + UNIQUE ochrana před duplicitními rolemi.
- Nativní date/time inputy se renderují podle locale prohlížeče (v headless testech en-US formát) — sjednotit cs-CZ, případně vlastní pickery.
- Naturální lokalizace: strojové anglické labely v tabulkách pacientů/zaměstnanců (Status „Živ“, názvy typů událostí/rolí z EN překladů) přeložit do CS i EN klíčů konzistentně.
- TestDashboard (`/tests`): stránka je značně děravá — zvážit smazání routy celé, nebo vysázení na reálná data.
- `IntakeFormLinkService.MarkUsedAsync` není atomický (race vracející „already used“): doplnit DB-level check `UsedAtUtc IS NULL` přímo v UPDATE.
- `UidService` stále může v race zdědit duplicitní UID při souběžném vytvoření dvou pacientů (gap-filling nelze serializovat). UNIQUE constraint už existuje (`UniqueUidWithBackfill`); zbývá retry při `DbUpdateException`/duplicitě.
- `TestResultsController` má kód seriálovitě na disk (test-results mount) — zvážit symlink-escape + path traversal review po sloučení do gateway (dnes za perm `tests.read`).

## Nápady z celosystémového průzkumu (2026-11-01)

Nové kandidáty z průzkumu backendu, frontendu, provozu a veřejného webu.
Nejsou to závazky; v každé skupině jsou seřazené zhruba podle hodnoty.

**Doporučené první:** integrita šifrování, validace uploadů a PII v logech.

### Bezpečnost a správnost dat

- **Šifrování souborů nemá integritu** — AES-CBC bez MAC a klíč z `PadRight/Substring` (není KDF); manipulace s ciphertextem se nepozná. Přejít na AES-GCM/Encrypt-then-MAC s korektní derivací a migrací stávajících souborů (`DocumentEncryptionService.cs`, `PhotoEncryptionService.cs`).
- **Uploady věří Content-Type a příponě** — u fotek i dokumentů validovat magic bytes/skutečné dekódování (ideálně antivirová kontrola), ne jen hlavičku od klienta (`PatientsController.cs`).
- **Relace a impersonace nejsou dohledatelné/odvolatelné** — 8h JWT bez refresh/logout endpointu, změna hesla ani deaktivace token nezruší, `AccessControlVersion` se neověřuje a audit nepropisuje `impersonator_id`; doplnit revokaci, server-side expiraci hesla a audit náhledu (`AuthService.cs`, `PermissionAuthorization.cs`, `AdminController.cs`).
- **Veřejný intake mění kmenová data bez revize** — anonymní odkaz přepisuje jméno, datum narození, pojišťovnu i adresu bez schválení personálem a submit (pacient → event → označení linku) není transakční; zvážit frontu změn k potvrzení a transakci (`EventsController.cs`).
- **PII v logech je v plaintextu** — `EmailLog` (adresa, jméno) i `ActivityLog` (e-mail) drží osobní údaje nešifrovaně a CSV export je vrací bez maskování; zvážit maskování/redakci a kratší retenci těchto sloupců (`EmailLog.cs`, `ActivityLog.cs`, `EmailLogsController.cs`).
- **Chybí unit/integration testy kritických endpointů a stránek** — reset hesla, uploady, kompletní intake a chybějící testy stránek (návštěvy, rezervace, admin, formuláře); doplnit cílené testy (doplňuje, nenahrazuje E2E TOP 1).

### Klinická agenda a produkt

- **Rezervace pro existujícího pacienta** — `BookingPanel` posílá vždy `newPerson`, i když API umí `personId`; přidat vyhledání pacienta, aby se osoba neopisovala znovu a nevznikaly duplicity (`BookingPanel.tsx`, `types/index.ts`).
- **Sloučení duplicitních pacientů** — při nejednoznačném matche se záměrně zakládá nová osoba; přidat admin nástroj na sloučení (kontakty, návštěvy, dokumenty, UID) s auditní stopou a náhledem dopadu (`PersonMatchingService.cs`, admin UI).
- **Detail pacienta bere náhodný intake a nemá stránkování** — `FormSubmissions.FirstOrDefault()` bez `OrderBy` může zobrazit starý formulář; vzít latest podle `SubmittedAtUtc` a historii eventů/dokumentů stránkovat (`PatientsController.cs`).
- **Legacy `Reservation` tabulka** — nikde se už nevytváří, ale `GetPatientDetail` ji stále merguje do appointments s negativními ID a `IntakeFormLink` na ni drží FK; rozhodnout migraci/odstranění (`Reservation.cs`, `PatientsController.cs`).
- **Nadcházející termíny na detailu pacienta** — `PatientDetail` obsahuje `appointments`, ale stránka zobrazuje jen jejich počet; zobrazit seznam nadcházejících rezervací/termínů (`PatientDetailPage.tsx`).
- **Filtry, řazení a export pacientů** — `PatientsPage` hardcoduje `sortBy=lastName`; API už `sortBy/sortOrder` umí, chybí filtr živý/mrtvý, centrum a CSV export (`PatientsPage.tsx`).
- **Tabulka vyšetření je slepá ulička** — řádky nejsou klikatelné (přitom obsahují `patientId`) a názvy dokumentů nejdou otevřít/stáhnout; propojit s pacientem a přílohami (`ExaminationsPage.tsx`).
- **Tisk/PDF/ZIP návštěvy a vyhodnocovacího reportu** — z detailu návštěvy ani reportu nelze vytisknout/uložit kompletní dokumentaci (poznámka + přílohy + hodnocení); přidat tiskový výstup/export (`VisitDetailPage.tsx`, `EvaluationReportModal.tsx`).
- **Ochrana neuložených změn** — dlouhý intake, poznámka návštěvy i editace pacienta se ztratí při navigaci/back/refresh; přidat router blocker a `beforeunload` (`VisitDetailPage.tsx`, `EditPatientPage.tsx`, `IntakeFormByLinkPage.tsx`).
- **Založení pacienta není atomické** — `AddPatientFormPage` nejprve vytvoří pacienta a pak zvlášť posílá intake; když druhý krok selže, pacient zůstane a opakování vytvoří duplicitu. Zavést idempotenci/kompenzaci nebo jeden transakční endpoint (`AddPatientFormPage.tsx`).
- **Rozhodnout osud obecných událostí** — `AddEventModal` (léky/symptomy/zranění/očkování) není nikde importován a `PatientEventsSection` je odpojená; buď event flow znovu napojit, nebo odstranit (`AddEventModal.tsx`, `patient/PatientEventsSection.tsx`).
- **Quick Preview nastavení se neukládá** — `SettingsController` GET vrací default a PUT jen echo; uživatel navíc při chybě dostane zavřený modal bez hlášky. Doplnit per-user persistenci a error handling (`SettingsController.cs`, `QuickPreviewSettingsModal.tsx`).
- **Dashboard stats je stub** — vrací jen počet zaměstnanců, last login a „Online“; doplnit dnešní sloty/návštěvy, nevyhodnocené případy a nevyplněné intake, ideálně se scope na centrum (`EmployeesController.cs`).
- **Návštěva nemá lékaře, typ ani stav** — `Visit` neumí evidovat, kdo pacienta vyšetřil, typ návštěvy ani `planned/completed/no-show`; doplnit pole a stavový automat (`Visit.cs`, `VisitDetailPage.tsx`).
- **E-mail log neumí retry ani časový filtr** — u `Failed` chybí „poslat znovu“ a filtr jen podle typu/stavu/hledání; přidat datumový rozsah, reset filtrů a bezpečný retry (`EmailLogPage.tsx`, `EmailLogsController.cs`).
- **Jednotné destruktivní dialogy a editor oprávnění** — část akcí používá nativní `window.confirm`, `PermissionsModal` zadává `scopeType/scopeId` jako volný text a existující override jen maže. Sjednotit potvrzovací modal s dopadem a udělat selecty/validaci (`PermissionsModal.tsx`, admin stránky).
- **Nekonzistentní politika hesel** — reset povolí 6 znaků, změna/vytvoření vyžaduje 8 a vytvoření zaměstnance nemá potvrzení hesla; sjednotit validaci (`ResetPasswordPage.tsx`, `PasswordChangeModal.tsx`, `CreateEmployeeModal.tsx`).

### UX, i18n a odolnost frontendu

- **Mobilní navigace v AppShell** — pod 900 px se schová jen e-mail, ale menu, jazyk, nastavení a odhlášení zůstávají v jednom neobalovaném řádku; přidat hamburger/drawer (`AppShell.tsx`, `AppShell.css`).
- **Chybí ErrorBoundary a 404 route** — pád renderu nebo lazy chunku nechá prázdnou stránku a neznámá URL vykreslí jen prázdný shell (`index.tsx`, `App.tsx`).
- **Chyby serveru se ztrácejí** — kód čte neexistující `err.response?.data`, zatímco `ApiError` má `message/status`; validační důvod z backendu se nikdy nezobrazí (`CreateEmployeeModal.tsx`, `AddIntakeFormEventModal.tsx`, `AddPatientFormPage.tsx`).
- **Accessibility mezipaměťových komponent** — `SearchableMultiSelect` nemá šipky/`aria-activedescendant`, `TdbSliceCard` přidává markery klikem na nefocusovatelný `div`, toasty jdou zavřít jen myší, `LanguageSwitcher` nemá label.
- **Landing path může zacyklit redirect** — `getLandingPath` vrací `defaultPath` bez kontroly oprávnění a `RoleRoute` nepovolenou stránku přesměruje na `/dashboard`, který ji vrátí znovu; validovat `defaultPath` proti roli nebo přesměrovat na bezpečný fallback (`landing.ts`, `RoleRoute.tsx`, `RoleManagementPage.tsx`).
- **Debug data v produkční editaci pacienta** — `EditPatientPage` renderuje „Structured FormSubmission“, „Signature Vector Raw Data“ a „Raw Patient JSON“; schovat za feature flag/admin debug (`EditPatientPage.tsx`).
- **Mrtvý kód a duplicity** — `CreatePatientModal` se nikdy neotevře (`showCreateModal` zůstává false), `CameraCapture`/`ExtractableText`/`PatientDocumentsSection` nejsou použité a `EditPatientPage` duplikuje parser z `utils/intakeSummary.ts`; uklidit (`PatientsPage.tsx`, `components/`).

### Provoz, CI a infrastruktura

- **Chybí `.dockerignore`** — frontend build context je celý repo, takže se do builderu posílá `.env`, `data/` s pacientskými soubory a `test-results/`; přidat ignore pro `.env`, `data`, `.venv`, `bin`, `obj`, `node_modules`, `test-results`.
- **Zálohy nejsou šifrované, offsite ani testované** — `backup_daily.sh` ukládá dump + tar na stejný disk, bez GPG, cronu a restore testu; `.env` s šifrovacími klíči se nezálohuje, takže po ztrátě serveru jsou soubory neobnovitelné (`scripts/backup_daily.sh`).
- **Testovací pipeline může projít zeleně bez testů** — `Dockerfile.test` volá neexistující `npm run test:coverage:tolerant` a maskuje chybu `|| echo`, `build.sh` vždy vrací 0 a `test-reporter` startuje bez `service_completed_successfully`; opravit fail-fast a pořadí (`frontend/Dockerfile.test`, `build.sh`, `docker-compose.test.yml`).
- **CI nemá hardening ani skenování závislostí** — chybí `permissions`, `concurrency`, NuGet cache, `compose config` validace a artefakty Playwrightu; přidat Dependabot/Renovate, `dotnet list package --vulnerable`, `npm audit`/Trivy a připnout plovoucí tagy (`mysql:8.0`, `nginx:alpine`, `alpine:latest`), sjednotit Node 20 vs 22 (`.github/workflows/ci.yml`, Dockerfily).
- **Chybí alerting, uptime a error tracking** — Prometheus nemá `rule_files`/Alertmanager, není externí uptime ani Sentry a TSDB nemá `--storage.tsdb.retention.size`; výpadek DB, plný disk nebo expirace certu nikoho neupozorní (`deploy/prometheus/prometheus.yml`, `docker-compose.prod.yml`).
- **Logy kontejnerů nejsou rotované ani centrální** — Serilog píše JSON na stdout a compose nemá `logging.options`, takže logy rostou bez limitu; nastavit `max-size/max-file` a zvážit Loki/Seq (`docker-compose.yml`, `docker-compose.prod.yml`).
- **Kontejnery běží jako root a DB pod rootem** — Dockerfily nemají `USER`, compose `read_only/cap_drop`, a connection string/healthcheck/`mysqldump` používají MySQL root; přejít na neprivilegované uživatele a dedikovaný DB účet (Dockerfily, `.env.example`, `scripts/backup_daily.sh`).
- **Activity log a retence nejsou škálovatelné** — middleware loguje i `/metrics` synchronně na každý request a retence načítá všechny staré řádky do paměti; vyjmout `/metrics`, zápis batchovat/asynchronizovat a mazat přes `ExecuteDeleteAsync` (`ActivityLoggingMiddleware.cs`, `ActivityLogRetentionService.cs`).
- **Vyhodnocování načítá celou tabulku do paměti** — `GetCases`/`GetNext` dělají `ToListAsync()` všech případů s Include a filtrují až v C#; country scope, filtr recenzí a stránkování přesunout do SQL (`EvaluationsController.cs`).
- **Edge nginx není hardeningovaný** — chybí `limit_req/limit_conn`, `server_tokens off` a CSP (immutable cache pro hashované bundly už je); přes frontend navíc veřejně prosvítá `/db-health` do DatabaseAPI (`nginx-reverse-proxy.conf`, `frontend/nginx.conf`).
- **Chybí validace konfigurace a readiness** — aplikace kontroluje jen neprázdnost, takže projde ukázkový JWT klíč nebo `localhost` URL; healthchecky vrací natvrdo `Healthy` bez dotazu do DB. Přidat startovní validaci placeholdrů, oddělit liveness/readiness s `CanConnectAsync`, vendorovat `wait-for-it.sh` a nastavit `ASPNETCORE_ENVIRONMENT=Production` jako default image (`Program.cs`, HealthControllery, `DatabaseAPI/Dockerfile`).
- **Onboarding a lokální tasky se rozcházejí s realitou** — `build.sh`/README odkazují na neexistující AI službu a běžné úkony (testy, migrace, záloha, reset DB) jsou rozeseté v dokumentaci; přidat `Makefile`/`justfile` a srovnat odkazy (`build.sh`, `README.md`, `TESTING.md`).
- **Migrace při startu bez zámku a fail-fast** — `MigrateAsync` běží při každém startu, chybu jen zaloguje a aplikace naskočí nad neúspěšnou migrací; při více replikách hrozí souběh. Zavést zámek a fail-fast/readiness gate, případně migrace přesunout do deploy kroku (`Program.cs`, `MigrationService.cs`).
- **Chybí retence a výmaz pacientských dat** — retence řeší jen activity/e-mail log; smazané dokumenty zůstávají na disku navždy a fotky/dokumenty/návštěvy se neanonymizují, což je problém pro GDPR i růst `data/` (`PatientDocumentService.cs`, `ExaminationDocumentService.cs`).

### Veřejný rezervační web (CEPEM-website)

- **Žádost o rezervaci neposílá potvrzení ani číslo rezervace** — web jen zablokuje slot a zobrazí hlášku; po odeslání žádosti pacient nedostane e-mail s detaily, stavem „čeká na potvrzení“ ani referencí pro komunikaci (`CEPEM-website/src/pages/Reservation.tsx`).
- **Chybí samoobslužná cancelace/přesun** — pacient nemůže rezervaci zrušit nebo změnit; přidat signed odkaz do potvrzovacího e-mailu a bezpečný endpoint (`CEPEM-website`, DatabaseAPI).
- **Veřejná rezervace nemá anti-spam ani service auth** — `block` endpoint je dostupný komukoli a FastAPI proxy neposílá `X-Service-Key`; přidat rate limit/CAPTCHA/honeypot a dedikované veřejné API nebo service key (`CEPEM-website/backend/app/main.py`).
- **Web nemá nizozemštinu** — `CEPEM-website` má jen cs/en, zatímco CEPEM už nl používá; doplnit `nl` locale a jazyk rezervace (`src/i18n/resources/`).
- **Chybí GDPR souhlas před odesláním rezervace** — formulář sbírá jméno, e-mail a telefon bez checkboxu souhlasu a odkazu na zásady; doplnit consent a uložit jeho verzi (`Reservation.tsx`).
- **Legacy availability endpoint a waitlist** — `/api/clinics/{id}/availability` má hardcoded otevírací hodiny 8–17 a mrtvý `Reservation` model; uklidit, případně přidat waitlist/„upozornit, až bude volný termín“ (`CEPEM-website/backend/app/main.py`).
