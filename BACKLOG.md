# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.
Stav k 2026-10-28 (průběh noční směny na větvi `refactor`): kompletní rozšíření
testů + všechny ne-provizní body z nasazení a vysoké/střední/nižší priority
dokončeny a commitnuty jednotlivě. Zůstávají jen body závislé na samotném
produkčním nasazení.

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
