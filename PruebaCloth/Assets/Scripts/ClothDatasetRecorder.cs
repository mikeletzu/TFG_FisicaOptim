using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using System.Globalization;
using NUnit.Framework.Constraints;
using System.Collections.Generic;

[RequireComponent(typeof(Cloth))]
public class ClothDatasetRecorder : MonoBehaviour
{
    [Header("Recording")]
    [SerializeField, Tooltip("Automatic record")]
	private bool autoRecord = true;
	[SerializeField, Tooltip("Currently recording")]
	private bool record = false;
	[SerializeField, Tooltip("Automatic record time")]
	private float recordTime = 3.5f;
	public float recordTimeLeft = 0.0f;
	private int[] vertexIndices;
	[SerializeField, Tooltip("Frames Recorded")]
	private int totalFramesRecorded = 0;
    [SerializeField, Tooltip("Frames Recorded")]
    private SphereCollider[] sphereColliders;
	[SerializeField, Tooltip("Frames Recorded")]
	private CapsuleCollider[] capsuleColliders;
	[SerializeField, Tooltip("If items part of collision are blended in sdf.")]
	private float collidersUnionSmoothness = 0.0f;

	[Header("Output")]
    public string fileName = "cloth_dataset.csv";
    private string fileDirectory;
    public string filePrefix = "clothDataset";
    public string fileExtension = ".csv";
    [SerializeField, Tooltip("Time between record frames")]
    public bool timed = false;
    private float snapShotTime = 0.3f;
    private float snapTimeLeft = 0.0f;
    // Cloth 
    private Cloth cloth;
    private Vector3[] particles;
    private Vector2[] UVs;

    // Estado en t
    private Vector3[] pos_t;
    private Vector3[] vel_t;
    private Vector3[] normal_t;
    private float [] sdf_t;
    private float[] maxDist_t;
    private Vector2[] uvs_t;

    // Para calcular velocidades
    private Vector3[] pos_t_minus_1;
    private bool hasPrevious = false;

    private float[] collisionTimer_t;     // Negativo si colisiona, positivo si no
    private float[] hasEverCollided_t;

    private StringBuilder sb;
    private string filePath;

    NumberFormatInfo nfi; // Para cambiar de formato de escritura al estadounidense (usar puntos)

    /// <summary>
    /// Informacion de un snapshot.
    /// </summary>
    public struct SnapInfo
    {
		public Vector3[] pos;
		public Vector3[] vel;
		public Vector3[] normal;
        public float[] sdf;
        public float[] maxDist;
        public Vector2[] uv;
        public float[] collisionTimer;
        public float[] hasEverCollided;
    }

    List<SnapInfo> snapshots;
     

    void Start()
    {
        cloth = GetComponent<Cloth>();

        
        SelectAllVertices(); // Para seleccionar todos los vertices
        //AutoSelectVertices(10); // Para seleccinar un conjunto random limitado

        if (vertexIndices == null || vertexIndices.Length == 0)
        {
            Debug.LogError("No vertex indices assigned.");
            enabled = false;
            return;
        }

        // Record automático
		if (autoRecord)
		{
			record = true;
			recordTimeLeft = recordTime;
            snapTimeLeft  = snapShotTime;
		}

		// Formato estadounidense (puntos en vez de comas)
		nfi = new CultureInfo("en-US", false).NumberFormat;
        nfi.NumberDecimalSeparator = ".";

        // Lista de snapshots
        snapshots = new List<SnapInfo> {};

        // Auxiliares para guardar datos del frame
		pos_t = new Vector3[vertexIndices.Length]; 
		vel_t = new Vector3[vertexIndices.Length];
		sdf_t = new float[vertexIndices.Length];
		normal_t = new Vector3[vertexIndices.Length];
        pos_t_minus_1 = new Vector3[vertexIndices.Length];
        maxDist_t = new float[vertexIndices.Length];
        uvs_t = new Vector2[vertexIndices.Length];
        collisionTimer_t = new float[vertexIndices.Length];
        hasEverCollided_t = new float[vertexIndices.Length];

        // Auxiliar para acceder rapidamente a las UVs
        // UVs = GetComponent<MeshFilter>().mesh.uv;

        // Info del cloth
        particles = cloth.vertices;

        // File guardado
        fileDirectory = Application.dataPath + "/Datasets/";
        GenerateFileName();
        filePath = fileDirectory + fileName;
        sb = new StringBuilder();
        Debug.Log(filePath);

        // CSV Header
        sb.Append("frame");
        foreach (int i in vertexIndices)  // Input
            sb.Append($",x{i},y{i},z{i},vx{i},vy{i},vz{i},sdf{i},nx{i},ny{i},nz{i},md{i},u{i},v{i},colTimer{i},everCol{i}");
        sb.AppendLine();
	}

