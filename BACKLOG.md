# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.
Stav k 2026-11-04: všechny dokončené body byly z dokumentu odstraněny — níže
zůstávají pouze otevřené úkoly a nápady.

## Chybí dodělat

- **Pokračování Vyhodnocování** — navazující flow podle specifikace (jednání podle doporučení: kdo, co, kdy). Čeká na zadání.
- **Rezervace → Návštěva (UI)** — backend už zakládá návštěvu z potvrzeného slotu; zbývá karta slotu → „zahájit návštěvu“ a zobrazení zdrojového slotu na detailu návštěvy.
- **Vstupní formulář jako PDF** — preview intake formuláře v návštěvě je teď text z komentáře; zvážit generování/ukládání reálného PDF dokumentu (konzistentní s ostatními přílohami, tisk).
- **Editace návštěvy v UI** — datum návštěvy i odpojení přílohy/smazání návštěvy (PATCH endpoint má, FE zatím umí jen poznámku).
- **Hlídání „orphan“ příloh** — dokumenty bez návštěvy po novém toku by už neměly vznikat; přidat index/view/report kontrolující, že každý dokument má `VisitDocument` záznam (dev DB má 0, ověřit na produkci).

## Technický dluh a nálezy

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

### Bezpečnost a správnost dat

- **Chybí unit/integration testy kritických endpointů a stránek** — reset hesla, uploady, kompletní intake a chybějící testy stránek (návštěvy, rezervace, admin, formuláře); doplnit cílené testy (doplňuje, nenahrazuje E2E TOP 1).

### Klinická agenda a produkt

- **Legacy `Reservation` tabulka** — nikde se už nevytváří, ale `GetPatientDetail` ji stále merguje do appointments s negativními ID a `IntakeFormLink` na ni drží FK; rozhodnout migraci/odstranění (`Reservation.cs`, `PatientsController.cs`).
- **Nadcházející termíny na detailu pacienta** — `PatientDetail` obsahuje `appointments`, ale stránka zobrazuje jen jejich počet; zobrazit seznam nadcházejících rezervací/termínů (`PatientDetailPage.tsx`).
- **Tabulka vyšetření je slepá ulička** — řádky nejsou klikatelné (přitom obsahují `patientId`) a názvy dokumentů nejdou otevřít/stáhnout; propojit s pacientem a přílohami (`ExaminationsPage.tsx`).
- **Tisk/PDF/ZIP návštěvy a vyhodnocovacího reportu** — z detailu návštěvy ani reportu nelze vytisknout/uložit kompletní dokumentaci (poznámka + přílohy + hodnocení); přidat tiskový výstup/export (`VisitDetailPage.tsx`, `EvaluationReportModal.tsx`).
- **Ochrana neuložených změn** — dlouhý intake, poznámka návštěvy i editace pacienta se ztratí při navigaci/back/refresh; přidat router blocker a `beforeunload` (`VisitDetailPage.tsx`, `EditPatientPage.tsx`, `IntakeFormByLinkPage.tsx`).
- **Rozhodnout osud obecných událostí** — `AddEventModal` (léky/symptomy/zranění/očkování) není nikde importován a `PatientEventsSection` je odpojená; buď event flow znovu napojit, nebo odstranit (`AddEventModal.tsx`, `patient/PatientEventsSection.tsx`).
- **Quick Preview nastavení se neukládá** — `SettingsController` GET vrací default a PUT jen echo; uživatel navíc při chybě dostane zavřený modal bez hlášky. Doplnit per-user persistenci a error handling (`SettingsController.cs`, `QuickPreviewSettingsModal.tsx`).
- **Dashboard stats je stub** — vrací jen počet zaměstnanců, last login a „Online“; doplnit dnešní sloty/návštěvy, nevyhodnocené případy a nevyplněné intake, ideálně se scope na centrum (`EmployeesController.cs`).
- **Návštěva nemá lékaře, typ ani stav** — `Visit` neumí evidovat, kdo pacienta vyšetřil, typ návštěvy ani `planned/completed/no-show`; doplnit pole a stavový automat (`Visit.cs`, `VisitDetailPage.tsx`).
- **E-mail log neumí retry ani časový filtr** — u `Failed` chybí „poslat znovu“ a filtr jen podle typu/stavu/hledání; přidat datumový rozsah, reset filtrů a bezpečný retry (`EmailLogPage.tsx`, `EmailLogsController.cs`).
- **Jednotné destruktivní dialogy a editor oprávnění** — část akcí používá nativní `window.confirm`, `PermissionsModal` zadává `scopeType/scopeId` jako volný text a existující override jen maže. Sjednotit potvrzovací modal s dopadem a udělat selecty/validaci (`PermissionsModal.tsx`, admin stránky).
### UX, i18n a odolnost frontendu

