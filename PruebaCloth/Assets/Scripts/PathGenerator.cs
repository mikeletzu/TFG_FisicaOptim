using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class PathGenerator : MonoBehaviour
{
    private float3 limitMin, limitMax;
    [SerializeField]
    public int pathPointMin;
    [SerializeField]
    public int pathPointMax;

    private void Start()
    {
        limitMin = transform.localPosition - transform.localScale / 2;
        limitMax = transform.localPosition + transform.localScale / 2;
    }
    public SplineContainer GeneratePath(float3 initPos)
    {
        int length = UnityEngine.Random.Range(pathPointMin, pathPointMax);
        Debug.Log("length: " + length);
        float3[] pathPoints = new float3[length];

        pathPoints[0] = initPos;
        for (int i = 1; i < length; i++)
        {
            pathPoints[i] = new float3(UnityEngine.Random.Range(limitMin.x, limitMax.x), UnityEngine.Random.Range(limitMin.y, limitMax.y), UnityEngine.Random.Range(limitMin.z, limitMax.z));

            Debug.Log(i + ": " + pathPoints[i]);
        }


        return CreatePath(pathPoints);
    }

    SplineContainer CreatePath(float3[] pathPoints)
    {
        GameObject BallPath = new GameObject("BallPath");

        var container = BallPath.AddComponent<SplineContainer>();
        var knots = new BezierKnot[pathPoints.Length];

        knots[0] = new BezierKnot(
                pathPoints[0],
                -1 * Vector3.right,
                1 * Vector3.right);

        for (int i = 1; i < pathPoints.Length; i++)
        {
            knots[i] = new BezierKnot(
                pathPoints[i],
                -1 * Vector3.right,
                1 * Vector3.right);
        }

        container.Spline.Knots = knots;

        return container;
    }
}