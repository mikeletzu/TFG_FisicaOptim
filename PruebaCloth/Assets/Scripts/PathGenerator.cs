using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class PathGenerator : MonoBehaviour
{
    private float3 limitMin, limitMax;
    [SerializeField]
    private int pathPointMin;
    [SerializeField]
    private int pathPointMax;

    [SerializeField]
    private bool simpleZmovement = false;

    private void Awake()
    {
		var box = GetComponent<BoxCollider>();
		Vector3 center = transform.TransformPoint(box.center);
		Vector3 halfSize = Vector3.Scale(box.size, transform.lossyScale) / 2;

		limitMin = (float3)(center - halfSize);
		limitMax = (float3)(center + halfSize);
	}
    public SplineContainer GeneratePath(float3 initPos)
    {
        int length = UnityEngine.Random.Range(pathPointMin, pathPointMax);
        float3[] pathPoints = new float3[length];

        pathPoints[0] = initPos;

        if (simpleZmovement)
        {
            if(initPos.x < 0)
			    pathPoints[1] = new float3(limitMax.x, UnityEngine.Random.Range(limitMin.y, limitMax.y), UnityEngine.Random.Range(limitMin.z, limitMax.z));
            else 
			    pathPoints[1] = new float3(limitMin.x, UnityEngine.Random.Range(limitMin.y, limitMax.y), UnityEngine.Random.Range(limitMin.z, limitMax.z));
		}
        else{
            // quitar for que solo sea uno 
		    for (int i = 1; i < length; i++) // si estoy en caja limitmina si no b
            {
                pathPoints[i] = new float3(UnityEngine.Random.Range(limitMin.x, limitMax.x), UnityEngine.Random.Range(limitMin.y, limitMax.y), UnityEngine.Random.Range(limitMin.z, limitMax.z));
            }
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
	        (float3)BallPath.transform.InverseTransformPoint(pathPoints[i]),
	        -1 * Vector3.right,
	         1 * Vector3.right);
		}

        container.Spline.Knots = knots;

        return container;
    }
}