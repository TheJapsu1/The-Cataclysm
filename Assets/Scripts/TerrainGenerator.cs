using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pinwheel.Griffin;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    private void Start()
    {
        GenerateTerrains();
    }

    private void GenerateTerrains()
    {
        using IEnumerator<GStylizedTerrain> terrains = GStylizedTerrain.ActiveTerrains.GetEnumerator();
        while (terrains.MoveNext())
        {
            GStylizedTerrain t = terrains.Current;

            if (t is null) continue;
            
            //t.ConnectNeighbor();

            // Set the pixels
            t.TerrainData.Geometry.HeightMap.SetPixels(GenerateHeightmap(t));
            t.TerrainData.Geometry.HeightMap.Apply();

            // Set the terrain to dirty, to regenerate.
            t.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
            t.TerrainData.SetDirty(GTerrainData.DirtyFlags.Geometry);
        }
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
                c.r = GetElevationAt(x, y);
                data[pixelIndex] = c;
            }
        }

        return data;
    }

    private float GetElevationAt(int x, int y)
    {
        return (Mathf.PerlinNoise(x, y) * 50) - 200;
    }
}
