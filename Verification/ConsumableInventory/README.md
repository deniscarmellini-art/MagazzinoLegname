# Fase 2C-2 — verifiche inventari consumabili

Il verificatore usa SQL Server reale e processi distinti. La modalità `offline` verifica formula, decimali, input, stati e assenza di fallback; non sostituisce i test SQL.

## Esecuzione

Dalla radice del repository, PowerShell (scegliere una cartella di output libera se l'app è aperta):

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MAGAZZINOLEGNAME_DB_SETTINGS = Join-Path (Get-Location) 'MagazzinoLegname/database.settings.json'
$testOutput = Join-Path (Get-Location) 'Verification/ConsumableInventory/bin/check'
dotnet build Verification/ConsumableInventory/ConsumableInventory.csproj -c Debug -o $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Build non riuscita' }
$testDll = Join-Path $testOutput 'ConsumableInventory.dll'
dotnet $testDll offline
if ($LASTEXITCODE -ne 0) { throw 'Verifiche locali non riuscite' }
$testId = [Guid]::NewGuid().ToString()
try {
    foreach ($step in @('create', 'update', 'verify')) {
        dotnet $testDll $step $testId
        if ($LASTEXITCODE -ne 0) { throw "Test SQL interrotto: $step" }
    }
} finally {
    dotnet $testDll cleanup $testId
}
```

Richiesti: database configurato MagazzinoLegname_Dev, catalogo della Fase 2C-1 già migrato e almeno un operatore SQL attivo. `create` applica soltanto la migration AddConsumableInventories se pendente; rifiuta migration estranee. I test creano articoli TI-/TN- con GUID della prova e sessioni dedicate, poi li eliminano. Non modificano operatori o dati legname.

## Copertura SQL

- A: 4 scatole x 7000 = 28000, OK con minimo 20000.
- B: nuova sessione 2 x 7000 = 14000, Da ordinare.
- C: cambio anagrafica a 7500, nome e UDM diversi; letture precedenti e relativi snapshot invariati.
- D: tre esecuzioni del verificatore, con rilettura persistente di ultimo inventario, giacenza e storico.
- E: articolo senza letture, Da verificare.
- Sessione ritentata con stesso ID e richiesta identica: nessun duplicato; dati diversi sullo stesso ID: conflitto.
- Inventario retrodatato inserito successivamente: non sostituisce quello più recente.
- RowVersion anagrafica cambiata e articolo inattivo: rifiuto senza sessioni parziali.
- Errore iniettato sull'INSERT delle letture dopo l'INSERT della sessione: rollback di entrambi.
- Vincolo univoco SQL SessionId + ConsumableItemId.
- Articolo inattivo escluso dall'inserimento; storico conservato.

## Esito 24/09/2026

Verifiche offline e SQL superate su localhost\SQLEXPRESS / MagazzinoLegname_Dev con crittografia configurata invariata. Migration applicata; dati temporanei eliminati. Build Debug del progetto applicativo e verificatore: 0 warning / 0 errori. Il test D è una verifica tra processi distinti, non un test manuale di chiusura e riapertura della finestra WPF.

## Controlli manuali UI

Nell'app aggiornata: Materiali di consumo > Inventario; verificare solo articoli attivi, selezione operatore, campo UDM rilevate, anteprima immediata, conferma e aggiornamento di Situazione/KPI/Storico. Provare input negativo, testo non valido e conteggio zero. Modificare Qty/UDM in un secondo client durante un conteggio per verificare messaggio e reload. Chiudere e riaprire la finestra per completare il test D visuale.

Stampa: A4, reparto poi prodotto, data/operatore/prodotto/reparto/UDM/Qtà per UDM, conteggio e note vuoti; nessuna giacenza precedente. La stampa mantiene l'implementazione preesistente; output fisico non verificato in questa sessione.
