# Changelog — PoopMan

## [1.4.0] — 2026-10-20

### ✨ Aggiunto

- **Nuovo mondo: i Golem** (dal livello 30): nemici di pietra che camminano invece di volare. Dal livello 30 una parte di ogni ondata (30 %, +2 % per livello, max 60 %) diventa golem.

  | Golem | Da | Abilità |
  |---|---|---|
  | Normal | Lv 30 | Nessuna abilità, più lento ma più resistente dei bat |
  | Lanciatore (Booler) | Lv 30 | Lancia una palla di roccia quando sei sulla sua riga o colonna |
  | Bruto (Brute) | Lv 32 | Robusto; 35 % di incassare un colpo, poi va in **furia** |
  | Rinoceronte (Rhino) | Lv 34 | **Carica** in linea retta: ti stordisce, contro un muro si stordisce lui |
  | Laser | Lv 36 | Carica (linea rossa di avviso) e spara un laser che ferisce, distrugge i blocchi e fa esplodere le bombe |
  | Godness *(boss raro)* | Lv 35 | Tutte le abilità, ti vede attraverso i muri e li attraversa. **Se ti tocca è game over**. Max 2 HP |

  La Godness compare con il 2 % di probabilità al livello 35 (+0,4 % per livello, max 8 %). Punti per kill: Normal +250, Lanciatore +350, Bruto e Rinoceronte +450, Laser +500, Godness +5000. L'Enciclopedia ha una scheda per ognuno.
- **Comandi personalizzabili**: nuovo pannello *COMANDI* (menu principale e pausa) con due tasti tastiera e un pulsante gamepad per ogni azione, salvati in `%APPDATA%\PoopMan\controls.cfg`. I conflitti vengono scambiati automaticamente.
- **Porte diverse per ogni bioma**, con e senza chiave.
- **Atmosfera dei biomi**: raggi di sole, aurora, nebbia, neve con vento, braci, cenere, bolle sui liquidi, gocce, lucciole e fuochi fatui.
- **Effetti di stato**: stelline da stordimento, ghiaccio da rallentamento, lampo da colpo, testo del danno, `IMMUNE`, `!` quando un bat ti avvista, scie per Ghost / Dasher / Berserk, anello d'allarme del Walid; effetto scudo e scia dello scatto dopo un colpo per il Minatore.
- **Gamepad completo nei menu**: pannello audio, pausa (`B` = indietro / riprendi), Enciclopedia (`LB` / `RB` per le schede), Classifica e inserimento nome. La schermata Istruzioni mostra la tabella tastiera + gamepad.
- Bioma Rovine: muri distruttibili e pavimenti preservati; nuove texture di terreno e oggetti.

### 🔧 Modificato

- **Movimento fluido del Minatore**: movimento continuo tra le caselle, priorità all'ultimo tasto premuto, svolte assistite, buffer delle svolte e inversione a metà casella.
- **IA dei pipistrelli**: niente salti in diagonale quando un altro bat occupa la casella, fuga che evita bombe e porta, inseguimento più fluido, scatto del Dasher davvero veloce, vagabondaggio senza oscillazioni avanti e indietro.
- **DETONATORE** e **MICCIA CORTA** sono ora mutuamente esclusivi: non vengono mai proposti insieme e, se ne hai uno, l'altro sparisce dal menu.
- Bioma Lava: la lava illumina molto di più l'ambiente (raggio e bagliore maggiori, ambiente più scuro).
- Il progetto di test unitari `PoopMan.UnitTests` è stato rimosso; i controlli restano affidati al workflow *Windows auto-play*. Pacchetti MonoGame aggiornati.

### 🐛 Risolto

- Le mini-esplosioni dell'upgrade **Catena** non avevano suono, lampo, screen shake né particelle: ora sono visibili come le altre esplosioni.
- La lanterna del Minatore ora si attenua nei biomi luminosi e vicino a lava ed esplosioni.
- Audio: riattivare il volume non riavvia più la musica quando il volume è a 0; la musica del titolo non si sente più nel menu di pausa (hover e click usano brevi suoni generati).
- HUD: le etichette delle abilità non si sovrappongono più alle bombe; si riducono o vanno su due righe.

---

> 📦 **Download**: scarica `PoopMan.v1.4.0.zip`, estrai e avvia `PoopMan.exe` (Windows, richiede il [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)).
> 🐛 **Segnala un bug**: apri una [Issue](https://github.com/FKProf/PoopMan/issues)
