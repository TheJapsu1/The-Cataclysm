using System.Collections;
using System.Collections.Generic;
using Pinwheel.Griffin;
using Pinwheel.Griffin.StampTool;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;


// WorldPointToNormalized = Convert a world position into terrain normalized (XYZ01) space.
// Also has inverse function


public class TerrainGenerator : SerializedMonoBehaviour
{
    [ReadOnly]
    public Dictionary<Vector3, GStylizedTerrain> TerrainChunks = new Dictionary<Vector3, GStylizedTerrain>();

    [SerializeField] private string seed = "030203";
    
    [SerializeField] private GGeometryStamper stamper;
    [SerializeField] private GTerrainData dataTemplate;

    [SerializeField] private Material terrainMat;
     
    [Tooltip("How many times the stamping process will be executed.")]
    [Range(1, 50)]
    [SerializeField] private int passCount = 10;
    
    [SerializeField] private float noiseFrequency = 200f;
    
    [Tooltip("Defines how much percentage of the max elevation the noise will take up in maximum.")]
    [Range(1, 100)]
    [SerializeField] private int noiseScale = 50;
    
    [SerializeField] private float flattenElevation = 50;

    [SerializeField] private bool smoothNormals = true;
    
    [SerializeField] private TerrainStamp[] terrainStamps;

    private Random rng;

    private void Start()
    {
        Vector3 position = transform.position;
        
        CreateTerrain(position);
        CreateTerrain(position + Vector3.left * dataTemplate.Geometry.Width);
        CreateTerrain(position + Vector3.right * dataTemplate.Geometry.Width);
        CreateTerrain(position + Vector3.forward * dataTemplate.Geometry.Length);
        CreateTerrain(position + Vector3.back * dataTemplate.Geometry.Length);
        
        GStylizedTerrain.ConnectAdjacentTiles();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            StartCoroutine(GenerateAllTerrains());
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartCoroutine(StampAllTerrains());
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            foreach (GStylizedTerrain t in GStylizedTerrain.ActiveTerrains)
                FlattenHeightmap(t, flattenElevation);
        }
    }

    private IEnumerator GenerateAllTerrains()
    {
        foreach (GStylizedTerrain t in GStylizedTerrain.ActiveTerrains)
        {
            GenerateTerrain(t);
        }
        
        GStylizedTerrain.MatchEdges(-1);
        //GStylizedTerrain.ConnectAdjacentTiles();
        
        yield return null;
    }

    private IEnumerator StampAllTerrains()
    {
        foreach (GStylizedTerrain t in GStylizedTerrain.ActiveTerrains)
        {
            StampTerrain(t);
        }
        
        yield return null;
    }

    private void StampTerrain(GStylizedTerrain terrain)
    {
        rng = new Random(seed.GetHashCode() + terrain.transform.position.GetHashCode());

        // Complete all the stamp passes
        for (int i = 0; i < passCount; i++)
        {
            // Get random horizontal coords inside the Terrain chunk
            float x = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.width) + terrain.transform.position.x;
            float z = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.height) + terrain.transform.position.z;
            
            // Get and set a random stamp
            TerrainStamp currentStamp = terrainStamps[rng.Next(0, terrainStamps.Length)];
            stamper.Stamp = currentStamp.Texture;

            // Set the Position, Rotation and Scale of the stamper
            stamper.Rotation = Quaternion.Euler(0, rng.Next(0, 360), 0);
            
            stamper.Position = new Vector3(x, 0, z);
            //stamper.Position = hit.point;
            stamper.Scale = currentStamp.GetSize(rng);
                        
            // Set the operation type
            GStampOperation stampOperation = currentStamp.GetOperation(rng);
            stamper.Operation = stampOperation;

            // Set the falloff
           stamper.Falloff = currentStamp.Falloff;

            // Apply, and perform stamp
            stamper.Apply();
            
            Debug.Log("Stamped " + currentStamp + ", with operation " + stampOperation + " at " + stamper.Position);
        }
    }

    private void GenerateTerrain(GStylizedTerrain terrain)
    {
        Texture2D hm = terrain.TerrainData.Geometry.HeightMap;
        Color[] data = hm.GetPixels();
        
        int resolution = terrain.TerrainData.Geometry.HeightMapResolution;

        for (int x = 0; x < terrain.TerrainData.Geometry.HeightMap.width; x++)
        {
            for (int y = 0; y < terrain.TerrainData.Geometry.HeightMap.height; y++)
            {
                int pixelIndex = y * resolution + x;
                Color c = data[pixelIndex];

                Vector2 enc = GCommon.EncodeTerrainHeight(GetNoiseAt(x + terrain.transform.position.x, y + terrain.transform.position.z) * (noiseScale / 100f));
                c.r = enc.x; 
                c.g = enc.y;
                data[pixelIndex] = c;
            }
        }
        
        hm.SetPixels(data);
        hm.Apply();

        terrain.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
        terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.GeometryTimeSliced);
        
        terrain.TerrainData.Geometry.ClearDirtyRegions();
    }

    private float GetNoiseAt(float x, float y)
    {
        return Mathf.Clamp01(Mathf.PerlinNoise(x / noiseFrequency + 12345, y / noiseFrequency + 23456));
    }
    
    private void CreateTerrain(Vector3 position)
    {
        GameObject g = new GameObject("Generated Terrain")
        {
            transform =
            {
                position = position,
                localRotation = Quaternion.identity,
                localScale = Vector3.one
            }
        };
        
        GStylizedTerrain terrain = g.AddComponent<GStylizedTerrain>();
        GTerrainData terrainData = ScriptableObject.CreateInstance<GTerrainData>();
        
        terrainData.Reset();
        
        dataTemplate.CopyTo(terrainData);

        terrainData.Shading.CustomMaterial = terrainMat;
        terrainData.Shading.UpdateMaterials();
        terrainData.Geometry.AllowTimeSlicedGeneration = true;
        terrainData.Geometry.SmoothNormal = smoothNormals;
        terrain.GroupId = 0;
        terrain.TerrainData = terrainData;
        terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Shading);
    }

    private static void FlattenHeightmap(GStylizedTerrain terrain, float elevation)
    {
        Texture2D hm = terrain.TerrainData.Geometry.HeightMap;
        Color[] data = hm.GetPixels();
        
        int width = terrain.TerrainData.Geometry.HeightMap.width;
        int length = terrain.TerrainData.Geometry.HeightMap.height;

        int endX = 0 + width - 1;
        int endY = 0 + length - 1;
        int resolution = terrain.TerrainData.Geometry.HeightMapResolution;

        for (int x = 0; x <= endX; x++)
        {
            for (int y = 0; y <= endY; y++)
            {
                int pixelIndex = y * resolution + x;
                Color c = data[pixelIndex];

                float normalized = elevation / terrain.TerrainData.Geometry.Height;
                Vector2 enc = GCommon.EncodeTerrainHeight(normalized);
                c.r = enc.x; 
                c.g = enc.y;
                data[pixelIndex] = c;
            }
        }
        
        hm.SetPixels(data);
        hm.Apply();

        terrain.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
        terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.GeometryTimeSliced);
        
        terrain.TerrainData.Geometry.ClearDirtyRegions();
    }

    private void OnDrawGizmos()
    {
        foreach (GStylizedTerrain t in GStylizedTerrain.ActiveTerrains)
        {
            if (t == null)
            {
                if (dataTemplate == null) return;
            
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(Vector3.zero + new Vector3(dataTemplate.Geometry.Width / 2, dataTemplate.Geometry.Height / 2, dataTemplate.Geometry.Length / 2), dataTemplate.Geometry.Size);

                return;
            }
        
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(t.Bounds.center, t.Bounds.size);
        }        
    }
}

