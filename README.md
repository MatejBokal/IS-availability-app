# AvailabilityCollector

### Seminarska naloga pri predmetu Informacijski sistemi  
Univerza v Ljubljani, Fakulteta za računalništvo in informatiko

---

## 👥 Avtorji

- **Matej Bokal** — 63200465  
- **Jožef Gabrijel Avsec** — 63220010

---

## 📋 Opis delovanja celotnega sistema

**AvailabilityCollector** je informacijski sistem za zbiranje, obdelavo in prikazovanje razpoložljivosti delavcev v okoljih z izmenskim delom. Sistem je sestavljen iz treh glavnih komponent:

### 🔵 Spletna aplikacija (ASP.NET Core MVC)
Spletna aplikacija omogoča upravljanje sistema preko spletnega brskalnika. Podpira:
- **Registracijo in prijavo** uporabnikov preko ASP.NET Core Identity
- **Administratorski vmesnik** za upravljanje mesečnih obdobij, nastavitev sistema, pregledovanje oddanih razpoložljivosti in upravljanje uporabnikov
- **Delavčev vmesnik** za oddajo razpoložljivosti preko spletnega brskalnika
- **Dinamične tabele** za prikaz razpoložljivosti vseh delavcev
- **Sistem obvestil** za opomnike o oddajanju razpoložljivosti

### 🟢 REST API storitev (.NET Web API)
API je integriran v isto ASP.NET Core aplikacijo in je dostopen preko prefixa `/api/`. Omogoča:
- **JWT avtentikacijo** za varno dostopanje do API-ja
- **CRUD operacije** za razpoložljivosti (Create, Read, Update, Delete)
- **Avtorizacijo** preko vlog (Worker, Admin)
- **Swagger dokumentacijo** na `/swagger` za pregled in testiranje vseh endpointov
- **JSON komunikacijo** z odjemalci

API podpira naslednje glavne skupine endpointov:
- `/api/auth` — avtentikacija (login, register)
- `/api/availability/my` — razpoložljivosti uporabnika (GET, POST, PUT, DELETE)
- `/api/months` — upravljanje mesečnih obdobij (GET unlocked months, POST unlock)
- `/api/settings` — nastavitve sistema

### 🟡 Android mobilna aplikacija
Mobilna aplikacija omogoča delavcem oddajo razpoložljivosti neposredno iz mobilne naprave:
- **Prijavo in registracijo** preko API-ja z JWT tokenjem
- **Pregled odklepanih mesečnih obdobij** za oddajo razpoložljivosti
- **Oddajo razpoložljivosti** (Create operacija) preko API-ja z uporabo Volley knjižnice
- **Pregled že oddanih razpoložljivosti** (Read operacija) za vsak mesec
- **Interaktiven koledarski vmesnik** za označevanje razpoložljivosti po dneh
- **Podporo za različne tipe razpoložljivosti**: nedostopen, cel dan, časovni razpon

Aplikacija uporablja **Volley** knjižnico za HTTP zahtevke in **JWT tokenje** za avtentikacijo pri vseh API kliceh (razen prijave in registracije).

### 🔄 Delovanje sistema
1. **Administrator** odklene mesec za oddajo razpoložljivosti preko spletne aplikacije ali API-ja
2. **Delavec** se prijavi v sistem (spletno ali mobilno aplikacijo) in vidi odklepane mesece
3. **Delavec** oddajo razpoložljivosti za izbrani mesec z označevanjem posameznih dni (nedostopen, cel dan, časovni razpon)
4. **Sistem** validira podatke (npr. minimalna dolžina časovnega razpona, dovoljeni časovni okvir)
5. **Administrator** pregleduje oddane razpoložljivosti in na podlagi njih sestavlja urnik

---

## 📸 Zaslonske slike grafičnega vmesnika

> **Opomba:** Zaslonske slike bodo dodane kasneje.

### Mobilna aplikacija (Android)
- Prikaz zaslonskih slik mobilne aplikacije

### Spletna aplikacija
- Prikaz zaslonskih slik spletne aplikacije

---

## 👨‍💻 Opis nalog, ki jih je izvedel vsak izmed študentov

### Matej Bokal (63200465)
- **Razvoj spletne aplikacije** (ASP.NET Core MVC) — vsi kontrolerji, pogledi in funkcionalnosti
- **Razvoj REST API storitve** — implementacija vseh API endpointov z JWT avtentikacijo
- **Načrtovanje in implementacija podatkovne baze** — Entity Framework Core modeli, migracije, relacije
- **Integracija ASP.NET Core Identity** — upravljanje uporabnikov, vloge in avtorizacija
- **Swagger dokumentacija** — konfiguracija in nastavitve API dokumentacije
- **Docker integracija** — konfiguracija SQL Server v Dockerju
- **Azure deployment** — priprava in dokumentacija za uvajanje v Azure
- **Izdelava dokumentacije** — SETUP.md, AZURE_DEPLOYMENT.md, API_INTEGRATION_GUIDE.md
- **Razširitve in optimizacije Android aplikacije** — dopolnitve in izboljšave

### Jožef Gabrijel Avsec (63220010)
- **Osnutek začetne Android aplikacije** — osnovna struktura, gradle konfiguracija, osnovni zasloni

---

## 🗄️ Podatkovni model podatkovne baze

> **Opomba:** Diagram podatkovnega modela bo dodan kasneje (generiran z SQL Server Management Studio).

### Opis podatkovnega modela

Podatkovna baza vsebuje naslednje glavne entitete:

#### Identiteta in uporabniki (ASP.NET Core Identity)
- **AspNetUsers** — uporabniki sistema (delavci in administratorji)
- **AspNetRoles** — vloge (Worker, Admin)
- **AspNetUserRoles** — povezava uporabnikov in vlog

#### Domenski podatki
- **AvailabilityMonths** — mesečna obdobja za oddajo razpoložljivosti (MonthKey, IsUnlocked, LockDateTimeUtc)
- **AvailabilitySubmissions** — oddane razpoložljivosti (povezana z uporabnikom in mesečnim obdobjem)
- **AvailabilityEntries** — posamezni vnosi razpoložljivosti za določen dan (tip: Unavailable, FullDay, TimeRange; časovni razponi)
- **Positions** — delovna mesta/pozicije
- **Holidays** — prazniki in posebni dnevi
- **ShiftMatrices** — izmenske matrice za različna obdobja
- **PositionShifts** — povezava pozicij in izmen znotraj matrike
- **ShiftEntries** — posamezni vnosi izmen
- **AppSettings** — nastavitve sistema (minimalna dolžina časovnega razpona, dovoljeni časovni okvir, itd.)
- **Notifications** — obvestila za uporabnike

#### Relacije
- Vsaka **AvailabilitySubmission** pripada enemu uporabniku (AspNetUsers) in enemu **AvailabilityMonth**
- Vsaka **AvailabilitySubmission** ima več **AvailabilityEntries** (ena za vsak dan)
- **ShiftMatrix** vsebuje več **PositionShifts**, ki so povezani z **Position**
- Vsak **PositionShift** ima več **ShiftEntries**

---

## 🏗️ Tehnologije

- **Spletna aplikacija in API:** ASP.NET Core MVC (.NET 10), ASP.NET Core Identity, Entity Framework Core
- **Podatkovna baza:** Microsoft SQL Server (Docker lokalno, Azure SQL v produkciji)
- **Android aplikacija:** Kotlin, Jetpack Compose, Volley
- **Avtentikacija:** JWT Bearer tokens
- **API dokumentacija:** Swagger/OpenAPI
