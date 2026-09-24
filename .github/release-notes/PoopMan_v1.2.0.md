# Changelog — PoopMan

## [1.2.0] — 2026-09-24

### ✨ Aggiunto

- **Esplosioni a catena** (stile Bomberman classico): una bomba raggiunta dalle fiamme esplode subito. Anche le esplosioni di Walid, Nuke e le mini-esplosioni *Catena* innescano le bombe del Minatore.
- **Nuovo upgrade `DETONATORE`**: premi `C` (o `Y` sul gamepad) per far esplodere all'istante tutte le bombe piazzate.
- **Supporto gamepad** (giocatore 1), in gioco e nei menu:

  | Pulsante | Azione |
  |---|---|
  | D-pad / levetta sinistra | Movimento / navigazione menu |
  | `A` | Bomba piccola / conferma |
  | `B` | Bomba grande |
  | `Y` | Detonatore |
  | `Start` | Pausa / conferma |
  | `Back` | Pausa / indietro |

- **Effetti esplosione**: flash radiale morbido su ogni esplosione, anello d'urto su bomba grande / Walid / Nuke e **screen shake** proporzionato alla potenza (l'HUD resta fermo).
- **Classifica**: dopo il game over `ENTER` / `R` fanno ripartire subito la partita.
- Indicatore `[DET]` nell'HUD quando il Detonatore è attivo.

### 🔧 Modificato

- **Massimo 28 pipistrelli per ondata** (prima `1 + livello`, es. 94 al livello 93): oltre questa soglia la difficoltà cresce con HP, velocità e varianti speciali.
- **Porta e chiave sempre raggiungibili** dallo spawn (eventualmente rompendo blocchi): niente più livelli bloccati da acqua o lava.
- Il menu upgrade **non propone più scelte inutili** (es. *+1 Vita* a vite piene, invincibilità già al massimo).
- `ESC` nelle pagine *Audio* ed *Enciclopedia* della pausa torna indietro invece di chiudere tutto il menu.
- L'hover del mouse cambia la selezione solo se il mouse si muove: un cursore fermo non blocca più la navigazione da tastiera.
- Breve ritardo (0,4 s) prima di confermare il menu upgrade, per evitare scelte accidentali.
- Istruzioni aggiornate (chiave dal livello 5, esplosioni a catena, gamepad, vita extra ogni 1000 punti).

### 🐛 Risolto

- **Crash** (`InvalidOperationException`) quando una mini-esplosione *Catena* uccideva un pipistrello Splitter.
- L'upgrade **CRITICO** si poteva scegliere ma non aveva alcun effetto: ora 20% di probabilità di uccidere il pipistrello al contatto (punti doppi).
- Posizioni "fantasma" dei pipistrelli del livello precedente che bloccavano il movimento dei nuovi.
- Il click del mouse nel menu upgrade poteva selezionare la carta sbagliata (layout diverso dal disegno).
- La carta *POTENZA* mostrava metà del bonus danno reale.
- Le mini-esplosioni *Catena* ora rispettano *Immortalità Mistica*.
- Perdita di memoria GPU (una texture per ogni pipistrello, risorse della scena non liberate).
- Possibile crash del testo con caratteri non supportati dal font.
- Il testo del pannello audio usciva dal riquadro.

---

> 📦 **Download**: scarica `PoopMan.v1.2.0.zip`, estrai e avvia `PoopMan.exe` (Windows, richiede il [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)).
> 🐛 **Segnala un bug**: apri una [Issue](https://github.com/FKProf/PoopMan/issues)
