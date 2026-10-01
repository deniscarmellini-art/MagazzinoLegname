# Fase 2G-1 — Import iniziale legname SQL

Verifica conclusiva: 1 ottobre 2026. Nessun import reale eseguito, nessun commit e nessuna migration creata/modificata.

## Architettura prima e dopo

Prima: LegacyExcelReader leggeva il workbook; LegacyImportAnalyzer produceva staging/analisi. LegacyInitialInventoryImportService registrava carichi e pacchi attraverso RegisterLegacyBatch nelle collection RAM. LegacyHistoricalStore conservava storico, batch e chiavi in memoria; la deduplicazione dipendeva dal fingerprint e dal numero di riga.

Ora: LegacySqlImportService rilegge il workbook corrente, valida una preview e importa giacenza e storico in una transazione SQL unica. I vecchi comandi RAM sono rimossi dal ViewModel/UI. Il vecchio servizio di giacenza resta come codice tecnico non collegato alla UI. LegacyHistoricalStore è una cache di lettura ricaricata da SQL nel refresh delle pagine di consultazione; non accetta più commit RAM.

## Schema utilizzato

Tabelle: Suppliers (associazione per nome normalizzato; nuovi fornitori inattivi), Loads, MaterialGroups, Packages, LegacyImportBatches, LegacyImportKeys, LegacyHistoricalRecords. ThicknessFamilies viene letta per validare gli spessori. Riutilizzati gli indici univoci su fingerprint/tipo batch, chiave sorgente e codice pacco. Nessuna migration necessaria.

Lo stato classificato è persistito in MaterialGroups.IsClassified. Non sono creati ClassificationMovements con operatori inventati. Il workbook non contiene i dati necessari a ricostruire rettifiche complete: nessun WasteAdjustment artificiale; tutti i MC correnti restano da consolidare. L'eventuale data di classificazione dei pacchi correnti non genera un movimento senza operatore storico attendibile; questo limite viene segnalato nella preview. Le date di classificazione dello storico chiuso sono conservate nella tabella legacy.

## Identità, deduplicazione e atomicità

La preview mostra percorso, nome, dimensione, ultima modifica, SHA-256 e istante dell'analisi. Alla conferma viene riletto il file e confrontato il fingerprint. Il file rimane aperto senza condivisione di scrittura fino alla fine della transazione. Se è cambiato occorre una nuova analisi.

La chiave TIMBER-V1 è un hash dell'identità normalizzata fornitore/anno/carico/etichetta pacco (QR se manca l'etichetta), indipendente dal file e dalla posizione della riga. Le righe note vengono ignorate senza sovrascrivere lo stato SQL. Duplicati ambigui bloccano l'import. Nuove righe di giacenza per un carico SQL già esistente vengono bloccate come import incrementale ambiguo: non si tratta di una sincronizzazione.

Transazione Serializable, lock applicativo SQL e indici univoci proteggono batch e dati. Qualsiasi errore prima del commit annulla anche il batch già inserito. La quadratura SQL viene verificata prima del commit. Un errore di refresh dopo commit viene segnalato separatamente e non provoca un secondo salvataggio.

## Prezzi, giacenza ed eventi

Prezzo letto esclusivamente dalla colonna nominata Prezzo; eliminato il ripiego sulla posizione X. Prezzo corrente mancante/non valido è bloccante. Valore = volume fisico originario × prezzo storico, senza listini attuali. Volume = pezzi × spessore ingresso × larghezza ingresso × lunghezza ingresso / 1.000.000.000, senza riduzioni. Persistenza secondo le precisioni SQL esistenti: volume 9 decimali, importi 4 decimali.

I gruppi includono il prezzo nella loro identità, evitando di mescolare prezzi storici differenti. Classificati e non classificati mantengono stati distinti; i non classificati sono disponibili al normale flusso dopo il reload SQL.

Chiusura datata = Scarico. Dicitura Reso = Reso, volume fisico originario, senza data artificiale. Scarto e altre chiusure testuali restano Chiusura legacy, non vengono trasformate in scarico o reso. Il riepilogo storico distingue queste categorie.

