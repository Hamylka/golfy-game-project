using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Surface colors, from the palette in ART_DIRECTION.md. Swapping this asset recolors a course.</summary>
    [CreateAssetMenu(fileName = "Palette", menuName = "Iso Golf/Palette")]
    public sealed class PaletteSettings : ScriptableObject
    {
        [SerializeField] private Color rough = new Color32(0x56, 0x71, 0x3A, 0xFF);
        [SerializeField] private Color fairwayLight = new Color32(0x8F, 0xA8, 0x5A, 0xFF);
        [SerializeField] private Color fairwayDark = new Color32(0x7E, 0x9A, 0x48, 0xFF);
        [SerializeField] private Color green = new Color32(0xA7, 0xB8, 0x6A, 0xFF);
        [SerializeField] private Color sand = new Color32(0xE8, 0xD9, 0xB5, 0xFF);
        [SerializeField] private Color water = new Color32(0x4F, 0xB0, 0xB8, 0xFF);
        [SerializeField] private Color earth = new Color32(0xBF, 0x6C, 0x43, 0xFF);

        public Color Rough => rough;
        public Color FairwayLight => fairwayLight;
        public Color FairwayDark => fairwayDark;
        public Color Green => green;
        public Color Sand => sand;
        public Color Water => water;
        public Color Earth => earth;
    }
}
