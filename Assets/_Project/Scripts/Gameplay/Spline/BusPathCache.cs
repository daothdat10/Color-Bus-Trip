using System.Collections.Generic;
using UnityEngine.Splines;

/// <summary>Caches spline path samplers until the level's splines change.</summary>
internal sealed class BusPathCache
{
    private readonly Dictionary<int, IBusPath> samplers = new Dictionary<int, IBusPath>();

    public IBusPath Get(SplineContainer container, int splineIndex)
    {
        if (container == null || splineIndex < 0 ||
            splineIndex >= container.Splines.Count)
        {
            return null;
        }

        if (!samplers.TryGetValue(splineIndex, out IBusPath sampler))
        {
            sampler = new UnitySplinePath(container, splineIndex);
            samplers.Add(splineIndex, sampler);
        }

        return sampler;
    }

    public void Clear()
    {
        samplers.Clear();
    }
}
