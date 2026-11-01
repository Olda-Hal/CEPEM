# Backlog — možné další přidání a opravy

Seřazeno podle důležitosti. Začněte shora.
Stav k 2026-11-01: všechny dokončené body byly z dokumentu odstraněny — níže
zůstávají pouze otevřené úkoly a nápady.

## Chybí dodělat

- **Jeden intake formulář pro každou návštěvu** — intake dotazník a patient-record formuláře sjednotit do jednoho formuláře. U každé návštěvy je potřeba vyplnit data pro zaznamenání historie.
  - Při vytvoření rezervace osoba zadá jméno, e-mail a telefon.
  - Systém se pokusí matchnout existujícího pacienta podle **e-mailu a telefonního čísla** (musí sedět obojí).
  - **Match:** pár dní před rezervací pošle na e-mail odkaz na formulář předvyplněný známými údaji — uživatel jen upraví, co je nové.
  - **Bez matche:** dostane prázdný formulář; po odeslání se z něj založí nový pacient.
  - **Varianta na místě:** pro seniory a méně technické lidi vyplnění na tabletu lékaře — lékař musí umět otevřít intake formulář pacienta. Cílem je přesunout vyplňování mimo dobu návštěvy, aby se na místě nečekalo.
  - **Přístup k datům:** odkaz jde na e-mail, takže k údajům se dostane jen ten, kdo zná e-mail i telefon dané osoby a má přístup k jejímu e-mailu.
- **Pokračování Vyhodnocování** — navazující flow podle specifikace (jednání podle doporučení: kdo, co, kdy). Čeká na zadání.
- **Rezervace → Návštěva** — rezervační slot propojit s návštěvou (karta slotu → „zahájit návštěvu“ vytvoří návštěvu na dnešek s předvyplněným pacientem; na návštěvě vidět, ze kterého slotu vznikla).
- **Vstupní formulář jako PDF** — preview intake formuláře v návštěvě je teď text z komentáře; zvážit generování/ukládání reálného PDF dokumentu (konzistentní s ostatními přílohami, tisk).
- **Editace návštěvy v UI** — datum návštěvy i odpojení přílohy/smazání návštěvy (PATCH endpoint má, FE zatím umí jen poznámku).
- **Intake formulář editovatelně dodatečně** — po odevzdání zatím jen náhled; povolit opravu (existující Event/FormSubmission edit flow nebo nová verze formuláře jako další „revize“ dokumentu).
- **Skener → návštěva** — rychlé naskenování dokumentu přímo do otevřené návštěvy (rozšíření stávající kamery/scan flow v DocumentUpload).
- **E2E suite pro TOP 1 toky** — playwright testy (scoping viditelnost zaměstnanců, vytvoření pacienta s/bez centra, návštěvy vč. backfill sanity) jako trvalé repro.
- **Hromadné přiřazení center** — při onboardingu nového pracovníka bez centra: akce „přiřadit do centra“ přímo z tabulky zaměstnanců (de facto hotovo v edit modálu, zvážit inline UX).
- **Hlídání „orphan“ příloh** — dokumenty bez návštěvy po novém toku by už neměly vznikat; přidat index/view/report kontrolující, že každý dokument má `VisitDocument` záznam (dev DB má 0, ověřit na produkci).

## Technický dluh a nálezy

- Znovupovolit výběr barevného schématu, až budou opraveny ostatní motivy: teď je natvrdo píchnutá `PINNED_THEME='aurora'` v `ThemeManager.ts` a odebraný `ThemeSelector` z AppShell headeru (komponenta zůstala v repu pro pozdější návrat; rozbité motivy = nebula-blue…crimson-night).
- `theme-overrides.css` postupně rozebrat — je to globální `!important` kladivo držící theming pohromadě; cíl: per-component theme tokeny.
- Audit log: staré záznamy nemají uživatele/metodu/status (v tabulce jen „—“) — datová díra v backendu, zvážit backfill nebo lepší render.
- DB hygiena: v tabulkách zaměstnanců jsou záznamy s UUID UID („Admin User“) a ve správě rolí bezjmenná role; skript na sjednocení + UNIQUE ochrana před duplicitními rolemi.
- Nativní date/time inputy se renderují podle locale prohlížeče (v headless testech en-US formát) — sjednotit cs-CZ, případně vlastní pickery.
- Naturální lokalizace: strojové anglické labely v tabulkách pacientů/zaměstnanců (Status „Živ“, názvy typů událostí/rolí z EN překladů) přeložit do CS i EN klíčů konzistentně.
- TestDashboard (`/tests`): stránka je značně děravá — zvážit smazání routy celé, nebo vysázení na reálná data.
- `IntakeFormLinkService.MarkUsedAsync` není atomický (race vracející „already used“): doplnit DB-level check `UsedAtUtc IS NULL` přímo v UPDATE.
- `UidService` stále může v race zdědit duplicitní UID při souběžném vytvoření dvou pacientů (gap-filling nelze serializovat); pragmaticky doplnit UNIQUE constraint + retry.
- `TestResultsController` má kód seriálovitě na disk (test-results mount) — zvážit symlink-escape + path traversal review po sloučení do gateway (dnes za perm `tests.read`).
