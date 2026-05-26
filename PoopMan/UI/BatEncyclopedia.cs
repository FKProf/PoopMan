using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.Input;

namespace PoopMan.UI;

/// <summary>
/// Enciclopedia dei pipistrelli speciali: mostra caratteristiche e abilità di ogni variante.
/// Si apre dal menu pausa e si chiude con ESC o con il pulsante Chiudi.
/// </summary>
public class BatEncyclopedia
{
    // ── Dati statici dei pipistrelli ──────────────────────────────────────
    private readonly record struct BatEntry(
        string Name,
        string UnlockInfo,
        Color PrimaryColor,
        Color AuraColor,
        string[] Abilities,
        string Tip,
        string Tag,
        Color TagColor
    );

    private static readonly BatEntry[] Entries = new[]
    {
        new BatEntry(
            Name: "PIPISTRELLO NORMALE",
            UnlockInfo: "Presente dal Livello 1",
            PrimaryColor: new Color(140, 90, 200),
            AuraColor: Color.Transparent,
            Abilities: new[]
            {
                "Insegue il Miner quando lo avvista",
                "Velocita e HP crescono col livello",
                "Nessuna abilita speciale",
            },
            Tip: "Usaci una bomba. Ai livelli alti diventa piu duro!",
            Tag: "BASE",
            TagColor: new Color(160, 120, 220)
        ),
        new BatEntry(
            Name: "ROBUSTO",
            UnlockInfo: "Appare dal Livello 20",
            PrimaryColor: new Color(255, 100, 100),
            AuraColor: Color.Transparent,
            Abilities: new[]
            {
                "Piu punti vita del normale",
                "2 HP al lv20, +1 ogni 5 lv (max 6)",
                "Stesso comportamento AI del normale",
            },
            Tip: "Servono piu bombe! Tienilo lontano finche non esplode.",
            Tag: "RESISTENTE",
            TagColor: new Color(255, 100, 100)
        ),
        new BatEntry(
            Name: "DASHER",
            UnlockInfo: "Sblocca al Livello 5",
            PrimaryColor: new Color(80, 200, 255),
            AuraColor: new Color(60, 180, 255, 80),
            Abilities: new[]
            {
                "Puo eseguire uno scatto improvviso",
                "Cooldown scatto: 3 secondi",
                "25% chance di scattare ad ogni passo",
            },
            Tip: "Allontanati dopo la bomba: lo scatto sorprende!",
            Tag: "VELOCE",
            TagColor: new Color(80, 200, 255)
        ),
        new BatEntry(
            Name: "WALID",
            UnlockInfo: "Sblocca al Livello 8",
            PrimaryColor: new Color(255, 140, 0),
            AuraColor: new Color(255, 100, 0, 90),
            Abilities: new[]
            {
                "Esplode alla morte con bomba piccola",
                "Esplosione area 7x7 tile (raggio 3)",
                "Knockback sulle unita adiacenti",
            },
            Tip: "Attenzione alla detonazione! Stai lontano quando muore.",
            Tag: "ESPLOSIVO",
            TagColor: new Color(255, 160, 0)
        ),
        new BatEntry(
            Name: "GHOST",
            UnlockInfo: "Sblocca al Livello 10",
            PrimaryColor: new Color(200, 200, 255),
            AuraColor: new Color(180, 180, 255, 70),
            Abilities: new[]
            {
                "Puo attraversare le bombe solide",
                "Fase fantasma per 2 secondi",
                "Cooldown 8 secondi tra le fasi",
            },
            Tip: "In fase le bombe non lo bloccano. Usa esplosioni dirette!",
            Tag: "FANTASMA",
            TagColor: new Color(180, 180, 255)
        ),
        new BatEntry(
            Name: "SPLITTER",
            UnlockInfo: "Sblocca al Livello 15",
            PrimaryColor: new Color(100, 220, 100),
            AuraColor: new Color(80, 200, 80, 80),
            Abilities: new[]
            {
                "Si divide in 2 mini-bat alla morte",
                "I mini-bat sono piu veloci ma fragili",
                "I mini-bat non si dividono piu",
            },
            Tip: "Elimina subito i mini-bat: sono veloci!",
            Tag: "DIVISORE",
            TagColor: new Color(100, 220, 100)
        ),
        new BatEntry(
            Name: "BERSERK",
            UnlockInfo: "Sblocca al Livello 16",
            PrimaryColor: new Color(200, 0, 255),
            AuraColor: new Color(160, 0, 255, 130),
            Abilities: new[]
            {
                "Furia se il Miner e a 3 tile",
                "Velocita x2.2 per 2 secondi",
                "Aggressione massima da vicino",
            },
            Tip: "Non avvicinarti! Da vicino e letale.",
            Tag: "BERSERK",
            TagColor: new Color(200, 80, 255)
        ),
        new BatEntry(
            Name: "NUKE",
            UnlockInfo: "Sblocca al Livello 20",
            PrimaryColor: new Color(255, 60, 60),
            AuraColor: new Color(255, 40, 0, 130),
            Abilities: new[]
            {
                "Esplode alla morte con bomba NUKE",
                "Area 13x13 tile (raggio 6)",
                "Fungo atomico + detriti radioattivi",
                "Instant kill e knockback estremo",
            },
            Tip: "Il piu pericoloso! Eliminalo a distanza massima.",
            Tag: "NUCLEARE",
            TagColor: new Color(255, 80, 60)
        ),
    };

