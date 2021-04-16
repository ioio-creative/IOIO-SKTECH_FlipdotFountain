using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RenderTextureConverter : MonoBehaviour
{
    public int Width;
    public int Height;


    public RenderTexture flipDotRT_ARGB32;
    public Texture2D flipDotTex2D_RGBA32;

    public FlipDotIO flipDotIOSharpClass;

    private bool keyPressed;
    [SerializeField]
    private float updateInterval;
    private float timer;

    private void Start()
    {
        // flipDotIOSharpClass.connect();
        timer = 0;

        flipDotTex2D_RGBA32 = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        flipDotTex2D_RGBA32.anisoLevel = 0;
        flipDotTex2D_RGBA32.filterMode = FilterMode.Point;
    }
    private void Update()
    {
        if (updateInterval > 0)
        {

            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer %= updateInterval;
                keyPressed = true;
            }
        }

    }

    private void OnPostRender()
    {
        if (keyPressed)
        {
            ReadToTex2D();
            var pixels = GetInt32Pixels(flipDotTex2D_RGBA32);

            flipDotIOSharpClass.SendFlipDotImage(pixels, false);

            //Debug.Log(pixels.Length + "Pixels");
            keyPressed = false;
        }
    }

    private void ReadToTex2D()
    {
        RenderTexture.active = flipDotRT_ARGB32;
        flipDotTex2D_RGBA32.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        flipDotTex2D_RGBA32.Apply();
        RenderTexture.active = null;
    }

    public Int32[] GetInt32Pixels(Texture2D R8Tex2D)
    {
        Color32[] tex2DPixels = R8Tex2D.GetPixels32();

        return tex2DPixels.Select(c => Convert.ToInt32(c.r)).ToArray();
    }
}
