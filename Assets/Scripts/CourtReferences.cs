using UnityEngine;

public class CourtReferences : MonoBehaviour
{
    public SpriteRenderer courtArtwork;
    [SerializeField] Sprite narrowCourtArtwork;
    public string classicCourtName = "Normal";
    public string narrowCourtName = "Narrow";
    public Color classicCourtTint = Color.white;
    public Transform ballSpawnPoint;
    public Transform blueGoal;
    public Transform orangeGoal;
    [Min(0.01f)] public float blueGoalHalfHeight = 2.457554f;
    [Min(0.01f)] public float orangeGoalHalfHeight = 2.437536f;
    public Transform bluePlayer1SpawnPoint;
    public Transform bluePlayer2SpawnPoint;
    public Transform orangePlayer1SpawnPoint;
    public Transform orangePlayer2SpawnPoint;
    public float topSideline = 3.5f;
    public float bottomSideline = -3.5f;
    public float leftPlayerBoundary = -7.3f;
    public float rightPlayerBoundary = 7.3f;
    [Range(0f, 1f)] public float wallBounce = 0.85f;
    [SerializeField] Collider2D[] obstacles;
    int activeMapIndex = -1;
    Sprite normalCourtArtwork;
    Vector3 normalArtworkScale;
    bool UsesNarrowArtwork => activeMapIndex == 1 && narrowCourtArtwork != null;

    public static CourtReferences FindActive() => FindFirstObjectByType<CourtReferences>();

    public string GetMapName(int mapIndex) => mapIndex == 1 ? narrowCourtName : classicCourtName;

    public void ApplyMapStyle(int mapIndex)
    {
        if (activeMapIndex == mapIndex) return;
        activeMapIndex = mapIndex;

        if (courtArtwork != null)
        {
            if (normalCourtArtwork == null)
            {
                normalCourtArtwork = courtArtwork.sprite;
                normalArtworkScale = courtArtwork.transform.localScale;
            }

            courtArtwork.sprite = UsesNarrowArtwork ? narrowCourtArtwork : normalCourtArtwork;
            Vector3 artworkScale = normalArtworkScale;
            if (UsesNarrowArtwork && normalCourtArtwork != null)
            {
                Vector2 normalSize = normalCourtArtwork.bounds.size;
                Vector2 narrowSize = narrowCourtArtwork.bounds.size;
                artworkScale.x *= normalSize.x / narrowSize.x;
                artworkScale.y *= normalSize.y / narrowSize.y;
            }
            courtArtwork.transform.localScale = artworkScale;
            courtArtwork.color = classicCourtTint;
        }

        SetGoalArtworkVisible(blueGoal, !UsesNarrowArtwork);
        SetGoalArtworkVisible(orangeGoal, !UsesNarrowArtwork);
    }

    static void SetGoalArtworkVisible(Transform goal, bool visible)
    {
        if (goal != null && goal.TryGetComponent(out SpriteRenderer renderer))
            renderer.forceRenderingOff = !visible;
    }

    public void GetSidelines(float x, out float bottom, out float top, out float slope)
    {
        bottom = bottomSideline;
        top = topSideline;
        slope = 0f;
        if (!UsesNarrowArtwork || courtArtwork == null) return;

        // The Narrow court's center lines sit farther in, with a diagonal at each end.
        float distance = x - courtArtwork.bounds.center.x;
        float innerEdge = distance < 0f ? 2.07f : 2.57f;
        float outerEdge = distance < 0f ? 2.68f : 3.14f;
        const float inset = 0.80f;
        float taper = Mathf.InverseLerp(innerEdge, outerEdge, Mathf.Abs(distance));
        top -= inset * (1f - taper);
        bottom += inset * (1f - taper);
        if (taper > 0f && taper < 1f)
            slope = Mathf.Sign(distance) * inset / (outerEdge - innerEdge);
    }

    public Collider2D[] GetMapObstacles() => obstacles ?? System.Array.Empty<Collider2D>();

    public Vector3 GetBallSpawnPosition()
    {
        return ballSpawnPoint != null ? ballSpawnPoint.position : Vector3.zero;
    }

    public Vector3 GetPlayerSpawnPosition(int team, int slot)
    {
        Transform spawnPoint = GetPlayerSpawnPoint(team, slot);
        if (spawnPoint != null) return spawnPoint.position;

        return new Vector3(team == 1 ? -4f : 4f, slot == 0 ? -0.8f : 0.8f, 0f);
    }

    public Transform GetPlayerSpawnPoint(int team, int slot)
    {
        if (team == 1) return slot == 0 ? bluePlayer1SpawnPoint : bluePlayer2SpawnPoint;
        if (team == 2) return slot == 0 ? orangePlayer1SpawnPoint : orangePlayer2SpawnPoint;
        return null;
    }

    void OnValidate()
    {
        if (courtArtwork == null)
        {
            Transform artwork = transform.Find("Court Artwork");
            if (artwork != null) courtArtwork = artwork.GetComponent<SpriteRenderer>();
        }
        if (ballSpawnPoint == null) ballSpawnPoint = transform.Find("Ball Spawnpoint");
        if (blueGoal == null) blueGoal = transform.Find("Blue Goal");
        if (orangeGoal == null) orangeGoal = transform.Find("Orange Goal");
        if (bluePlayer1SpawnPoint == null) bluePlayer1SpawnPoint = transform.Find("1BlueSpawnPoint");
        if (bluePlayer2SpawnPoint == null) bluePlayer2SpawnPoint = transform.Find("2BlueSpawnPoint");
        if (orangePlayer1SpawnPoint == null) orangePlayer1SpawnPoint = transform.Find("1OrangeSpawnPoint");
        if (orangePlayer2SpawnPoint == null) orangePlayer2SpawnPoint = transform.Find("2OrangeSpawnPoint");
    }
}