## Ultima quadratura di prova

Workbook generato di test, non workbook reale. Il numero di righe della fixture varia tra esecuzioni; tutti gli assert sono calcolati dai dati del workbook appena letto.

- File: fixture-20261001071902/fixture.xlsm nell'output locale work/legacy-tests.
- SHA-256: 83D34A68212C5D5F99C6154DB9E359EC2ACD0455CECB65092B9B805D79F01819.
- Righe: 7 lette, 7 valide, 0 escluse.
- Giacenza: 1 carico, 4 pacchi, 2 classificati e 2 da classificare.
- MC fisici: 0,828000000; MC da consolidare: 0,828000000; MC reali ricostruibili: 0.
- Valore storico: 269,5600; 4 prezzi presenti, 0 mancanti.
- Storico: 3 righe; 1 scarico datato, 1 reso, 1 altra chiusura legacy (Scarto).
- Duplicati: 0. Bloccanti: 0. Warning: 4 (due chiusure senza data e due classificazioni senza elementi per ricostruire movimento/rettifica).
- SQL coincide con la preview per conteggi, classificati, volumi e valori.

## Test

A: import su database SQL temporaneo pulito, quadratura superata.
B: avvio di un secondo processo, ricaricamento SQL e stessa quadratura; non è una prova fisica da un secondo PC.
C: stesso file e file diverso con righe riordinate, nessun duplicato e nessuna variazione SQL.
D: file modificato dopo preview, import respinto e nessuna scrittura.
E: errore forzato dopo inserimento batch SQL, rollback integrale verificato.
F: Dashboard, Giacenze, Storico e Statistiche ricaricano i dati SQL; verificati valori, MC da consolidare, resi e riepilogo scarichi.
G: prezzi storici corretti anche con listino fornitore volutamente diverso.
H: classificati/non classificati persistiti e ricaricati correttamente, nessun evento inventato.
I: quadratura numerica workbook/SQL superata.

Controlli aggiuntivi: prezzo mancante e duplicato ambiguo bloccanti. Test su database MagazzinoLegname_TestLegacy_<GUID>, creato e rimosso dal processo di verifica. Nessuna modifica ai dati del database operativo.

Esecuzione: impostare MAGAZZINOLEGNAME_DB_SETTINGS sul file di configurazione del server di test, quindi avviare Verification/LegacySql/LegacySql.csproj. Il programma crea un database dal nome univoco; richiede permessi di creazione/eliminazione di quel database. Non invoca l'avvio WPF ordinario che migra il database operativo.

## Prima dell'import reale

Usare una nuova preview del workbook definitivo, risolvere tutti i blocchi e controllare warning/identità fornitori; validare la quadratura sul file aggiornato. Concordare il fermo Excel e disporre di backup e destinazione SQL verificata. L'import reale richiede autorizzazione separata e conferma del fingerprint. Effettuare anche la verifica dal secondo PC nell'ambiente finale.

Dopo il go-live il comando può essere disabilitato impostando MAGAZZINOLEGNAME_DISABLE_INITIAL_IMPORT=1: il pulsante non è abilitato e il servizio rifiuta l'import. Non è stato disabilitato in questa fase.

## File

Nuovo: Services/LegacySqlImportService.cs; Verification/LegacySql (progetto, programma, questo report).
Modificati: Services/LegacyExcelReader.cs, LegacyImportAnalyzer.cs, LegacyHistoricalStore.cs, ConsultationSqlRefresh.cs; Models/LegacyImportModels.cs, LegacyHistoricalRecord.cs; Persistence/Repositories/InboundLoadRepository.cs (sola lettura metadati legacy); ViewModels/SettingsViewModel.cs, HistoryViewModel.cs; Views/SettingsView.xaml e SettingsView.xaml.cs.

Flussi ordinari, consumabili, pianificazione, LoadMaster e schema/migration invariati.

Build finali Debug e Release: 0 errori, 0 warning (applicazione e progetto di verifica).

