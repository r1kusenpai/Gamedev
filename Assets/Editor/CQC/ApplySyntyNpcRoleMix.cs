#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class ApplySyntyNpcRoleMix
 {
  const string Request="Assets/Editor/CQC/ApplySyntyNpcRoleMix-v1.request.txt";static double next;
  [Serializable]class Outfit{public string Role,Name,TexturePath;public string[] Parts;}
  [Serializable]class Outfits{public Outfit[] Items;}
  static ApplySyntyNpcRoleMix(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ApplySyntyNpcRoleMix-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/NPC/Apply Requested Six Character Groups")]
  public static void Apply()
  {
   var root=GameObject.Find("Synty Walking Castle And City Population 750");if(!root)throw new InvalidOperationException("750 NPC population missing.");var crowd=root.GetComponent<CityCrowdManager>();if(crowd.People.Length!=750)throw new InvalidOperationException("Expected 750 NPCs.");AssetDatabase.Refresh();var outfits=JsonUtility.FromJson<Outfits>(File.ReadAllText("Assets/CityExpansion/SyntyNpcRoles/RoleOutfits.json"));if(outfits.Items.Length!=12)throw new InvalidOperationException("Twelve role outfits required.");
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets/Chr_FantasyHero_Preset_1.prefab");var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CityExpansion/SyntyNpcPopulation1500/CityWalkIdle.controller");var walk=AssetDatabase.LoadAllAssetsAtPath("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));if(!source||!controller)throw new InvalidOperationException("Source character and walking controller required.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-npc-role-mix-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/CityExpansion/SyntyNpcRoles/Generated-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();var original=crowd.People.Select(p=>p.Position).ToArray();var appearances=new List<CityCrowdAppearance>();var materials=new Dictionary<string,Material>();
   for(int index=0;index<outfits.Items.Length;index++)
   {
    var outfit=outfits.Items[index];var visual=(GameObject)PrefabUtility.InstantiatePrefab(source);
    try
    {
     PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);visual.name=outfit.Name;var wanted=new HashSet<string>(outfit.Parts);var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);foreach(var r in renderers)r.gameObject.SetActive(false);foreach(string part in wanted){var r=renderers.FirstOrDefault(r=>r.name==part);if(!r)throw new InvalidOperationException("Modular character part missing: "+part);for(var t=r.transform;t&&t!=visual.transform;t=t.parent)t.gameObject.SetActive(true);}
     foreach(var r in renderers)if(!wanted.Contains(r.name))UnityEngine.Object.DestroyImmediate(r.gameObject);foreach(var t in visual.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))if(script)UnityEngine.Object.DestroyImmediate(script);foreach(var col in visual.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);
     var animator=visual.GetComponentInChildren<Animator>();if(!animator||!animator.avatar||!animator.avatar.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Role outfit has no valid Humanoid avatar.");animator.runtimeAnimatorController=controller;animator.fireEvents=false;animator.applyRootMotion=false;
     if(!materials.TryGetValue(outfit.Role,out var material)){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(outfit.TexturePath);if(!texture)throw new InvalidOperationException("Role palette missing: "+outfit.TexturePath);material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="NPC "+outfit.Role,enableInstancing=true};material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",outfit.Role=="Knights"?.3f:.18f);AssetDatabase.CreateAsset(material,folder+"/"+outfit.Role+".mat");materials.Add(outfit.Role,material);}
     foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>material).ToArray();r.updateWhenOffscreen=false;}
     var bounds=BoundsOf(visual);visual.transform.localScale*=1.75f/bounds.size.y;bounds=BoundsOf(visual);visual.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);var skin=PrefabUtility.SaveAsPrefabAsset(visual,folder+"/"+outfit.Name+".prefab");var appearance=TestSyntyCityPopulation1500.BakeAppearance(visual,animator,walk,skin,200+index,folder);appearance.Role=outfit.Role;appearances.Add(appearance);
    }
    finally{UnityEngine.Object.DestroyImmediate(visual);}
   }
   string[] roles={"Knights","Guards","Adventurers","Rogues","Mages","Townspeople"};int[] counts={60,60,255,60,60,255};var assignments=new List<int>();for(int role=0;role<roles.Length;role++){var skins=Enumerable.Range(0,appearances.Count).Where(i=>appearances[i].Role==roles[role]).ToArray();for(int n=0;n<counts[role];n++)assignments.Add(skins[n%skins.Length]);}var random=new System.Random(74129);for(int i=assignments.Count-1;i>0;i--){int j=random.Next(i+1);int value=assignments[i];assignments[i]=assignments[j];assignments[j]=value;}
   Undo.RecordObject(crowd,"Apply requested NPC role percentages");crowd.Appearances=appearances.ToArray();crowd.DetailedPerAppearance=6;for(int i=0;i<crowd.People.Length;i++){crowd.People[i].Skin=assignments[i];crowd.People[i].Role=appearances[assignments[i]].Role;if(crowd.People[i].Position!=original[i])throw new InvalidOperationException("NPC placement changed.");}
   for(int i=0;i<roles.Length;i++)if(crowd.People.Count(p=>p.Role==roles[i])!=counts[i])throw new InvalidOperationException("Role distribution mismatch.");if(crowd.EditorPreview)Undo.DestroyObjectImmediate(crowd.EditorPreview);var preview=new GameObject("Six NPC Groups Preview - Hidden During Play");Undo.RegisterCreatedObjectUndo(preview,"Update NPC role previews");preview.transform.SetParent(root.transform,false);crowd.EditorPreview=preview;foreach(int i in Enumerable.Range(0,48).Select(i=>i*750/48)){var p=crowd.People[i];var g=new GameObject(p.Role+" NPC Preview "+i);g.transform.SetParent(preview.transform,false);g.transform.SetPositionAndRotation(p.Position,Quaternion.Euler(0,p.Yaw,0));var a=appearances[p.Skin];for(int part=0;part<a.PoseA.Length;part++){var piece=new GameObject("Walking Outfit "+part);piece.transform.SetParent(g.transform,false);piece.AddComponent<MeshFilter>().sharedMesh=a.PoseA[part];piece.AddComponent<MeshRenderer>().sharedMaterial=a.Materials[part];}}
   EditorUtility.SetDirty(crowd);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ApplySyntyNpcRoleMix-report.txt","750 NPC role mix installed and counts verified: Knights 60 (8%), Guards 60 (8%), Adventurers 255 (34%), Rogues 60 (8%), Mages 60 (8%), Townspeople 255 (34%). Twelve customized Synty Humanoid outfits, male/female variant per role. Clothing/armor meshes and palette colors selected per role. Existing NPC positions, routes, boarding controller and five-second funicular timetable preserved. Nearby animated pool remains capped at 72. Live appearance review still required.\nBackup: "+backup);Debug.Log("Requested six-group NPC mix applied: 60/60/255/60/60/255. Scene saved.");SceneView.RepaintAll();
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
