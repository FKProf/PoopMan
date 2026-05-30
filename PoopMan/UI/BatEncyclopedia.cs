using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.GameObjects;
using PoopManLibrary.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static PoopMan.GameObjects.MinerUpgradeDescriptions;

namespace PoopMan.UI;

/// <summary>
///     Enciclopedia di gioco con tre sezioni: Nemici, Upgrade, Biomi.
///     Si apre dal menu pausa e si chiude con ESC o pulsante Chiudi.
/// </summary>
public class BatEncyclopedia
{
    // -- Layout costanti -------------------------
    private const float CardW = 310f;
    private const float CardH = 490f;
    private const float CardGap = 20f;
    private const float ScrollLerpSpeed = 0.18f;
    private const float ScrollSettledThreshold = 2f;
    private const float AnimSpeed = 0.14f;

    // -- Tab layout
    private const int TabBarY = 70;   // y-coordinate of the tab bar
    private const int TabH = 32;
    private const int TabW = 150;
    private const int TabGap = 12;

    private static readonly string[] TabNames = { "NEMICI", "UPGRADE", "BIOMI" };
    private int _tab; // 0=Nemici, 1=Upgrade, 2=Biomi

    // --------------------------------------------------------------------
    // SEZIONE NEMICI
    // --------------------------------------------------------------------
    private static readonly BatEntry[] BatEntries =
    {
        new(
            "PIPISTRELLO NORMALE",
            "Presente dal Livello 1",
            new Color(140, 90, 200),
            Color.Transparent,
            new[]
            {
                "Insegue il Miner quando lo avvista",
                "Velocita e HP crescono col livello",
                "Dal lv 20: resistente (+1 HP ogni 5 lv, max 6)",
                "Nessuna abilita speciale"
            },
            "Usaci una bomba. Ai livelli alti diventa piu duro!",
            "BASE",
            new Color(160, 120, 220)
        ),
        new(
            "BAT ROBUSTO",
            "Sblocca al Livello 20",
            new Color(255, 100, 100),
            Color.Transparent,
            new[]
            {
                "Versione corazzata del pipistrello normale",
                "Ha da 2 a 6 HP in base al livello (lv 20+)",
                "Ogni 5 livelli oltre il 20 guadagna +1 HP (max 6 HP)",
                "Velocita identica al bat normale ma piu resistente",
                "Richiede piu bombe o esplosioni potenziate per essere eliminato",
                "+75 punti al kill (base)"
            },
            "Usa upgrade Potenza o Danno+ per abbatterlo rapidamente!",
            "ROBUSTO",
            new Color(255, 100, 100)
        ),
        new(
            "DASHER",
            "Sblocca al Livello 5",
            new Color(80, 200, 255),
            new Color(60, 180, 255, 80),
            new[]
            {
                "Puo eseguire uno scatto improvviso",
                "Cooldown scatto: 3 secondi",
                "25% chance di scattare ad ogni passo",
                "+50 punti al kill"
            },
            "Allontanati dopo la bomba: lo scatto sorprende!",
            "VELOCE",
            new Color(80, 200, 255)
        ),
        new(
            "WALID",
            "Sblocca al Livello 8",
            new Color(255, 140, 0),
            new Color(255, 100, 0, 90),
            new[]
            {
                "Ti rincorre e ti esplode addosso",
                "Esplode alla morte: area 7x7 tile (raggio 3)",
                "0.5 s di lampeggio prima della detonazione",
                "Knockback forte su bat e Miner vicini",
                "Rispetta invincibilita e scudo del Miner",
                "+125 punti al kill"
            },
            "Lampeggia arancione prima di esplodere. Scappa subito!",
            "ESPLOSIVO",
            new Color(255, 160, 0)
        ),
        new(
            "GHOST BAT",
            "Sblocca al Livello 10",
            new Color(200, 200, 255),
            new Color(180, 180, 255, 70),
            new[]
            {
                "Attraversa le bombe solide in fase fantasma",
                "Durata fase: 2 secondi",
                "Cooldown: 8 secondi tra le fasi",
                "10% chance di attivare la fase ogni step",
                "+100 punti al kill"
            },
            "In fase le bombe non lo bloccano. Usa esplosioni dirette!",
            "FANTASMA",
            new Color(180, 180, 255)
        ),
        new(
            "SPLITTER",
            "Sblocca al Livello 15",
            new Color(100, 220, 100),
            new Color(80, 200, 80, 80),
            new[]
            {
                "Si divide in 2 mini-bat alla morte",
                "I mini-bat sono piu veloci ma fragili (1 HP)",
                "I mini-bat NON si dividono ulteriormente",
                "+150 punti al kill (50 per mini-bat)"
            },
            "Elimina subito i mini-bat: sono veloci e imprevedibili!",
            "DIVISORE",
            new Color(100, 220, 100)
        ),
        new(
            "BERSERK",
            "Sblocca al Livello 16",
            new Color(200, 0, 255),
            new Color(160, 0, 255, 130),
            new[]
            {
                "Furia quando il Miner e a 3 tile di distanza",
                "Velocita x2.2 per 2 secondi in stato furioso",
                "Insegue sempre il Miner da vicino",
                "+250 punti al kill"
            },
            "Non avvicinarti! Da vicino e letale. Usa bombe a distanza.",
            "BERSERK",
            new Color(200, 80, 255)
        ),
        new(
            "NUKE",
            "Sblocca al Livello 20",
            new Color(255, 60, 60),
            new Color(255, 40, 0, 130),
            new[]
            {
                "Esplode alla morte: area 13x13 tile (raggio 6)",
                "Instant kill su tutti i nemici nell'area",
                "Fungo atomico + detriti radioattivi verdi",
                "La porta e SEMPRE immune all'esplosione",
                "Rispetta invincibilita e scudo del Miner",
                "+200 punti al kill"
            },
            "Il piu pericoloso! Eliminalo a distanza massima con big bomb.",
            "NUCLEARE",
            new Color(255, 80, 60)
        )
    };