    // ── Stato UI ──────────────────────────────────────────────────────────
    private int _selected = 0;
    private int _hoveredCard = -1;   // solo per highlight visivo; non muta _selected
    private int _hoveredArrow = 0;   // -1 = sinistra, 0 = nessuna, 1 = destra
    private bool _closeHovered = false;
    private float _pulse = 0f;
    private float _scrollOffset = 0f;
    private const float CardW = 320f;
    private const float CardH = 470f;
    private const float CardGap = 22f;
    private const float ScrollLerpSpeed = 0.18f;
    private const float ScrollSettledThreshold = 2f; // px: sotto questo lo scroll è "fermo"

    // ── Dipendenze ────────────────────────────────────────────────────────
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private Texture2D _batTexture;
    private Dictionary<string, List<Rectangle>> _batAnimations = new();
    private float _animTimer = 0f;
    private int _animFrame = 0;
    private const float AnimSpeed = 0.14f;

    public BatEncyclopedia(SpriteFont font, Texture2D pixel, ContentManager content)
    {
        _font = font;
        _pixel = pixel;
        LoadBatAnimations(content);
    }

    private void LoadBatAnimations(ContentManager content)
    {
        try
        {
            string xmlPath = Path.Combine(content.RootDirectory, "image", "enemies", "bat.xml");
            XDocument doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root == null) return;
            var textureEl = root.Element("Texture");
            if (textureEl == null) return;
            _batTexture = content.Load<Texture2D>(textureEl.Value);

            var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();
            foreach (var region in root.Descendants("Region").Where(r => r.Attribute("Name") != null))
            {
                string fullName = region.Attribute("Name")!.Value;
                if (!int.TryParse(region.Attribute("X")?.Value, out int rx)) continue;
                if (!int.TryParse(region.Attribute("Y")?.Value, out int ry)) continue;
                if (!int.TryParse(region.Attribute("Width")?.Value, out int rw)) continue;
                if (!int.TryParse(region.Attribute("Height")?.Value, out int rh)) continue;

                int fs = fullName.Length;
                while (fs > 0 && char.IsDigit(fullName[fs - 1])) fs--;
                if (fs >= fullName.Length || fs == 0) continue;
                string animName = fullName[..fs].TrimEnd('_', '-', ' ');
                if (!int.TryParse(fullName[fs..], out int fnum)) continue;

                if (!temp.ContainsKey(animName)) temp[animName] = new();
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
        _selected = 0;
        _hoveredCard = -1;
        _hoveredArrow = 0;
        _closeHovered = false;
        _pulse = 0f;
        _scrollOffset = 0f;
        _animTimer = 0f;
        _animFrame = 0;
    }

    /// <summary>Aggiorna input. Restituisce true se l'enciclopedia deve chiudersi.</summary>
    public bool Update(GameTime gameTime, KeyboardInfo kb, MouseInfo mouse, GraphicsDevice gd, bool escPressed)
    {
        if (escPressed) return true;

        _pulse += (float)gameTime.ElapsedGameTime.TotalSeconds * 3f;

        // Avanza frame animazione bat
        _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_animTimer >= AnimSpeed) { _animTimer = 0f; _animFrame++; }

        int vw = gd.Viewport.Width;
        int vh = gd.Viewport.Height;

        // ── Navigazione tastiera ──────────────────────────────────────────
        bool keyLeft = kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Left) ||
                       kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.A);
        bool keyRight = kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Right) ||
                        kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D);
        if (keyLeft)  _selected = Math.Max(0, _selected - 1);
        if (keyRight) _selected = Math.Min(Entries.Length - 1, _selected + 1);

        // ── Scroll fluido verso la card selezionata ───────────────────────
        float targetScroll = _selected * (CardW + CardGap) - vw / 2f + CardW / 2f;
        float delta = targetScroll - _scrollOffset;
        _scrollOffset += delta * ScrollLerpSpeed;
        // Snap finale per evitare drift infinitesimale
        if (Math.Abs(delta) < 0.5f) _scrollOffset = targetScroll;

        // Lo scroll è considerato "fermo" quando la differenza è piccola:
        // solo in questo stato il mouse può cambiare la selezione tramite le card.
        bool scrollSettled = Math.Abs(delta) < ScrollSettledThreshold;

        // ── Hit-test geometria (uguale a Draw per coerenza) ──────────────
        float totalW = Entries.Length * (CardW + CardGap) - CardGap;
        float startX = vw / 2f - totalW / 2f - _scrollOffset;
        int cardY = vh / 2 - (int)CardH / 2 + 14;

        Point mp = mouse.Position;

        // Reset hover state ogni frame prima di ricalcolare
        _hoveredCard = -1;
        _hoveredArrow = 0;
        _closeHovered = false;

        // Hover card (non muta _selected — solo feedback visivo)
        for (int i = 0; i < Entries.Length; i++)
        {
            float cx = startX + i * (CardW + CardGap);
            if (cx + CardW < 0 || cx > vw) continue;
            var r = new Rectangle((int)cx, cardY, (int)CardW, (int)CardH);
            if (r.Contains(mp))
            {
                _hoveredCard = i;
                break; // un solo hover per frame
            }
        }

        // Hover frecce
        var leftArrowRect  = new Rectangle(8, vh / 2 - 20, 40, 40);
        var rightArrowRect = new Rectangle(vw - 48, vh / 2 - 20, 40, 40);
        if (_selected > 0 && leftArrowRect.Contains(mp))  _hoveredArrow = -1;
        if (_selected < Entries.Length - 1 && rightArrowRect.Contains(mp)) _hoveredArrow = 1;

        // Hover pulsante chiudi
        var closeRect = new Rectangle(vw / 2 - 80, vh - 52, 160, 34);
        _closeHovered = closeRect.Contains(mp);

        // ── Click mouse ───────────────────────────────────────────────────
        if (mouse.WasButtonJustPressed(MouseButton.Left))
        {
            // Priorità 1: pulsante chiudi
            if (_closeHovered) return true;

            // Priorità 2: frecce navigazione
            if (_hoveredArrow == -1) { _selected = Math.Max(0, _selected - 1); }
            else if (_hoveredArrow == 1) { _selected = Math.Min(Entries.Length - 1, _selected + 1); }
            // Priorità 3: click su card — solo se lo scroll è fermo (evita click accidentali durante l'animazione)
            else if (scrollSettled && _hoveredCard >= 0)
            {
                _selected = _hoveredCard;
            }
        }

        // Scroll wheel per cambiare card rapidamente
        int scroll = mouse.ScrollWheelDelta;
        if (scroll < 0) _selected = Math.Min(Entries.Length - 1, _selected + 1);
        else if (scroll > 0) _selected = Math.Max(0, _selected - 1);

        if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter) ||
            kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
            return true;

        return false;
    }

    public void Draw(SpriteBatch sb)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;

        // Sfondo semitrasparente
        DrawRect(sb, new Rectangle(0, 0, vw, vh), new Color(5, 5, 20, 230));

        // Titolo
        DrawTextCentered(sb, "ENCICLOPEDIA DEI PIPISTRELLI", cx, 24, Color.Yellow, 1.6f);
        DrawRect(sb, new Rectangle(cx - 260, 46, 520, 2), new Color(120, 90, 30));

        // Sottotitolo navigazione
        DrawTextCentered(sb, $"{_selected + 1} / {Entries.Length}", cx, 58, Color.Gray, 1.0f);

        float totalW = Entries.Length * (CardW + CardGap) - CardGap;
        float startX = cx - totalW / 2f - _scrollOffset;
        int cardY = vh / 2 - (int)CardH / 2 + 14;

        for (int i = 0; i < Entries.Length; i++)
        {
            float cardX = startX + i * (CardW + CardGap);
            if (cardX + CardW < 0 || cardX > vw) continue;
            // Una card è "evidenziata" solo se è sia selezionata sia hoveredCard
            bool isSelected = i == _selected;
            bool isHovered  = i == _hoveredCard && !isSelected;
            DrawCard(sb, Entries[i], (int)cardX, cardY, isSelected, isHovered);
        }

        // Frecce navigazione (bordi schermo)
        if (_selected > 0)
        {
            Color arrowBg = _hoveredArrow == -1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            Color arrowFg = _hoveredArrow == -1 ? Color.White : new Color(255, 220, 80);
            DrawRect(sb, new Rectangle(8, vh / 2 - 20, 40, 40), arrowBg);
            DrawRect(sb, new Rectangle(9, vh / 2 - 19, 38, 38), new Color(80, 70, 120, 100));
            DrawTextCentered(sb, "<", 28, vh / 2, arrowFg, 2.0f);
        }
        if (_selected < Entries.Length - 1)
        {
            Color arrowBg = _hoveredArrow == 1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            Color arrowFg = _hoveredArrow == 1 ? Color.White : new Color(255, 220, 80);
            DrawRect(sb, new Rectangle(vw - 48, vh / 2 - 20, 40, 40), arrowBg);
            DrawRect(sb, new Rectangle(vw - 47, vh / 2 - 19, 38, 38), new Color(80, 70, 120, 100));
            DrawTextCentered(sb, ">", vw - 28, vh / 2, arrowFg, 2.0f);
        }

        // Hint tastiera
        DrawTextCentered(sb, "< > / A D : sfoglia    scroll: scorre    ESC: chiudi", cx, vh - 68, Color.DimGray, 0.95f);

        // Pulsante Chiudi
        int closeBtnX = cx - 80;
        int closeBtnY = vh - 52;
        Color closeBorder = _closeHovered ? Color.White : Color.Yellow;
        Color closeBg     = _closeHovered ? new Color(60, 40, 120) : new Color(30, 20, 70);
        DrawRect(sb, new Rectangle(closeBtnX - 1, closeBtnY - 1, 162, 36), closeBorder);
        DrawRect(sb, new Rectangle(closeBtnX, closeBtnY, 160, 34), closeBg);
        DrawTextCentered(sb, "CHIUDI", cx, closeBtnY + 17, closeBorder, 1.2f);
    }

    private void DrawCard(SpriteBatch sb, BatEntry entry, int x, int y, bool selected, bool hovered)
    {
        float pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        Color borderColor = selected ? Color.Yellow
                          : hovered  ? new Color(160, 140, 220)
                                     : new Color(70, 60, 100);
        Color bgColor = selected ? new Color(30, 20, 70, 245)
                      : hovered  ? new Color(22, 16, 52, 235)
                                 : new Color(15, 12, 35, 220);
        int w = (int)CardW;
        int h = (int)CardH;

        // Aura glow dietro la card selezionata
        if (selected && entry.AuraColor != Color.Transparent)
        {
            int glow = 8;
            DrawRect(sb, new Rectangle(x - glow, y - glow, w + glow * 2, h + glow * 2),
                entry.AuraColor * (0.3f + 0.1f * (float)Math.Sin(_pulse)));
        }

        // Sfondo e bordo
        DrawRect(sb, new Rectangle(x - 2, y - 2, w + 4, h + 4), borderColor * pulse);
        DrawRect(sb, new Rectangle(x, y, w, h), bgColor);

        int iy = y + 10;

        // Sprite bat reale (animato, tintato con il colore della variante)
        int spriteSize = 80;
        int spriteCX = x + w / 2;
        int spriteCY = iy + spriteSize / 2;
        DrawBatSprite(sb, entry, spriteCX, spriteCY, spriteSize, pulse);
        iy += spriteSize + 4;

        // Badge tag variante
        Vector2 tagSz = _font.MeasureString(entry.Tag) * 0.95f;
        int tagW = (int)tagSz.X + 12;
        int tagX = x + w / 2 - tagW / 2;
        DrawRect(sb, new Rectangle(tagX - 1, iy - 1, tagW + 2, (int)tagSz.Y + 4), entry.TagColor * 0.6f);
        DrawRect(sb, new Rectangle(tagX, iy, tagW, (int)tagSz.Y + 2), entry.TagColor * 0.25f);
        sb.DrawString(_font, entry.Tag, new Vector2(tagX + 6 + 1, iy + 1 + 1), Color.Black * 0.85f, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
        sb.DrawString(_font, entry.Tag, new Vector2(tagX + 6, iy + 1), entry.TagColor, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
        iy += (int)tagSz.Y + 10;

        // Separatore
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), entry.PrimaryColor * 0.6f);
        iy += 5;

        // Nome
        DrawTextCentered(sb, entry.Name, x + w / 2, iy + 10, entry.PrimaryColor * pulse, 1.15f);
        iy += 26;

        // Unlock info
        DrawTextCentered(sb, entry.UnlockInfo, x + w / 2, iy, new Color(160, 200, 160), 1.05f);
        iy += 22;

        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 6;

        // Abilità
        foreach (var ab in entry.Abilities)
        {
            float sc = 1.0f;
            string[] abLines = WrapText(ab, w - 30, sc);
            bool firstLine = true;
            foreach (var abLine in abLines)
            {
                Vector2 sz = _font.MeasureString(abLine) * sc;
                if (firstLine)
                {
                    // Pallino solo sulla prima riga
                    DrawRect(sb, new Rectangle(x + 10, iy + (int)(sz.Y / 2), 5, 5), entry.PrimaryColor * 0.8f);
                    firstLine = false;
                }
                int lineX = firstLine ? 20 : 20; // indent uniforme
                sb.DrawString(_font, abLine, new Vector2(x + 21, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                sb.DrawString(_font, abLine, new Vector2(x + 20, iy), Color.White, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                iy += (int)sz.Y + 2;
            }
            iy += 1; // piccolo spazio tra le abilità
        }

        iy += 4;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 5;

        // Tip
        string tip = entry.Tip;
        string[] tipLines = WrapText(tip, w - 20, 0.97f);
        foreach (var line in tipLines)
        {
            sb.DrawString(_font, line, new Vector2(x + 11, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, 0.97f, SpriteEffects.None, 0f);
            sb.DrawString(_font, line, new Vector2(x + 10, iy), new Color(255, 235, 120), 0f, Vector2.Zero, 0.97f, SpriteEffects.None, 0f);
            iy += (int)(_font.MeasureString(line).Y * 0.97f) + 2;
        }
    }

    private string[] WrapText(string text, int maxPx, float scale)
    {
        var lines = new List<string>();
        string[] words = text.Split(' ');
        string current = "";
        foreach (var word in words)
        {
            string test = current.Length == 0 ? word : current + " " + word;
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
        // Aura glow
        if (entry.AuraColor != Color.Transparent)
        {
            int glowSize = size + 14;
            DrawRect(sb, new Rectangle(cx - glowSize / 2, cy - glowSize / 2, glowSize, glowSize),
                entry.AuraColor * (0.35f + 0.15f * (float)Math.Sin(_pulse)));
        }

        if (_batTexture != null)
        {
            // Cerca il frame "fly_front" o fallback
            string[] candidates = { "fly_front", "fly", "idle", "walk" };
            List<Rectangle>? frames = null;
            foreach (var c in candidates)
                if (_batAnimations.TryGetValue(c, out frames)) break;

            if (frames != null && frames.Count > 0)
            {
                int frame = _animFrame % frames.Count;
                var src = frames[frame];
                float scale = (float)size / Math.Max(src.Width, src.Height);
                Vector2 origin = new Vector2(src.Width * 0.5f, src.Height * 0.5f);
                // Ombra
                sb.Draw(_batTexture, new Vector2(cx + 2, cy + 2), src, Color.Black * 0.4f,
                    0f, origin, scale, SpriteEffects.None, 0f);
                // Sprite colorato
                sb.Draw(_batTexture, new Vector2(cx, cy), src, entry.PrimaryColor * pulse,
                    0f, origin, scale, SpriteEffects.None, 0f);
                return;
            }
        }

        // Fallback: cerchio colorato se la texture non e disponibile
        int r = size / 2;
        DrawRect(sb, new Rectangle(cx - r, cy - r, size, size), entry.PrimaryColor * 0.8f);
        DrawRect(sb, new Rectangle(cx - r + 3, cy - r + 3, size - 6, size - 6), entry.PrimaryColor);
    }

    private void DrawFilledCircle(SpriteBatch sb, int cx, int cy, int r, Color color)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (dx * dx + dy * dy <= r * r)
                    DrawRect(sb, new Rectangle(cx + dx, cy + dy, 1, 1), color);
    }

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(_pixel, r, c);

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos = new Vector2(cx, cy);
        Color outline = Color.Black * 0.92f;
        float d = Math.Max(1f, scale * 1.5f);
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
