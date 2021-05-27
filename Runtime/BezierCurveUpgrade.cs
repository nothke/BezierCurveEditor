using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BezierCurveUpgrade
{
    public static void Upgrade(BezierCurve curve)
    {
        // resolution logic change - adapt old serialized resolution value to new behavior
        if (curve.version < 2)
        {
            if (curve.pointCount >= 2)
            {
                Debug.Log(string.Format("[BezierCurve upgrade] adapting curve resolution value for '{0}'", curve.name), curve);

                // will use maximum resolution that matches old curve interpolation density
                float shortestSegmentLength = float.MaxValue;
                for (int i = 0; i < curve.pointCount - 1; i++)
                {
                    float length = BezierCurve.ApproximateLength(curve[i], curve[i + 1], numPoints: 5);
                    if (length < shortestSegmentLength) shortestSegmentLength = length;
                }
                float newRes = curve.resolution / shortestSegmentLength;
                Debug.Log(string.Format("Old resolution: {0}, new resolution: {1}", curve.resolution, newRes), curve);

                curve.resolution = newRes;
            }

            curve.version = 2;
        }

        if (curve.version < 3)
        {
            Debug.Log("Upgrading to version 3");

            if (curve.legacyPoints != null && curve.legacyPoints.Length > 0)
            {
                if (curve.pointCount > 0)
                    for (int i = curve.pointCount - 1; i >= 0; i--)
                    {
                        curve.RemovePoint(i);
                    }

                for (int i = 0; i < curve.legacyPoints.Length; i++)
                {
                    BezierPoint bp = curve.legacyPoints[i];

#if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(curve, "Upgrading to version 3");
#endif

                    CurvePoint cp = new CurvePoint(curve);
                    cp.position = bp.transform.position;
                    cp.handle1 = bp.handle1;
                    cp.handle2 = bp.handle2;
                }

                for (int i = curve.legacyPoints.Length - 1; i >= 0; i--)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(curve.legacyPoints[i].gameObject);
                    //Object.DestroyImmediate(curve.gameObject);
                }

                int ct = curve.legacyPoints.Length;
                curve.legacyPoints = null;

                Debug.Log($"Upgraded curve \"{curve.name}\" to v3 and converted {ct} points to GameObjectless points", curve);
            }
            else
                Debug.LogWarning($"Upgraded \"{curve.name}\", but no BezierPoints found", curve);

            curve.version = 3;
        }
    }
}
