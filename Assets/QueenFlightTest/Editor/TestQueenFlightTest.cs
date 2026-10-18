#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace QueenFlightTest.Editor
{
    public static class TestQueenFlightTest
    {
        static double started;
        static QueenAnimationTest test;
        static bool captured, checkedRapid;
        public static void RunBatch()
        {
            try
            {
                var initial=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(string.IsNullOrEmpty(initial.path))EditorSceneManager.SaveScene(initial,BuildQueenFlightTest.Folder+"/BatchBootstrap.unity");
                BuildQueenFlightTest.Build();EditorSceneManager.OpenScene(BuildQueenFlightTest.ScenePath);
                SessionState.SetBool("QueenCleanBatch",true);EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.WriteAllText("Logs/QueenFlightTest-play-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void CaptureBatch()
        {
            EditorSceneManager.OpenScene(BuildQueenFlightTest.ScenePath);
            var t=UnityEngine.Object.FindAnyObjectByType<QueenAnimationTest>();
            var graph=UnityEngine.Playables.PlayableGraph.Create("Visual check");graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
            var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",t.Queen);graph.Play();
            var camera=Camera.main;var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
            for(int i=0;i<2;i++)
            {
                var clip=UnityEngine.Animations.AnimationClipPlayable.Create(graph,t.Clips[i==0?0:4]);output.SetSourcePlayable(clip);clip.SetTime(i==0?0:1.15);graph.Evaluate(0);
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes("Logs/QueenFlightTest-"+(i==0?"idle":"cast")+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);graph.DestroyPlayable(clip);
            }
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);graph.Destroy();EditorApplication.Exit(0);
        }
        [InitializeOnLoadMethod] static void Resume()
        {EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!SessionState.GetBool("QueenCleanBatch",false)||!EditorApplication.isPlaying)return;
            try
            {
                if(started==0)started=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-started>100)throw new TimeoutException("Queen clean test timeout: "+(test?test.Status:"no component"));
                if(!test)test=UnityEngine.Object.FindAnyObjectByType<QueenAnimationTest>();if(!test||Time.time<.2f)return;
                if(!test.Playing)throw new InvalidOperationException("Animation graph is not playing.");
                if(!checkedRapid)
                {
                    test.Play(4);test.Play(5);test.Play(2);test.ResetTest();test.AutoSequence=true;checkedRapid=true;Time.timeScale=3;
                }
                if(test.CurrentIndex==4&&test.SpawnedEffects>=1&&!captured){ScreenCapture.CaptureScreenshot("Logs/QueenFlightTest-in-game.png");captured=true;}
                if(test.CompletedClips<7)return;
                if(test.SpawnedEffects!=3)throw new InvalidOperationException("Expected exactly three spells in first sequence, got "+test.SpawnedEffects);
                if(test.CurrentIndex!=0)throw new InvalidOperationException("Sequence failed to return to idle.");
                test.AutoSequence=false;test.ResetTest();
                File.WriteAllText("Logs/QueenFlightTest-play-PASS.txt","PASS: all seven clips advanced, first idle did not trap sequence; both dodges completed; three effects fired once each; rapid clip selection and reset succeeded; sequence returned to idle.\n");
                SessionState.SetBool("QueenCleanBatch",false);Time.timeScale=1;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetBool("QueenCleanBatch",false);File.WriteAllText("Logs/QueenFlightTest-play-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
#endif
