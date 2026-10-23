#if UNITY_EDITOR
using System.IO;
using UnityEngine;

/// <summary>Batch-mode check of the facade materials: renders a few lit quads to a PNG.</summary>
public static class FacadePreview
{
    public static void Run()
    {
        var root = new GameObject("preview");
        var lg = new GameObject("sun").AddComponent<Light>();
        lg.type = LightType.Directional; lg.transform.rotation = Quaternion.Euler(35, 150, 0); lg.intensity = 1.2f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f); RenderSettings.ambientEquatorColor = new Color(0.6f, 0.62f, 0.65f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
        int i = 0;
        foreach (int s in new[] { 0, 1, 2, 4, 6, 8 })
        {
            var m = Gad.Photo.Facade(s);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.transform.SetParent(root.transform);
            q.transform.position = new Vector3((i % 3) * 1.1f, (i / 3) * -1.1f, 0);
            q.transform.rotation = Quaternion.Euler(0, 180, 0);
            bool unlit = System.Environment.GetCommandLineArgs().Length > 0 && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-unlit") >= 0;
            if (unlit) { var um = new Material(Shader.Find("Universal Render Pipeline/Unlit")); um.SetTexture("_BaseMap", m.GetTexture("_BaseMap")); q.GetComponent<Renderer>().sharedMaterial = um; }
            else q.GetComponent<Renderer>().sharedMaterial = m;
            Debug.Log($"[Preview] f{s} base={m.GetTexture("_BaseMap")?.name} {m.GetTexture("_BaseMap")?.width} n={m.GetTexture("_BumpMap")?.name} msk={m.GetTexture("_MetallicGlossMap")?.name} inst={m.GetInstanceID()}");
            i++;
        }
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.transform.position = new Vector3(1.1f, -0.55f, 2.2f); cam.transform.rotation = Quaternion.Euler(0, 180, 0);
        cam.backgroundColor = Color.gray; cam.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(1200, 800, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1200, 800, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(System.Environment.GetEnvironmentVariable("HOME"), "CODE/gad-22x/facade_preview.png"), tex.EncodeToPNG());
        Debug.Log("[Preview] done");
    }
}
#endif
