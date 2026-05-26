using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary.Input;

namespace PoopMan.UI;

/// <summary>
///     Enciclopedia dei pipistrelli speciali: mostra caratteristiche e abilità di ogni variante.
///     Si apre dal menu pausa e si chiude con ESC o con il pulsante Chiudi.
/// </summary>
public class BatEncyclopedia
{
    private const float CardW = 320f;
    private const float CardH = 470f;
    private const float CardGap = 22f;
    private const float ScrollLerpSpeed = 0.18f;
    private const float ScrollSettledThreshold = 2f; // px: sotto questo lo scroll è "fermo"
    private const float AnimSpeed = 0.14f;

    private static readonly BatEntry[] Entries = new[]
    {
        new BatEntry(
            "PIPISTRELLO NORMALE",
            "Presente dal Livello 1",
            new Color(140, 90, 200),
            Color.Transparent,
            new[]
            {
                "Insegue il Miner quando lo avvista",
                "Velocita e HP crescono col livello",
                "Nessuna abilita speciale"
            },
            "Usaci una bomba. Ai livelli alti diventa piu duro!",
            "BASE",
            new Color(160, 120, 220)
        ),
        new BatEntry(
            "ROBUSTO",
            "Appare dal Livello 20",
            new Color(255, 100, 100),
            Color.Transparent,
            new[]
            {
                "Piu punti vita del normale",
                "2 HP al lv20, +1 ogni 5 lv (max 6)",
                "Stesso comportamento AI del normale"
            },
            "Servono piu bombe! Tienilo lontano finche non esplode.",
            "RESISTENTE",
            new Color(255, 100, 100)
        ),
        new BatEntry(
            "DASHER",
            "Sblocca al Livello 5",
            new Color(80, 200, 255),
            new Color(60, 180, 255, 80),
            new[]
            {
                "Puo eseguire uno scatto improvviso",
                "Cooldown scatto: 3 secondi",
                "25% chance di scattare ad ogni passo"
            },
            "Allontanati dopo la bomba: lo scatto sorprende!",
            "VELOCE",
            new Color(80, 200, 255)
        ),
        new BatEntry(
            "WALID",
            "Sblocca al Livello 8",
            new Color(255, 140, 0),
            new Color(255, 100, 0, 90),
            new[]
            {
                "Esplode alla morte con bomba piccola",
                "Esplosione area 7x7 tile (raggio 3)",
                "Knockback sulle unita adiacenti"
            },
            "Attenzione alla detonazione! Stai lontano quando muore.",
            "ESPLOSIVO",
            new Color(255, 160, 0)
        ),
        new BatEntry(
            "GHOST",
            "Sblocca al Livello 10",
            new Color(200, 200, 255),
            new Color(180, 180, 255, 70),
            new[]
            {
                "Puo attraversare le bombe solide",
                "Fase fantasma per 2 secondi",
                "Cooldown 8 secondi tra le fasi"
            },
            "In fase le bombe non lo bloccano. Usa esplosioni dirette!",
            "FANTASMA",
            new Color(180, 180, 255)
        ),
        new BatEntry(
            "SPLITTER",
            "Sblocca al Livello 15",
            new Color(100, 220, 100),
            new Color(80, 200, 80, 80),
            new[]
            {
                "Si divide in 2 mini-bat alla morte",
                "I mini-bat sono piu veloci ma fragili",
                "I mini-bat non si dividono piu"
            },
            "Elimina subito i mini-bat: sono veloci!",
            "DIVISORE",
            new Color(100, 220, 100)
        ),
        new BatEntry(
            "BERSERK",
            "Sblocca al Livello 16",
            new Color(200, 0, 255),
            new Color(160, 0, 255, 130),
            new[]
            {
                "Furia se il Miner e a 3 tile",
                "Velocita x2.2 per 2 secondi",
                "Aggressione massima da vicino"
            },
            "Non avvicinarti! Da vicino e letale.",
            "BERSERK",
            new Color(200, 80, 255)
        ),
        new BatEntry(
            "NUKE",
            "Sblocca al Livello 20",
            new Color(255, 60, 60),
            new Color(255, 40, 0, 130),
            new[]
            {
                "Esplode alla morte con bomba NUKE",
                "Area 13x13 tile (raggio 6)",
                "Fungo atomico + detriti radioattivi",
                "Instant kill e knockback estremo"
            },
            "Il piu pericoloso! Eliminalo a distanza massima.",
            "NUCLEARE",
            new Color(255, 80, 60)
        )
    };

