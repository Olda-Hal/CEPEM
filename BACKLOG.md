# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.
Stav k 2026-10-28 (průběh noční směny na větvi `refactor`): kompletní rozšíření
testů + všechny ne-provizní body z nasazení a vysoké/střední/nižší priority
dokončeny a commitnuty jednotlivě. Zůstávají jen body závislé na samotném
produkčním nasazení.

## TOP 1 — pořadač po UI refactoru (zadáno 2026-10-29, implementovat po dokončení UI oprav/testů)

- ~~**Scoping rolí pro administrátory center a zemí**~~ ✅ *(commit ba00c74)*
  - Administrátor centra: vidí a edituje **pouze zaměstnance svého centra** a může udělovat **pouze role Vyšetřující a Doktor**; smí **deaktivovat účty** a **vytvářet nové zaměstnance** (EmployeeScopeService + seed grants employees.* pro Center Admin).
  - Administrátor země: vytváří centra **jen v zemích, ke kterým má přiřazené administrátorství** (kontroly v HospitalsController), a jen k nim vytváří/přiřazuje pracovníky (vč. Center Admin rolí). Spravované země = vlastní země + země přiřazených center.
  - Server-side enforcement (DatabaseAPI), GET /api/admin/employees/scope pro FE zrcadlení.
- ~~**Přiřazení zaměstnanců k zemi/centru v editaci zaměstnance**~~ ✅ *(commit ba00c74)*
  - Edit modál i create modál zaměstnance mají výběr země a center; server validuje scope.
- ~~**Defaulty při vytváření pacienta**~~ ✅ ✅
  - `Patient.HomeHospitalId` (nový sloupec, migrace `AddPatientHomeHospital`) = první centrum přiřazené tvůrci; home země = země tvůrce (Person.CountryCode).
  - Pracovník bez přiřazeného centra **nemůže pacienta založit** (400 + česká hláška). ověřeno na živém stacku + 3 testy.
- ~~**Stránka pacienta → Návštěvy (místo Termínů + Dokumentů + Událostí)**~~ ✅
  - Nové entity Visit + VisitDocument (migrace `AddVisits` vč. SQL backfillu: Events → Visits s poznámkou z komentáře, PatientDocuments roztříděné do návštěv podle dne nahrání s preferencí event-návštěvy; žádná data smazána).
  - Detail pacienta: sekce Termíny odstraněna, Události+Dokumenty nahrazeny seznamem Návštěv; nová stránka detailu návštěvy `/patients/:id/visits/:visitId` (sidebar karta pacienta, poznámka, dokumenty s uploadem, vstupní formulář jako propojitelný prohlížitelný obsah).
  - Backend VisitsController (list/create/update/attach) + gateway proxy routy; 4 nové backend testy; ověřeno živě včetně backfillu.
- ~~**Hover submenu v horní navigaci**~~ ✅
  - Administrativa a Admin v top menu mají hover/focus dropdown s podsekcemi; položky filtrované rolí uživatele.

### Další nálezy ze smoke testu 2026-10-29 (naplánovat v rámci výše uvedeného)

