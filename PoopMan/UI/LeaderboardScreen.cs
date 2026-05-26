using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.Scenes;
using PoopManLibrary;
using PoopManLibrary.Input;
using PoopManLibrary.Scenes;

namespace PoopMan.UI;

/// <summary>
///     Schermata classifica: mostra i migliori 20 punteggi, evidenzia l'ultima partita,
///     supporta scroll con tastiera/mouse e offre pulsanti Riavvia, Menu e Cancella.
/// </summary>
public sealed class LeaderboardScreen : Scene
{
    private const int VisibleRows = 10;
    private const int RowH = 38;
    private readonly List<Btn> _buttons = new();
    private readonly bool _fromGameOver;
    private readonly int _highlightIndex;

    // ── Dati ──────────────────────────────────────────────────────────────
    private IReadOnlyList<LeaderboardEntry> _entries;
    private SpriteFont _font;
    private int _hoveredBtn = -1;
    private Texture2D _pixel;

    // ── Risorse ───────────────────────────────────────────────────────────
    private SpriteBatch _sb;

    // ── Scroll ────────────────────────────────────────────────────────────
    private int _scrollOffset; // prima riga visibile

    // ── Costruttore ───────────────────────────────────────────────────────
    /// <param name="highlightIndex">Indice (nella lista ordinata) dell'ultima entry salvata; -1 se nessuna.</param>
    /// <param name="fromGameOver">Se true mostra anche il pulsante "Riavvia".</param>
    public LeaderboardScreen(int highlightIndex = -1, bool fromGameOver = false)
    {
        _highlightIndex = highlightIndex;
        _fromGameOver = fromGameOver;
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void LoadContent()
    {
        base.LoadContent();
        _sb = new SpriteBatch(Core.GraphicsDevice);
        _font = Content.Load<SpriteFont>("font/Score");
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _entries = LeaderboardManager.GetEntries();

        // Scroll iniziale: porta l'ultima entry nel centro del pannello
        if (_highlightIndex >= 0)
            _scrollOffset = Math.Max(0, _highlightIndex - VisibleRows / 2);
    }

    private void BuildButtons(int vw, int vh)
    {
        var btnW = 200;
        var btnH = 38;
        var gap = 16;
        var bottomY = vh - 56;
        var cx = vw / 2;

        _buttons.Clear();

        if (_fromGameOver)
        {
            // Con game over: MENU | RIAVVIA — centrati insieme
            var totalW = btnW * 2 + gap;
            var startX = cx - totalW / 2;
            _buttons.Add(new Btn(BtnId.Menu, "MENU",
                new Rectangle(startX, bottomY, btnW, btnH)));
            _buttons.Add(new Btn(BtnId.Restart, "RIAVVIA",
                new Rectangle(startX + btnW + gap, bottomY, btnW, btnH)));
        }
        else
        {
            // Dal menu: solo MENU centrato
            _buttons.Add(new Btn(BtnId.Menu, "MENU",
                new Rectangle(cx - btnW / 2, bottomY, btnW, btnH)));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        var kb = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;

        // Scroll tastiera
        if (kb.WasKeyJustPressed(Keys.Up) || kb.WasKeyJustPressed(Keys.W))
            _scrollOffset = Math.Max(0, _scrollOffset - 1);
        if (kb.WasKeyJustPressed(Keys.Down) || kb.WasKeyJustPressed(Keys.S))
            _scrollOffset = Math.Min(Math.Max(0, _entries.Count - VisibleRows), _scrollOffset + 1);

        // Scroll rotella mouse
        var wheel = mouse.ScrollWheelDelta;
        if (wheel > 0) _scrollOffset = Math.Max(0, _scrollOffset - 1);
        if (wheel < 0) _scrollOffset = Math.Min(Math.Max(0, _entries.Count - VisibleRows), _scrollOffset + 1);

        // Hover pulsanti
        var mp = mouse.Position;
        _hoveredBtn = -1;
        for (var i = 0; i < _buttons.Count; i++)
            if (_buttons[i].Rect.Contains(mp))
            {
                _hoveredBtn = i;
                if (mouse.WasButtonJustPressed(MouseButton.Left))
                    HandleButton(_buttons[i].Id);
            }

        // ESC → menu
        if (kb.WasKeyJustPressed(Keys.Escape))
            GoToMenu();
    }

    private void HandleButton(BtnId id)
    {
        switch (id)
        {
            case BtnId.Menu:
                GoToMenu();
                break;
            case BtnId.Restart:
                Core.ChangeScene(new GameScene());
                break;
            case BtnId.Clear:
                LeaderboardManager.Clear();
                _entries = LeaderboardManager.GetEntries();
                _scrollOffset = 0;
                break;
        }
    }

    private void GoToMenu()
    {
        AudioManager.StopGameAudio();
        AudioManager.StartTitleAudio();
        Core.ChangeScene(new TitleScene());
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        var vw = Core.GraphicsDevice.Viewport.Width;
        var vh = Core.GraphicsDevice.Viewport.Height;
        var cx = vw / 2;

        BuildButtons(vw, vh);

        Core.GraphicsDevice.Clear(new Color(12, 10, 28));

        _sb.Begin(samplerState: SamplerState.PointClamp);

        // ── Titolo ───────────────────────────────────────────────────────
        DrawTextCentered("* CLASSIFICA *", cx, 28, Color.Gold, 2.0f);
        DrawRect(new Rectangle(cx - 320, 68, 640, 2), new Color(100, 80, 200));

        // ── Intestazione colonne ─────────────────────────────────────────
        var tableX = cx - 320;
        var tableW = 640;
        var headerY = 78;
        DrawRect(new Rectangle(tableX, headerY, tableW, 26), new Color(30, 20, 60));
        DrawText("#", tableX + 8, headerY + 5, Color.LightGray, 1f);
        DrawText("NOME", tableX + 46, headerY + 5, Color.LightGray, 1f);
        DrawText("SCORE", tableX + tableW - 220, headerY + 5, Color.LightGray, 1f);
        DrawText("LV", tableX + tableW - 90, headerY + 5, Color.LightGray, 1f);
        DrawText("DATA", tableX + tableW - 40, headerY + 5, Color.LightGray * 0.6f, 0.7f);

        // ── Righe ────────────────────────────────────────────────────────
        var rowsY = headerY + 30;
        var maxVisible = Math.Min(VisibleRows, _entries.Count - _scrollOffset);

        for (var i = 0; i < maxVisible; i++)
        {
            var realIdx = i + _scrollOffset;
            var e = _entries[realIdx];
            var ry = rowsY + i * RowH;

            var isHighlight = realIdx == _highlightIndex;
            var isFirst = realIdx == 0;

            var rowBg = isHighlight
                ? new Color(60, 40, 100)
                : realIdx % 2 == 0
                    ? new Color(20, 14, 44)
                    : new Color(14, 10, 32);

            DrawRect(new Rectangle(tableX, ry, tableW, RowH - 2), rowBg);

            // Bordo laterale dorato per il primo posto
            if (isFirst)
                DrawRect(new Rectangle(tableX, ry, 3, RowH - 2), Color.Gold);
            else if (isHighlight)
                DrawRect(new Rectangle(tableX, ry, 3, RowH - 2), Color.Cyan);

            var textColor = isFirst ? Color.Gold : isHighlight ? Color.Cyan : Color.White;
            var scale = isFirst ? 1.1f : 1f;

            var rankStr = isFirst ? "1." : $"{realIdx + 1}.";
            DrawText(rankStr, tableX + 6, ry + 8, textColor, scale);
            DrawText(Truncate(e.Name, 16), tableX + 46, ry + 8, textColor, scale);
            DrawText($"{e.Score}", tableX + tableW - 220, ry + 8, textColor, scale);
            DrawText($"{e.Level}", tableX + tableW - 90, ry + 8, textColor, scale);
            DrawText(e.Date, tableX + tableW - 40, ry + 10, Color.Gray * 0.7f, 0.7f);
        }

        // Riga "vuota" se non ci sono dati
        if (_entries.Count == 0)
            DrawTextCentered("Nessun record salvato.", cx, rowsY + VisibleRows * RowH / 2, Color.Gray, 1f);

        // ── Scroll indicator ─────────────────────────────────────────────
        if (_entries.Count > VisibleRows)
        {
            var trackH = VisibleRows * RowH;
            var trackX = tableX + tableW + 8;
            DrawRect(new Rectangle(trackX, rowsY, 6, trackH), new Color(40, 30, 80));
            var thumbFrac = (float)VisibleRows / _entries.Count;
            var thumbPos = (float)_scrollOffset / (_entries.Count - VisibleRows);
            var thumbH = (int)(trackH * thumbFrac);
            var thumbY = rowsY + (int)((trackH - thumbH) * thumbPos);
            DrawRect(new Rectangle(trackX, thumbY, 6, thumbH), new Color(120, 80, 200));
        }

        // ── Pulsanti ─────────────────────────────────────────────────────
        for (var i = 0; i < _buttons.Count; i++)
        {
            var btn = _buttons[i];
            var hov = i == _hoveredBtn;
            var bgCol = btn.Id == BtnId.Clear
                ? hov ? new Color(220, 60, 60) : new Color(140, 30, 30)
                : hov
                    ? new Color(100, 80, 200)
                    : new Color(50, 35, 110);
            DrawRect(btn.Rect, bgCol);
            DrawRect(new Rectangle(btn.Rect.X, btn.Rect.Y, btn.Rect.Width, 2),
                hov ? Color.White : new Color(120, 80, 200));
            DrawTextCentered(btn.Label, btn.Rect.Center.X, btn.Rect.Y + 9, Color.White, 1f);
        }

        // ── Suggerimento scroll ───────────────────────────────────────────
        if (_entries.Count > VisibleRows)
            DrawTextCentered("W/S  /  rotella mouse per scorrere", cx, vh - 18, Color.Gray * 0.6f, 0.75f);

        _sb.End();
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private void DrawRect(Rectangle r, Color c)
    {
        _sb.Draw(_pixel, r, c);
    }

    private void DrawText(string text, int x, int y, Color color, float scale)
    {
        _sb.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawTextCentered(string text, int cx, int y, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        _sb.DrawString(_font, text, new Vector2(cx - size.X / 2f, y), color, 0f, Vector2.Zero, scale,
            SpriteEffects.None, 0f);
    }

    private static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s[..max] + "...";
    }

    // ── Pulsanti ──────────────────────────────────────────────────────────
    private enum BtnId
    {
        Menu,
        Restart,
        Clear
    }

    private record Btn(BtnId Id, string Label, Rectangle Rect);
}