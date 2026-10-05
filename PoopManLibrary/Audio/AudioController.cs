using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PoopManLibrary.Audio;

/// <summary>
///     Controller audio centrale. Gestisce BGM (Song) e SoundEffect.
///     Da inizializzare una volta sola e accessibile tramite istanza statica.
/// </summary>
public class AudioController : IDisposable
{
    // ── Cartella preferenze ───────────────────────────────────────────────
    private static readonly string _prefPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoopMan", "audio.cfg");

    private static readonly Random _rand = new();

    // ── BGM ───────────────────────────────────────────────────────────────
    private readonly List<Song> _bgmTracks = new();

    // ── Suoni piazza-bomba (fart) ──────────────────────────────────────────
    private readonly List<SoundEffect> _placeBombSounds = new();
    private float _bgmVolume = 0.6f;
    private int _currentBgmIndex = -1;
    private bool _disposed;
    private SoundEffect? _explosionBig;

    // ── Suoni esplosione ────────────────────────────────────────────────
    private SoundEffect? _explosionSmall;
    private bool _isMuted;
    private int _lastPlaceBombIndex = -1; // evita ripetizioni consecutive
    private float _sfxVolume = 1.0f;

    // ── Suoni UI ──────────────────────────────────────────────────────────
    private SoundEffect? _uiSound;
    private SoundEffectInstance? _uiSoundInst;
    private bool _uiLoopWanted; // la scena vuole la musica del titolo (se udibile)
    private SoundEffect? _clickBlip;
    private SoundEffect? _hoverBlip;

    private AudioController()
    {
    }

    // ── Singleton leggero ─────────────────────────────────────────────────
    public static AudioController Instance { get; private set; } = new();

