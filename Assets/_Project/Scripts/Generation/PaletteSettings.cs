using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Surface colors, from the palette in ART_DIRECTION.md. Swapping this asset recolors a course.</summary>
    [CreateAssetMenu(fileName = "Palette", menuName = "Iso Golf/Palette")]
    public sealed class PaletteSettings : ScriptableObject
    {
        [SerializeField] private Color rough = new Color32(0x6A, 0x85, 0x41, 0xFF);
        [SerializeField] private Color fairwayLight = new Color32(0x8F, 0xA8, 0x5A, 0xFF);
        [SerializeField] private Color fairwayDark = new Color32(0x7E, 0x9A, 0x48, 0xFF);
        [SerializeField] private Color green = new Color32(0xA7, 0xB8, 0x6A, 0xFF);
        [SerializeField] private Color sand = new Color32(0xE8, 0xD9, 0xB5, 0xFF);
        [SerializeField] private Color water = new Color32(0x4F, 0xB0, 0xB8, 0xFF);
        [SerializeField] private Color earth = new Color32(0xBF, 0x6C, 0x43, 0xFF);

        [Header("Edge tufts")]
        [SerializeField] private Color tuftLight = new Color32(0x7E, 0x9A, 0x48, 0xFF);
        [SerializeField] private Color tuftMid = new Color32(0x6A, 0x85, 0x41, 0xFF);
        [SerializeField] private Color tuftDark = new Color32(0x56, 0x71, 0x3A, 0xFF);

        [Header("Trees")]
        [SerializeField] private Color foliageLight = new Color32(0x7E, 0x9A, 0x48, 0xFF);
        [SerializeField] private Color foliageMid = new Color32(0x56, 0x71, 0x3A, 0xFF);
        [SerializeField] private Color foliageDark = new Color32(0x34, 0x48, 0x2A, 0xFF);
        [SerializeField] private Color trunk = new Color32(0x6A, 0x41, 0x28, 0xFF);

        [Header("Clouds")]
        [SerializeField] private Color cloudLight = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color cloudShade = new Color32(0xD6, 0xDE, 0xED, 0xFF);

        [Header("Ball and hole")]
        [SerializeField] private Color ball = new Color32(0xF2, 0xEE, 0xE6, 0xFF);
        [Tooltip("Slightly transparent and cool, like cast shadows, and never black.")]
        [SerializeField] private Color ballShadow = new Color32(0x3E, 0x42, 0x60, 0x80);
        [SerializeField] private Color hole = new Color32(0x34, 0x48, 0x2A, 0xFF);
        [SerializeField] private Color flag = new Color32(0xC8, 0x42, 0x3B, 0xFF);
        [SerializeField] private Color flagPole = new Color32(0xF1, 0xEC, 0xE0, 0xFF);

        [Header("Aiming")]
        [Tooltip("The dots and the landing ring.")]
        [SerializeField] private Color aimMark = new Color32(0xF2, 0xEE, 0xE6, 0xFF);
        [SerializeField] private Color gaugeFill = new Color32(0xE6, 0xB5, 0x3A, 0xFF);
        [Tooltip("The gauge's border and its empty part. Warm and dark, and never black.")]
        [SerializeField] private Color gaugeBorder = new Color32(0x3A, 0x2E, 0x25, 0xFF);

        public Color Rough => rough;
        public Color FairwayLight => fairwayLight;
        public Color FairwayDark => fairwayDark;
        public Color Green => green;
        public Color Sand => sand;
        public Color Water => water;
        public Color Earth => earth;
        public Color TuftLight => tuftLight;
        public Color TuftMid => tuftMid;
        public Color TuftDark => tuftDark;
        public Color FoliageLight => foliageLight;
        public Color FoliageMid => foliageMid;
        public Color FoliageDark => foliageDark;
        public Color Trunk => trunk;
        public Color CloudLight => cloudLight;
        public Color CloudShade => cloudShade;
        public Color Ball => ball;
        public Color BallShadow => ballShadow;
        public Color Hole => hole;
        public Color Flag => flag;
        public Color FlagPole => flagPole;
        public Color AimMark => aimMark;
        public Color GaugeFill => gaugeFill;
        public Color GaugeBorder => gaugeBorder;
    }
}
