using UnityEngine;
using UnityEngine.AI;
public sealed class CityPopulationNavigation : MonoBehaviour
{
 public NavMeshData Data;
 NavMeshDataInstance instance;
 void OnEnable(){if(Data)instance=NavMesh.AddNavMeshData(Data);}
 void OnDisable(){if(instance.valid)instance.Remove();}
}
