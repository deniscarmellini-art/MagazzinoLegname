# Verifica Fase 2D-1 — Pianificazione SQL

Progetto console di integrazione SQL e WPF. Usa esclusivamente MagazzinoLegname_Dev e un fornitore TEST identificato dal GUID passato. Non usare GUID di fornitori reali.

Compilare Planning.csproj in Debug. Impostare MAGAZZINOLEGNAME_DB_SETTINGS al database.settings.json del progetto. Eseguire, con lo stesso GUID nuovo:

```powershell
dotnet Planning.dll create <guid>
dotnet Planning.dll update <guid>
dotnet Planning.dll verify <guid>
dotnet Planning.dll wpf <guid>
dotnet Planning.dll verify <guid>
dotnet Planning.dll cleanup <guid>
```

create ammette soltanto le migration AddPlannedArrivals, AddPlannedConsumptions e RestoreWeeklyPlannedConsumptions, applica la migration e crea la fixture. Se la migration è già applicata, ne verifica comunque la coerenza con il modello.

A: INSERT e reload, lettura da processo distinto.
B: modifica da 1 a 2 carichi con MC da parametri SQL e conservazione CreatedAtUtc.
C: DELETE con RowVersion e assenza da processo distinto.
D: vista di sole due settimane A/B lun-ven e sei righe materiali con due celle di consumo settimanali e quattro colonne per settimana. Navigazione avanti/indietro su periodi diversi: caricamento arrivi fuori periodo e ritorno ai precedenti, senza modificare quantità, MC o RowVersion SQL.
E: due copie della stessa riga, secondo UPDATE e DELETE obsoleti rifiutati; servizio ricarica il vincitore e restituisce messaggio di conflitto.

Altri controlli: celle vuote non persistite; duplicato fornitore/giorno rifiutato; fornitore inattivo leggibile e non ammesso per nuovi arrivi; conferma arrivo persistente.

wpf inizializza risorse applicative e controllo PlanningView in thread STA, esegue layout e dispatcher, quindi prova i comandi delle celle contro SQL. Non simula input mouse/tastiera e non equivale a un collaudo visuale completo dell'app.

cleanup elimina soltanto righe della fixture e il fornitore con GUID e codice test corrispondenti. Non rimuove la migration.
