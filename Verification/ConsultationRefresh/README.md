# Fase 2F-1 — verifica refresh consultazione

Eseguire soltanto su MagazzinoLegname_Dev, impostando MAGAZZINOLEGNAME_DB_SETTINGS al file di configurazione desiderato.

`dotnet run --project Verification/ConsultationRefresh/ConsultationRefresh.csproj -c Debug`

Il test crea un fornitore, operatore e carico con GUID univoco, usa i repository applicativi da contesti indipendenti per registrazione, classificazione (due eventi), rettifica e scarico. Le quattro pagine WPF vengono aperte e riaperte tramite NavigationService senza aprire Pianificazione. Verifica KPI, tabella giacenze, storico completo, grafici, pulsanti Aggiorna, DateTo nullo/valorizzato e date entrambe nulle. Simula timeout tramite il punto di iniezione del caricamento, verifica il contenuto collassato e il retry. La fixture viene eliminata in finally con transazione ed execution strategy.

Non applica migration e non usa un secondo PC fisico. Non importa dati legacy. La verifica UI usa controlli WPF compilati e dispatcher STA, senza automazione del desktop.
