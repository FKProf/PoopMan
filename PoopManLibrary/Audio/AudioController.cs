using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;

namespace PoopManLibrary.Audio;

/// <summary>
/// Controller audio centrale. Gestisce BGM (Song) e SoundEffect.
/// Da inizializzare una volta sola e accessibile tramite istanza statica.
/// </summary>
public class AudioController : IDisposable
{
    // ── Singleton leggero ─────────────────────────────────────────────────
    public static AudioController Instance { get; private set; } = new AudioController();

    // ── BGM ───────────────────────────────────────────────────────────────
    private readonly List<Song> _bgmTracks = new();
    private int   _currentBgmIndex = -1;
    private float _bgmVolume       = 0.6f;

    // ── Suoni UI ──────────────────────────────────────────────────────────
    private SoundEffect?         _uiSound;
    private SoundEffectInstance? _uiSoundInst;

    // ── Suoni piazza-bomba (fart) ──────────────────────────────────────────
    private readonly List<SoundEffect> _placeBombSounds = new();
    private int _lastPlaceBombIndex = -1;   // evita ripetizioni consecutive
    private float _sfxVolume = 1.0f;

    // ── Suoni esplosione ────────────────────────────────────────────────
    private SoundEffect? _explosionSmall;
    private SoundEffect? _explosionBig;

    private static readonly Random _rand = new();
    private bool _disposed;

    private AudioController() { }

    // ─────────────────────────────────────────────────────────────────────
    // Caricamento
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Carica tutti i BGM (ogg) dalla cartella Audio/BMG.</summary>
    public void LoadBgm(ContentManager content, IEnumerable<string> assetPaths)
    {
        _bgmTracks.Clear();
        foreach (var path in assetPaths)
        {
            try { _bgmTracks.Add(content.Load<Song>(path)); }
            catch { /* file mancante: ignora */ }
        }
    }

    /// <summary>Carica il suono UI dalla cartella Audio/UISounds.</summary>
    public void LoadUiSound(ContentManager content, string assetPath)
    {
        try { _uiSound = content.Load<SoundEffect>(assetPath); }
        catch { }
    }

    /// <summary>Carica i suoni piazza-bomba (wav) dalla cartella Audio/PlaceBomb.</summary>
    public void LoadPlaceBombSounds(ContentManager content, IEnumerable<string> assetPaths)
    {
        _placeBombSounds.Clear();
        foreach (var path in assetPaths)
        {
            try { _placeBombSounds.Add(content.Load<SoundEffect>(path)); }
            catch { }
        }
    }

    /// <summary>Carica i suoni di esplosione (fxs = piccola, fxsBig = grande).</summary>
    public void LoadExplosionSounds(ContentManager content, string smallPath, string bigPath)
    {
        try { _explosionSmall = content.Load<SoundEffect>(smallPath); } catch { }
        try { _explosionBig   = content.Load<SoundEffect>(bigPath);   } catch { }
    }

    /// <summary>Riproduce il suono di esplosione corrispondente al tipo di bomba.</summary>
    public void PlayExplosion(bool bigBomb)
    {
        var sfx = bigBomb ? _explosionBig : _explosionSmall;
        if (sfx == null) return;
        var inst = sfx.CreateInstance();
        inst.Volume = _sfxVolume;
        inst.Play();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Proprietà volume
    // ─────────────────────────────────────────────────────────────────────

    public float BgmVolume
    {
        get => _bgmVolume;
        set { _bgmVolume = Math.Clamp(value, 0f, 1f); MediaPlayer.Volume = _bgmVolume; }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0f, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Riproduce un BGM specifico per indice (ciclico), fermando quello precedente.
    /// </summary>
    public void PlayBgm(int index)
    {
        if (_bgmTracks.Count == 0) return;
        index = index % _bgmTracks.Count;
        if (_currentBgmIndex == index && MediaPlayer.State == MediaState.Playing) return;

        _currentBgmIndex = index;
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume      = _bgmVolume;
        MediaPlayer.Play(_bgmTracks[_currentBgmIndex]);
    }

    /// <summary>
    /// Sceglie il BGM in base al tema della mappa.
    /// Forest→0, Cave→1, Stone→2, Desert→3, poi ricicla se ci sono più tracce.
    /// </summary>
    public void PlayBgmForTheme(int themeIndex)
    {
        if (_bgmTracks.Count == 0) return;
        PlayBgm(themeIndex % _bgmTracks.Count);
    }

    /// <summary>Riproduce un BGM a caso tra quelli disponibili (usato per TitleScene).</summary>
    public void PlayRandomBgm()
    {
        if (_bgmTracks.Count == 0) return;
        PlayBgm(_rand.Next(_bgmTracks.Count));
    }

    public void StopBgm() => MediaPlayer.Stop();
    public void PauseBgm() => MediaPlayer.Pause();
    public void ResumeBgm() => MediaPlayer.Resume();

    // ─────────────────────────────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Riproduce il suono UI (loop opzionale). Ferma quello precedente prima.</summary>
    public void PlayUiSound(bool loop = false)
    {
        if (_uiSound == null) return;
        _uiSoundInst?.Stop();
        _uiSoundInst = _uiSound.CreateInstance();
        _uiSoundInst.IsLooped = loop;
        _uiSoundInst.Volume   = _sfxVolume * 0.7f;
        _uiSoundInst.Play();
    }

    public void StopUiSound()
    {
        _uiSoundInst?.Stop();
        _uiSoundInst = null;
    }

    /// <summary>
    /// Riproduce un suono piazza-bomba casuale, evitando di ripetere
    /// lo stesso suono due volte di fila.
    /// </summary>
    public void PlayPlaceBomb()
    {
        if (_placeBombSounds.Count == 0) return;

        int idx;
        if (_placeBombSounds.Count == 1)
        {
            idx = 0;
        }
        else
        {
            do { idx = _rand.Next(_placeBombSounds.Count); }
            while (idx == _lastPlaceBombIndex);
        }

        _lastPlaceBombIndex = idx;
        var inst = _placeBombSounds[idx].CreateInstance();
        inst.Volume = _sfxVolume;
        inst.Play();
    }

    // ─────────────────────────────────────────────────────────────────────
    // IDisposable
    // ─────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        MediaPlayer.Stop();
        _uiSoundInst?.Stop();
        foreach (var s in _placeBombSounds) s.Dispose();
        _uiSound?.Dispose();
        _explosionSmall?.Dispose();
        _explosionBig?.Dispose();
        foreach (var t in _bgmTracks) t.Dispose();
    }
}