    // ─────────────────────────────────────────────────────────────────────
    // Proprietà volume
    // ─────────────────────────────────────────────────────────────────────

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Math.Clamp(value, 0f, 1f);
            ApplyBgmVolume();
            ApplyUiLoopVolume();
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Math.Clamp(value, 0f, 1f);
            // La musica del titolo segue il volume musica, non quello SFX
            ApplyUiLoopVolume();
        }
    }

    /// <summary>
    ///     Mute totale: quando true silenzia MediaPlayer e tutte le SoundEffectInstance.
    ///     Usando IsMuted di MediaPlayer il silenzio è garantito anche su WindowsDX.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            // Rispetta anche il volume a 0: togliere il mute non deve riaccendere la musica
            ApplyBgmVolume();
            ApplyUiLoopVolume();
        }
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
        _clickBlip?.Dispose();
        _hoverBlip?.Dispose();
        _explosionSmall?.Dispose();
        _explosionBig?.Dispose();
        foreach (var t in _bgmTracks) t.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Caricamento
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Carica tutti i BGM (ogg) dalla cartella Audio/BMG.</summary>
    public void LoadBgm(ContentManager content, IEnumerable<string> assetPaths)
    {
        _bgmTracks.Clear();
        foreach (var path in assetPaths)
            try
            {
                _bgmTracks.Add(content.Load<Song>(path));
            }
            catch
            {
                /* file mancante: ignora */
            }
    }

    /// <summary>Carica il suono UI dalla cartella Audio/UISounds.</summary>
    public void LoadUiSound(ContentManager content, string assetPath)
    {
        try
        {
            _uiSound = content.Load<SoundEffect>(assetPath);
        }
        catch
        {
        }
    }

    /// <summary>Carica i suoni piazza-bomba (wav) dalla cartella Audio/PlaceBomb.</summary>
    public void LoadPlaceBombSounds(ContentManager content, IEnumerable<string> assetPaths)
    {
        _placeBombSounds.Clear();
        foreach (var path in assetPaths)
            try
            {
                _placeBombSounds.Add(content.Load<SoundEffect>(path));
            }
            catch
            {
            }
    }

    /// <summary>Carica i suoni di esplosione (fxs = piccola, fxsBig = grande).</summary>
    public void LoadExplosionSounds(ContentManager content, string smallPath, string bigPath)
    {
        try
        {
            _explosionSmall = content.Load<SoundEffect>(smallPath);
        }
        catch
        {
        }

        try
        {
            _explosionBig = content.Load<SoundEffect>(bigPath);
        }
        catch
        {
        }
    }

    /// <summary>Riproduce il suono di esplosione corrispondente al tipo di bomba.</summary>
    public void PlayExplosion(bool bigBomb)
    {
        var sfx = bigBomb ? _explosionBig : _explosionSmall;
        if (sfx == null) return;
        PlayOneShot(sfx, _isMuted ? 0f : _sfxVolume);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers interni
    // ─────────────────────────────────────────────────────────────────────
    private void ApplyBgmVolume()
    {
        // Il volume va sempre allineato (anche a 0), altrimenti togliendo il mute
        // MediaPlayer riparte con l'ultimo volume non nullo.
        MediaPlayer.Volume = _isMuted ? 0f : _bgmVolume;
        MediaPlayer.IsMuted = _isMuted || _bgmVolume <= 0f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Persistenza preferenze
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Salva bgmVolume, sfxVolume e isMuted in %APPDATA%\PoopMan\audio.cfg.</summary>
    public void SavePreferences()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_prefPath)!);
            File.WriteAllText(_prefPath,
                $"{_bgmVolume:F3}\n{_sfxVolume:F3}\n{(_isMuted ? 1 : 0)}");
        }
        catch
        {
            /* non critico */
        }
    }

    /// <summary>Carica le preferenze salvate; se il file non esiste usa i valori di default.</summary>
    public void LoadPreferences()
    {
        try
        {
            if (!File.Exists(_prefPath)) return;
            var lines = File.ReadAllLines(_prefPath);
            if (lines.Length >= 1 && float.TryParse(lines[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var bgm))
                _bgmVolume = Math.Clamp(bgm, 0f, 1f);
            if (lines.Length >= 2 && float.TryParse(lines[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var sfx))
                _sfxVolume = Math.Clamp(sfx, 0f, 1f);
            if (lines.Length >= 3 && int.TryParse(lines[2], out var muted))
                _isMuted = muted != 0;
            // Applica subito
            ApplyBgmVolume();
        }
        catch
        {
            /* file corrotto: usa default */
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Riproduce un BGM specifico per indice (ciclico), fermando quello precedente.
    /// </summary>
    public void PlayBgm(int index)
    {
        if (_bgmTracks.Count == 0) return;
        index = index % _bgmTracks.Count;
        if (_currentBgmIndex == index && MediaPlayer.State == MediaState.Playing) return;

        _currentBgmIndex = index;
        MediaPlayer.IsRepeating = true;
        MediaPlayer.Play(_bgmTracks[_currentBgmIndex]);
        ApplyBgmVolume(); // imposta Volume e IsMuted dopo Play (richiesto da MonoGame)
    }

    /// <summary>
    ///     Sceglie il BGM in base al tema della mappa.
    ///     Forest→0, Cave→1, Stone→2, Desert→3, poi ricicla se ci sono più tracce.
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

    public void StopBgm()
    {
        MediaPlayer.Stop();
    }

    public void PauseBgm()
    {
        MediaPlayer.Pause();
    }

    public void ResumeBgm()
    {
        MediaPlayer.Resume();
    }

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
        _uiLoopWanted = true;
        ApplyUiLoopVolume();
    }

    public void StopUiSound()
    {
        _uiLoopWanted = false;
        _uiSoundInst?.Stop();
        _uiSoundInst = null;
    }

    /// <summary>
    ///     Allinea la musica del titolo (loop UISound) a mute e volume musica.
    ///     Con volume effettivo 0 il loop viene proprio fermato: su WindowsDX un
    ///     SoundEffectInstance a Volume 0 impostato prima di Play() può suonare lo stesso.
    ///     Quando il volume torna sopra 0 il loop riparte.
    /// </summary>
    private void ApplyUiLoopVolume()
    {
        if (_uiSoundInst == null || !_uiLoopWanted) return;
        var volume = _isMuted ? 0f : _bgmVolume * 0.7f;
        if (volume <= 0f)
        {
            if (_uiSoundInst.State != SoundState.Stopped) _uiSoundInst.Stop();
            return;
        }

        _uiSoundInst.Volume = volume;
        if (_uiSoundInst.State != SoundState.Playing) _uiSoundInst.Play();
        _uiSoundInst.Volume = volume; // ripetuto dopo Play(): ora la voce esiste di sicuro
    }

    /// <summary>Riproduce uno SFX one-shot solo se udibile (volume impostato prima e dopo Play).</summary>
    private static void PlayOneShot(SoundEffect sfx, float volume, float pitch = 0f)
    {
        if (volume <= 0f) return;
        var inst = sfx.CreateInstance();
        inst.Pitch = pitch;
        inst.Volume = Math.Clamp(volume, 0f, 1f);
        inst.Play();
        inst.Volume = Math.Clamp(volume, 0f, 1f);
    }

    /// <summary>
    ///     Riproduce un breve click UI. Usa un blip generato via codice: UISound.wav è la
    ///     musica della schermata home (~1 minuto) e non va riprodotta nei menu di gioco.
    /// </summary>
    public void PlayClickSound()
    {
        _clickBlip ??= CreateBlip(880f, 660f, 0.07f);
        PlayOneShot(_clickBlip, _isMuted ? 0f : _sfxVolume * 0.45f);
    }

    /// <summary>Riproduce un suono hover UI (blip breve, più soft e basso del click).</summary>
    public void PlayHoverSound()
    {
        _hoverBlip ??= CreateBlip(520f, 520f, 0.04f);
        PlayOneShot(_hoverBlip, _isMuted ? 0f : _sfxVolume * 0.25f);
    }

    /// <summary>
    ///     Crea un breve tono sinusoidale (mono, 16 bit) con glissando da
    ///     <paramref name="startHz" /> a <paramref name="endHz" /> e dissolvenza finale.
    /// </summary>
    private static SoundEffect CreateBlip(float startHz, float endHz, float seconds)
    {
        const int sampleRate = 44100;
        var samples = (int)(sampleRate * seconds);
        var data = new byte[samples * 2];
        var phase = 0.0;
        for (var i = 0; i < samples; i++)
        {
            var t = i / (float)samples;
            var hz = startHz + (endHz - startHz) * t;
            phase += 2.0 * Math.PI * hz / sampleRate;
            var attack = Math.Min(1f, i / (sampleRate * 0.004f)); // evita il "pop" iniziale
            var env = attack * (1f - t) * (1f - t);
            var v = (short)(Math.Sin(phase) * env * short.MaxValue * 0.6f);
            data[i * 2] = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        return new SoundEffect(data, sampleRate, AudioChannels.Mono);
    }

    /// <summary>
    ///     Riproduce un suono piazza-bomba casuale, evitando di ripetere
    ///     lo stesso suono due volte di fila.
    /// </summary>
    public void PlayPlaceBomb()
    {
        if (_placeBombSounds.Count == 0) return;

        int idx;
        if (_placeBombSounds.Count == 1)
            idx = 0;
        else
            do
            {
                idx = _rand.Next(_placeBombSounds.Count);
            } while (idx == _lastPlaceBombIndex);

        _lastPlaceBombIndex = idx;
        PlayOneShot(_placeBombSounds[idx], _isMuted ? 0f : _sfxVolume);
    }
}