using UnityEngine;
using UnityEngine.Rendering;
namespace StarterAssets
{
    [ExecuteAlways]
    public class ForestGroundCover : MonoBehaviour
    {
        public ForestGroundCoverData Data;
        public float GameDistance = 160f;
        public float EditorDistance = 650f;
        private readonly Plane[] planes = new Plane[6];
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            RenderPipelineManager.beginCameraRendering += Draw;
            Camera.onPreCull -= DrawBuiltIn;
            Camera.onPreCull += DrawBuiltIn;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            Camera.onPreCull -= DrawBuiltIn;
        }
        private void DrawBuiltIn(Camera camera)
        { if (GraphicsSettings.currentRenderPipeline == null) Render(camera); }
        private void Draw(ScriptableRenderContext context, Camera camera) { Render(camera); }
        private void Render(Camera camera)
        {
            if (!isActiveAndEnabled || !Data || Data.Batches == null || !camera || !SystemInfo.supportsInstancing) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            float range = camera.cameraType == CameraType.SceneView ? EditorDistance : GameDistance;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach (var batch in Data.Batches)
            {
                if (!batch.Mesh || !batch.Material || !batch.Material.enableInstancing || batch.Matrices == null || batch.Matrices.Length == 0) continue;
                float distance = Mathf.Sqrt(batch.Bounds.SqrDistance(camera.transform.position));
                if (distance > range || !GeometryUtility.TestPlanesAABB(planes,batch.Bounds)) continue;
                Mesh mesh = distance > 65f && batch.DistantMesh ? batch.DistantMesh : batch.Mesh;
                if (batch.Submesh >= mesh.subMeshCount) mesh = batch.Mesh;
                var settings = new RenderParams(batch.Material) { camera = camera, worldBounds = batch.Bounds,
                    layer = gameObject.layer, renderingLayerMask = 1u, receiveShadows = true, shadowCastingMode = distance < 65f ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    lightProbeUsage = LightProbeUsage.BlendProbes };
                Graphics.RenderMeshInstanced(settings,mesh,batch.Submesh,batch.Matrices);
            }
        }
    }
}