    // Le descrizioni degli upgrade del Miner sono definite in MinerUpgradeDescriptions.cs
    // Gli upgrade Mythic sono inclusi alla fine dell'array Standard con IsMythic=true

    // --------------------------------------------------------------------
    // SEZIONE BIOMI
    // --------------------------------------------------------------------
    private static readonly BiomeEntry[] BiomeEntries =
    {
        new("PRATERIA", "Livelli 1-4", new Color(80, 200, 80), new Color(60, 180, 60, 60),
            "Il bioma di partenza. Terreno verde e luminoso, ideale per imparare le meccaniche di gioco.",
            new[]
            {
                "Visibilita ottimale senza ostacoli visivi",
                "Breakable distribuiti casualmente",
                "Nessun pericolo ambientale aggiuntivo",
                "Bat normali e Dasher come varianti principali"
            },
            "Impara i movimenti e la gestione delle bombe qui prima di avanzare.",
            new Color(60, 200, 60)),

        new("GHIACCIO", "Livelli 5-9", new Color(140, 200, 255), new Color(100, 180, 255, 70),
            "Ambiente innevato e gelido. Dal livello 5 la porta richiede la chiave per aprirsi.",
            new[]
            {
                "DAL LV 5: serve la CHIAVE per aprire la porta",
                "Sfondo bianco-azzurro con cristalli di ghiaccio",
                "Walid compare in questo bioma (lv 8)",
                "Trova la chiave prima di avvicinarti all'uscita"
            },
            "Trova la chiave prima di cercare la porta! Walid esplode: stai lontano.",
            new Color(120, 180, 255)),

        new("LAVA", "Livelli 10-14", new Color(255, 100, 20), new Color(255, 80, 0, 80),
            "Zone vulcaniche con lava animata. L'ambiente rosso-arancione crea alta tensione.",
            new[]
            {
                "Sfondo rosso-arancione con lava in movimento",
                "Ghost Bat sblocca in questo bioma (lv 10)",
                "Maggiore densita di blocchi breakable strategici",
                "Il Ghost ignora le bombe solide: usa esplosioni dirette"
            },
            "Il Ghost Bat e imprevedibile: evita di usare solo bombe-blocco.",
            new Color(255, 120, 0)),

        new("PALUDE", "Livelli 15-19", new Color(80, 160, 80), new Color(60, 140, 60, 80),
            "Terreno fangoso e cupo. I pipistrelli piu pericolosi iniziano a comparire.",
            new[]
            {
                "Sfondo verde scuro con ambiance pesante",
                "Splitter (lv 15) e Berserk (lv 16) sbloccati",
                "I mini-bat dello Splitter sono molto veloci",
                "Il Berserk entra in furia a 3 tile di distanza"
            },
            "Non lasciare mai lo Splitter vicino a te quando esplode.",
            new Color(80, 180, 80)),

        new("CAVERNA", "Livelli 20+", new Color(180, 100, 255), new Color(160, 80, 255, 100),
            "Grotte oscure e claustrofobiche. Il Nuke compare qui per la prima volta.",
            new[]
            {
                "Sfondo viola scuro con elementi rocciosi",
                "NUKE sblocca al livello 20: area 13x13 tile",
                "I bat normali diventano resistenti (multi-HP)",
                "Varianti miste: piu bat speciali per ondata",
                "La porta e SEMPRE immune alle esplosioni Nuke"
            },
            "Usa la big bomb per il Nuke ma solo da distanza di sicurezza!",
            new Color(160, 100, 255)),
    };

