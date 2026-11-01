using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[CreateAssetMenu(fileName ="Color",menuName ="Data/Colors")]
public class ColorData : ScriptableObject
{
    public Material[] matColor;
    public ColorWait ColorWait;
}

[System.Serializable] public class ColorWait
{
    public Material[] matColorWait;
}