    // ── Dipendenze ────────────────────────────────────────────────────────
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private int _animFrame;
    private float _animTimer;
    private Dictionary<string, List<Rectangle>> _batAnimations = new();
    private Texture2D _batTexture;
    private bool _closeHovered;
    private int _hoveredArrow; // -1 = sinistra, 0 = nessuna, 1 = destra
    private int _hoveredCard = -1; // solo per highlight visivo; non muta _selected
    private float _pulse;
    private float _scrollOffset;

    // ── Stato UI ──────────────────────────────────────────────────────────
    private int _selected;

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

                if (!temp.ContainsKey(animName)) temp[animName] = new List<(int frame, Rectangle rect)>();
                temp[animName].Add((fnum, new Rectangle(rx, ry, rw, rh)));
            }

            _batAnimations = temp.ToDictionary(
                p => p.Key,
                p => p.Value.OrderBy(f => f.frame).Select(f => f.rect).ToList());
        }
        catch
        {
        }
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
        if (_animTimer >= AnimSpeed)
        {
            _animTimer = 0f;
            _animFrame++;
        }

        var vw = gd.Viewport.Width;
        var vh = gd.Viewport.Height;

        // ── Navigazione tastiera ──────────────────────────────────────────
        var keyLeft = kb.WasKeyJustPressed(Keys.Left) ||
                      kb.WasKeyJustPressed(Keys.A);
        var keyRight = kb.WasKeyJustPressed(Keys.Right) ||
                       kb.WasKeyJustPressed(Keys.D);
        if (keyLeft) _selected = Math.Max(0, _selected - 1);
        if (keyRight) _selected = Math.Min(Entries.Length - 1, _selected + 1);

        // ── Scroll fluido verso la card selezionata ───────────────────────
        var targetScroll = _selected * (CardW + CardGap) - vw / 2f + CardW / 2f;
        var delta = targetScroll - _scrollOffset;
        _scrollOffset += delta * ScrollLerpSpeed;
        // Snap finale per evitare drift infinitesimale
        if (Math.Abs(delta) < 0.5f) _scrollOffset = targetScroll;

        // Lo scroll è considerato "fermo" quando la differenza è piccola:
        // solo in questo stato il mouse può cambiare la selezione tramite le card.
        var scrollSettled = Math.Abs(delta) < ScrollSettledThreshold;

        // ── Hit-test geometria (uguale a Draw per coerenza) ──────────────
        var totalW = Entries.Length * (CardW + CardGap) - CardGap;
        var startX = vw / 2f - totalW / 2f - _scrollOffset;
        var cardY = vh / 2 - (int)CardH / 2 + 14;

        var mp = mouse.Position;

        // Reset hover state ogni frame prima di ricalcolare
        _hoveredCard = -1;
        _hoveredArrow = 0;
        _closeHovered = false;

        // Hover card (non muta _selected — solo feedback visivo)
        for (var i = 0; i < Entries.Length; i++)
        {
            var cx = startX + i * (CardW + CardGap);
            if (cx + CardW < 0 || cx > vw) continue;
            var r = new Rectangle((int)cx, cardY, (int)CardW, (int)CardH);
            if (r.Contains(mp))
            {
                _hoveredCard = i;
                break; // un solo hover per frame
            }
        }

        // Hover frecce
        var leftArrowRect = new Rectangle(8, vh / 2 - 20, 40, 40);
        var rightArrowRect = new Rectangle(vw - 48, vh / 2 - 20, 40, 40);
        if (_selected > 0 && leftArrowRect.Contains(mp)) _hoveredArrow = -1;
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
            if (_hoveredArrow == -1)
                _selected = Math.Max(0, _selected - 1);
            else if (_hoveredArrow == 1)
                _selected = Math.Min(Entries.Length - 1, _selected + 1);
            // Priorità 3: click su card — solo se lo scroll è fermo (evita click accidentali durante l'animazione)
            else if (scrollSettled && _hoveredCard >= 0) _selected = _hoveredCard;
        }

        // Scroll wheel per cambiare card rapidamente
        var scroll = mouse.ScrollWheelDelta;
        if (scroll < 0) _selected = Math.Min(Entries.Length - 1, _selected + 1);
        else if (scroll > 0) _selected = Math.Max(0, _selected - 1);

        if (kb.WasKeyJustPressed(Keys.Enter) ||
            kb.WasKeyJustPressed(Keys.Escape))
            return true;

        return false;
    }

    public void Draw(SpriteBatch sb)
    {
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;
        var cx = vw / 2;

        // Sfondo semitrasparente
        DrawRect(sb, new Rectangle(0, 0, vw, vh), new Color(5, 5, 20, 230));

        // Titolo
        DrawTextCentered(sb, "ENCICLOPEDIA DEI PIPISTRELLI", cx, 24, Color.Yellow, 1.6f);
        DrawRect(sb, new Rectangle(cx - 260, 46, 520, 2), new Color(120, 90, 30));

        // Sottotitolo navigazione
        DrawTextCentered(sb, $"{_selected + 1} / {Entries.Length}", cx, 58, Color.Gray, 1.0f);

        var totalW = Entries.Length * (CardW + CardGap) - CardGap;
        var startX = cx - totalW / 2f - _scrollOffset;
        var cardY = vh / 2 - (int)CardH / 2 + 14;

        for (var i = 0; i < Entries.Length; i++)
        {
            var cardX = startX + i * (CardW + CardGap);
            if (cardX + CardW < 0 || cardX > vw) continue;
            // Una card è "evidenziata" solo se è sia selezionata sia hoveredCard
            var isSelected = i == _selected;
            var isHovered = i == _hoveredCard && !isSelected;
            DrawCard(sb, Entries[i], (int)cardX, cardY, isSelected, isHovered);
        }

        // Frecce navigazione (bordi schermo)
        if (_selected > 0)
        {
            var arrowBg = _hoveredArrow == -1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            var arrowFg = _hoveredArrow == -1 ? Color.White : new Color(255, 220, 80);
            DrawRect(sb, new Rectangle(8, vh / 2 - 20, 40, 40), arrowBg);
            DrawRect(sb, new Rectangle(9, vh / 2 - 19, 38, 38), new Color(80, 70, 120, 100));
            DrawTextCentered(sb, "<", 28, vh / 2, arrowFg, 2.0f);
        }

        if (_selected < Entries.Length - 1)
        {
            var arrowBg = _hoveredArrow == 1 ? new Color(80, 60, 160, 230) : new Color(30, 20, 70, 200);
            var arrowFg = _hoveredArrow == 1 ? Color.White : new Color(255, 220, 80);
            DrawRect(sb, new Rectangle(vw - 48, vh / 2 - 20, 40, 40), arrowBg);
            DrawRect(sb, new Rectangle(vw - 47, vh / 2 - 19, 38, 38), new Color(80, 70, 120, 100));
            DrawTextCentered(sb, ">", vw - 28, vh / 2, arrowFg, 2.0f);
        }

        // Hint tastiera
        DrawTextCentered(sb, "< > / A D : sfoglia    scroll: scorre    ESC: chiudi", cx, vh - 68, Color.DimGray, 0.95f);

        // Pulsante Chiudi
        var closeBtnX = cx - 80;
        var closeBtnY = vh - 52;
        var closeBorder = _closeHovered ? Color.White : Color.Yellow;
        var closeBg = _closeHovered ? new Color(60, 40, 120) : new Color(30, 20, 70);
        DrawRect(sb, new Rectangle(closeBtnX - 1, closeBtnY - 1, 162, 36), closeBorder);
        DrawRect(sb, new Rectangle(closeBtnX, closeBtnY, 160, 34), closeBg);
        DrawTextCentered(sb, "CHIUDI", cx, closeBtnY + 17, closeBorder, 1.2f);
    }

    private void DrawCard(SpriteBatch sb, BatEntry entry, int x, int y, bool selected, bool hovered)
    {
        var pulse = selected ? 0.85f + 0.15f * (float)Math.Sin(_pulse) : 1f;
        var borderColor = selected ? Color.Yellow
            : hovered ? new Color(160, 140, 220)
            : new Color(70, 60, 100);
        var bgColor = selected ? new Color(30, 20, 70, 245)
            : hovered ? new Color(22, 16, 52, 235)
            : new Color(15, 12, 35, 220);
        var w = (int)CardW;
        var h = (int)CardH;

        // Aura glow dietro la card selezionata
        if (selected && entry.AuraColor != Color.Transparent)
        {
            var glow = 8;
            DrawRect(sb, new Rectangle(x - glow, y - glow, w + glow * 2, h + glow * 2),
                entry.AuraColor * (0.3f + 0.1f * (float)Math.Sin(_pulse)));
        }

        // Sfondo e bordo
        DrawRect(sb, new Rectangle(x - 2, y - 2, w + 4, h + 4), borderColor * pulse);
        DrawRect(sb, new Rectangle(x, y, w, h), bgColor);

        var iy = y + 10;

        // Sprite bat reale (animato, tintato con il colore della variante)
        var spriteSize = 80;
        var spriteCX = x + w / 2;
        var spriteCY = iy + spriteSize / 2;
        DrawBatSprite(sb, entry, spriteCX, spriteCY, spriteSize, pulse);
        iy += spriteSize + 4;

        // Badge tag variante
        var tagSz = _font.MeasureString(entry.Tag) * 0.95f;
        var tagW = (int)tagSz.X + 12;
        var tagX = x + w / 2 - tagW / 2;
        DrawRect(sb, new Rectangle(tagX - 1, iy - 1, tagW + 2, (int)tagSz.Y + 4), entry.TagColor * 0.6f);
        DrawRect(sb, new Rectangle(tagX, iy, tagW, (int)tagSz.Y + 2), entry.TagColor * 0.25f);
        sb.DrawString(_font, entry.Tag, new Vector2(tagX + 6 + 1, iy + 1 + 1), Color.Black * 0.85f, 0f, Vector2.Zero,
            0.95f, SpriteEffects.None, 0f);
        sb.DrawString(_font, entry.Tag, new Vector2(tagX + 6, iy + 1), entry.TagColor, 0f, Vector2.Zero, 0.95f,
            SpriteEffects.None, 0f);
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
            var sc = 1.0f;
            var abLines = WrapText(ab, w - 30, sc);
            var firstLine = true;
            foreach (var abLine in abLines)
            {
                var sz = _font.MeasureString(abLine) * sc;
                if (firstLine)
                {
                    // Pallino solo sulla prima riga
                    DrawRect(sb, new Rectangle(x + 10, iy + (int)(sz.Y / 2), 5, 5), entry.PrimaryColor * 0.8f);
                    firstLine = false;
                }

                var lineX = firstLine ? 20 : 20; // indent uniforme
                sb.DrawString(_font, abLine, new Vector2(x + 21, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, sc,
                    SpriteEffects.None, 0f);
                sb.DrawString(_font, abLine, new Vector2(x + 20, iy), Color.White, 0f, Vector2.Zero, sc,
                    SpriteEffects.None, 0f);
                iy += (int)sz.Y + 2;
            }

            iy += 1; // piccolo spazio tra le abilità
        }

        iy += 4;
        DrawRect(sb, new Rectangle(x + 10, iy, w - 20, 1), new Color(50, 50, 80));
        iy += 5;

        // Tip
        var tip = entry.Tip;
        var tipLines = WrapText(tip, w - 20, 0.97f);
        foreach (var line in tipLines)
        {
            sb.DrawString(_font, line, new Vector2(x + 11, iy + 1), Color.Black * 0.85f, 0f, Vector2.Zero, 0.97f,
                SpriteEffects.None, 0f);
            sb.DrawString(_font, line, new Vector2(x + 10, iy), new Color(255, 235, 120), 0f, Vector2.Zero, 0.97f,
                SpriteEffects.None, 0f);
            iy += (int)(_font.MeasureString(line).Y * 0.97f) + 2;
        }
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
        // Aura glow
        if (entry.AuraColor != Color.Transparent)
        {
            var glowSize = size + 14;
            DrawRect(sb, new Rectangle(cx - glowSize / 2, cy - glowSize / 2, glowSize, glowSize),
                entry.AuraColor * (0.35f + 0.15f * (float)Math.Sin(_pulse)));
        }

        if (_batTexture != null)
        {
            // Cerca il frame "fly_front" o fallback
            string[] candidates = { "fly_front", "fly", "idle", "walk" };
            List<Rectangle>? frames = null;
            foreach (var c in candidates)
                if (_batAnimations.TryGetValue(c, out frames))
                    break;

            if (frames != null && frames.Count > 0)
            {
                var frame = _animFrame % frames.Count;
                var src = frames[frame];
                var scale = (float)size / Math.Max(src.Width, src.Height);
                var origin = new Vector2(src.Width * 0.5f, src.Height * 0.5f);
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
        var r = size / 2;
        DrawRect(sb, new Rectangle(cx - r, cy - r, size, size), entry.PrimaryColor * 0.8f);
        DrawRect(sb, new Rectangle(cx - r + 3, cy - r + 3, size - 6, size - 6), entry.PrimaryColor);
    }

    private void DrawFilledCircle(SpriteBatch sb, int cx, int cy, int r, Color color)
    {
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
            if (dx * dx + dy * dy <= r * r)
                DrawRect(sb, new Rectangle(cx + dx, cy + dy, 1, 1), color);
    }

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
    {
        sb.Draw(_pixel, r, c);
    }

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        var pos = new Vector2(cx, cy);
        var outline = Color.Black * 0.92f;
        var d = Math.Max(1f, scale * 1.5f);
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale,
            SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

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
}