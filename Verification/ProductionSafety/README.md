# Verifica produzione 2F-2

Eseguire ProductionSafety.csproj separatamente in Debug e Release, con MAGAZZINOLEGNAME_DB_SETTINGS impostata sul database di verifica e senza altri client che lo modifichino durante il test.

Il programma avvia l'app WPF e apre Impostazioni (Consumabili e Import legacy legname). Verifica i pulsanti compilati, l'assenza dei comandi rimossi dal ViewModel e l'assenza del servizio reset nel binario Release. In Debug invoca il reset solo nel processo di verifica. Confronta conteggi e checksum delle tabelle prima/dopo: nessun dato viene inserito o cancellato dal test.

Prerequisiti: database già migrato e configurazioni già presenti. Il test si interrompe prima dell'avvio se ci sono migration pendenti o configurazioni da inizializzare. Il confronto checksum è un controllo di regressione, non una prova crittografica; eventuali scritture di altri client possono farlo fallire.

Esito 2026-09-30: test A-D superati, build Debug e Release con 0 errori e 0 warning.

Audit sorgente: nessun seeding demo rilevato. L'inizializzazione esistente inserisce soltanto configurazioni applicative mancanti (famiglie spessore e parametri), non fornitori, operatori, carichi, consumabili o pianificazioni demo. Importer consumabili conservato come archivio tecnico, senza collegamenti UI/ViewModel e senza accesso ai repository SQL. Import legname invariato.
