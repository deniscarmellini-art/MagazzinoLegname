# Fase 2C-3 — verificatore ordini consumabili SQL

## Semantica preservata

Il vecchio editor scriveva `ConsumableOrderInfo.Quantity` direttamente, senza conversione mediante QuantityPerUnit. Il nuovo OrderedQuantity conserva quel valore nell'UDM fotografata alla creazione. Ordini Ordered e PartiallyReceived contribuiscono per l'intera quantità inserita; non esistono in questa fase una quantità ricevuta distinta, una sottrazione automatica per consegne parziali o movimenti di carico. Ricevuto/annullato non contribuiscono. In caso di cambio UDM, gli importi di unità differenti sono mostrati separatamente, senza conversioni implicite.

## Esecuzione PowerShell dalla radice del repository

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MAGAZZINOLEGNAME_DB_SETTINGS = Join-Path (Get-Location) 'MagazzinoLegname/database.settings.json'
$testOutput = Join-Path (Get-Location) 'Verification/ConsumableOrders/bin/check'
dotnet build Verification/ConsumableOrders/ConsumableOrders.csproj -c Debug -o $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Build fallita' }
$testDll = Join-Path $testOutput 'ConsumableOrders.dll'
dotnet $testDll offline
if ($LASTEXITCODE -ne 0) { throw 'Verifiche locali fallite' }
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

Richiesti: MagazzinoLegname_Dev, fasi 2C-1 e 2C-2 già migrate e almeno un operatore SQL attivo. `create` applica soltanto AddConsumableOrders quando pendente, rifiutando altre migration. Il verificatore crea quattro articoli con codice OL-/OH-/ON-/OI- e GUID della prova, sessione e ordini dedicati, poi li rimuove. Non modifica operatori o dati legname.

## Copertura ed esito 24/09/2026

Verifiche locali e SQL superate:

- A: 14000/20000 senza ordine => Da ordinare.
- B: ordine aperto => Sotto scorta · In ordine; quantità diretta, senza moltiplicazione x7000; giacenza 14000.
- C: 28000/20000 con ordine => In ordine.
- D: ricezione => esclusione dalla somma, ritorno Da ordinare, stock invariato, reload del ViewModel e storico persistente.
- E: due INSERT concorrenti distinti per articolo => somma SQL; modifica quantità e stato parziale.
- F: tre processi distinti => ordini aperti/chiusi/annullati, somme e stati persistenti.
- G: client A chiude, client B modifica con vecchia RowVersion => conflitto, messaggio, reload SQL e nessuna sovrascrittura.
- Annullamento persistente; ordini conclusi non riaperti né cancellati dal repository.
- Nessuna nuova lettura inventariale e quantità sempre invariata dopo le operazioni ordine.
- Anagrafica cambiata durante una nuova bozza => conflitto; snapshot dei vecchi ordini invariati.
- Somme separate per UDM diverse; KPI sugli attivi; articolo senza lettura rimane Da verificare anche con ordine aperto.
- Articolo inattivo escluso da Situazione/KPI, storico mantenuto e ordine esistente ancora chiudibile dal repository.
- Validazione quantità e precisione, stato None non persistibile, modifica della bozza senza alterare i totali SQL.

La prova F verifica riavvii del processo verificatore, non clic manuali di chiusura e riapertura WPF. Dati temporanei eliminati al termine. La configurazione cifrata di SQL Server è stata mantenuta.

## Verifica manuale UI

Situazione > selezionare un articolo > Nuovo ordine > quantità diretta e data > Salva ordine. Verificare nuova somma e stato, giacenza invariata. Usare il selettore per un ordine aperto; impostare Ricevuto/chiuso oppure Annullato e salvare. Consultare tutti gli ordini nella tab Storico ordini, inclusi quelli di articoli inattivi. Ripetere con due app aperte per il conflitto e con un riavvio della finestra. Le nuove operazioni si usano avviando la build aggiornata; l'istanza eventualmente già aperta non viene terminata dal verificatore.
