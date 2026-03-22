using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class TrackCreator : MonoBehaviour
{
    [SerializeField] private SplineContainer track;
    [SerializeField] private bool loopedTrack = false;
    [SerializeField] private float tangentLength = 2f;

    public void GenerateTrack()
    {
        if (track == null)
        {
            Debug.LogError("No SplineContainer assigned.", this);
            return;
        }

        int childCount = track.transform.childCount;

        Spline spline = new Spline();

        for (int i = 0; i < childCount; i++)
        {
            Transform child = track.transform.GetChild(i);

            float3 localPos = track.transform.InverseTransformPoint(child.position);

            float3 forward =
                math.normalize((float3)track.transform.InverseTransformDirection(child.forward));

            if (math.lengthsq(forward) < 0.0001f)
                forward = new float3(0, 0, 1);

            float3 tangentIn = -forward * tangentLength;
            float3 tangentOut = forward * tangentLength;

            BezierKnot knot = new BezierKnot(localPos, tangentIn, tangentOut, quaternion.identity);
            spline.Add(knot);
        }

        spline.Closed = loopedTrack;

        // 🔥 แก้ตรงนี้
        track.Spline = spline;

        Debug.Log("Track generated!", this);
    }
}