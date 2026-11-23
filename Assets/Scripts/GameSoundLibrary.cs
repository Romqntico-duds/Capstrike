using UnityEngine;

public enum GameSound : byte
{
    Hover, Click, Back, Confirm, LobbyJoin, LobbyLeave, Ready, MapSelect,
    BallPickup, BallDribble, BallPass, BallShoot, WallBounce, BallIntercept,
    TackleAttempt, TackleSuccess, TackleCooldown, CountdownTick, CountdownGo,
    GoalScored, GoalReset, TimerWarning, MatchEnd, Victory, Defeat, Draw
}

[CreateAssetMenu(menuName = "CapStrike/Sound Library")]
public class GameSoundLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Effect
    {
        public GameSound cue;
        public AudioClip clip;
    }

    public AudioClip menuMusic;
    public AudioClip matchMusic;
    [Range(0f, 1f)] public float musicVolume = 0.3f;
    [Range(0f, 1f)] public float effectsVolume = 0.85f;
    public Effect[] effects;

    public AudioClip GetClip(GameSound cue)
    {
        if (effects != null)
            foreach (Effect effect in effects)
                if (effect.cue == cue) return effect.clip;
        return null;
    }
}
