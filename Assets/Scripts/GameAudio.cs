using UnityEngine;

public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }
    const string VolumeKey = "CapStrikeSoundVolume";
    GameSoundLibrary library;
    AudioSource effects;
    AudioSource music;
    float nextPreviewTime;
    GameSound? resultCue;
    float resultAt;
    float goalResetAt = float.PositiveInfinity;
    int previousPhase = MatchController.WaitingPhase;

    public float Volume => PlayerPrefs.GetFloat(VolumeKey, 0.75f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        effects = CreateSource(false);
        music = CreateSource(true);
    }

    AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        return source;
    }

    public void Configure(GameSoundLibrary soundLibrary)
    {
        library = soundLibrary;
        ApplySavedVolume();
        UpdateMusic(library != null ? library.menuMusic : null);
    }

    public void SetVolume(float value)
    {
        PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        ApplySavedVolume();
    }

    public void ApplySavedVolume() => AudioListener.volume = Volume;

    public void UpdateMatchAudio(int phase, int team, int blue, int orange)
    {
        if (library == null) return;
        bool inMatch = phase == MatchController.CountdownPhase || phase == MatchController.PlayingPhase;
        if (phase == MatchController.FinishedPhase && previousPhase != phase)
        {
            Play(GameSound.MatchEnd);
            resultCue = blue == orange ? GameSound.Draw :
                team == (blue > orange ? 1 : 2) ? GameSound.Victory : GameSound.Defeat;
            resultAt = Time.unscaledTime + (library.GetClip(GameSound.MatchEnd)?.length ?? 0f);
        }
        if (phase != MatchController.FinishedPhase) resultCue = null;
        if (!inMatch) goalResetAt = float.PositiveInfinity;
        if (Time.unscaledTime >= goalResetAt)
        {
            goalResetAt = float.PositiveInfinity;
            Play(GameSound.GoalReset);
        }
        if (resultCue.HasValue && Time.unscaledTime >= resultAt)
        {
            Play(resultCue.Value);
            resultCue = null;
        }
        UpdateMusic(inMatch ? library.matchMusic :
            phase == MatchController.FinishedPhase ? null : library.menuMusic);
        previousPhase = phase;
    }

    void UpdateMusic(AudioClip clip)
    {
        music.volume = library != null ? library.musicVolume : 0f;
        if (music.clip == clip) return;
        music.Stop();
        music.clip = clip;
        if (clip != null) music.Play();
    }

    public void Play(GameSound cue)
    {
        AudioClip clip = library != null ? library.GetClip(cue) : null;
        if (clip != null) effects.PlayOneShot(clip, library.effectsVolume);
    }

    public void QueueGoalReset() => goalResetAt = Time.unscaledTime + 0.65f;

    public void PlayVolumePreview()
    {
        if (Time.unscaledTime < nextPreviewTime || Volume <= 0f) return;
        nextPreviewTime = Time.unscaledTime + 0.25f;
        Play(GameSound.Confirm);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
