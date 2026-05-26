using Microsoft.Xna.Framework.Content;
using PoopManLibrary;
using PoopManLibrary.Audio;

namespace PoopMan.UI;

/// <summary>
///     Gestore audio di alto livello per le scene del gioco.
///     Coordina BGM (tramite AudioController/MediaPlayer) e SFX (UISound, fart).
///     Usa AudioController come backend; questa classe aggiunge la logica di scenario.
/// </summary>
public static class AudioManager
{
    // ── Percorsi asset ────────────────────────────────────────────────────
    private static readonly string[] BgmPaths =
    {
        "Audio/BMG/beanfeast",
        "Audio/BMG/dramatic_boi",
        "Audio/BMG/dramatic_boi_v3",
        "Audio/BMG/frozen_winter",
        "Audio/BMG/hope",
        "Audio/BMG/woo_scary"
    };

    private static readonly string[] FartPaths =
    {
        "Audio/PlaceBomb/Fart_1", "Audio/PlaceBomb/Fart_2", "Audio/PlaceBomb/Fart_3",
        "Audio/PlaceBomb/Fart_4", "Audio/PlaceBomb/Fart_5", "Audio/PlaceBomb/Fart_6",
        "Audio/PlaceBomb/Fart_7", "Audio/PlaceBomb/Fart_8", "Audio/PlaceBomb/Fart_9",
        "Audio/PlaceBomb/Fart_10"
    };

    private static bool _loaded;

    // ─────────────────────────────────────────────────────────────────────
    // Volume globale
    // ─────────────────────────────────────────────────────────────────────

    public static float BgmVolume
    {
        get => AudioController.Instance.BgmVolume;
        set => AudioController.Instance.BgmVolume = value;
    }

    public static float SfxVolume
    {
        get => AudioController.Instance.SfxVolume;
        set => AudioController.Instance.SfxVolume = value;
    }

    public static bool IsMuted
    {
        get => AudioController.Instance.IsMuted;
        set => AudioController.Instance.IsMuted = value;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Caricamento (chiamato una volta sola; tollera chiamate multiple)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Carica tutti i file audio usando il ContentManager globale (mai disposto).
    ///     Sicuro da chiamare più volte: salta se già caricato.
    ///     Il parametro <paramref name="content" /> è ignorato: si usa sempre Core.ContentManager
    ///     per evitare ObjectDisposedException al cambio di scena.
    /// </summary>
    public static void Load(ContentManager content)
    {
        if (_loaded) return;
        _loaded = true;

        // Usa il ContentManager globale di Core: non viene mai disposto al cambio scena.
        var globalContent = Core.ContentManager;
        var audio = AudioController.Instance;
        audio.LoadBgm(globalContent, BgmPaths);
        audio.LoadUiSound(globalContent, "Audio/UISounds/UISound");
        audio.LoadPlaceBombSounds(globalContent, FartPaths);
        audio.LoadExplosionSounds(globalContent, "Audio/FxsSound/fxs", "Audio/FxsSound/fxsBig");

        // Ripristina preferenze salvate (volume + mute)
        audio.LoadPreferences();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Schermata Titolo
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Avvia l'audio della schermata titolo: UISound.wav in loop.
    ///     NON avvia un BGM simultaneamente: su MonoGame 3.8 WindowsDX,
    ///     MediaPlayer (Song/XAudio2 SourceVoice) silenzia i SoundEffectInstance
    ///     già in riproduzione quando viene inizializzato. I BGM .ogg sono riservati al gameplay.
    /// </summary>
    public static void StartTitleAudio()
    {
        AudioController.Instance.StopBgm();
        AudioController.Instance.PlayUiSound(true);
    }

    /// <summary>Ferma tutta la musica della schermata titolo.</summary>
    public static void StopTitleAudio()
    {
        AudioController.Instance.StopUiSound();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gameplay
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Avvia l'audio di gioco: ferma UISound e sceglie il BGM
    ///     corrispondente al tema della mappa (Forest=0, Cave=1, Stone=2, Desert=3).
    /// </summary>
    public static void StartGameAudio(int themeIndex)
    {
        AudioController.Instance.StopUiSound();
        AudioController.Instance.PlayBgmForTheme(themeIndex);
    }

    /// <summary>Cambia il BGM quando si passa al livello successivo (nuovo tema).</summary>
    public static void OnLevelChanged(int newThemeIndex)
    {
        AudioController.Instance.PlayBgmForTheme(newThemeIndex);
    }

    /// <summary>Ferma il BGM di gioco (es. tornando al titolo dopo game over).</summary>
    public static void StopGameAudio()
    {
        AudioController.Instance.StopBgm();
    }

    // ─────────────────────────────────────────────────────────────────────
    // SFX gameplay
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Riproduce un suono fart casuale ogni volta che viene piazzata una bomba.
    ///     Non ripete mai lo stesso suono due volte di fila.
    /// </summary>
    public static void PlayBombPlaced()
    {
        AudioController.Instance.PlayPlaceBomb();
    }

    /// <summary>Riproduce il suono di esplosione (fxs = piccola, fxsBig = grande).</summary>
    public static void PlayExplosion(bool bigBomb)
    {
        AudioController.Instance.PlayExplosion(bigBomb);
    }

    /// <summary>Riproduce un click UI breve (conferma selezione nei menu).</summary>
    public static void PlayUIClick()
    {
        AudioController.Instance.PlayClickSound();
    }

    /// <summary>Riproduce un suono hover UI (cambio selezione nei menu).</summary>
    public static void PlayUIHover()
    {
        AudioController.Instance.PlayHoverSound();
    }

    /// <summary>Salva le preferenze audio correnti su disco.</summary>
    public static void SavePreferences()
    {
        AudioController.Instance.SavePreferences();
    }
}