#region OBSOLETE STUFF


// Raycast the terrain surface for a spot where to stamp
//GStylizedTerrain.Raycast(new Ray(new Vector3(x, terrain.TerrainData.Geometry.Height + 10, z), Vector3.down),
//    out RaycastHit hit, terrain.TerrainData.Geometry.Height * 2, terrain.GroupId);

    /*
    private static void SetupTextureGrid(GStylizedTerrain t, Material mat)
        {
            mat.SetTexture(MAIN_TEX_L,
                t.LeftNeighbor && t.LeftNeighbor.TerrainData ?
                t.LeftNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_TL,
                t.LeftNeighbor && t.LeftNeighbor.TopNeighbor && t.LeftNeighbor.TopNeighbor.TerrainData ?
                t.LeftNeighbor.TopNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);
            mat.SetTexture(MAIN_TEX_TL,
                t.TopNeighbor && t.TopNeighbor.LeftNeighbor && t.TopNeighbor.LeftNeighbor.TerrainData ?
                t.TopNeighbor.LeftNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_T,
                t.TopNeighbor && t.TopNeighbor.TerrainData ?
                t.TopNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_TR,
                t.RightNeighbor && t.RightNeighbor.TopNeighbor && t.RightNeighbor.TopNeighbor.TerrainData ?
                t.RightNeighbor.TopNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);
            mat.SetTexture(MAIN_TEX_TR,
                t.TopNeighbor && t.TopNeighbor.RightNeighbor && t.TopNeighbor.RightNeighbor.TerrainData ?
                t.TopNeighbor.RightNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_R,
                t.RightNeighbor && t.RightNeighbor.TerrainData ?
                t.RightNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_RB,
                t.RightNeighbor && t.RightNeighbor.BottomNeighbor && t.RightNeighbor.BottomNeighbor.TerrainData ?
                t.RightNeighbor.BottomNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);
            mat.SetTexture(MAIN_TEX_RB,
                t.BottomNeighbor && t.BottomNeighbor.RightNeighbor && t.BottomNeighbor.RightNeighbor.TerrainData ?
                t.BottomNeighbor.RightNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_B,
                t.BottomNeighbor && t.BottomNeighbor.TerrainData ?
                t.BottomNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);

            mat.SetTexture(MAIN_TEX_BL,
                t.LeftNeighbor && t.LeftNeighbor.BottomNeighbor && t.LeftNeighbor.BottomNeighbor.TerrainData ?
                t.LeftNeighbor.BottomNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);
            mat.SetTexture(MAIN_TEX_BL,
                t.BottomNeighbor && t.BottomNeighbor.LeftNeighbor && t.BottomNeighbor.LeftNeighbor.TerrainData ?
                t.BottomNeighbor.LeftNeighbor.TerrainData.Geometry.HeightMap :
                Texture2D.blackTexture);
        }*/

    /*
    private float GetElevationAt(float x, float y)
    {
        float elevation = GetNoiseAt(x, y);

        float normalized = (elevation - 0.5f) * 2;
        
        return normalized;
    }*/

        /*
        for (int i = 0; i < passCount; i++)
        {
            // Setup the render texture
            int heightMapResolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture rt = new RenderTexture(heightMapResolution, heightMapResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            
            float x = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.width) + terrain.transform.position.x;
            float y = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.height) + terrain.transform.position.z;
            
            // Get the current stamp
            TerrainStamp currentStamp = terrainStamps[rng.Next(0, terrainStamps.Length)];

            float elevation = GetElevationAt(x + terrain.transform.position.x, y + terrain.transform.position.z);
            
            int pass = 0;
            if (elevation < 0)
            {
                pass = 1;
                elevation *= -1;

                if (!currentStamp.CanBeNegative) pass = 0;
            }
        
            Texture2D bg = terrain.TerrainData.Geometry.HeightMap;
            GCommon.CopyToRT(bg, rt);

            Material mat = PainterMaterial;
            
            mat.SetTexture(MAIN_TEX, bg);
            
            SetupTextureGrid(terrain, mat);
            
            mat.SetTexture(MASK, currentStamp.Texture);
            
            //mat.SetFloat(OPACITY, Mathf.Pow(elevation, GTerrainTexturePainter.GEOMETRY_OPACITY_EXPONENT));
            //mat.SetFloat(OPACITY, Mathf.Pow(elevation, currentStamp.Strength * elevation));
            
            mat.SetFloat(OPACITY, currentStamp.Strength);
            
            mat.SetTexture(TERRAIN_MASK, Texture2D.blackTexture);
            
            //TODO: Check if radius of 0.8f ... 1.2f yields better results?
            Vector3[] worldPointCorners = GCommon.GetBrushQuadCorners(new Vector3(x, 0, y), currentStamp.GetRadius(rng), rng.Next(0, 360));
        
            Vector2[] uvCorners = GPaintToolUtilities.WorldToUvCorners(terrain, worldPointCorners);
            Rect dirtyRect = GUtilities.GetRectContainsPoints(uvCorners);

            GCommon.DrawQuad(rt, uvCorners, mat, pass);
        
            RenderTexture.active = rt;
            terrain.TerrainData.Geometry.HeightMap.ReadPixels(
                new Rect(0, 0, heightMapResolution, heightMapResolution), 0, 0);
            terrain.TerrainData.Geometry.HeightMap.Apply();
            RenderTexture.active = null;

            Vector2 uv = terrain.WorldPointToUV(new Vector3(x, 0, y));
            Vector4 sample = terrain.GetHeightMapSample(uv);   //TODO: If bugs with terrain edges, change to GetInterpolatedHeightMapSample()
            Debug.Log(sample);

            terrain.TerrainData.Geometry.SetRegionDirty(dirtyRect);
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.GeometryTimeSliced);
        }*/

