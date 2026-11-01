using UnityEngine;

public enum RoadTheme
{
    Town,
    Winter,
    Desert,
    Egypt,
    Forest,
    Graveyard,
    JapaneseGarden
}

/// <summary>
/// Keeps the original ground and road material paired for each environment.
/// The road materials already reference Texture_Gradient_04 in Texture2D.
/// </summary>
public sealed class RoadThemeMaterials : ScriptableObject
{
    [SerializeField] private Material groundTown;
    [SerializeField] private Material roadTown;
    [SerializeField] private Material groundWinter;
    [SerializeField] private Material roadWinter;
    [SerializeField] private Material groundDesert;
    [SerializeField] private Material roadDesert;
    [SerializeField] private Material groundEgypt;
    [SerializeField] private Material roadEgypt;
    [SerializeField] private Material groundForest;
    [SerializeField] private Material roadForest;
    [SerializeField] private Material groundGraveyard;
    [SerializeField] private Material roadGraveyard;
    [SerializeField] private Material groundJapaneseGarden;
    [SerializeField] private Material roadJapaneseGarden;

    public bool TryGet(RoadTheme theme, out Material ground, out Material road)
    {
        switch (theme)
        {
            case RoadTheme.Town: ground = groundTown; road = roadTown; break;
            case RoadTheme.Winter: ground = groundWinter; road = roadWinter; break;
            case RoadTheme.Desert: ground = groundDesert; road = roadDesert; break;
            case RoadTheme.Egypt: ground = groundEgypt; road = roadEgypt; break;
            case RoadTheme.Forest: ground = groundForest; road = roadForest; break;
            case RoadTheme.Graveyard: ground = groundGraveyard; road = roadGraveyard; break;
            case RoadTheme.JapaneseGarden:
                ground = groundJapaneseGarden;
                road = roadJapaneseGarden;
                break;
            default:
                ground = null;
                road = null;
                return false;
        }

        return ground != null && road != null && road.mainTexture != null;
    }
}