- Znovupovolit výběr barevného schématu, až budou opraveny ostatní motivy: teď je natvrdo píchnutá `PINNED_THEME='aurora'` v `ThemeManager.ts` a odebraný `ThemeSelector` z AppShell headeru (komponenta zůstala v repu pro pozdější návrat; rozbité motivy = nebula-blue…crimson-night).
- `theme-overrides.css` postupně rozebrat — je to globální `!important` kladivo držící theming pohromadě; cíl: per-component theme tokeny.
- Audit log: staré záznamy nemají uživatele/metodu/status (v tabulce jen „—") — datová díra v backendu, zvážit backfill nebo lepší render.
- DB hygiena: v tabulkách zaměstnanců jsou záznamy s UUID UID („Admin User") a ve správě rolí bezjmenná role.
- Nativní date/time inputy se renderují podle locale prohlížeče (v headless testech en-US formát) — sjednotit cs-CZ, případně vlastní pickery.
- Health dashboard: po deploy ověřit nové routy `/health` a `/db-health` v produkčním nginxu (nginx.conf se peče do image, nutný rebuild frontendu).

## Další možnosti (nápady navrhované po dokončení TOP 1, 2026-10-29)

- **Rezervace → Návštěva**: rezervační slot propojit s návštěvou (karta slotu → "zahájit návštěvu" vytvoří návštěvu na dnešek s předvyplněným pacientem; na návštěvě vidět, ze kterého slotu vznikla).
- **Vstupní formulář jako PDF**: preview intake formuláře v návštěvě je teď text z komentáře; zvážit generování/ukládání reálného PDF dokumentu (konzistentní s ostatními přílohami, tisk).
- **Editace návštěvy v UI**: datum návštěvy i odpojení přílohy/smazání návštěvy (PATCH endpoint má, FE zatím jen poznámka).
- **Intake formulář editovatelně dodatečně**: po odevzdání zatím jen náhled — povolit opravu (existující Event/FormSubmission edit flow nebo nová verze formuláře jako další "revize" dokumentu).
- **hygiena dat admin plochy**: bezjmenná role („") ve správě rolí a zaměstnanci s UUID UID („Admin User") — skript na sjednocení + UNIQUE ochrana před duplicitními rolemi.
- **Naturální lokalizace**: strojové anglické label v tabulkách pacientů/employes (Status "Živ", názvy typů událostí/rolí z EN překladů) přeložit do CS i EN klíčů konzistentně.
- **E2E suite pro TOP 1 toky**: playwright testy (scoping viditelnost zaměstnanců, vytvoření pacienta s/bez centra, návštěvy vč. backfill sanity) jako trvalé repro (dev smoke skripty se mažou).
- **Hromadné přiřazení center**: při onboardingu nového pracovníka bez centra — akce "přiřadit do centra" přímo z tabulky zaměstnanců (de facto hotovo v edit modálu, zvážit inline UX).
- **Skener → návštěva**: rychlé naskenování dokumentu přímo do otevřené návštěvy (rozšíření stávající kamery/scan flow v DocumentUpload).
- **Hlídání "orphan" příloh**: dokumenty bez návštěvy po novém toku by už neměly vznikat — přidat index/view/report kontrolující, že každý dokument má VisitDocument záznam (dev DB má 0, ověřit na produkci).

## Nasazení (nejdřív tohle) — zbývá udělat na produkci

- Nasadit větev refactor na produkci: `sudo docker compose -f docker-compose.prod.yml up -d --build` (aplikují se nové migrace a permission seedy samy)
- Po nasazení projít hlavní toky v UI: login → pacienti → detail → rezervační sloty → audit log
- Zkontrolovat logy database-api po prvním startu (migrace musí projít čistě)
- Nahrát produkční .env na server (SERVICE_AUTH_KEY, ADMIN_INITIAL_PASSWORD, GF_SECURITY_ADMIN_PASSWORD a nové proměnné musí být vyplněné)
- Provést rotaci klíčů a rešifrování pacientských souborů (pokud ještě neproběhlo — viz REFACTOR_PLAN.md, manuální kroky)
- Nastavit cron pro scripts/backup_daily.sh a jednou vyzkoušet obnovu z backupu (obnova se nikdy netestovala)

## Hotovo v noční směně 2026-10-28 (ověřeno: DB API 187 testů, HC API 65 testů, FE 48 testů, tsc clean, eslint clean)

- Cron/network-ops věci zůstaly jako repo změny (bez dotyku servru):
  - ~~Změnit ASPNETCORE_ENVIRONMENT na Production v dev compose~~ — prod compose již trvale používá Production; .env.example nyní explicitně [✓] upozorňuje
  - ~~Admin initial Password do compose env~~ — doplněno do docker-compose.prod.yml + .env.example [✓]
- ~~Pravidelná retence ActivityLogs~~ — ActivityLogRetentionService (daily, 365d, konfigurovatelné přes `ActivityLogRetention`, zapnuté v prod compose) [✓]
- ~~Composite index (HospitalId, StartDateTime) + index na ContactEmails.Email (varchar(320) místo longtext kvůli indexu)~~ — EF migrace `SlotAndContactEmailIndexes` [✓]
- Unit testy riskových cest: auth lockout matice, slot overlap matice, permission precedence, intake magic links, IDOR u dokumentů [✓]
- ~~Pacienti: sjednotit identifikátor (UID) a skrýt e-maily ve výpisu~~ [✓]
- ~~Audit log: klikací detail záznamu, export CSV, filtr podle metody a statusu~~ (+ backend `/api/activitylogs/export`) [✓]
- ~~Skeleton loading u výpisů (pacienti, sloty)~~ [✓]
- ~~Dokončit split EventsControlleru: options a intake-link endpointy do EventService~~ (nové EventOptionsService + IntakeFormLinkService) [✓]
- ~~Migrovat zbývající stránky na TanStack Query (Patients, ReservationSlots, Admin)~~ [✓]
- ~~Rozšířit E2E testy: sloty, audit log, přepínání jazyků, intake-form přes token~~ [✓]
- Strukturované logování: Serilog JSON v prod + correlation ID napříč službami (X-Correlation-ID HealthcareAPI→DatabaseAPI) [✓]
- Observabilita: Prometheus /metrics na obou API + docker-compose stack Prometheus + Grafana s credential­s čistě přes env [✓]
- Jemnější rate limiting per endpoint (globální read budget, restriktivnější api-write, zachovaný login limiter) [✓]
- ~~Paginace a export v admin tabulkách~~ (klient-side paginace 25 řádků + CSV export) [✓]
- Změna hesla admin účtu: nový `/api/admin/employees/{id}/reset-password` (one-time heslo + forced change) ✓ backend; **zbývá: napojit tlačítko v AdminEmployeesPage**
- Health dashboard: `/admin/health` probíhá gateway i DatabaseAPI každých 30 s [✓]
- Tmavý režim: testy ThemeSelectoru (skupiny, přepínání, perzistence) [✓]
- Rozdělit PatientsController (961 řádků) do service vrstvy — extrahováno vytváření pacienta do IPatientService po vzoru EventService [✓]
- CI linting (ESLint flat config + Prettier + stylelint; CI job `lint`) [✓]
- TypeScript strictness (noUnusedLocals, noUnusedParameters, noUncheckedIndexedAccess — vyčištěno) [✓]

## Nově zaznamenané nálezy pro další iteraci

- Napojit "Reset hesla" v AdminEmployeesPage — backend hotový (`POST /api/admin/employees/{id}/reset-password`, vrací one-time heslo), chybí modal + reklama adminovi (GDLP: dočasně zobrazit jen jednou)
- TestDashboard (/tests): stránka je značně děravá po odstranění `generate-test-report.sh` z Tieru 5 — zvážit smazání routy celé, nebo vysázení na reálné data
- IntakeFormLinkService.MarkUsedAsync není atomický vracecí-lrace (2 souběžné submit requesty mohou oba projít "already used" kódem; doplnit DB-level check `UsedAtUtc IS NULL` přímo v UPDATE)
- UidService stále může v race dědit duplicitní UID při souběžném vytvoření dvou pacientů (gap-filling nelze serializovat); praetoricky doplnit UNIQUE constraint retry
- docker-compose.test.yml vynechejte Serilog/Prometheus zkušebním containerům (test suites teď prod stack nenačítají, ale net-repo config musí zůstat minimal)
- Prettier normalization (commit 8c91d01) pokryla všechny src soubory — CI lint nyní selže při jakémkoli driftu; držiť format-on-save v editoringu
- TestResultsController má kodi seriálovitě na disk (test-results mount) — zvážit symlink-escape + path traversal review po sloučení do gateway (dnes za perm `tests.read`)

## Známá omezení (říct týmu)

- Unit testy pokrývají hlavně kontrakty — auth edge case a slotové překryvy nemají přímé testy
- Dev DB měla ručně opravenou migrační historii — produkční má čistou, ale první deploy hlídat
- Intake dotazník a patient-record formuláře jsou záměrně oddělené (různé domény)
