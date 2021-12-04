using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Neon : MonoBehaviour
{
    Material outlineMat;

    float intensity = 1;
    public float toggle = 1;

    private void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        outlineMat = sr.material;
    }

    private void Update()
    {
        intensity += Time.deltaTime * toggle;
        if(intensity>4)
        {
            intensity = 4f;
            toggle *= -1;
        }
        else if (intensity < 1)
        {
            intensity = 1f;
            toggle *= -1;
        }

        outlineMat.SetFloat("_Intensity", intensity);
    }
}
