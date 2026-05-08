using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public static class SDFUtil
{
	/// <summary>
	/// Estructura que representa una cápsula.
	/// </summary>
	public struct Capsule
	{
		public Vector3 pointBase; // Centro de la semiesfera inferior.
		public Vector3 pointTip; // Centro de semiesfera superior.
		public float radius;

		public Capsule(Vector3 a, Vector3 b, float r)
		{
			pointBase = a;
			pointTip = b;
			radius = r;
		}
	}

	/// <summary>
	/// Devuelve la distancia mínima a un punto de la superfície de una esfera.
	/// </summary>
	/// <param name="point"> Punto desde el que queremos medir la distancia. </param>
	/// <param name="center"> Centro de la esfera. </param>
	/// <param name="radius"> Radio de la esfera. </param>
	/// <returns></returns>
	public static float SDFSphere(Vector3 point, Vector3 center, float radius, Transform tr = null)
	{
		if (tr != null) tr.InverseTransformPoint(center);
		float a = Vector3.Distance(point, center) - radius;
		return a;
	}

	/// <summary>
	/// Devuelve un objeto que facilita los cálculos del SDF a partir de un collider de tipo cápsula.
	/// </summary>
	/// <param name="col"> Collider de tipo cápsula. </param>
	/// <returns></returns>
	private static SDFUtil.Capsule GetSDFCapsuleFromCollider(CapsuleCollider col, Transform tr = null)
	{
		// Eje local según dirección del collider
		Vector3 direction = Vector3.up; // Y
		if (col.direction == 0) direction = Vector3.right; // X
		else if (col.direction == 2) direction = Vector3.forward; // Z

		// Distancia desde el centro a los centros de las semiesferas
		float halfInternalLength = Mathf.Max(0, (col.height * 0.5f) - col.radius);

		// Puntos A, B distancia local para SDF
		Vector3 localA = col.center - direction * halfInternalLength;
		Vector3 localB = col.center + direction * halfInternalLength;

		// WorldSpace
		Vector3 worldA = col.transform.TransformPoint(localA);
		Vector3 worldB = col.transform.TransformPoint(localB);

		Vector3 colLossyScale = col.transform.lossyScale;
		float worldRadius = col.radius * Mathf.Max(colLossyScale.x, Mathf.Max(colLossyScale.y, colLossyScale.z));

		// Local space de la tela si queremos local
		if (tr != null)
		{
			Vector3 finalA = tr.InverseTransformPoint(worldA);
			Vector3 finalB = tr.InverseTransformPoint(worldB);

			// El radio también debe convertirse al espacio local de la tela.
			// Asumimos que la tela tiene escala uniforme. Si la tela está escalada x2 en el mundo, 
			// el radio local debe dividirse entre 2 para que físicamente mida lo mismo.
			Vector3 clothScale = tr.lossyScale;
			float maxClothScale = Mathf.Max(clothScale.x, Mathf.Max(clothScale.y, clothScale.z));
			float finalRadius = worldRadius / maxClothScale;

			return new SDFUtil.Capsule(finalA, finalB, finalRadius);
		}
		// Sino en world space
		return new SDFUtil.Capsule(worldA, worldB, worldRadius);
	}

	private static float SDFCapsule(Vector3 p, Capsule capsule)
	{
		Vector3 pa = p - capsule.pointBase;
		Vector3 ba = capsule.pointTip - capsule.pointBase;

		float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));

		Vector3 closestPoint = ba * h;
		return (pa - closestPoint).magnitude - capsule.radius;
	}

	/// <summary>
	/// Distancia SDF de una posición a la superfície de una cápsula.
	/// </summary>
	/// <param name="p">Punto desde el que queremos medir la distancia.</param>
	/// <param name="capsule">Collider de la cápsula.</param>
	/// <returns></returns>
	public static float SDFCapsule(Vector3 p, CapsuleCollider capsule, Transform tr = null)
	{
		return SDFCapsule(p, GetSDFCapsuleFromCollider(capsule, tr));
	}

	/// <summary>
	/// Polynomial Smooth Union (smin de Íñigo Quílez)
	/// </summary>
	/// <param name="d1"> SDF obj 1</param>
	/// <param name="d2"> SDF obj 2</param>
	/// <param name="k"> Parámetro de suavidad. 0 conlleva objetos separados completamente, 1 unión muy gruesa entre ellos. </param>
	/// <returns></returns>
	public static float SmoothMin(float d1, float d2, float k)
	{
		// Factor de interpolación 'h' basado en la diferencia de distancias y la suavidad 'k'
		float h = Mathf.Clamp01(0.5f + 0.5f * (d2 - d1) / k);

		// Mezcla lineal de las distancias menos la compensación polinómica
		return Mathf.Lerp(d2, d1, h) - k * h * (1.0f - h);
	}

	/// <summary>
	/// Distancia SDF de un punto a una superfície generada por una lista de cápsulas y esferas.
	/// </summary>
	/// <param name="p">Punto desde el que queremos medir la distancia.</param>
	/// <param name="capsules">Lista de cápsulas.</param>
	/// <param name="smoothness">Parámetro de suavidad. 0 conlleva objetos separados completamente, 1 unión muy gruesa entre ellos.</param>
	/// <returns></returns>
	public static float getSDFOfSet(Vector3 p, CapsuleCollider[] capsules, SphereCollider[] spheres, float smoothness, Transform tr = null)
	{
		float totalDistance = float.MaxValue;
		bool isFirstShape = true;

		// Cápsulas
		if (capsules != null)
		{
			for (int i = 0; i < capsules.Length; i++)
			{
				float currentDist = SDFCapsule(p, capsules[i], tr);

				if (isFirstShape)
				{
					totalDistance = currentDist;
					isFirstShape = false;
				}
				else
				{
					totalDistance = SmoothMin(totalDistance, currentDist, smoothness); // Fusionamos SDFs
				}
			}
		}

		// Esferas
		if (spheres != null)
		{
			for (int i = 0; i < spheres.Length; i++)
			{
				float currentDist = SDFSphere(p, spheres[i].center, spheres[i].radius, tr);

				if (isFirstShape)
				{
					totalDistance = currentDist;
					isFirstShape = false;
				}
				else
				{
					totalDistance = SmoothMin(totalDistance, currentDist, smoothness);
				}
			}
		}

		return totalDistance;
	}
}
	