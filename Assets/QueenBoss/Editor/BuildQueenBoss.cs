#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QueenBattle.Editor
{
    public static class BuildQueenBoss
    {
        public const string Folder="Assets/QueenBoss";
        public static QueenBossBattle Configure(GameObject root)
        {
            var boss=root.AddComponent<QueenBossBattle>();
            boss.QueenPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/QueenBossVisual.prefab");
            boss.Clips=AssetDatabase.LoadAllAssetsAtPath("Assets/QueenFlightTest/Models/QueenFlight.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).OrderBy(c=>c.name).ToArray();
            boss.Spells=new[]{"Fireball","Lightning","LightBlast"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QueenFlightTest/Effects/"+n+".prefab")).ToArray();
            boss.TelegraphMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/QueenTelegraph.mat");return boss;
        }
        [MenuItem("Tools/Queen Boss/Build Boss Assets")]
        public static void BuildAssets()
        {
            var initial=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(initial.path))EditorSceneManager.SaveScene(initial,Folder+"/BatchBootstrap.unity");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var root=new GameObject("QueenBossVisual");var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QueenFlightTest/Models/QueenFlight.fbx");
                var model=UnityEngine.Object.Instantiate(source,root.transform);
                if(!model.GetComponentInChildren<Animator>())model.AddComponent<Animator>();
                foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    string name=r.name.Contains("QueenBody")?"QueenPBR":r.name.Contains("RoyalFur")?"RoyalFur":r.name.Contains("RoyalLining")?"RoyalLining":"CapePBR";
                    var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/CityExpansion/ThroneQueen/"+name+".mat");r.sharedMaterials=Enumerable.Repeat(m,r.sharedMesh.subMeshCount).ToArray();r.updateWhenOffscreen=true;
                }
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/QueenBossVisual.prefab");UnityEngine.Object.DestroyImmediate(root);
                string matPath=Folder+"/QueenTelegraph.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,matPath);}mat.color=new Color(1.5f,.25f,.06f);
                var integration=new GameObject("Queen Boss Encounter");Configure(integration);
                EditorSceneManager.SaveScene(scene,Folder+"/QueenBossIntegration.unity");AssetDatabase.SaveAssets();
            }
            finally{SceneManager.SetActiveScene(initial);EditorSceneManager.CloseScene(scene,true);}
            BuildTestArena();
        }
        static void BuildTestArena()
        {
            var initial=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var room=new GameObject("Test throne room");var seat=new GameObject("Throne Seat");seat.transform.SetParent(room.transform,false);seat.transform.position=new Vector3(0,0,1.2f);
                var calm=new GameObject("Queen Beside Throne");calm.transform.SetParent(room.transform,false);calm.transform.position=new Vector3(2,0,1.4f);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Hall Floor Support";floor.transform.SetParent(room.transform,false);floor.transform.position=new Vector3(0,-.27f,18.1f);floor.transform.localScale=new Vector3(25,.5f,36.2f);
                var arrival=new GameObject("Arrival");arrival.transform.SetParent(room.transform,false);arrival.transform.position=new Vector3(0,0,28);
                var player=GameObject.CreatePrimitive(PrimitiveType.Capsule);player.name="Player";player.tag="Player";UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());player.AddComponent<CharacterController>();player.transform.position=new Vector3(0,0,4.2f);
                var root=new GameObject("Queen Boss Encounter");var boss=Configure(root);boss.RoomRoot=room.transform;boss.ThroneSeat=seat.transform;boss.Arrival=arrival.transform;boss.FloorSupport=floor.GetComponent<BoxCollider>();boss.Player=player.transform;
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(6,4,12);camera.transform.LookAt(new Vector3(0,1,6));camera.gameObject.AddComponent<AudioListener>();camera.backgroundColor=new Color(.03f,.035f,.045f);camera.clearFlags=CameraClearFlags.SolidColor;
                var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientLight=Color.gray;
                EditorSceneManager.SaveScene(scene,Folder+"/QueenBossCombatTest.unity");
            }
            finally{SceneManager.SetActiveScene(initial);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
#endif
