using UnityEngine;
using UnityEditor;
using System.IO;

public static class ChefJellySetup
{
    [MenuItem("Tools/Chef Jelly/Create collision test")]
    static void Create()
    {
        string[] scripts=AssetDatabase.FindAssets("ChefImpactJelly t:MonoScript");
        if(scripts.Length==0) return;
        string folder=Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(scripts[0])).Replace('\\','/');
        string path=folder+"/Chef_Jelly.fbx";
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        if(importer==null){Debug.LogError("Keep Chef_Jelly.fbx next to ChefImpactJelly.cs.");return;}
        importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;importer.isReadable=true;importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root=new GameObject("Chef Collision Test");Undo.RegisterCreatedObjectUndo(root,"Create jelly chef");
        var visual=new GameObject("Visual").transform;visual.SetParent(root.transform,false);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(visual,false);
        foreach(var animator in instance.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
        // Normalize the model to two metres, with its base at the root origin.
        var renderers=instance.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0){Object.DestroyImmediate(root);return;}
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float factor=2f/Mathf.Max(.001f,bounds.size.y);instance.transform.localScale*=factor;
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        instance.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        var rb=root.AddComponent<Rigidbody>();rb.mass=1;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.constraints=RigidbodyConstraints.FreezeRotation;
        var collider=root.AddComponent<CapsuleCollider>();collider.height=2;collider.radius=.4f;collider.center=Vector3.up;
#if UNITY_6000_0_OR_NEWER
        var rubber=new PhysicsMaterial("Chef bounce");rubber.bounceCombine=PhysicsMaterialCombine.Maximum;
#else
        var rubber=new PhysicMaterial("Chef bounce");rubber.bounceCombine=PhysicMaterialCombine.Maximum;
#endif
        rubber.bounciness=.65f;rubber.dynamicFriction=.35f;rubber.staticFriction=.35f;
        string matPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/ChefBounce.physicMaterial");AssetDatabase.CreateAsset(rubber,matPath);collider.sharedMaterial=rubber;
        var jelly=root.AddComponent<ChefImpactJelly>();jelly.visual=visual;
        // A single palette material works for all UV-mapped surfaces and render pipelines.
        string texPath=folder+"/Chef_palette.png";
        var ti=AssetImporter.GetAtPath(texPath) as TextureImporter;
        if(ti!=null){ti.sRGBTexture=false;ti.filterMode=FilterMode.Point;ti.mipmapEnabled=false;ti.SaveAndReimport();}
        Shader shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        if(shader!=null){var material=new Material(shader);material.name="Chef Ivory Palette";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);material.mainTexture=tex;
            if(material.HasProperty("_BaseColorMap"))material.SetTexture("_BaseColorMap",tex);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.12f);
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.12f);
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(folder+"/ChefPalette.mat"));
            foreach(var r in renderers){var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=material;r.sharedMaterials=slots;}
        }
        foreach(var s in instance.GetComponentsInChildren<SkinnedMeshRenderer>()){s.updateWhenOffscreen=true;Bounds b=s.localBounds;b.Expand(b.size*.7f);s.localBounds=b;}
        PrefabUtility.SaveAsPrefabAsset(root,AssetDatabase.GenerateUniqueAssetPath(folder+"/ChefImpact.prefab"));
        root.transform.position=new Vector3(0,2,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Chef Test Floor";floor.transform.position=new Vector3(0,-.25f,0);floor.transform.localScale=new Vector3(10,.5f,10);Undo.RegisterCreatedObjectUndo(floor,"Create jelly floor");
        Selection.activeGameObject=root;SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("Chef ready. Press Play: falls onto the floor, rebounds and jiggles. No Animator or animation clips required.",root);
    }
}
