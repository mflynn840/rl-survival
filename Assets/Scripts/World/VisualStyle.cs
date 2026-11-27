using UnityEngine;

namespace RLSurvival
{
    /// <summary>Shared visual language and runtime quality tiers for the roadside district.</summary>
    public static class VisualStyle
    {
        // Sun-washed coastal motel: warm plaster, faded asphalt, deep teal foliage, and coral accents.
        public static readonly Color SkyTop = new Color(.30f,.55f,.78f);
        public static readonly Color SkyHorizon = new Color(.78f,.72f,.61f);
        public static readonly Color GroundBounce = new Color(.16f,.20f,.17f);
        public static readonly Color Sun = new Color(1f,.82f,.60f);
        public static readonly Color Foliage = new Color(.18f,.38f,.25f);
        public static readonly Color MotelWarmth = new Color(1f,.53f,.24f);

        static readonly string[] labels = { "Performance", "Balanced", "High", "Ultra" };
        public static string Label(int value) { return labels[Mathf.Clamp(value,0,labels.Length-1)]; }

        public static void ApplyQuality(int preset)
        {
            preset=Mathf.Clamp(preset,0,labels.Length-1);
            string[] names=QualitySettings.names;
            int qualityIndex=names==null||names.Length==0?0:Mathf.RoundToInt((names.Length-1)*(preset/3f));
            QualitySettings.SetQualityLevel(Mathf.Clamp(qualityIndex,0,Mathf.Max(0,names.Length-1)),true);
            QualitySettings.shadowDistance=preset==0?28f:preset==1?52f:preset==2?85f:110f;
            QualitySettings.shadowResolution=preset<2?ShadowResolution.Medium:ShadowResolution.High;
            QualitySettings.antiAliasing=preset<2?0:4;
            QualitySettings.lodBias=preset==0?.55f:preset==1?.8f:preset==2?1.2f:1.55f;
        }
    }
}