    // -- Dipendenze ------------------------------
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private int _animFrame;
    private float _animTimer;
    private Dictionary<string, List<Rectangle>> _batAnimations = new();
    private Texture2D? _batTexture;
    private bool _closeHovered;
    private int _hoveredArrow;
    private int _hoveredCard = -1;
    private float _pulse;
    private int _prevTab = -1;  // detects tab switches to reset scroll instantly

    // -- Scroll / selezione per sezione ----------
    private readonly float[] _scrollOffsets = new float[3];
    private readonly int[] _selectedCards = new int[3];

    public BatEncyclopedia(SpriteFont font, Texture2D pixel, ContentManager content)
    {
        _font = font;
        _pixel = pixel;
        LoadBatAnimations(content);
    }

    private int CurrentCount => _tab switch
    {
        0 => BatEntries.Length,
        1 => Standard.Length,
        _ => BiomeEntries.Length
    };

    private int Selected
    {
        get => _selectedCards[_tab];
        set => _selectedCards[_tab] = value;
    }

    private float ScrollOffset
    {
        get => _scrollOffsets[_tab];
        set => _scrollOffsets[_tab] = value;
    }

    private void LoadBatAnimations(ContentManager content)
    {
        try
        {
            var xmlPath = Path.Combine(content.RootDirectory, "image", "enemies", "bat.xml");
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root == null) return;
            var textureEl = root.Element("Texture");
            if (textureEl == null) return;
            _batTexture = content.Load<Texture2D>(textureEl.Value);

            var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();
            foreach (var region in root.Descendants("Region").Where(r => r.Attribute("Name") != null))
            {
                var fullName = region.Attribute("Name")!.Value;
                if (!int.TryParse(region.Attribute("X")?.Value, out var rx)) continue;
                if (!int.TryParse(region.Attribute("Y")?.Value, out var ry)) continue;
                if (!int.TryParse(region.Attribute("Width")?.Value, out var rw)) continue;
                if (!int.TryParse(region.Attribute("Height")?.Value, out var rh)) continue;

                var fs = fullName.Length;
                while (fs > 0 && char.IsDigit(fullName[fs - 1])) fs--;
                if (fs >= fullName.Length || fs == 0) continue;
                var animName = fullName[..fs].TrimEnd('_', '-', ' ');
                if (!int.TryParse(fullName[fs..], out var fnum)) continue;

                if (!temp.ContainsKey(animName)) temp[animName] = new List<(int, Rectangle)>();
                temp[animName].Add((fnum, new Rectangle(rx, ry, rw, rh)));
            }

            _batAnimations = temp.ToDictionary(
                p => p.Key,
                p => p.Value.OrderBy(f => f.frame).Select(f => f.rect).ToList());
        }
        catch { }
    }

    public void Open()
    {
        _tab = 0;
        for (var i = 0; i < 3; i++)
        {
            _selectedCards[i] = 0;
            _scrollOffsets[i] = 0f;
        }

        _hoveredCard = -1;
        _hoveredArrow = 0;
        _closeHovered = false;
        _pulse = 0f;
        _animTimer = 0f;
        _animFrame = 0;
    }

    /// <summary>Aggiorna input. Restituisce true se l'enciclopedia deve chiudersi.</summary>
    public bool Update(GameTime gameTime, KeyboardInfo kb, MouseInfo mouse, GraphicsDevice gd, bool escPressed)
    {
        if (escPressed) return true;

        _pulse += (float)gameTime.ElapsedGameTime.TotalSeconds * 3f;
        _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_animTimer >= AnimSpeed) { _animTimer = 0f; _animFrame++; }

        var vw = gd.Viewport.Width;
        var vh = gd.Viewport.Height;

        // -- Navigazione tab -------------------------
        if (kb.WasKeyJustPressed(Keys.Tab) || kb.WasKeyJustPressed(Keys.E))
            _tab = (_tab + 1) % 3;
        if (kb.WasKeyJustPressed(Keys.Q))
            _tab = (_tab - 1 + 3) % 3;

        // -- Navigazione card ------------------------
        if (kb.WasKeyJustPressed(Keys.Left) || kb.WasKeyJustPressed(Keys.A))
            Selected = Math.Max(0, Selected - 1);
        if (kb.WasKeyJustPressed(Keys.Right) || kb.WasKeyJustPressed(Keys.D))
            Selected = Math.Min(CurrentCount - 1, Selected + 1);

        // -- Scroll fluido ---------------------------
        // On tab switch, snap immediately to avoid mismatched offsets
        if (_tab != _prevTab)
        {
            _prevTab = _tab;
            var snapTotal = CurrentCount * (CardW + CardGap) - CardGap;
            ScrollOffset = Selected * (CardW + CardGap) + CardW / 2f - snapTotal / 2f;
        }

        var total = CurrentCount * (CardW + CardGap) - CardGap;
        var targetScroll = Selected * (CardW + CardGap) + CardW / 2f - total / 2f;
        // Clamp so we never scroll into empty space
        var minS = CardW / 2f - total / 2f;
        var maxS = (CurrentCount - 1) * (CardW + CardGap) + CardW / 2f - total / 2f;
        targetScroll = Math.Clamp(targetScroll, minS, maxS);
        var delta = targetScroll - ScrollOffset;
        ScrollOffset += delta * ScrollLerpSpeed;
        if (Math.Abs(delta) < 0.5f) ScrollOffset = targetScroll;
        var scrollSettled = Math.Abs(delta) < ScrollSettledThreshold;

        // -- Hit-test --------------------------------
        var startX = vw / 2f - (CurrentCount * (CardW + CardGap) - CardGap) / 2f - ScrollOffset;
        var cardY = GetCardY(vh);
        var mp = mouse.Position;

        _hoveredCard = -1;
        _hoveredArrow = 0;
        _closeHovered = false;

        for (var i = 0; i < CurrentCount; i++)
        {
            var cx2 = startX + i * (CardW + CardGap);
            if (cx2 + CardW < 0 || cx2 > vw) continue;
            var r = new Rectangle((int)cx2, cardY, (int)CardW, (int)CardH);
            if (r.Contains(mp)) { _hoveredCard = i; break; }
        }

        // Tab hit-test
        var tabsStartX = vw / 2 - (3 * (TabW + TabGap) - TabGap) / 2;
        for (var t = 0; t < 3; t++)
        {
            var tr = new Rectangle(tabsStartX + t * (TabW + TabGap), TabBarY, TabW, TabH);
            if (tr.Contains(mp) && mouse.WasButtonJustPressed(MouseButton.Left))
                _tab = t;
        }

        if (Selected > 0 && new Rectangle(8, vh / 2 - 20, 40, 40).Contains(mp)) _hoveredArrow = -1;
        if (Selected < CurrentCount - 1 && new Rectangle(vw - 48, vh / 2 - 20, 40, 40).Contains(mp)) _hoveredArrow = 1;

        _closeHovered = new Rectangle(vw / 2 - 80, vh - 48, 160, 34).Contains(mp);

        if (mouse.WasButtonJustPressed(MouseButton.Left))
        {
            if (_closeHovered) return true;
            if (_hoveredArrow == -1) Selected = Math.Max(0, Selected - 1);
            else if (_hoveredArrow == 1) Selected = Math.Min(CurrentCount - 1, Selected + 1);
            else if (scrollSettled && _hoveredCard >= 0) Selected = _hoveredCard;
        }

        var scroll = mouse.ScrollWheelDelta;
        if (scroll < 0) Selected = Math.Min(CurrentCount - 1, Selected + 1);
        else if (scroll > 0) Selected = Math.Max(0, Selected - 1);

        if (kb.WasKeyJustPressed(Keys.Enter) || kb.WasKeyJustPressed(Keys.Escape))
            return true;

        return false;
    }

    private int GetCardY(int vh) => TabBarY + TabH + 36;

    public void Draw(SpriteBatch sb)
    {
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;
        var cx = vw / 2;

        // Sfondo
        DrawRect(sb, new Rectangle(0, 0, vw, vh), new Color(5, 5, 20, 235));

        // Titolo
        DrawTextCentered(sb, "ENCICLOPEDIA", cx, 18, Color.Yellow, 1.6f);
        DrawRect(sb, new Rectangle(cx - 240, 38, 480, 2), new Color(120, 90, 30));

        // -- Tab -------------------------------------
        var tabsStartX = cx - (3 * (TabW + TabGap) - TabGap) / 2;
        for (var t = 0; t < 3; t++)
        {
            var tx = tabsStartX + t * (TabW + TabGap);
            var isActive = t == _tab;
            DrawRect(sb, new Rectangle(tx - 1, TabBarY - 1, TabW + 2, TabH + 2), isActive ? Color.Yellow : new Color(70, 60, 100));
            DrawRect(sb, new Rectangle(tx, TabBarY, TabW, TabH), isActive ? new Color(60, 40, 130) : new Color(20, 14, 50));
            DrawTextCentered(sb, TabNames[t], tx + TabW / 2, TabBarY + TabH / 2, isActive ? Color.Yellow : Color.Gray, 1.0f);
        }

        // Hint tab + counter
        var hintY = TabBarY + TabH + 8;
        DrawTextCentered(sb, "TAB/Q/E: cambia sezione", cx, hintY, new Color(80, 80, 120), 0.9f);
        DrawTextCentered(sb, $"{Selected + 1} / {CurrentCount}", cx, hintY + 14, Color.Gray, 0.95f);

        // -- Cards -----------------------------------
        var totalW2 = CurrentCount * (CardW + CardGap) - CardGap;
        // The scroll offset represents (selectedCard center) - (total cards center)
        // so the first card starts at: cx - totalW2/2 - ScrollOffset + offset_of_card_0_center
        // Simplified: first card left edge = cx - totalW2/2 - ScrollOffset
        var startX2 = cx - totalW2 / 2f - ScrollOffset;
        var cardY = GetCardY(vh);

        for (var i = 0; i < CurrentCount; i++)
        {
            var cardX = startX2 + i * (CardW + CardGap);
            if (cardX + CardW < 0 || cardX > vw) continue;
            var isSel = i == Selected;
            var isHov = i == _hoveredCard && !isSel;
            switch (_tab)
            {
                case 0: DrawBatCard(sb, BatEntries[i], (int)cardX, cardY, isSel, isHov); break;
                case 1: DrawUpgradeCard(sb, Standard[i], (int)cardX, cardY, isSel, isHov); break;
                case 2: DrawBiomeCard(sb, BiomeEntries[i], (int)cardX, cardY, isSel, isHov); break;
            }
        }

        // Frecce navigazione
        if (Selected > 0)
        {
            var bg = _hoveredArrow == -1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            DrawRect(sb, new Rectangle(8, vh / 2 - 20, 40, 40), bg);
            DrawTextCentered(sb, "<", 28, vh / 2, _hoveredArrow == -1 ? Color.White : new Color(255, 220, 80), 2.0f);
        }
        if (Selected < CurrentCount - 1)
        {
            var bg = _hoveredArrow == 1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            DrawRect(sb, new Rectangle(vw - 48, vh / 2 - 20, 40, 40), bg);
            DrawTextCentered(sb, ">", vw - 28, vh / 2, _hoveredArrow == 1 ? Color.White : new Color(255, 220, 80), 2.0f);
        }

        // Hint basso
        DrawTextCentered(sb, "< > / A D : sfoglia    scroll: scorre    ESC: chiudi", cx, vh - 64, Color.DimGray, 0.9f);

        // Pulsante Chiudi
        var cbx = cx - 80;
        var cby = vh - 48;
        DrawRect(sb, new Rectangle(cbx - 1, cby - 1, 162, 36), _closeHovered ? Color.White : Color.Yellow);
        DrawRect(sb, new Rectangle(cbx, cby, 160, 34), _closeHovered ? new Color(60, 40, 120) : new Color(30, 20, 70));
        DrawTextCentered(sb, "CHIUDI", cx, cby + 17, _closeHovered ? Color.White : Color.Yellow, 1.2f);
    }

    // --------------------------------------------------------------------
    private void DrawBatCard(SpriteBatch sb, BatEntry e, int x, int y, bool selected, bool hovered)
    {
        DrawCardBase(sb, x, y, (int)CardW, (int)CardH, selected, hovered, e.PrimaryColor, e.AuraColor);
        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var w = (int)CardW;
        var iy = y + 10;

        DrawBatSprite(sb, e, x + w / 2, iy + 36, 68, pulse);
        iy += 78;
        iy = DrawTag(sb, e.Tag, e.TagColor, x, iy, w);
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), e.PrimaryColor * 0.6f);
        iy += 5;
        DrawTextCentered(sb, e.Name, x + w / 2, iy + 9, e.PrimaryColor * pulse, 1.05f);
        iy += 22;
        DrawTextCentered(sb, e.UnlockInfo, x + w / 2, iy, new Color(160, 200, 160), 0.9f);
        iy += 18;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 5;
        var yBotBat = y + (int)CardH - 36;
        iy = DrawBulletList(sb, e.Abilities, x, iy, w, e.PrimaryColor, Color.White, 0.88f, yBotBat);
        if (iy + 10 < yBotBat)
        {
            DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
            iy += 5;
            DrawWrappedText(sb, e.Tip, x + 10, iy, w - 20, new Color(255, 235, 120), 0.86f, yBotBat);
        }
    }

    private void DrawUpgradeCard(SpriteBatch sb, MinerUpgradeEntry e, int x, int y, bool selected, bool hovered)
    {
        if (e.IsMythic)
        {
            DrawMythicCard(sb, e, x, y, selected, hovered);
            return;
        }

        DrawCardBase(sb, x, y, (int)CardW, (int)CardH, selected, hovered, e.Color, Color.Transparent);
        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var w = (int)CardW;
        var iy = y + 10;

        // Icona
        var iconSz = 60;
        var iconX = x + w / 2 - iconSz / 2;
        DrawRect(sb, new Rectangle(iconX - 2, iy - 2, iconSz + 4, iconSz + 4), e.Color * (0.35f + 0.15f * (float)Math.Sin(_pulse)));
        DrawRect(sb, new Rectangle(iconX, iy, iconSz, iconSz), new Color(20, 14, 40));
        DrawTextCentered(sb, GetUpgradeIcon(e.Category), x + w / 2, iy + iconSz / 2, e.Color * pulse, 2.0f);
        iy += iconSz + 6;

        iy = DrawTag(sb, e.Category, e.Color, x, iy, w);
        DrawTextCentered(sb, $"[{e.EffectType}]", x + w / 2, iy, new Color(140, 140, 180), 0.85f);
        iy += 16;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), e.Color * 0.6f);
        iy += 5;
        DrawTextCentered(sb, e.Name, x + w / 2, iy + 9, e.Color * pulse, 1.05f);
        iy += 22;
        var maxStr = e.MaxLevel == int.MaxValue ? "illimitato" : $"{e.MaxLevel}";
        DrawTextCentered(sb, $"Max livello: {maxStr}", x + w / 2, iy, new Color(160, 200, 160), 0.87f);
        iy += 18;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 5;
        var yBotUpg = y + (int)CardH - 10;
        iy = DrawWrappedText(sb, e.Description, x + 10, iy, w - 20, Color.White, 0.88f, yBotUpg);
        iy += 5;
        DrawRect(sb, new Rectangle(x + 10, Math.Min(iy, yBotUpg - 1), w - 20, 1), new Color(50, 50, 80));
        iy += 5;
        DrawTextCentered(sb, "EFFETTI", x + w / 2, iy + 6, e.Color, 0.90f);
        iy += 18;
        DrawBulletList(sb, e.Effects, x, iy, w, e.Color, new Color(220, 220, 180), 0.86f, yBotUpg);
    }

    private void DrawMythicCard(SpriteBatch sb, MinerUpgradeEntry e, int x, int y, bool selected, bool hovered)
    {
        // Aura dorata/rossa pulsante per le card Mythic
        var aura = e.Color.R > 200 && e.Color.G > 140
            ? new Color(220, 180, 30, 90)
            : new Color(255, 80, 80, 90);
        DrawCardBase(sb, x, y, (int)CardW, (int)CardH, selected, hovered, e.Color, aura);

        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var shimmer = 0.6f + 0.4f * (float)Math.Sin(_pulse * 1.3f);
        var w = (int)CardW;
        var iy = y + 10;

        // Icona grande con doppio bordo dorato/viola
        var iconSz = 64;
        var iconX = x + w / 2 - iconSz / 2;
        DrawRect(sb, new Rectangle(iconX - 4, iy - 4, iconSz + 8, iconSz + 8), new Color(180, 80, 255) * shimmer);
        DrawRect(sb, new Rectangle(iconX - 2, iy - 2, iconSz + 4, iconSz + 4), new Color(220, 180, 30) * shimmer);
        DrawRect(sb, new Rectangle(iconX, iy, iconSz, iconSz), new Color(10, 6, 24));
        DrawTextCentered(sb, "*", x + w / 2, iy + iconSz / 2, e.Color * pulse, 2.5f);
        iy += iconSz + 8;

        // Tag MYTHIC con colore speciale
        iy = DrawTag(sb, "MYTHIC", e.Color, x, iy, w);
        DrawTextCentered(sb, $"[{e.EffectType}]  |  0.5% per slot", x + w / 2, iy, new Color(220, 180, 30) * shimmer, 0.83f);
        iy += 16;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), e.Color * 0.8f);
        iy += 5;
        DrawTextCentered(sb, e.Name, x + w / 2, iy + 9, e.Color * pulse, 1.05f);
        iy += 22;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(80, 50, 80));
        iy += 5;
        var yBotMyt = y + (int)CardH - 10;
        iy = DrawWrappedText(sb, e.Description, x + 10, iy, w - 20, Color.White, 0.88f, yBotMyt);
        iy += 5;
        DrawRect(sb, new Rectangle(x + 10, Math.Min(iy, yBotMyt - 1), w - 20, 1), new Color(80, 50, 80));
        iy += 5;
        DrawTextCentered(sb, "EFFETTI E LIMITI", x + w / 2, iy + 6, e.Color, 0.90f);
        iy += 18;
        DrawBulletList(sb, e.Effects, x, iy, w, e.Color, new Color(220, 220, 180), 0.86f, yBotMyt);
    }

    private void DrawBiomeCard(SpriteBatch sb, BiomeEntry e, int x, int y, bool selected, bool hovered)
    {
        DrawCardBase(sb, x, y, (int)CardW, (int)CardH, selected, hovered, e.PrimaryColor, e.AuraColor);
        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var w = (int)CardW;
        var iy = y + 10;

        var iconSz = 64;
        var iconX = x + w / 2 - iconSz / 2;
        DrawRect(sb, new Rectangle(iconX - 2, iy - 2, iconSz + 4, iconSz + 4), e.PrimaryColor * (0.4f + 0.15f * (float)Math.Sin(_pulse)));
        DrawRect(sb, new Rectangle(iconX, iy, iconSz, iconSz), new Color(15, 12, 30));
        DrawTextCentered(sb, GetBiomeIcon(e.Name), x + w / 2, iy + iconSz / 2, e.PrimaryColor * pulse, 2.5f);
        iy += iconSz + 8;

        iy = DrawTag(sb, e.LevelRange, e.PrimaryColor, x, iy, w);
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), e.PrimaryColor * 0.6f);
        iy += 5;
        DrawTextCentered(sb, e.Name, x + w / 2, iy + 9, e.PrimaryColor * pulse, 1.1f);
        iy += 24;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 5;
        var yBotBio = y + (int)CardH - 36;
        iy = DrawWrappedText(sb, e.Description, x + 10, iy, w - 20, Color.White, 0.88f, yBotBio);
        iy += 5;
        DrawRect(sb, new Rectangle(x + 10, Math.Min(iy, yBotBio - 1), w - 20, 1), new Color(50, 50, 80));
        iy += 5;
        iy = DrawBulletList(sb, e.Features, x, iy, w, e.PrimaryColor, Color.White, 0.88f, yBotBio);
        if (iy + 10 < yBotBio)
        {
            DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
            iy += 5;
            DrawWrappedText(sb, e.Tip, x + 10, iy, w - 20, new Color(255, 235, 120), 0.86f, yBotBio);
        }
    }

    // --------------------------------------------------------------------
    // Shared helpers
    // --------------------------------------------------------------------

    private void DrawCardBase(SpriteBatch sb, int x, int y, int w, int h,
        bool selected, bool hovered, Color primary, Color aura)
    {
        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var borderColor = selected ? Color.Yellow
            : hovered ? new Color(160, 140, 220)
            : new Color(70, 60, 100);
        var bgColor = selected ? new Color(30, 20, 70, 245)
            : hovered ? new Color(22, 16, 52, 235)
            : new Color(15, 12, 35, 220);

        if (selected && aura != Color.Transparent)
        {
            const int glow = 8;
            DrawRect(sb, new Rectangle(x - glow, y - glow, w + glow * 2, h + glow * 2),
                aura * (0.3f + 0.1f * (float)Math.Sin(_pulse)));
        }

        DrawRect(sb, new Rectangle(x - 2, y - 2, w + 4, h + 4), borderColor * pulse);
        DrawRect(sb, new Rectangle(x, y, w, h), bgColor);
    }

    private int DrawTag(SpriteBatch sb, string tag, Color tagColor, int x, int iy, int w)
    {
        var tagSz = _font.MeasureString(tag) * 0.95f;
        var tagW = (int)tagSz.X + 12;
        var tagX = x + w / 2 - tagW / 2;
        DrawRect(sb, new Rectangle(tagX - 1, iy - 1, tagW + 2, (int)tagSz.Y + 4), tagColor * 0.6f);
        DrawRect(sb, new Rectangle(tagX, iy, tagW, (int)tagSz.Y + 2), tagColor * 0.25f);
        sb.DrawString(_font, tag, new Vector2(tagX + 7, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
        sb.DrawString(_font, tag, new Vector2(tagX + 6, iy), tagColor, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
        return iy + (int)tagSz.Y + 10;
    }

    private int DrawBulletList(SpriteBatch sb, string[] items, int x, int iy, int w,
        Color bulletColor, Color textColor, float scale, int yMax = int.MaxValue)
    {
        foreach (var ab in items)
        {
            if (iy >= yMax) break;
            var lines = WrapText(ab, w - 30, scale);
            var first = true;
            foreach (var line in lines)
            {
                if (iy >= yMax) break;
                var sz = _font.MeasureString(line) * scale;
                if (first)
                {
                    DrawRect(sb, new Rectangle(x + 10, iy + (int)(sz.Y / 2), 5, 5), bulletColor * 0.8f);
                    first = false;
                }
                sb.DrawString(_font, line, new Vector2(x + 21, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(_font, line, new Vector2(x + 20, iy), textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                iy += (int)sz.Y + 2;
            }
            iy += 1;
        }
        return iy;
    }

    private int DrawWrappedText(SpriteBatch sb, string text, int x, int iy, int maxW, Color color, float scale, int yMax = int.MaxValue)
    {
        foreach (var line in WrapText(text, maxW, scale))
        {
            if (iy >= yMax) break;
            sb.DrawString(_font, line, new Vector2(x + 1, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, line, new Vector2(x, iy), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            iy += (int)(_font.MeasureString(line).Y * scale) + 2;
        }
        return iy;
    }

    private string[] WrapText(string text, int maxPx, float scale)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = "";
        foreach (var word in words)
        {
            var test = current.Length == 0 ? word : current + " " + word;
            if (_font.MeasureString(test).X * scale > maxPx && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines.ToArray();
    }

    private void DrawBatSprite(SpriteBatch sb, BatEntry entry, int cx, int cy, int size, float pulse)
    {
        if (entry.AuraColor != Color.Transparent)
        {
            var gs = size + 14;
            DrawRect(sb, new Rectangle(cx - gs / 2, cy - gs / 2, gs, gs),
                entry.AuraColor * (0.35f + 0.15f * (float)Math.Sin(_pulse)));
        }

        if (_batTexture != null)
        {
            string[] candidates = { "fly_front", "fly", "idle", "walk" };
            List<Rectangle>? frames = null;
            foreach (var c in candidates)
                if (_batAnimations.TryGetValue(c, out frames)) break;

            if (frames != null && frames.Count > 0)
            {
                var frame = _animFrame % frames.Count;
                var src = frames[frame];
                var scale = (float)size / Math.Max(src.Width, src.Height);
                var origin = new Vector2(src.Width * 0.5f, src.Height * 0.5f);
                sb.Draw(_batTexture, new Vector2(cx + 2, cy + 2), src, Color.Black * 0.4f, 0f, origin, scale, SpriteEffects.None, 0f);
                sb.Draw(_batTexture, new Vector2(cx, cy), src, entry.PrimaryColor * pulse, 0f, origin, scale, SpriteEffects.None, 0f);
                return;
            }
        }

        var r = size / 2;
        DrawRect(sb, new Rectangle(cx - r, cy - r, size, size), entry.PrimaryColor * 0.8f);
        DrawRect(sb, new Rectangle(cx - r + 3, cy - r + 3, size - 6, size - 6), entry.PrimaryColor);
    }

    private static string GetUpgradeIcon(string category) => category switch
    {
        "VITA" => "+",
        "OFFENSIVO" => "!",
        "MOVIMENTO" => ">",
        "DIFENSIVO" => "O",
        "MYTHIC" => "*",
        _ => "*"
    };

    private static string GetBiomeIcon(string name) => name switch
    {
        "PRATERIA" => "~",
        "GHIACCIO" => "*",
        "LAVA" => "^",
        "PALUDE" => "%",
        _ => "#"
    };

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
        => sb.Draw(_pixel, r, c);

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        var pos = new Vector2(cx, cy);
        var d = Math.Max(1f, scale * 1.5f);
        var outline = Color.Black * 0.92f;
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // -- Record types ----------------------------
    private readonly record struct BatEntry(
        string Name, string UnlockInfo, Color PrimaryColor, Color AuraColor,
        string[] Abilities, string Tip, string Tag, Color TagColor);

    // UpgradeEntry is defined as MinerUpgradeEntry in MinerUpgradeDescriptions.cs

    private readonly record struct BiomeEntry(
        string Name, string LevelRange, Color PrimaryColor, Color AuraColor,
        string Description, string[] Features, string Tip, Color TagColor);
}