- **Chyby serveru se ztrácejí** — kód čte neexistující `err.response?.data`, zatímco `ApiError` má `message/status`; validační důvod z backendu se nikdy nezobrazí (`CreateEmployeeModal.tsx`, `AddIntakeFormEventModal.tsx`, `AddPatientFormPage.tsx`).
- **Accessibility mezipaměťových komponent** — `SearchableMultiSelect` nemá šipky/`aria-activedescendant`, `TdbSliceCard` přidává markery klikem na nefocusovatelný `div`, toasty jdou zavřít jen myší, `LanguageSwitcher` nemá label.
- **Landing path může zacyklit redirect** — `getLandingPath` vrací `defaultPath` bez kontroly oprávnění a `RoleRoute` nepovolenou stránku přesměruje na `/dashboard`, který ji vrátí znovu; validovat `defaultPath` proti roli nebo přesměrovat na bezpečný fallback (`landing.ts`, `RoleRoute.tsx`, `RoleManagementPage.tsx`).
- **Debug data v produkční editaci pacienta** — `EditPatientPage` renderuje „Structured FormSubmission“, „Signature Vector Raw Data“ a „Raw Patient JSON“; schovat za feature flag/admin debug (`EditPatientPage.tsx`).
- **Mrtvý kód a duplicity** — `CreatePatientModal` se nikdy neotevře (`showCreateModal` zůstává false), `CameraCapture`/`ExtractableText`/`PatientDocumentsSection` nejsou použité a `EditPatientPage` duplikuje parser z `utils/intakeSummary.ts`; uklidit (`PatientsPage.tsx`, `components/`).

### Provoz, CI a infrastruktura

- **Edge nginx není hardeningovaný** — chybí `limit_req/limit_conn`, `server_tokens off` a CSP (immutable cache pro hashované bundly už je); přes frontend navíc veřejně prosvítá `/db-health` do DatabaseAPI (`nginx-reverse-proxy.conf`, `frontend/nginx.conf`).
- **Onboarding a lokální tasky se rozcházejí s realitou** — `build.sh`/README odkazují na neexistující AI službu a běžné úkony (testy, migrace, záloha, reset DB) jsou rozeseté v dokumentaci; přidat `Makefile`/`justfile` a srovnat odkazy (`build.sh`, `README.md`, `TESTING.md`).
### Veřejný rezervační web (CEPEM-website)

- **Žádost o rezervaci neposílá potvrzení ani číslo rezervace** — web jen zablokuje slot a zobrazí hlášku; po odeslání žádosti pacient nedostane e-mail s detaily, stavem „čeká na potvrzení“ ani referencí pro komunikaci (`CEPEM-website/src/pages/Reservation.tsx`).
- **Chybí samoobslužná cancelace/přesun** — pacient nemůže rezervaci zrušit nebo změnit; přidat signed odkaz do potvrzovacího e-mailu a bezpečný endpoint (`CEPEM-website`, DatabaseAPI).
- **Veřejná rezervace nemá anti-spam ani service auth** — `block` endpoint je dostupný komukoli a FastAPI proxy neposílá `X-Service-Key`; přidat rate limit/CAPTCHA/honeypot a dedikované veřejné API nebo service key (`CEPEM-website/backend/app/main.py`).
- **Web nemá nizozemštinu** — `CEPEM-website` má jen cs/en, zatímco CEPEM už nl používá; doplnit `nl` locale a jazyk rezervace (`src/i18n/resources/`).
- **Chybí GDPR souhlas před odesláním rezervace** — formulář sbírá jméno, e-mail a telefon bez checkboxu souhlasu a odkazu na zásady; doplnit consent a uložit jeho verzi (`Reservation.tsx`).
- **Legacy availability endpoint a waitlist** — `/api/clinics/{id}/availability` má hardcoded otevírací hodiny 8–17 a mrtvý `Reservation` model; uklidit, případně přidat waitlist/„upozornit, až bude volný termín“ (`CEPEM-website/backend/app/main.py`).

