// CurveAttribute.cs
// Created by Alexander Ameye
// Version 1.1.0
 
using UnityEngine;
 
public class CurveAttribute : PropertyAttribute
{
    public float PosX, PosY;
    public float RangeX, RangeY;
    public bool b;
    public int x;
 
    public CurveAttribute(float PosX, float PosY,float RangeX, float RangeY, bool b)
    {
        this.PosX = PosX;
        this.PosY = PosY;
        this.RangeX = RangeX;
        this.RangeY = RangeY;
        this.b = b;
    }
}