    /// <summary>
    /// Selecciona vértices random de la cloth.
    /// </summary>
    /// <param name="count">Número de vértices a seleccionar. </param>
    void AutoSelectVertices(int count)
    {
        vertexIndices = new int[count];
        for (int i = 0; i < count; i++)
            vertexIndices[i] = i; // Esto es para guardarse el número de índice sólo? Weird si vamos a hacer bucles igualmente
    }

    /// <summary>
    ///  Selecciona todos los vértices de la cloth.
    /// </summary>
    void SelectAllVertices()
    {
		vertexIndices = new int[cloth.vertices.Length]; // Esto si desde luego
        for (int i = 0; i < vertexIndices.Length; i++)
        {// Creo que esto es prescindible
            vertexIndices[i] = i;

            Debug.Log("V: " + i + " Pos: " + cloth.vertices[i]);
        }
    }

    void stopRecording()
    {
        record = false;
    }
    private void OnDestroy()
    {
        if (autoRecord)
        {
            SaveSnapsToCSV(snapshots);
        }
    }

	void FixedUpdate() 
    { 
        //Se actualiza a la vez que la física. 
        if (!record) return;

        recordTimeLeft -= Time.fixedDeltaTime;
        if (recordTimeLeft < 0)
        {
            stopRecording();
            return;
        }

        particles = cloth.vertices;

        //cloth.coefficients[].maxDistance
        ClothSkinningCoefficient[] coeffs = cloth.coefficients;

        // Sphere collider of cloth
        //Vector3 spherePos = cloth.sphereColliders[0].first.transform.position; // cventer para que es
        //float sphereRad = cloth.sphereColliders[0].first.radius;

        for (int j = 0; j < vertexIndices.Length; j++)
        {
            int idx = vertexIndices[j];
            pos_t[j] = particles[idx]; // Local position of vertices
            //pos_t[j] = transform.TransformPoint(particles[idx]); // Global position of vertices

			if (hasPrevious)
            {
                vel_t[j] = (particles[idx] - pos_t_minus_1[j]) / Time.fixedDeltaTime;
            }
            else
            {
                vel_t[j] = Vector3.zero;
            }
            //if()
            //// sdf_t[j] = SDFSphere(pos_t[j], transform.InverseTransformPoint(spherePos), sphereRad);(
            sdf_t[j] = SDFUtil.getSDFOfSet(pos_t[j], capsuleColliders, sphereColliders, collidersUnionSmoothness, transform);
            normal_t[j] = Vector3.zero;  //NormalToSphere(pos_t[j], spherePos); ESTO PORQUE AUN NO LO USAMOS!!!


            maxDist_t[j] = Mathf.Clamp(coeffs[idx].maxDistance, 0f, 1f);
            /**
             * Constraints
             * cloth.GetVirtualParticleWeights();
             * Esto no te da los vértices sino listas con las coordenadas de cada vértice por tipo de peso.
             **/

            if (sdf_t[j] <= 0.01f) // COLISIONA
            {
                // Si estaba en positivo (sin colisionar), lo reseteamos a 0 y empezamos a restar
                if (collisionTimer_t[j] > 0f) collisionTimer_t[j] = 0f;

                collisionTimer_t[j] -= Time.fixedDeltaTime;
                hasEverCollided_t[j] = 1.0f; // Ha colisionado alguna vez
            }
            else // NO COLISIONA
            {
                // Si estaba en negativo (colisionando), lo reseteamos a 0 y empezamos a sumar
                if (collisionTimer_t[j] < 0f) collisionTimer_t[j] = 0f;

                collisionTimer_t[j] += Time.fixedDeltaTime;
            }
            // ---------------------------------------

            pos_t_minus_1[j] = pos_t[j];

            // Recolectar UVs de cada vertice
            //
            //uvs_t[j] = UVs[idx];
        }
        
        hasPrevious = true;

        // Guardamos snapshot
		snapTimeLeft -= Time.fixedDeltaTime;
		if (snapTimeLeft < 0 || !timed)
		{
            RecordSnapshot(pos_t, vel_t, sdf_t, normal_t, maxDist_t, uvs_t, collisionTimer_t, hasEverCollided_t);
            snapTimeLeft = snapShotTime;
            totalFramesRecorded++;
		}
		
	}

