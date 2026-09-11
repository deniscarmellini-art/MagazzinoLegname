# Verifica Fase 2C-1

Il progetto ConsumableCatalog.csproj verifica SQL Server reale, senza provider in-memory. Richiede la configurazione MagazzinoLegname_Dev. Non fa parte della soluzione applicativa.

Dalla radice del repository, in PowerShell:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MAGAZZINOLEGNAME_DB_SETTINGS = Join-Path (Get-Location) 'MagazzinoLegname\database.settings.json'
dotnet build Verification\ConsumableCatalog\ConsumableCatalog.csproj -c Debug
$testId = [Guid]::NewGuid().ToString()
$testDll = 'Verification\ConsumableCatalog\bin\Debug\net10.0-windows10.0.19041\ConsumableCatalog.dll'
try {
    foreach ($step in @('create', 'update', 'verify')) {
        dotnet $testDll $step $testId
        if ($LASTEXITCODE -ne 0) { throw "Test interrotto: $step" }
    }
} finally {
    dotnet $testDll cleanup $testId
}
```

create controlla il modello EF, applica le migration pendenti e crea un articolo TEST SQL con ID dedicato. update e verify sono processi distinti: verificano la persistenza oltre la vita del processo, decimal(19,6), parametri, inattivazione, due copie concorrenti, messaggio e reload del servizio, editing separato, validazione e cancellazione concorrente. La pulizia interessa esclusivamente l'articolo con ID e codice del test.

Verifica manuale WPF da completare: Impostazioni > Materiali di consumo; Nuovo articolo TEST SQL; compilare e salvare; chiudere e riaprire l'applicazione; verificare articolo e parametri. Ripetere con Qtà per UDM modificata e articolo inattivo. Per conflitto, aprire il dettaglio in due istanze prima di salvare la prima, poi tentare il salvataggio nella seconda: deve apparire il conflitto e la lista deve essere ricaricata da SQL. Verificare anche input numerico non valido e ritorno alla lista senza salvare.

Esito della sessione: build verificatore Debug 0 errori / 0 warning; controllo modello/migration superato. Esecuzione SQL bloccata prima della migration: SqlClient errore 20, crittografia richiesta non supportata dall'ambiente di esecuzione. Test A-D non dichiarati superati. Nessuna verifica manuale WPF eseguita.
