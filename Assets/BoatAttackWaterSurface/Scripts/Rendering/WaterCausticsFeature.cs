// BoatAttack's caustics projection, adapted to URP 17.6 RenderGraph.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    public class WaterCausticsFeature : ScriptableRendererFeature
    {
        public Material material;
        CausticsPass _pass;

        public override void Create()
        {
            _pass = new CausticsPass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!material || renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection) return;
            _pass.material = material;
            renderer.EnqueuePass(_pass);
        }

        class CausticsPass : ScriptableRenderPass
        {
            public Material material;
            class PassData { public Material material; public Matrix4x4 sun; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                using (var builder = graph.AddRasterRenderPass<PassData>("BoatAttack Surface Caustics", out var data))
                {
                    data.material = material;
                    data.sun = RenderSettings.sun ? RenderSettings.sun.transform.localToWorldMatrix : Matrix4x4.identity;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        pass.material.SetMatrix("_MainLightDir", pass.sun);
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
