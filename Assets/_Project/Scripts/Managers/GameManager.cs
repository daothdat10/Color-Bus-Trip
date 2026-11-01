using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    private const string CurrentLevelKey = "CurrentLevel";

    public int currentLevel = 1;

    public int CurrentLevel => PlayerPrefs.GetInt(CurrentLevelKey, currentLevel);

    protected override void Awake()
    {
        base.Awake();
        LoadCurrentLevel();
    }

    public void LoadCurrentLevel()
    {
        currentLevel = Mathf.Max(1, PlayerPrefs.GetInt(CurrentLevelKey, currentLevel));
    }

    

    public void SetCurrentLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevel);
        PlayerPrefs.Save();
    }

    public void NextLevel()
    {
        SetCurrentLevel(currentLevel + 1);
        Debug.Log($"Next Level: {currentLevel}");
    }
}
