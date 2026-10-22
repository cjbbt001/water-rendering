using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    public class WaterTerrainDepthFeature : ScriptableRendererFeature
    {
        public Material material;
        DepthPass _pass;
        public override void Create() { _pass = new DepthPass { renderPassEvent = RenderPassEvent.AfterRenderingOpaques }; }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!material) return;
            _pass.material = material;
            renderer.EnqueuePass(_pass);
        }
        class DepthPass : ScriptableRenderPass
        {
            public Material material;
            struct Draw { public Mesh mesh; public Matrix4x4 matrix; }
            class PassData { public Material material; public List<Draw> draws; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                using (var builder = graph.AddRasterRenderPass<PassData>("Capture Surface Terrain Height", out var data))
                {
                    data.material = material;
                    data.draws = new List<Draw>();
                    foreach (var renderer in Object.FindObjectsByType<MeshRenderer>())
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                            (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter && filter.sharedMesh)
                            data.draws.Add(new Draw { mesh = filter.sharedMesh, matrix = renderer.localToWorldMatrix });
                    }
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        foreach (var draw in pass.draws)
                            for (int submesh = 0; submesh < draw.mesh.subMeshCount; submesh++)
                                context.cmd.DrawMesh(draw.mesh, draw.matrix, pass.material, submesh, 0);
                    });
                }
            }
        }
    }
}
