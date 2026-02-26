using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using System.Globalization;
using NUnit.Framework.Constraints;

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
	public float timeLeft = 0.0f;
	private int[] vertexIndices;
	[SerializeField, Tooltip("Frames Recorded")]
	private int totalFramesRecorded = 0;

    [Header("Output")]
    public string fileName = "cloth_dataset.csv";
    private string fileDirectory;
    public string filePrefix = "clothDataset";
    public string fileExtension = ".csv";

    private Cloth cloth;

    // Estado en t
    private Vector3[] pos_t;
    private Vector3[] vel_t;
    private Vector3[] normal_t;
    private float [] sdf_t;

    // Para calcular velocidades
    private Vector3[] pos_t_minus_1;
    private bool hasPrevious = false;

    private StringBuilder sb;
    private string filePath;

    NumberFormatInfo nfi; // Para cambiar de formato de escritura al estadounidense (usar puntos)
    

    void Start()
    {
        cloth = GetComponent<Cloth>();

        // SelectAllVertices(); // Para seleccionar todos los vertices
        AutoSelectVertices(10); // Para seleccinar un conjunto random limitado

        if (vertexIndices == null || vertexIndices.Length == 0)
        {
            Debug.LogError("No vertex indices assigned.");
            enabled = false;
            return;
        }

        fileDirectory = Application.dataPath + "/Datasets/";
        generateFileName();
        filePath = fileDirectory + fileName;
        sb = new StringBuilder();

        Debug.Log(filePath);

        // CSV Header
        sb.Append("frame");
        foreach (int i in vertexIndices)  // Input
            sb.Append($",x{i},y{i},z{i},vx{i},vy{i},vz{i}, sdf{i}, nx{i}, ny{i}, nz{i}");
        foreach (int i in vertexIndices) // Output
            sb.Append($",dx{i},dy{i},dz{i}");
        sb.AppendLine();

        // Record automático
		if (autoRecord)
		{
			record = true;
			timeLeft = recordTime;
		}

		// Formato estadounidense (puntos en vez de comas)
		nfi = new CultureInfo("en-US", false).NumberFormat;
        nfi.NumberDecimalSeparator = ".";
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
		for (int i = 0; i < vertexIndices.Length; i++) // Creo que esto es prescindible
			vertexIndices[i] = i;
	}

    void stopRecording()
    {
        record = false;
    }

    void FixedUpdate() // Puede estar eestar justamanete antes? Va por frames? 
    { //Se actualiza a la vez que la física. Puedes estar guardando antes de que acabe
        if (!record) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft < 0)
        {
            stopRecording();
            return;
        }

        Vector3[] particles = cloth.vertices;

        pos_t = new Vector3[vertexIndices.Length]; // No hay que hacer un new por frame
        vel_t = new Vector3[vertexIndices.Length];
        sdf_t = new float[vertexIndices.Length];
        normal_t = new Vector3[vertexIndices.Length];

        // Sphere collider of cloth
        Vector3 spherePos = cloth.sphereColliders[0].first.transform.position; // cventer para que es
        float sphereRad = cloth.sphereColliders[0].first.radius;

        for (int j = 0; j < vertexIndices.Length; j++)
        {
            int idx = vertexIndices[j];
            pos_t[j] = transform.TransformPoint(particles[idx]); // Global position of vertices

			if (hasPrevious)
            {
                vel_t[j] = (particles[idx] - pos_t_minus_1[j]) / Time.fixedDeltaTime;
            }
            else
            {
                vel_t[j] = Vector3.zero;
            }

            sdf_t[idx] = SDFSphere(pos_t[j], spherePos, sphereRad);
            normal_t[idx] = NormalToSphere(pos_t[j], spherePos);
            
            /**
             * Constraints
             * cloth.GetVirtualParticleWeights();
             * Esto no te da los vértices sino listas con las coordenadas de cada vértice por tipo de peso.
             **/

            pos_t_minus_1[j] = pos_t[j];
        }
        

		totalFramesRecorded++;
        hasPrevious = true;
		SaveSample(pos_t, vel_t, null); // Guardar en memoria cada cierto tiempo, escribir en archivo al final
	}

    void SaveSample(Vector3[] pos, Vector3[] vel, Vector3[] delta)
    {
        sb.Append(Time.frameCount.ToString(nfi));

        for (int j = 0; j < pos.Length; j++)
        {
            sb.Append($",{pos[j].x.ToString(nfi)},{pos[j].y.ToString(nfi)},{pos[j].z.ToString(nfi)}");
            sb.Append($",{vel[j].x.ToString(nfi)},{vel[j].y.ToString(nfi)},{vel[j].z.ToString(nfi)}");
        }

        for (int j = 0; j < delta.Length; j++)
        {
            sb.Append($",{delta[j].x.ToString(nfi)},{delta[j].y.ToString(nfi)},{delta[j].z.ToString(nfi)}");
        }

		for (int j = 0; j < sdf_t.Length; j++)
		{
			sb.Append($",{sdf_t[j].ToString(nfi)}");
		}

		for (int j = 0; j < normal_t.Length; j++)
		{
            sb.Append($",{normal_t[j].x.ToString(nfi)},{normal_t[j].y.ToString(nfi)},{normal_t[j].z.ToString(nfi)}");
		}

		sb.AppendLine();

        File.AppendAllText(filePath, sb.ToString());
        sb.Length = 0;
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
	private void generateFileName()
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
}
