using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Rendering;
using DemonViglu.FirePlay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>Editor authoring only. Preserves existing gameplay identities and character assets.</summary>
    public static class SnowValleyEnvironmentRebuild
    {
        private const string ScenePath = "Assets/Scenes/SnowValley_Playable.unity";
        private const string Output = "Assets/FirePlay/Art/SnowValleyRebuild";
        private const string Nature = "Assets/Resources/Art/Ultimate Nature Pack - Jun 2019-20260728T054020Z-1-001/Ultimate Nature Pack - Jun 2019/FBX/";
        private static readonly List<Vector3> Pads = new();
        private static readonly List<Renderer> GroundRenderers = new();
        private static readonly string[] ReplacedGroups = {
            "00_Snow_Grand_Ground", "01_Snow_Terrain_Layers", "03_North_Canyon",
            "03_Snow_Landmarks", "04_Sparse_Snow_Tree_Groves",
            "02_Long_Stone_Bridge", "08_South_Canyon_Crossing"
        };

        [MenuItem("FirePlay/Environment/Rebuild Snow Valley Landscape")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (current.isDirty) throw new InvalidOperationException("Save current scene edits before rebuilding.");
            Directory.CreateDirectory("Authoring/EnvironmentRebuild");
            const string backup = "Authoring/EnvironmentRebuild/SnowValley_BeforeRebuild.unity.bak";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            Folder("Assets/FirePlay/Art", "SnowValleyRebuild");
            Folder(Output, "Meshes");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(o => o.name == "Envir");
            foreach (var child in environment.transform.Cast<Transform>().ToArray())
                if (ReplacedGroups.Contains(child.name)) child.gameObject.SetActive(false);
            var lakes=environment.transform.Cast<Transform>().FirstOrDefault(t=>t.name=="02_Frozen_Lakes");
            if(lakes!=null)
                foreach(Transform lake in lakes)
                {
                    if(lake.position.y>15){lake.gameObject.SetActive(false);continue;}
                    var renderers=lake.GetComponentsInChildren<Renderer>(true);
                    if(renderers.Length==0)continue;
                    var bounds=renderers[0].bounds;
                    foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    if(bounds.center.y>15)lake.gameObject.SetActive(false);
                }
            var previous = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "SnowValley_RebuiltEnvironment");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            var root = new GameObject("SnowValley_RebuiltEnvironment");
            Pads.Clear(); GroundRenderers.Clear();
            foreach (var anchor in UnityEngine.Object.FindObjectsByType<ActivityAnchorNode>())
                Pads.Add(anchor.transform.position - Vector3.up * .12f);
            foreach(var source in UnityEngine.Object.FindObjectsByType<FlameSource>()) Pads.Add(source.transform.position);
            foreach(var tree in UnityEngine.Object.FindObjectsByType<WorldTreeContribution>()) Pads.Add(tree.transform.position);
            // Preserve the original interactive lakes and their shoreline elevations.

            var snowTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/FirePlay/LookDev/Materials/WarmthSnow.mat");
            var snow = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Snow.mat");
            if (snow == null) { snow = new Material(snowTemplate); AssetDatabase.CreateAsset(snow, Output + "/Snow.mat"); }
            snow.SetColor("_BaseColor", new Color(.85f, .92f, .97f));
            snow.SetFloat("_Smoothness", .22f);
            var rock = Lit("AlpineRock", new Color(.30f, .37f, .43f));
            var path = Lit("PackedSnow", new Color(.66f, .77f, .83f));
            if(lakes!=null)
            {
                foreach(Transform lake in lakes)
                {
                    if(lake.name=="MainLake_PhysicalBasin")
                        foreach(var r in lake.GetComponentsInChildren<Renderer>())r.enabled=false;
                    if(lake.name!="MainLake_ColdWater")continue;
                    var waterVerts=new List<Vector3>{Vector3.zero};var waterTris=new List<int>();
                    for(var i=0;i<128;i++){var a=i*Mathf.PI*2/128;waterVerts.Add(new Vector3(Mathf.Cos(a)*35,0,Mathf.Sin(a)*53));}
                    for(var i=0;i<128;i++)waterTris.AddRange(new[]{0,(i+1)%128+1,i+1});
                    var waterMesh=new Mesh{name="MainLakeWater"};waterMesh.SetVertices(waterVerts);waterMesh.SetTriangles(waterTris,0);waterMesh.RecalculateNormals();
                    lake.GetComponent<MeshFilter>().sharedMesh=SaveMesh(waterMesh,"MainLakeWater");
                    lake.localScale=Vector3.one;
                    var water=Lit("GlacialWater",new Color(.22f,.48f,.59f));water.SetFloat("_Smoothness",.82f);EditorUtility.SetDirty(water);
                    lake.GetComponent<MeshRenderer>().sharedMaterial=water;
                }
            }
            var terrainRoot = Child(root.transform, "01_ContinuousAlpineTerrain");
            for (var z = 0; z < 8; z++)
                for (var x = 0; x < 8; x++) BuildTile(terrainRoot, x, z, snow, rock);
            BuildTrail(root.transform, path);
            Populate(root.transform);
            foreach (var receiver in UnityEngine.Object.FindObjectsByType<WarmthSnowReceiver>())
            {
                var so = new SerializedObject(receiver);
                var targets = so.FindProperty("_targetRenderers");
                targets.arraySize = GroundRenderers.Count;
                for (var i = 0; i < GroundRenderers.Count; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = GroundRenderers[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var sun = environment.GetComponentsInChildren<Light>().FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.name == "Snow_Sun");
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(27, -38, 0);
                sun.color = new Color(1, .85f, .68f); sun.intensity = 1.35f;
                sun.shadows=LightShadows.Soft;
            }
            foreach(var fill in environment.GetComponentsInChildren<Light>())
                if(fill.type==LightType.Directional && fill!=sun)fill.intensity=.18f;
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Output+"/AlpineSky.mat");
            if(sky==null){sky=new Material(Shader.Find("FirePlay/Environment/AlpineSky"));AssetDatabase.CreateAsset(sky,Output+"/AlpineSky.mat");}
            sky.shader=Shader.Find("FirePlay/Environment/AlpineSky");
            sky.SetColor("_TopColor",new Color(.18f,.38f,.61f));sky.SetColor("_HorizonColor",new Color(.68f,.81f,.88f));
            sky.SetColor("_GroundColor",new Color(.49f,.62f,.72f));RenderSettings.skybox=sky;
            EditorUtility.SetDirty(sky);EditorUtility.SetDirty(snow);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.63f, .76f, .84f);
            RenderSettings.fogDensity = .00135f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.53f, .68f, .82f);
            RenderSettings.ambientEquatorColor = new Color(.37f, .46f, .56f);
            RenderSettings.ambientGroundColor = new Color(.23f, .29f, .36f);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Capture();
            Debug.Log("SnowValley environment rebuilt: 1 km terrain, forest groves, alpine trail. Original character retained.");
        }

        private static float Peak(float x, float z, float cx, float cz, float width, float height)
        {
            var dx=(x-cx)/width; var dz=(z-cz)/(width*.8f);
            var radius=Mathf.Sqrt(dx*dx+dz*dz);
            var angle=Mathf.Atan2(dz,dx);
            var ridgeWeight=Mathf.SmoothStep(0,1,Mathf.Clamp01(radius/.4f));
            var ridges=1-.12f*(1-Mathf.Cos(angle*5+radius*3))*ridgeWeight;
            var folds=.86f+.22f*Mathf.PerlinNoise(x*.024f+45,z*.024f+19);
            return height*Mathf.Exp(-Mathf.Pow(radius,1.7f)*1.65f)*ridges*folds;
        }
        private static float Height(float x, float z)
        {
            var n = Mathf.PerlinNoise(x*.007f+31,z*.007f+64);
            var detail = Mathf.PerlinNoise(x*.032f+73,z*.032f+26);
            var h=Peak(x,z,-240,170,130,128)+Peak(x,z,235,225,150,165)
                +Peak(x,z,-365,340,150,233)+Peak(x,z,65,400,150,215)
                +Peak(x,z,355,385,120,235)+Peak(x,z,-345,-180,170,105)
                +Peak(x,z,320,-190,150,90)+Peak(x,z,-70,-390,180,112);
            h += (n-.45f)*15 + (detail-.5f)*Mathf.Clamp(h*.16f, .3f, 16);
            var radius = new Vector2(x,z).magnitude;
            var central = Mathf.SmoothStep(0,1,Mathf.InverseLerp(35,115,radius));
            h=Mathf.Lerp(.20f,h,central);
            // Walkable, winding northward route through the low saddle.
            var routeX = -28+35*Mathf.Sin(z*.011f);
            var routeWeight = Mathf.Exp(-Mathf.Pow((x-routeX)/19,2));
            if (z>45 && z<330) h=Mathf.Lerp(h, .2f+(z-45)*.10f,routeWeight*.94f);
            foreach(var p in Pads)
            {
                var d=Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.z));
                var size=p.y>10?42:9;
                var influence=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(size,size+24,d));
                h=Mathf.Lerp(h,p.y,influence);
            }
            // Lower the ground beneath the main lake without placing a snow lid over its ice.
            var lake=Mathf.Sqrt(Mathf.Pow((x-56)/35,2)+Mathf.Pow((z-27)/53,2));
            if(lake<1.08f) h=Mathf.Lerp(-13f,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1.08f,lake)));
            return h;
        }
        private static void BuildTile(Transform parent,int tx,int tz,Material snow,Material rock)
        {
            const int steps=48;
            var verts=new Vector3[(steps+1)*(steps+1)];var uv=new Vector2[verts.Length];
            var white=new List<int>();var stone=new List<int>();
            for(var z=0;z<=steps;z++) for(var x=0;x<=steps;x++)
            {
                var wx=-512+tx*128+x*128f/steps;var wz=-512+tz*128+z*128f/steps;
                var i=z*(steps+1)+x;verts[i]=new Vector3(wx,Height(wx,wz),wz);uv[i]=new Vector2(wx,wz)*.035f;
            }
            for(var z=0;z<steps;z++) for(var x=0;x<steps;x++)
            {
                var a=z*(steps+1)+x;var b=a+steps+1;
                var steep=Vector3.Angle(Vector3.Cross(verts[b]-verts[a],verts[a+1]-verts[a]),Vector3.up);
                var target=steep>56?stone:white;
                target.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
            var mesh=new Mesh{name=$"Alpine_{tx}_{tz}"};mesh.vertices=verts;mesh.uv=uv;
            mesh.subMeshCount=2;mesh.SetTriangles(white,0);mesh.SetTriangles(stone,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            // Sample the same height field across tile borders to prevent lighting seams.
            var normals=new Vector3[verts.Length];
            for(var i=0;i<verts.Length;i++)
            {var v=verts[i];normals[i]=new Vector3(Height(v.x-1,v.z)-Height(v.x+1,v.z),2,Height(v.x,v.z-1)-Height(v.x,v.z+1)).normalized;}
            mesh.normals=normals;
            mesh=SaveMesh(mesh,$"Terrain_{tx}_{tz}");
            var go=Child(parent,$"Terrain_{tx}_{tz}").gameObject;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{snow,rock};GroundRenderers.Add(renderer);
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
            go.AddComponent<CampfirePlacementSurface>();
        }
        private static void BuildTrail(Transform root,Material material)
        {
            var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            for(var i=0;i<=150;i++)
            {
                var z=10+i*2f;var x=-28+35*Mathf.Sin(z*.011f);
                for(var edge=0;edge<2;edge++)
                {var xx=x+(edge==0?-2.1f:2.1f);verts.Add(new Vector3(xx,Height(xx,z)+.055f,z));uv.Add(new Vector2(edge,i*.2f));}
                if(i>0){var a=(i-1)*2;tris.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
            var mesh=new Mesh{name="NorthwardTrail"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            var go=Child(root,"02_NorthwardSnowTrail").gameObject;go.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"Trail");go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static void Populate(Transform root)
        {
            var trees=Child(root,"03_AlpineGroves");var rocks=Child(root,"04_RockOutcrops");
            var random=new System.Random(170926);
            var centers=new[]{new Vector2(-65,50),new Vector2(-110,130),new Vector2(120,115),new Vector2(150,-85),new Vector2(-120,-90),new Vector2(-65,240),new Vector2(115,280)};
            for(var i=0;i<245;i++)
            {
                var center=centers[i%centers.Length];var angle=(float)random.NextDouble()*Mathf.PI*2;var r=Mathf.Sqrt((float)random.NextDouble())*43;
                var x=center.x+Mathf.Cos(angle)*r;var z=center.y+Mathf.Sin(angle)*r;
                if(Mathf.Abs(x-(-28+35*Mathf.Sin(z*.011f)))<7)continue;
                if(x>12&&x<103&&z>-34&&z<87)continue;
                Place(Nature+$"PineTree_Snow_{new[]{1,3,5}[i%3]}.fbx",trees,new Vector3(x,Height(x,z),z),5.5f+(float)random.NextDouble()*6,(float)random.NextDouble()*360);
            }
            for(var i=0;i<80;i++)
            {
                var x=(float)random.NextDouble()*500-250;var z=(float)random.NextDouble()*450-180;
                if(new Vector2(x,z).magnitude<40 || (x>10&&x<110&&z>-35&&z<90))continue;
                Place(Nature+$"Rock_Snow_{(i%2==0?1:3)}.fbx",rocks,new Vector3(x,Height(x,z)-.2f,z),2+(float)random.NextDouble()*5,(float)random.NextDouble()*360);
            }
        }
        private static void Place(string path,Transform parent,Vector3 position,float height,float yaw)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new InvalidOperationException(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(parent,false);
            go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0)*Quaternion.Euler(-90,0,0);
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.position+=Vector3.up*(position.y-bounds.min.y);
        }
        private static void Capture()
        {
            var go=new GameObject("AuthoringPreviewCamera");var camera=go.AddComponent<Camera>();
            go.transform.position=new Vector3(145,135,-220);go.transform.LookAt(new Vector3(0,30,110));
            camera.fieldOfView=55;camera.farClipPlane=1800;
            var target=new RenderTexture(1600,900,24);camera.targetTexture=target;
            var previous=RenderTexture.active;
            try{camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("Authoring/EnvironmentRebuild/SnowValley_Overview.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);}
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            var path=Output+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            existing.Clear();existing.vertices=mesh.vertices;existing.uv=mesh.uv;existing.normals=mesh.normals;
            existing.subMeshCount=mesh.subMeshCount;
            for(var i=0;i<mesh.subMeshCount;i++)existing.SetTriangles(mesh.GetTriangles(i),i);
            existing.RecalculateBounds();EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }
        private static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static void Folder(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
        private static Material Lit(string name,Color color)
        {
            var path=Output+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(mat);return mat;
        }
    }
}
