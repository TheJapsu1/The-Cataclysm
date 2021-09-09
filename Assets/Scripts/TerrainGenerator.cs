using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pinwheel.Griffin;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public GTerrainData terrainDataTemplate;
    //public GTerrainGeneratedData terrainGeneratedDataTemplate;

    [SerializeField] private float noiseScale;
    [SerializeField] private float noiseAmplitude;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            GStylizedTerrain t = CreateTerrain(terrainDataTemplate);
            
            Debug.Log(t.TerrainData.GeometryData.name);
            
            GenerateTerrain(t);
        }
    }

    private void GenerateTerrain(GStylizedTerrain t)
    {
        Color[] oldHeightMapData = t.TerrainData.Geometry.HeightMap.GetPixels();

        // Set the pixels
        t.TerrainData.Geometry.HeightMap.SetPixels(GenerateHeightmap(t));
        t.TerrainData.Geometry.HeightMap.Apply();
        
        Color[] newHeightMapData = t.TerrainData.Geometry.HeightMap.GetPixels();

        // Set the terrain to dirty, to regenerate.
        IEnumerable<Rect> dirtyRects = GCommon.CompareTerrainTexture(5, oldHeightMapData, newHeightMapData);
        
        t.TerrainData.Geometry.SetRegionDirty(dirtyRects);
        t.TerrainData.SetDirty(GTerrainData.DirtyFlags.GeometryTimeSliced);
        
        //Clean up
        t.TerrainData.Geometry.ClearDirtyRegions();
    }

    private Color[] GenerateHeightmap(GStylizedTerrain terrain)
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
                
                c.r = GetElevationAt(x + terrain.transform.position.x, y + terrain.transform.position.z);
                
                data[pixelIndex] = c;
            }
        }

        return data;
    }

    private float GetElevationAt(float x, float y)
    {
        float elevation = Mathf.PerlinNoise(x / noiseScale, y / noiseScale) * noiseAmplitude;
        
        return elevation;
    }
    
    private GStylizedTerrain CreateTerrain(GTerrainData data)
    {
        GameObject g = new GameObject("Stylized Terrain")
        {
            transform =
            {
                position = transform.position,
                localRotation = Quaternion.identity,
                localScale = Vector3.one
            }
        };

        GStylizedTerrain terrain = g.AddComponent<GStylizedTerrain>();
        terrain.GroupId = 0;
        //terrain.TerrainData = Instantiate(data);
        terrain.TerrainData = data;
        //terrain.TerrainData.GeometryData = Instantiate(terrainGeneratedDataTemplate);
        terrain.Refresh();
        return terrain;
    }
}