    void RecordSnapshot(Vector3[] pos, Vector3[] vel, float[] sdf, Vector3[] normals, float[] maxDist, Vector2[] uv, float[] colTimer, float[] everCol) // Si vemos que no queremos acceder a snapshot anterior guardamos en vector concatenando directamente
    {
        SnapInfo snapInfo = new SnapInfo
        {
            pos = new Vector3[vertexIndices.Length],
            vel = new Vector3[vertexIndices.Length],
            sdf = new float[vertexIndices.Length],
            normal = new Vector3[vertexIndices.Length],
            maxDist = new float[vertexIndices.Length],
            uv = new Vector2[vertexIndices.Length],
            collisionTimer = new float[vertexIndices.Length],
            hasEverCollided = new float[vertexIndices.Length]
        };

        pos.CopyTo(snapInfo.pos, 0);
        vel.CopyTo(snapInfo.vel , 0);
        sdf.CopyTo(snapInfo.sdf, 0);
        normals.CopyTo(snapInfo.normal, 0);
        maxDist.CopyTo(snapInfo.maxDist, 0);
        uv.CopyTo(snapInfo.uv, 0); 
        colTimer.CopyTo(snapInfo.collisionTimer, 0);
        everCol.CopyTo(snapInfo.hasEverCollided, 0);

        snapshots.Add(snapInfo);


        // sb.Append(Time.frameCount.ToString(nfi)); // Queremos numero de frames totales? O tiempos?
    }

    void SaveSnapsToCSV(List<SnapInfo> snaps)
    {
        int i = 0;
        foreach (SnapInfo snap in snaps)
        {
			sb.Append(i.ToString(nfi));
            i++;

			for (int j = 0; j < snap.pos.Length; j++)
            {
                sb.Append($",{snap.pos[j].x.ToString(nfi)},{snap.pos[j].y.ToString(nfi)},{snap.pos[j].z.ToString(nfi)}");
                sb.Append($",{snap.vel[j].x.ToString(nfi)},{snap.vel[j].y.ToString(nfi)},{snap.vel[j].z.ToString(nfi)}");
				sb.Append($",{snap.sdf[j].ToString(nfi)}");
				sb.Append($",{snap.normal[j].x.ToString(nfi)},{snap.normal[j].y.ToString(nfi)},{snap.normal[j].z.ToString(nfi)}");
                sb.Append($",{snap.maxDist[j].ToString(nfi)}");
                sb.Append($",{snap.uv[j].x.ToString(nfi)}");
                sb.Append($",{snap.uv[j].y.ToString(nfi)}"); 
                sb.Append($",{snap.collisionTimer[j].ToString(nfi)}");
                sb.Append($",{snap.hasEverCollided[j].ToString(nfi)}");

            }
            sb.AppendLine();
		}

        File.AppendAllText(filePath, sb.ToString());
        sb.Length = 0;

        snapshots.Clear();
    }

    /// <summary>
    /// Devuelve la distancia mínima a un punto de la superfície de una esfera.
    /// </summary>
    /// <param name="point"> Punto desde el que queremos medir la distancia. </param>
    /// <param name="center"> Centro de la esfera. </param>
    /// <param name="radius"> Radio de la esfera. </param>
    /// <returns></returns>
	float SDFSphere(Vector3 point, Vector3 center, float radius)
	{
        float a = Vector3.Distance(point, center) - radius;
        return a;
	}

    /// <summary>
    /// Dirección a la superficie de un esfera.
    /// </summary>
    /// <param name="point"> Punto desde el que medir la distancia. </param>
    /// <param name="center"> Centro de la esfera. </param>
    /// <returns></returns>
    Vector3 NormalToSphere(Vector3 point, Vector3 center)
    {
		return (point - center).normalized;
	}

	/// <summary>
    /// Generamos un nombre para el dataset segun lo ya guardado.
    /// </summary>
	private void GenerateFileName()
	{
        // Directorio donde guardamos de momento los datasets.
		DirectoryInfo directoryInfo = new DirectoryInfo(fileDirectory);
		int numRecord = 0;

		// Calculamos que numero de record es
		foreach (FileInfo info in directoryInfo.GetFiles())
		{
			string[] split = info.Name.Split('_');
			if (split[0] == filePrefix)
			{
				numRecord = Math.Max(numRecord, int.Parse(split[split.Length-2]) + 1);
			}
		}

		// Nombre del csv generado a partir de la fecha, nombre, número de prueba y extension.
		fileName = filePath + filePrefix + "_" + numRecord + "_" + fileExtension; //  DateTime.Now.ToString("-d-M-yyyy") // Si quisieramos guardar fecha
	}

	public Vector3 GetVelocity(int i)
	{
		return vel_t[i];
	}

	public float GetSDF(int i)
	{
		return sdf_t[i];
	}

	public float GetMaxDistance(int i)
	{
		return maxDist_t[i];
	}

	public Vector2 GetUV(int i, Mesh mesh)
	{
		return mesh.uv[i];
	}
}
