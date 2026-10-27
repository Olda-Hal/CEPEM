# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.

## Nasazení (nejdřív tohle)

- Nasadit větev refactor na produkci: `sudo docker compose up -d --build` (aplikují se nové migrace a permission seedy samy)
- Po nasazení projít hlavní toky v UI: login → pacienti → detail → rezervační sloty → audit log
- Zkontrolovat logy database-api po prvním startu (migrace musí projít čistě)
- Nahrát produkční .env na server (SERVICE_AUTH_KEY a nové proměnné musí být vyplněné)
- Provést rotaci klíčů a rešifrování pacientských souborů (pokud ještě neproběhlo — viz REFACTOR_PLAN.md, manuální kroky)

## Vysoká priorita (provoz a bezpečnost)

- Nastavit cron pro scripts/backup_daily.sh a jednou vyzkoušet obnovu z backupu (obnova se nikdy netestovala)
- Přidat retenci/mazání starých záznamů v ActivityLogs (tabulka roste donekonečna)
- Přidat composite index (HospitalId, StartDateTime) na ReservationSlots pro slotové dotazy
- Přidat index na ContactEmails.Email (login lookup dnes dělá full scan)
- Změnit ASPNETCORE_ENVIRONMENT na Production v dev compose (Development běží i na produkci — zpomalení a detailní chybové stránky)

## Střední priorita (funkce a UI)

- Pacienti: sjednotit zobrazení identifikátorů (jedno UID místo ID/UID/PersonId dohromady) a skrýt e-maily ve výpisu (GDLP)
- Audit log: klikací detail záznamu, export do CSV, filtr podle metody a statusu
- Skeleton loading u výpisů (pacienti, sloty) místo textu Načítání…
- Dokončit split EventsControlleru: options a intake-link endpointy do EventService (cca 300 řádků)
- Migrovat zbývající stránky na TanStack Query podle vzoru z DashboardPage (Patients, ReservationSlots, Admin)
- Rozšířit E2E testy: rezervační sloty (vytvoření, blokace), audit log, přepínání jazyků, intake-form přes token
- Přidat unit testy na riskové cesty: account lockout, slotové překryvy, permission evaluace

## Nižší priorita (kvalita a rozvoj)

- Strukturované logování: Serilog s JSON výstupem + correlation ID přes všechny služby
- Observabilita: Prometheus + Grafana s datasource přes env (bez hesla v repu), případně OpenTelemetry
- Jemnější rate limiting per endpoint (zápisy přísněji než čtení)
- Přidat Admin initial Password do compose env (dnes jen v .env dokumentace)
- Paginace a export v admin tabulkách (employees, roles)
- Změna hesla admin účtu přes UI (dnes jen administrativní cesta přes DB)
- Health dashboard: jednoduchá stránka se stavem služeb (volá /health endpointy)
- Tmavý režim doladit a otestovat napříč stránkami
- Rozdělit PatientsController (980 řádků) do service vrstvy podle vzoru EventService
- Přidat linting do CI (ESLint config + prettier + stylelint)
- Zvýšit TypeScript strictnost (noUnusedLocals, noUncheckedIndexedAccess)

## Známá omezení (říct týmu)

- Unit testy pokrývají hlavně kontrakty — auth edge case a slotové překryvy nemají přímé testy
- Dev DB měla ručně opravenou migrační historii — produkční má čistou, ale první deploy hlídat
- Intake dotazník a patient-record formuláře jsou záměrně oddělené (různé domény)
