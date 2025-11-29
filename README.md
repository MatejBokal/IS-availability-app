# AvailabilityCollector

### Seminarska naloga pri predmetu Informacijski sistemi  
Univerza v Ljubljani, Fakulteta za računalništvo in informatiko

---

## 👥 Avtorji
- **Matej Bokal** — 63200465  
- **Jožef Gabrijel Avsec** — 63220010  

---

## 📌 Opis projekta

**AvailabilityCollector** je informacijski sistem, ki rešuje problem zbiranja, obdelave in prikazovanja **razpoložljivosti delavcev** v okoljih z izmenskim delom.  
Aplikacija je namenjena predvsem podjetjem s srednjim do večjim številom študentov, občasnih delavcev ali fleksibilno zaposlenih oseb, kjer je urnik potrebno usklajevati pogosto in pregledno.

### Sistem omogoča:
- oddajo razpoložljivosti za **tedenski** ali **mesečni** urnik,  
- označevanje **mehkih in trdih omejitev** (npr. ne morem delati, raje popoldne, samo med 13–18 …),  
- oddajo razpoložljivosti po **urah**,  
- pregled preteklih razpoložljivosti,  
- vodenje napredka števila mesečnih ur,  
- prikaz razpoložljivosti v **dinamični tabeli** za administracijo,  
- analizo kritičnih izmen,  
- obveščanje in opomnike,  
- podporo za različne **skupine** in **delovna mesta**,  
- prilagodljivo izmensko matrico.

---

## 🏗️ Tehnologije

- **ASP.NET Core MVC (.NET 10)**  
- **ASP.NET Core Identity**  
- **Entity Framework Core (EF Core)**  
- **Microsoft SQL Server (Docker)**  
- **Razor Pages** (Identity UI)  
- **C#**  
- **Bootstrap**

---

## 🗄️ Podatkovni model

Projekt vsebuje dva konteksta:

### **1️⃣ AppContextDb** (domenski podatki)
- Worker  
- Razpolozljivost  
- Matrica  
- UrnikRazpolozljivost  
- UrnikKoncni  
- Status  

### **2️⃣ AppIdentityContext** (uporabniki in vloge)
- AspNetUsers  
- AspNetRoles  
- AspNetUserRoles  
- AspNetUserClaims  
- AspNetUserTokens  
- AspNetRoleClaims  

---

## 🔐 Avtentikacija in avtorizacija

Aplikacija uporablja **ASP.NET Identity**.  
Privzeti administrator:

```
Email: example@example.si
Geslo: Example123.
```

Trenutno je dostop do **Workers** dovoljen samo administratorjem.

---

## ▶️ Navodila za zagon

Za celoten postopek glej **SETUP.md** v repozitoriju.  
Vključuje:

- kloniranje projekta  
- zagon SQL Serverja v Dockerju  
- prilagoditev connection stringa  
- EF migracije  
- zagon aplikacije  
- informacije o administratorju  

---

## 🚀 Napredek do 1. zagovora

✔ MVC aplikacija deluje  
✔ Podatkovna baza (domain + identity) vzpostavljena  
✔ CRUD vmesniki za vse tabele  
✔ Prijava/registracija (Identity)  
✔ Vloge in avtorizacija  
✔ Docker + SQL Server integracija  
✔ SETUP.md za ekipe  
✔ GitHub repozitorij pripravljen

---

## 📎 Licenca

Projekt je pripravljen za seminarsko nalogo pri predmetu Informacijski sistemi (FRI UL).
