
using System;
using Pinwheel.Griffin;
using Pinwheel.Griffin.PaintTool;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;


// WorldPointToNormalized = Convert a world position into terrain normalized (XYZ01) space.
// Also has inverse function


public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] private int seed = 030203;
    
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

    private GStylizedTerrain t;
    private Random rng;

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

    private void Start()
    {
        t = CreateTerrain();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            GenerateTerrain(t);
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            StampTerrain(t);
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            FlattenHeightmap(t, flattenElevation);
        }
    }

    private void StampTerrain(GStylizedTerrain terrain)
    {
        rng = new Random(seed);
        
        for (int i = 0; i < passCount; i++)
        {
            // Setup the render texture
            int heightMapResolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture rt = new RenderTexture(heightMapResolution, heightMapResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            
            float x = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.width);
            float y = rng.Next(0, terrain.TerrainData.Geometry.HeightMap.height);
            
            // Get the current stamp
            TerrainStamp currentStamp = terrainStamps[rng.Next(0, terrainStamps.Length)];

            float elevation = GetElevationAt(x, y);
            
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

            Vector2 uv = t.WorldPointToUV(new Vector3(x, 0, y));
            Vector4 sample = t.GetHeightMapSample(uv);   //TODO: If bugs with terrain edges, change to GetInterpolatedHeightMapSample()
            Debug.Log(sample);

            terrain.TerrainData.Geometry.SetRegionDirty(dirtyRect);
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.GeometryTimeSliced);
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

                Vector2 enc = GCommon.EncodeTerrainHeight(GetNoiseAt(x, y) * (noiseScale / 100f));
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
        }

    private float GetElevationAt(float x, float y)
    {
        float elevation = GetNoiseAt(x, y);

        float normalized = (elevation - 0.5f) * 2;
        
        return normalized;
    }

    private float GetNoiseAt(float x, float y)
    {
        return Mathf.Clamp01(Mathf.PerlinNoise(x / noiseFrequency, y / noiseFrequency));
    }
    
    private GStylizedTerrain CreateTerrain()
    {
        GameObject g = new GameObject("Generated Terrain")
        {
            transform =
            {
                position = transform.position,
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
        //terrain.Refresh();
        
        return terrain;
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
        if (t == null)
        {
            if (dataTemplate == null) return;
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero + new Vector3(dataTemplate.Geometry.Width / 2, dataTemplate.Geometry.Height / 2, dataTemplate.Geometry.Length / 2), dataTemplate.Geometry.Size);

            return;
        }
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(t.Bounds.center, t.Bounds.size);
        //Gizmos.DrawWireCube(t.transform.position + new Vector3(t.TerrainData.Geometry.Width / 2, t.TerrainData.Geometry.Height / 2, t.TerrainData.Geometry.Length / 2), t.TerrainData.Geometry.Size);
    }
}

[Serializable]
public class TerrainStamp
{
    [SerializeField] private Texture2D texture;
    [MinMaxSlider(5f, 500f)]
    [SerializeField] private Vector2 radius = new Vector2(20, 50);
    [Range(0f, 1f)]
    [SerializeField] private float strength = 0.5f;
    [SerializeField] private bool canBeNegative;

    public float Strength => strength;

    public float GetRadius(Random rng)
    {
        return (float) (rng.NextDouble() * (radius.y - radius.x) + radius.x);
    }
    public Texture2D Texture => texture;
    public bool CanBeNegative => canBeNegative;

    public override string ToString()
    {
        return Texture.name;
    }
}