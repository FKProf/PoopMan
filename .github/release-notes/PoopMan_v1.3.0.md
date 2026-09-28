# Changelog — PoopMan

## [1.3.0] — 2026-09-28

### ✨ Aggiunto

- **Grafica rinnovata** (sempre 2D pixel-art, senza shader):
  - **Illuminazione dinamica**: ogni bioma ha la sua luce (foresta luminosa, grotta buia, lava calda). Il minatore porta una lanterna; micce, esplosioni, pozze di lava, chiave, porta e pipistrelli emettono luce colorata.
  - **Profondità**: muri e blocchi proiettano ombre morbide sul pavimento; ombre sotto minatore, pipistrelli, bombe e oggetti.
  - **Fiamme a croce stile Bomberman**: raggi luminosi che collegano le caselle di ogni esplosione.
  - **Particelle**: schegge e polvere dei blocchi distrutti, fumo e scintille delle esplosioni, scintille delle micce, polvere dei passi, esplosione di scintille quando raccogli un oggetto.
  - **Animazioni**: le bombe "respirano" e lampeggiano più in fretta a fine miccia, i pipistrelli e gli oggetti fluttuano, dissolvenza all'inizio di ogni livello.
  - **Testi a comparsa**: `+100`, `CRIT!`, `COMBO x3`, `CHIAVE!`, `+1 TNT`, `-1 VITA` e il nome dell'upgrade scelto.
  - **Interfaccia moderna**: pannelli con angoli arrotondati, ombra e sfumatura in tutti i menu, pulsante selezionato luminoso, logo del titolo che fluttua con alone e braci.
- **HUD**: sfondo sfumato con il colore del bioma e indicatore delle **bombe ancora piazzabili**.
- **Test automatico su Windows** (workflow *Windows auto-play*): a ogni modifica il vero `PoopMan.exe` viene avviato e giocato con tasti reali, poi un bot gioca centinaia di livelli controllando crash, pipistrelli nei muri, porta e chiave raggiungibili.

### 🔧 Modificato

- **DETONATORE**: le bombe **non hanno più la miccia** ed esplodono solo quando premi `C` (o `Y` sul gamepad), oppure se le raggiunge un'altra esplosione. Le bombe telecomandate lampeggiano di rosso.
- Con il Detonatore il menu non propone più *Miccia corta*, che non avrebbe effetto.
- Chiave, casse e porta si raccolgono anche mentre il minatore lampeggia dopo un colpo.
- L'HUD si nasconde quando è aperta l'Enciclopedia (prima ne copriva il titolo).

### 🐛 Risolto

- Con il Detonatore le bombe **esplodevano comunque dopo qualche secondo**.
- I pipistrelli spinti da un'esplosione potevano attraversare in diagonale gli angoli dei pilastri o **restare incastrati dentro un muro**.
- Un pipistrello la cui casella di arrivo veniva occupata da una bomba poteva tagliare l'angolo di un muro tornando indietro.
- Nella Classifica la colonna della data usciva dalla tabella.
- Nel menu di pausa il testo di aiuto usciva dal riquadro.
- Le particelle delle esplosioni restavano ferme a mezz'aria durante l'animazione di morte.

---

> 📦 **Download**: scarica `PoopMan.v1.3.0.zip`, estrai e avvia `PoopMan.exe` (Windows, richiede il [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)).
> 🐛 **Segnala un bug**: apri una [Issue](https://github.com/FKProf/PoopMan/issues)
