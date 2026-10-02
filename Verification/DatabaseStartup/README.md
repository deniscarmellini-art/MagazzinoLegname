# Fase 2H-1B — Configurazione ambiente SQL e migration controllate

## Destinazione

Il vincolo fisso era in `Persistence/SqlPersistenceRoot.cs`, metodo `InitializeDatabase()`. Ora la destinazione resta esterna in `database.settings.json`, eventualmente selezionato da `MAGAZZINOLEGNAME_DB_SETTINGS`.

Campi di protezione obbligatori:
- `Environment`: `Development` oppure `Shared` (default prudente `Shared`).
- `AllowedServers`: nomi server/endpoint autorizzati per quella configurazione.
- `AllowedDatabases`: nomi database autorizzati per quella configurazione.

Server e database devono appartenere alle rispettive liste. Liste vuote o destinazione diversa bloccano l'avvio prima della connessione. Non esiste un nome produttivo hardcoded nella logica. La configurazione viene acquisita dalla factory una volta per processo: cambiare file richiede un riavvio, evitando un cambio database durante la sessione.

La configurazione Dev mantiene `localhost\SQLEXPRESS` / `MagazzinoLegname_Dev`. Il file `MagazzinoLegname/database.settings.shared.example.json` illustra `MagazzinoLegname` in ambiente Shared. Porta 1433 solo esemplificativa, non configurata né verificata come endpoint attivo. Gli esempi senza i campi di protezione devono essere completati prima dell'uso; non è previsto un ripiego permissivo.

Autenticazione esclusivamente Windows. Rimossi i campi UserId/Password dal JSON Dev; un file che li contiene viene respinto senza mostrarne il contenuto. SQL Authentication non è supportata da questo percorso. Il footer della finestra indica ambiente, server e database; gli errori di avvio includono la destinazione validata.

## Migration

Development: `AutoMigrateDevelopment=true` permette aggiornamenti automatici, soltanto se ci sono migration pendenti. Il flag può essere impostato a false per sola verifica.

Shared: `AutoMigrateDevelopment` non abilita migration. Senza `MAGAZZINOLEGNAME_APPLY_MIGRATIONS=1`, vengono eseguiti soltanto apertura della connessione e controllo della cronologia migration. Schema pendente o migration sconosciute alla versione installata bloccano l'avvio con messaggio comprensibile.

La manutenzione esplicita richiede ENTRAMBE le condizioni:
1. variabile di processo `MAGAZZINOLEGNAME_APPLY_MIGRATIONS=1`;
2. `MigrationHost` uguale al nome della macchina corrente.

Un lock SQL di sessione, circoscritto al database, impedisce due aggiornamenti contemporanei. Dopo l'acquisizione del lock le migration pendenti vengono ricontrollate. Il secondo avvio a schema aggiornato non chiama Migrate. Rimuovere il flag di processo dopo la manutenzione. Queste protezioni applicative non sostituiscono i futuri permessi SQL: solo la postazione/identità amministrativa dovrà avere i privilegi DDL necessari.

Il servizio apre un database già esistente: se manca, non lo crea implicitamente. Nessuna migration esistente è stata modificata o aggiunta. La normale inizializzazione dei parametri applicativi preesistente non è stata modificata; non è un aggiornamento dello schema.

## Diagnostica

Distinti: configurazione non autorizzata, server non raggiungibile, login Windows negato, database non disponibile, timeout, schema non aggiornato/incompatibile, manutenzione concorrente e connessione riuscita. Il controllo DatabaseHealthService è sempre read-only, anche con il flag migration impostato.

Il codice SQL 4060 non distingue con certezza un database inesistente da un database non visibile/autorizzato: il messaggio espone entrambe le possibilità senza affermare una causa non verificabile. Il codice 911 segnala invece database inesistente; 18456/18452/18470 accesso Windows non autorizzato. Non si mostrano stack trace all'operatore Release; i dettagli tecnici restano in Debug. Non viene abilitato il logging EF dei valori sensibili.

## Test A-F — 2 ottobre 2026

A PASS: configurazione Dev e inizializzazione database corretta. Interceptor blocca qualunque DDL/DML/EXEC: nessuna scrittura al database Dev.
B PASS: il nome MagazzinoLegname viene accettato dalla configurazione Shared; nessuna creazione o connessione al database produttivo.
C PASS: database test vuoto, 12 migration pendenti, nessun flag; avvio bloccato, nessuna tabella creata e nessun comando di scrittura.
D PASS: flag esplicito e host autorizzato applicano le migration al database test; tutte registrate una sola volta.
E PASS: secondo avvio Shared normale, nessuna migration/DDL tentata e cronologia invariata.
F PASS: connessione TCP a endpoint locale non disponibile, errore gestito dal servizio e messaggio senza stack tecnico. L'app tratta il risultato negativo con messaggio e uscita controllata.

Ulteriori verifiche: destinazioni fuori allowlist e SQL Authentication respinte prima della connessione; host manutenzione errato respinto; lock concorrente verificato con due connessioni; controllo diagnostico read-only; database inesistente; mapping codici login Windows/timeout.

Il test usa esclusivamente un nuovo database `MagazzinoLegname_TestStartup_<GUID>`, creato per la prova e rimosso nel finally. I privilegi/autenticazione SQL non sono stati cambiati. Il test login negato verifica il mapping del codice, senza creare/disabilitare utenti reali. I test di avvio verificano il percorso database, non una sessione manuale completa delle pagine WPF.

Per eseguire: compilare `Verification/DatabaseStartup/DatabaseStartup.csproj`, impostare `MAGAZZINOLEGNAME_DB_SETTINGS` alla configurazione Dev e avviare l'eseguibile. Il test richiede che Dev sia già aggiornato, ne vieta le scritture e crea una sola destinazione test col prefisso controllato.

## File modificati

- MagazzinoLegname/Persistence/DatabaseSettings.cs
- MagazzinoLegname/Persistence/MagazzinoDbContextFactory.cs
- MagazzinoLegname/Persistence/SqlPersistenceRoot.cs
- MagazzinoLegname/Persistence/DatabaseStartupService.cs (nuovo)
- MagazzinoLegname/Persistence/DatabaseErrorHandling.cs
- MagazzinoLegname/App.xaml.cs
- MagazzinoLegname/MainWindow.xaml e MainWindow.xaml.cs
- MagazzinoLegname/database.settings.json
- MagazzinoLegname/database.settings.shared.example.json (nuovo)
- Verification/DatabaseStartup/DatabaseStartup.csproj, Program.cs, README.md (nuovi)

## Da fare nella fase successiva

Definire indirizzo/nome DNS stabile e porta effettiva, configurare TCP e firewall solo dopo autorizzazione, predisporre database MagazzinoLegname e autorizzazioni Windows, distribuire configurazioni client con allowlist corrette, definire permessi separati per manutenzione, verificare collegamento da altro PC e backup.

Per il futuro database condiviso è consigliato AUTO_CLOSE OFF: Microsoft lo raccomanda per database usati frequentemente, per evitare il costo di aperture/chiusure ripetute. Riferimento: https://learn.microsoft.com/en-us/sql/relational-databases/policy-based-management/set-the-auto-close-database-option-to-off?view=sql-server-ver17 . Nessuna opzione del database Dev è stata modificata.

SisLog/LoadMaster, porte, firewall, servizi, Browser SQL, DNS/DHCP e utenti SQL non sono stati modificati. Nessun database produttivo creato. Nessun import, commit o fase successiva eseguiti.

Build Debug e Release completate: 0 errori, 0 warning (applicazione e progetto di verifica).

