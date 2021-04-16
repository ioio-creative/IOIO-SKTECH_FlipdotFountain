using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneSplash : MonoBehaviour
{
    [SerializeField] private MeshRenderer floor;
    private Material floorMat;
    [SerializeField] private float splashDelay;

    private void Start()
    {
        if (floor != null)
        {
            floorMat = floor.material;
            floorMat.SetColor("_EmissionColor", Color.black);
        }
    }

    public void Splash(float duration)
    {
        StartCoroutine(i());

        IEnumerator i()
        {
            yield return new WaitForSeconds(splashDelay);
            floorMat.SetColor("_EmissionColor", Color.white);
            yield return new WaitForSeconds(duration);
            floorMat.SetColor("_EmissionColor", Color.black);
        }

    }
}
