
using System.Linq;
using Pinwheel.Griffin;
using Pinwheel.Griffin.PaintTool;
using UnityEngine;
using UnityEngine.UI;

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] private GTerrainData dataTemplate;

    [SerializeField] private Material terrainMat;
    [SerializeField] private Texture2D[] brushMasks;
    [SerializeField] private int passCount = 10;
    
    [SerializeField] private float noiseScale;
    [SerializeField] private float flattenElevation = 50;

    private GStylizedTerrain t;
    
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
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            t = CreateTerrain();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            GenerateTerrain(t);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            FlattenHeightmap(t, flattenElevation);
        }
    }

    private void GenerateTerrain(GStylizedTerrain terrain)
    {
        Debug.Log(terrain.TerrainData.Geometry.HeightMap.name + " : " + terrain.TerrainData.Geometry.HeightMap.height);
        
        for (int i = 0; i < passCount; i++)
        {
            int heightMapResolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture rt = new RenderTexture(heightMapResolution, heightMapResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            //NOTE: Paint here!

            float x = Random.Range(0, terrain.TerrainData.Geometry.HeightMap.width);
            float y = Random.Range(0, terrain.TerrainData.Geometry.HeightMap.height);

            float elevation = GetElevationAt(x, y);

            int pass = 0;
            if (elevation < 0)
            {
                pass = 1;
                elevation *= 1;
            }
        
            Texture2D bg = terrain.TerrainData.Geometry.HeightMap;
            GCommon.CopyToRT(bg, rt);

            Material mat = PainterMaterial;
            mat.SetTexture(MAIN_TEX, bg);
            SetupTextureGrid(terrain, mat);
            mat.SetTexture(MASK, brushMasks[Random.Range(0, brushMasks.Length)]);
            mat.SetFloat(OPACITY, Mathf.Pow(elevation, GTerrainTexturePainter.GEOMETRY_OPACITY_EXPONENT));
            mat.SetTexture(TERRAIN_MASK, Texture2D.blackTexture);
        
            Debug.Log("x: " + x + " y: " + y + ", ElevationSample: " + elevation);

            //TODO: This might not be necessary...
            RaycastHit hit = Physics.RaycastAll(new Vector3(x, terrain.TerrainData.Geometry.Height + 10, y), Vector3.down,
                terrain.TerrainData.Geometry.Height * 2).FirstOrDefault();
            
            if(hit.collider == null) return;
        
            Vector3[] worldPointCorners = GCommon.GetBrushQuadCorners(hit.point, Random.Range(10f, 300f), Random.Range(0f, 360f));
        
            Vector2[] uvCorners = GPaintToolUtilities.WorldToUvCorners(terrain, worldPointCorners);
            Rect dirtyRect = GUtilities.GetRectContainsPoints(uvCorners);

            GCommon.DrawQuad(rt, uvCorners, mat, pass);
        
            RenderTexture.active = rt;
            terrain.TerrainData.Geometry.HeightMap.ReadPixels(
                new Rect(0, 0, heightMapResolution, heightMapResolution), 0, 0);
            terrain.TerrainData.Geometry.HeightMap.Apply();
            RenderTexture.active = null;

            terrain.TerrainData.Geometry.SetRegionDirty(dirtyRect);
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Geometry);
        }

        //previewImg.texture = terrain.TerrainData.Geometry.HeightMap;

        /*
        // Set the pixels
        Debug.Log(t.TerrainData.Geometry.HeightMap);
        Debug.Log(t.TerrainData.Geometry.HeightMap.GetPixels().Length);

        Color[] colors = GenerateHeightmap(t);
        
        t.TerrainData.Geometry.HeightMap.SetPixels(colors);
        t.TerrainData.Geometry.HeightMap.Apply();
        
        // Set the terrain to dirty, to regenerate.
        t.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
        t.TerrainData.SetDirty(GTerrainData.DirtyFlags.Geometry);
        
        //Clean up
        t.TerrainData.Geometry.ClearDirtyRegions();*/
    }

    private Color[] GenerateHeightmap(GStylizedTerrain terrain)
    {
        Texture2D hm = new Texture2D(dataTemplate.Geometry.HeightMap.width, dataTemplate.Geometry.HeightMap.height);
        Color[] data = hm.GetPixels();
        int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
        
        Debug.Log("Res: " + resolution + " Template Width: " + dataTemplate.Geometry.HeightMap.width + " Template height: " + dataTemplate.Geometry.HeightMap.height);

        for (int x = 0; x < dataTemplate.Geometry.HeightMap.width; x++)
        {
            for (int y = 0; y < dataTemplate.Geometry.HeightMap.height; y++)
            {
                int pixelIndex = y * resolution + x;
                
                Color c = data[pixelIndex];

                Vector2 enc = GCommon.EncodeTerrainHeight(GetElevationAt(x + terrain.transform.position.x,
                    y + terrain.transform.position.z));
                //c.r = GetElevationAt(x + terrain.transform.position.x, y + terrain.transform.position.z);

                c.r = enc.x;
                c.g = enc.y;
                
                data[pixelIndex] = c;
            }
        }

        return data;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>Value that's approximately in the range -1...1</returns>
    private float GetElevationAt(float x, float y)
    {
        float elevation = Mathf.PerlinNoise(x / noiseScale, y / noiseScale);

        float normalized = (elevation - 0.5f) * 2;
        
        return normalized;
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
        terrain.GroupId = 0;
        terrain.TerrainData = terrainData;
        terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Shading);
        //terrain.Refresh();
        
        return terrain;
    }

    private static void FlattenHeightmap(GStylizedTerrain terrain, float elevation)
    {
        Debug.Log(terrain.TerrainData.Geometry.HeightMap.name + " : " + terrain.TerrainData.Geometry.HeightMap.height);
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
                Vector2 enc = GCommon.EncodeTerrainHeight(elevation);
                c.r = enc.x;
                c.g = enc.y;
                data[pixelIndex] = c;
            }
        }
        
        hm.SetPixels(data);
        hm.Apply();

        terrain.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
        terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Geometry);
        
        //terrain.TerrainData.Geometry.ClearDirtyRegions();
        Debug.Log(terrain.TerrainData.Geometry.HeightMap.name + " : " + terrain.TerrainData.Geometry.HeightMap.height);
    }
}