/*
#region STAMP_PROPERTIES

private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");

private static readonly int MAIN_TEX_L = Shader.PropertyToID("_MainTex_Left");
private static readonly int MAIN_TEX_TL = Shader.PropertyToID("_MainTex_TopLeft");
private static readonly int MAIN_TEX_T = Shader.PropertyToID("_MainTex_Top");
private static readonly int MAIN_TEX_TR = Shader.PropertyToID("_MainTex_TopRight");
private static readonly int MAIN_TEX_R = Shader.PropertyToID("_MainTex_Right");
private static readonly int MAIN_TEX_RB = Shader.PropertyToID("_MainTex_BottomRight");
private static readonly int MAIN_TEX_B = Shader.PropertyToID("_MainTex_Bottom");
private static readonly int MAIN_TEX_BL = Shader.PropertyToID("_MainTex_BottomLeft");

private static readonly int MASK = Shader.PropertyToID("_Mask");
private static readonly int OPACITY = Shader.PropertyToID("_Opacity");
private static readonly int TERRAIN_MASK = Shader.PropertyToID("_TerrainMask");

#endregion

private static Material painterMaterial;
private static Material PainterMaterial
{
    get
    {
        if (painterMaterial == null)
        {
            painterMaterial = new Material(GRuntimeSettings.Instance.internalShaders.elevationPainterShader);
        }
        return painterMaterial;
    }
}
*/

#endregion