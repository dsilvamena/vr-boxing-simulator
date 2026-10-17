using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserCursor : OVRCursor
{
    public Transform dot;
    public float defaultLength = 3f;
    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.005f;
        if (dot) dot.gameObject.SetActive(false);
    }

    // El rayo no toca ningún botón: línea de largo fijo, sin punto
    public override void SetCursorRay(Transform ray)
    {
        lr.SetPosition(0, ray.position);
        lr.SetPosition(1, ray.position + ray.forward * defaultLength);
        if (dot) dot.gameObject.SetActive(false);
    }

    // El rayo toca un botón: la línea termina ahí y aparece el punto
    public override void SetCursorStartDest(Vector3 start, Vector3 dest, Vector3 normal)
    {
        lr.SetPosition(0, start);
        lr.SetPosition(1, dest);
        if (dot)
        {
            dot.gameObject.SetActive(true);
            dot.position = dest;
            if (normal != Vector3.zero) dot.rotation = Quaternion.LookRotation(normal);
        }
    }
}