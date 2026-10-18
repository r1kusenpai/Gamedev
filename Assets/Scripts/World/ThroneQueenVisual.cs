using UnityEngine;

[ExecuteAlways]
public sealed class ThroneQueenVisual : MonoBehaviour
{
    public GameObject AnimatedModel;
    public GameObject EditorPose;
    void OnEnable(){RefreshVisuals();}
    void Update(){RefreshVisuals();}
    public void RefreshVisuals()
    {
        if(AnimatedModel&&AnimatedModel.activeSelf!=Application.isPlaying)AnimatedModel.SetActive(Application.isPlaying);
        if(EditorPose&&EditorPose.activeSelf==Application.isPlaying)EditorPose.SetActive(!Application.isPlaying);
    }
}
