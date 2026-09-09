# Persistenza SQL Server — Fase 2A

Fornitori, operatori e configurazioni applicative usano SQL Server. I flussi operativi legname e i materiali di consumo restano temporaneamente in-memory.

`database.settings.json` viene copiato accanto all'eseguibile ed è modificabile senza ricompilazione. In alternativa la variabile ambiente `MAGAZZINOLEGNAME_DB_SETTINGS` può indicare un file esterno. `Authentication` accetta `Windows` oppure `SqlServer`.

Non sono definiti seed EF e non vengono creati fornitori, operatori, carichi o pacchi demo. Alla prima apertura di un database vuoto il servizio applicativo inizializza esclusivamente le famiglie 23/34/44, la certificazione PEFC e gli MC standard per carico.

All'avvio vengono applicate le migration pendenti al database configurato. Se SQL Server non è disponibile l'applicazione mostra un errore e termina, senza fallback silenzioso agli store in-memory.

I repository creano un `MagazzinoDbContext` per ogni operazione e lo rilasciano subito; non esiste un contesto singleton. Le pagine anagrafiche ricaricano i dati SQL a ogni navigazione.

Gli eventi terminali condividono la tabella `PackageTerminalEvents`, con indice univoco su `PackageId`: un pacco può avere al massimo una tra uscita, reso, rimozione o uscita supplementare. Il comando applicativo futuro dovrà inserire l'evento e aggiornare il pacco nella stessa transazione.