## Nápady na nové funkce (2026-11-04)

Není to seznam oprav — jde o nové schopnosti, které by platformu posunuly dál.
Seřazené podle poměru přínos / náročnost.

### Klinická hodnota

- **Srovnání měření v čase** — na detailu pacienta překrýt aktuální a předchozí `.tdb` pro stejnou stranu/polohu, zobrazit delta mapu a trend (nové ložisko, změna denzity). Navazuje na hotovou vizualizaci `.tdb`.
- **AI předčtení měření** — model označí podezřelé řezy/oblasti jako „druhý názor“ s vysvětlením a jistotou; lékař vždy potvrdí nebo zamítne. Nesmí nahradit dvoučlenné hodnocení, jen zkrátit čas.
- **Doporučení jako sledovaný úkol** — z vyhodnocení vytvořit follow-up (kontrola za X měsíců, mamografie, biopsie, doporučení praktikovi), přiřadit odpovědnost a hlídat splnění; přehled otevřených doporučení na dashboardu.
- **Kvalita a akreditace** — míra shody dvou lékařů (inter-rater), čas od měření k reportu, recall rate, podíl „OK vs. Findings“ po centrech a lékařích; export pro audit.
- **Kontrola kvality přístroje** — evidence kalibrací MEIK zařízení, párování měření se zařízením a upozornění na rostoucí šum / odchylku proti referenci.

### Pacient a komunikace

- **Pacientský portál** — bezpečný magic link: nadcházející termín, předvyplnění intake, souhlasy, vysvětlení výsledku lidskou řečí a potvrzení doporučení.
- **Samoobslužné potvrzení/zrušení/přesun termínu** — signed odkaz v e-mailu/SMS, automatické uvolnění slotu a nabídka waitlistu.
- **Rozšířený reminder engine** — kaskáda připomínek (7 dní / 1 den / 2 hodiny), jazyk podle země, eskalace při nepotvrzení a automatické doplnění slotu z waitlistu.
- **Souhlasová kampaň** — hromadná obnova GDPR souhlasů s verzováním textu, expirací a přehledem, kdo ještě nepotvrdil.

### Provoz a integrace

- **Tablet intake v offline režimu** — PWA s lokální frontou odpovědí; při výpadku Wi-Fi se formulář uloží a odešle po obnovení spojení.
- **Dávkové operace** — hromadné uzavření návštěv, export dokumentace, odeslání připomínek a přiřazení doporučení.
- **Napojení na NIS/FHIR** — export pacienta, návštěvy a reportu do nemocničního systému; import laborních výsledků a DICOM dostupných vyšetření.
- **Příchozí složka z MEIK zařízení** — automatické načtení `.tdb` z watch folderu, přiřazení podle UID/termínu a upozornění na nespárované soubory.
- **PDF/ZIP balík návštěvy** — kompletní dokumentace (poznámka, intake, renderované `.tdb`, hodnocení, doporučení) jedním klikem pro tisk nebo předání.

### Platforma a bezpečnost

- **Passkeys / SSO** — WebAuthn a přihlášení přes Azure AD/Google pro zaměstnance, krátkodobé session a step-up ověření před GDPR exportem.
- **Auditní časová osa a obnovení** — kdo co kdy změnil u pacienta/návštěvy, diff hodnot a bezpečné vrácení omylem upraveného záznamu.
- **Chytré hledání** — fulltext přes OCR text dokumentů, `.tdb` podmínky (strana, poloha) a uložené filtry napříč sekcemi.
- **Provozní alerty do týmu** — pravidla „nevyhodnoceno > 48 h“, „intake chybí u dnešní návštěvy“, „fronta roste“ s doručením e-mailem/Teams/Slack.
