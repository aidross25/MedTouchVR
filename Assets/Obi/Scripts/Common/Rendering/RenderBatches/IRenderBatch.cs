using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    public interface IRenderBatch : IComparable<IRenderBatch>
    {
        bool TryMergeWith(IRenderBatch other);
    }

    [Serializable]
    public struct RenderBatchParams
    {
        [HideInInspector] public int layer;
        public LightProbeUsage lightProbeUsage;
        public ReflectionProbeUsage reflectionProbeUsage;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public MotionVectorGenerationMode motionVectors;
        public uint renderingLayerMask;

        public RenderBatchParams(bool receiveShadow)
        {
            layer = 0;
            lightProbeUsage = LightProbeUsage.BlendProbes;
            reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            shadowCastingMode = ShadowCastingMode.On;
            receiveShadows = receiveShadow;
            motionVectors = MotionVectorGenerationMode.Camera;
            renderingLayerMask = 0xffffffff;
        }

        public RenderBatchParams(Renderer renderer)
        {
            this.layer = renderer.gameObject.layer;
            this.lightProbeUsage = renderer.lightProbeUsage;
            this.reflectionProbeUsage = renderer.reflectionProbeUsage;
            this.shadowCastingMode = renderer.shadowCastingMode;
            this.receiveShadows = renderer.receiveShadows;
            this.motionVectors = renderer.motionVectorGenerationMode;
            this.renderingLayerMask = renderer.renderingLayerMask;
        }

        public int CompareTo(RenderBatchParams param)
        {
            int cmp = layer.CompareTo(param.layer);
            if (cmp == 0) cmp = renderingLayerMask.CompareTo(param.renderingLayerMask);
            if (cmp == 0) cmp = lightProbeUsage.CompareTo(param.lightProbeUsage);
            if (cmp == 0) cmp = reflectionProbeUsage.CompareTo(param.reflectionProbeUsage);
            if (cmp == 0) cmp = shadowCastingMode.CompareTo(param.shadowCastingMode);
            if (cmp == 0) cmp = receiveShadows.CompareTo(param.receiveShadows);
            if (cmp == 0) cmp = motionVectors.CompareTo(param.motionVectors);

            return cmp;
        }

        public RenderParams ToRenderParams()
        {
            var renderParams = new RenderParams();

            // URP and HDRP don't work without this line.
            renderParams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;

            renderParams.lightProbeUsage = lightProbeUsage;
            renderParams.reflectionProbeUsage = reflectionProbeUsage;
            renderParams.shadowCastingMode = shadowCastingMode;
            renderParams.receiveShadows = receiveShadows;
            renderParams.motionVectorMode = motionVectors;
            renderParams.renderingLayerMask = renderingLayerMask;
            renderParams.layer = layer;
            return renderParams;
        }
    }
}