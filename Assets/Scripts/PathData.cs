using System.Collections;
using UnityEngine;
using System.Collections.Generic;
public class PathData : MonoBehaviour
{
  

    private Vector3[] points;
    

    void Awake()
    {
        // Use the standard List syntax
        List<Vector3> pts = new List<Vector3>();

        // Loop through children to grab positions
        foreach (Transform child in transform)
        {
            pts.Add(child.position);
        }

        points = pts.ToArray();
        //Debug.Log($"{gameObject.name} loaded {points.Length} waypoints.");
    }

    public Vector3[] GetPoints() => points;


}
