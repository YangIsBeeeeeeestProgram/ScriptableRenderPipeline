using UnityEngine.Events;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    using _ = CoreEditorUtils;
    using CED = CoreEditorDrawer<RenderPipelineSettingsUI, SerializedRenderPipelineSettings>;

    class RenderPipelineSettingsUI : BaseUI<SerializedRenderPipelineSettings>
    {
        static RenderPipelineSettingsUI()
        {
            Inspector = CED.Group(
                    SectionPrimarySettings,
                    CED.space,
                    CED.Select(
                        (s, d, o) => s.lightLoopSettings,
                        (s, d, o) => d.lightLoopSettings,
                        GlobalLightLoopSettingsUI.Inspector
                        ),
                    CED.space,
                    CED.Select(
                        (s, d, o) => s.shadowInitParams,
                        (s, d, o) => d.shadowInitParams,
                        ShadowInitParametersUI.SectionAtlas
                        ),
                    CED.space,
                    CED.Select(
                        (s, d, o) => s.decalSettings,
                        (s, d, o) => d.decalSettings,
                        GlobalDecalSettingsUI.Inspector
                        )

                    );
        }

        public static readonly CED.IDrawer Inspector;

        public static readonly CED.IDrawer SectionPrimarySettings = CED.Group(
                CED.Action(Drawer_SectionPrimarySettings)
                );

        GlobalLightLoopSettingsUI lightLoopSettings = new GlobalLightLoopSettingsUI();
        GlobalDecalSettingsUI decalSettings = new GlobalDecalSettingsUI();
        ShadowInitParametersUI shadowInitParams = new ShadowInitParametersUI();

        public RenderPipelineSettingsUI()
            : base(0)
        {
        }

        public override void Reset(SerializedRenderPipelineSettings data, UnityAction repaint)
        {
            lightLoopSettings.Reset(data.lightLoopSettings, repaint);
            shadowInitParams.Reset(data.shadowInitParams, repaint);
            decalSettings.Reset(data.decalSettings, repaint);
            base.Reset(data, repaint);
        }

        public override void Update()
        {
            lightLoopSettings.Update();
            shadowInitParams.Update();
            decalSettings.Update();
            base.Update();
        }

        static void Drawer_SectionPrimarySettings(RenderPipelineSettingsUI s, SerializedRenderPipelineSettings d, Editor o)
        {
            EditorGUILayout.LabelField(_.GetContent("Render Pipeline Settings"), EditorStyles.boldLabel);
            ++EditorGUI.indentLevel;
			
			// Raytracing
            EditorGUILayout.PropertyField(d.raytracingEnabled, _.GetContent("Raytracing Enabled | Enable hardware accelerate raytracing."));

            // Lighting
            EditorGUILayout.PropertyField(d.supportShadowMask, _.GetContent("Support Shadow Mask|Enable memory (Extra Gbuffer in deferred) and shader variant for shadow mask."));
            EditorGUILayout.PropertyField(d.supportSSR, _.GetContent("Support SSR|Enable memory use by SSR effect."));
            EditorGUILayout.PropertyField(d.supportSSAO, _.GetContent("Support SSAO|Enable memory use by SSAO effect."));
            EditorGUILayout.PropertyField(d.supportSubsurfaceScattering, _.GetContent("Support Subsurface Scattering"));
            EditorGUILayout.PropertyField(d.increaseSssSampleCount, _.GetContent("Increase SSS Sample Count|This allows for better SSS quality. Warning: high performance cost, do not enable on consoles."));
            EditorGUILayout.PropertyField(d.supportVolumetrics, _.GetContent("Support volumetrics|Enable memory and shader variant for volumetric."));
            EditorGUILayout.PropertyField(d.increaseResolutionOfVolumetrics, _.GetContent("Increase resolution of volumetrics|Increase the resolution of volumetric lighting buffers. Warning: high performance cost, do not enable on consoles."));
            EditorGUILayout.PropertyField(d.supportLightLayers, _.GetContent("Support LightLayers|Enable light layers. In deferred this imply an extra render target in memory and extra cost."));
            EditorGUILayout.PropertyField(d.supportOnlyForward, _.GetContent("Support Only Forward|Remove all the memory and shader variant of GBuffer. The renderer can be switch to deferred anymore."));

            // Engine
            EditorGUILayout.PropertyField(d.supportDecals, _.GetContent("Support Decals|Enable memory and variant for decals buffer and cluster decals"));

            // MSAA is an option that is only available in full forward
            if (d.supportOnlyForward.boolValue)
            {
                EditorGUILayout.PropertyField(d.supportMSAA, _.GetContent("Support Multi Sampling Anti-Aliasing|This feature doesn't work currently."));
                if (d.supportMSAA.boolValue)
                {
                    EditorGUILayout.PropertyField(d.MSAASampleCount, _.GetContent("MSAA Sample Count|Allow to select the level of MSAA."));
                }
            }
            else
            {
                // Otherwise we force the MSAA flag to false
                d.supportMSAA.boolValue =  false;
            }

            EditorGUILayout.PropertyField(d.supportMotionVectors, _.GetContent("Support Motion Vectors|Motion vector are use for Motion Blur, TAA, temporal re-projection of various effect like SSR."));
            EditorGUILayout.PropertyField(d.supportRuntimeDebugDisplay, _.GetContent("Support runtime debug display|Remove all debug display shader variant only in the player. Allow faster build."));
            EditorGUILayout.PropertyField(d.supportDitheringCrossFade, _.GetContent("Support dithering cross fade|Remove all dithering cross fade shader variant only in the player. Allow faster build."));

            --EditorGUI.indentLevel;
        }
    }
}
