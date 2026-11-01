using UnityEngine;

public class ManagerSpline : MonoBehaviour
{
    [SerializeField] private SplineControll[] listSplines;
    private int indexSplines;
    public void SetUpSpline(int idx)
    {
        if (listSplines == null || listSplines.Length == 0)
        {
            indexSplines = -1;
            return;
        }

        indexSplines = Mathf.Clamp(idx, 0, listSplines.Length - 1);
        if (listSplines[indexSplines] != null)
        {
            listSplines[indexSplines].gameObject.SetActive(true);
        }
    }

    public SplineControll GetSpline()
    {
        if (listSplines == null || indexSplines < 0 || indexSplines >= listSplines.Length)
        {
            return null;
        }
        return listSplines[indexSplines];
    }

}
