# Progressivo supplementare per carico — verifica 2026-10-02

## Diagnosi prima della modifica

`SqlInboundLoadRepository.AddSupplementary` interrogava e bloccava i supplementari del solo `MaterialGroupId`, mentre il codice non comprende il gruppo ed è globalmente univoco. La seconda creazione su un altro gruppo ripartiva da 1.

Riproduzione SQL reale su database temporaneo con implementazione precedente:

- LoadId: `3317cd84-b096-42e2-9b07-0211738992ca`.
- Primo gruppo: `c140ed3a-d4ea-4efb-ae78-5af285d4aa30`.
- Secondo gruppo: `3fe1da35-ee44-44e1-a379-12bc291fcd44`.
- Entrambi: SupplementarySequence = 1, PackageCode = `TEST-4-26-S01`.
- Eccezione: `DbUpdateException`, InnerException `Microsoft.Data.SqlClient.SqlException`, SQL error **2601**.
- Indice: `IX_Packages_PackageCode`, tabella `dbo.Packages`, chiave duplicata `(TEST-4-26-S01)`.
- Metodo: `SqlInboundLoadRepository.AddSupplementary`, `db.SaveChanges()` (riga 154 nella versione precedente).

Lettura del DB Dev, senza scritture: carico 4-26, LoadId `a4408d29-b705-45ad-9b7c-7ecfac8ca98e`; gruppo 44x190 `3c7cfde1-3dad-4816-b7b3-e2d994c0e555` con S01/S02; gruppo 34x180 `e9278ef3-8ca8-49b5-b98f-0c1e9d8682d9` senza supplementari. Nessun progressivo duplicato per carico rilevato nella verifica preliminare.

## Correzione

Transazione Serializable esistente mantenuta. Lock `UPDLOCK, HOLDLOCK` sulla riga padre Loads prima della lettura dei supplementari; massimo letto da SQL su tutto il LoadId sotto lock. Il lock padre copre anche il caso di nessun supplementare esistente. Codice, QR e associazione al gruppo conservano le regole precedenti.

Nuova migration `20261002122456_SupplementarySequencePerLoad`: sostituisce l'indice univoco filtrato `(MaterialGroupId, PackageType, SupplementarySequence)` con `(LoadId, PackageType, SupplementarySequence)`. Filtro invariato: PackageType = 1 e sequenza non nulla. Mantiene l'indice non filtrato LoadId, aggiunge quello MaterialGroupId per la FK, non modifica l'unicità globale PackageCode. Nessuna migration precedente modificata.

La migration non aggiorna righe. Prima di cambiare gli indici controlla eventuali progressivi duplicati per carico e, se presenti, si arresta con errore 51002 richiedendo verifica manuale; non rinumera nulla.

## Test eseguiti

Database SQL temporaneo dedicato creato e rimosso dal programma. Migrazione dalla versione precedente con dati già presenti; snapshot di codici, ID, QR, gruppo, sequenze e RowVersion invariato. Modello EF e snapshot coerenti.

- A PASS: gruppi diversi S01/S02, riferimenti al gruppo corretti.
- B PASS: ritorno al primo gruppo, S03.
- C PASS: selezione ristampa tramite ClassificationViewModel, stesso ID/codice/QR S01, nessun S04 inserito. Handler UI verificato: selezione e anteprima del pacco esistente, nessuna chiamata AddSupplementary. Non eseguita stampa fisica.
- D PASS: nuovo processo, rilettura SQL, nuovo S04. Verifica automatizzata del riavvio del processo; non chiusa l'app WPF dell'utente.
- E PASS: due repository/context SQL concorrenti su gruppi diversi dello stesso carico, S05/S06 e QR distinti.
- F PASS: altro carico riparte da S01.
- Dati preesistenti S01/S02: creazione su altro gruppo produce S03 senza alterare i precedenti.
- Pezzi, MC, valori e RowVersion ufficiali invariati; supplementari senza pezzi, MC o valore.
- Il nuovo indice respinge una sequenza duplicata anche quando il codice è diverso.

Build Debug del progetto di verifica e applicazione referenziata: 0 errori, 0 warning.
Build Release applicazione: 0 errori, 0 warning.
Output separati perché l'eseguibile Debug ordinario è utilizzato dall'app aperta. Un primo tentativo sulla cartella ordinaria è fallito per il file bloccato; le build finali sopra sono riuscite.

## Esecuzione

Impostare `MAGAZZINOLEGNAME_DB_SETTINGS` a un file di configurazione valido con Windows Authentication. Il test sostituisce la destinazione con `MagazzinoLegname_TestSupplementary_<GUID>` e usa una allowlist dedicata. L'account deve poter creare/eliminare il database temporaneo. Compilare il progetto e avviare l'eseguibile SupplementarySequence.exe.

## Applicazione al database operativo

La nuova migration è stata applicata soltanto al database temporaneo di verifica, non a MagazzinoLegname_Dev. L'app aperta non è stata interrotta. Per utilizzare la correzione occorre distribuire/eseguire la nuova build con la migration applicata secondo la politica di avvio già esistente (Dev automatico se abilitato; Shared solo aggiornamento esplicito autorizzato). Non usare contemporaneamente client precedenti e nuovi durante l'aggiornamento.

Nessun commit; nessuna modifica agli altri moduli o a LoadMaster.
