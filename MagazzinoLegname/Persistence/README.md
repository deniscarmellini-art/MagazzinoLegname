# Persistenza SQL Server — Fase 1

Questa cartella contiene la fondazione EF Core. Gli store in-memory esistenti non sono ancora sostituiti.

`database.settings.json` viene copiato accanto all'eseguibile ed è modificabile senza ricompilazione. In alternativa la variabile ambiente `MAGAZZINOLEGNAME_DB_SETTINGS` può indicare un file esterno. `Authentication` accetta `Windows` oppure `SqlServer`.

Non sono definiti seed: nessun fornitore, operatore, carico o pacco demo viene inserito. Anche le famiglie 23/34/44 restano per ora negli store correnti e saranno migrate esplicitamente in una fase successiva.

La migration `InitialCreate` prepara lo schema, ma non viene applicata automaticamente. Per un database di sviluppo si potrà usare `dotnet ef database update` dopo aver verificato il file di configurazione.

Gli eventi terminali condividono la tabella `PackageTerminalEvents`, con indice univoco su `PackageId`: un pacco può avere al massimo una tra uscita, reso, rimozione o uscita supplementare. Il comando applicativo futuro dovrà inserire l'evento e aggiornare il pacco nella stessa transazione.
