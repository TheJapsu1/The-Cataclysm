using System;
using Pinwheel.Griffin.StampTool;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;

[Serializable]
public class TerrainStamp
{
    [SerializeField] private Texture2D texture;
    
    [Tooltip("Size of the stamp on the X/Z-axis.")]
    [MinMaxSlider(5f, 500f)]
    [SerializeField] private Vector2 size = new Vector2(20, 50);
    
    [Tooltip("The size multiplier of y-axis relative to the x/z-axis.")]
    [Range(0f, 1f)]
    [SerializeField] private float height = 1f;

    [Curve(0, 0, 1f, 1f, true)]
    [SerializeField] private AnimationCurve falloff;
    
    [Tooltip("All the math operations that can be used on this stamp when stamping.")]
    [SerializeField] private GStampOperation[] operations;

    public AnimationCurve Falloff => falloff;

    public GStampOperation GetOperation(Random rng)
    {
        if(operations.Length < 1)
            Debug.LogError("No operations defined for stamp " + ToString());
        
        return operations[rng.Next(0, operations.Length)];
    }

    public Vector3 GetSize(Random rng)
    {
        float val = (float) (rng.NextDouble() * (size.y - size.x) + size.x);
        return new Vector3(val, val * height, val);
    }
    public Texture2D Texture => texture;

    public override string ToString()
    {
        return Texture.name;
    }
}