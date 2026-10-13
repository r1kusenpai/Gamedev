using UnityEngine;
namespace CastleWorld { public class WindmillRotor : MonoBehaviour { public float DegreesPerSecond=12f; public Vector3 LocalAxis=Vector3.right; void Update(){transform.Rotate(LocalAxis,DegreesPerSecond*Time.deltaTime,Space.Self);} } }
