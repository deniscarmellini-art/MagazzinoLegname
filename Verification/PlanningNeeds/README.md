# Verifica Pianificazione — consumi settimanali

Questa suite sostituisce i test della versione giornaliera errata. La sezione inferiore prevede esclusivamente due settimane, quattro colonne per settimana (INIZIALE, ARRIVI, CONSUMO, FINALE) e un solo input CONSUMO per ciascuna delle sei combinazioni materiale/settimana.

Eseguire solo su `MagazzinoLegname_Dev`, con un GUID fixture nuovo. Il programma rifiuta altri database e controlla che le settimane di prova (lunedì 7 e 14 maggio 2040) non contengano fabbisogni prima di preparare i dati. Le fixture usano fornitore, operatore e carico propri; non vengono modificati dati operativi preesistenti. Non eseguire suite contemporanee sulle stesse settimane.

Build e configurazione (PowerShell):

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
# Impostare DOTNET_CLI_HOME a una directory di lavoro esterna al repository.
$env:MAGAZZINOLEGNAME_DB_SETTINGS = (Resolve-Path 'MagazzinoLegname/database.settings.json').Path
dotnet build Verification/PlanningNeeds/PlanningNeeds.csproj -c Debug --no-restore -o C:/Temp/PlanningWeekly
$id = [guid]::NewGuid().ToString()
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll inspect $id
```

Solo se la migration `RestoreWeeklyPlannedConsumptions` è ancora pendente, eseguire una volta `migrate`: prepara tre letture di prova del vecchio schema (12 + 8 nella settimana A e 30 nella B), applica la migration incrementale, verifica conservazione dei totali di tutti i record esistenti, unicità/Monday e snapshot EF. Rimuove infine le tre righe di prova, riunite in due. Non esegue downgrade e non modifica migration precedenti.

```powershell
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll migrate $id
```

A schema settimanale applicato, eseguire in ordine controllando `$LASTEXITCODE` dopo ciascun comando. Ogni comando è un nuovo processo:

```powershell
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll create $id
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll verify $id
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll wpf $id C:/Temp/PlanningWeekly/preview.png
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll verifyfinal $id
dotnet C:/Temp/PlanningWeekly/PlanningNeeds.dll cleanup $id
```

- A: giacenza dalla proiezione SQL della fixture = 100; arrivi martedì 50 e giovedì 40; consumo A 20; finale A 170.
- B: iniziale B 170; arrivo B 50; consumo B 30; finale B 190.
- C: consumo A 20→40; finale A 150, iniziale B 150, finale B 170. Verifica anche il percorso WPF di edit/reload.
- D: processi distinti verificano persistenza di entrambi i consumi settimanali e delle successive modifiche.
- E: navigazione avanti/indietro ricarica per WeekStart, senza alterare quantità o RowVersion.
- SQL: controllo indice univoco settimanale e tre CHECK; rifiuto di un WeekStart non lunedì. UPDATE e DELETE obsoleti respinti; due INSERT simultanei producono un solo vincitore e reload nel perdente. Repository guasto simulato verifica invalidazione senza fallback RAM.
- Calcolo: spessori/qualità indipendenti; esclusione arrivi confermati e fine settimana; nessuna distribuzione del consumo sui giorni.
- WPF: XAML compilato in thread STA, layout e dispatcher; sei righe, due gruppi A/B, esattamente dodici TextBox consumo e quattro intestazioni per gruppo; input interamente contenuti nelle righe compatte; nessun errore di binding. INSERT/DELETE tramite binding reali dei TextBox, parsing italiano e refresh dopo modifica SQL della sola rettifica fixture. Gli arrivi superiori mantengono cinque giorni lun–ven per settimana.
- Cleanup: rimuove solo entità della fixture identificate dal GUID e derivati; mantiene schema/migration. Le celle temporanee create via UI sono cancellate dalla stessa prova WPF.

La proiezione della fixture usa volume iniziale 200, rettifica a 100, tre pacchi terminali (scarico, reso, rimozione) e un supplementare escluso dal volume. I tre arrivi di prova vengono inseriti con snapshot SQL 50/40/50 senza cambiare gli standard/carico globali. Il calcolo A/B filtra la sola fixture; la vista WPF e la sua anteprima usano invece tutta la giacenza reale del database. Per questo l'anteprima può mostrare una base diversa da 100.

Questo è un collaudo SQL e WPF automatizzato, compreso il rendering della vista. Non simula una sessione manuale dell'utente. Per verificare separatamente la regressione degli arrivi usare anche `Verification/Planning`.
