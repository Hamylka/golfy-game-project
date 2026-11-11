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
        [Tooltip("Water at the rim of a pond, lighter. A facet's tone runs from here (at the bank) to Water Deep (at the middle).")]
        [SerializeField] private Color waterShallow = new Color32(0x7F, 0xCE, 0xCB, 0xFF);
        [SerializeField] private Color waterDeep = new Color32(0x3A, 0x95, 0xA3, 0xFF);
        [Tooltip("Each water facet gets one of a few fixed tones, up to this much lighter or darker (a share of the colour, 0.06 is subtle).")]
        [SerializeField, Range(0f, 0.3f)] private float waterToneVariation = 0.07f;
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
        [Tooltip("Olive crowns' light shade: the grass row's hue, a little greyer and lighter, so olives read silvery.")]
        [SerializeField] private Color oliveLight = new Color32(0x8A, 0x9E, 0x5E, 0xFF);

        [Header("Stone")]
        [SerializeField] private Color stoneLight = new Color32(0xF1, 0xEC, 0xE0, 0xFF);
        [SerializeField] private Color stoneMid = new Color32(0xDA, 0xD3, 0xC3, 0xFF);
        [SerializeField] private Color stoneDark = new Color32(0xB8, 0xB0, 0xA0, 0xFF);

        [Header("Built props (M4 PR 4.4)")]
        [Tooltip("Wood: benches, doors, buckets, rakes. The mid shade is Trunk.")]
        [SerializeField] private Color woodLight = new Color32(0x8B, 0x5A, 0x35, 0xFF);
        [SerializeField] private Color woodDark = new Color32(0x4A, 0x2C, 0x1C, 0xFF);
        [SerializeField] private Color tileLight = new Color32(0xD4, 0x87, 0x5E, 0xFF);
        [SerializeField] private Color tileDark = new Color32(0xB2, 0x66, 0x3F, 0xFF);
        [SerializeField] private Color slateLight = new Color32(0xA3, 0xA3, 0xA0, 0xFF);
        [SerializeField] private Color slateMid = new Color32(0x7F, 0x80, 0x7E, 0xFF);
        [SerializeField] private Color slateDark = new Color32(0x5C, 0x5D, 0x5C, 0xFF);
        [SerializeField] private Color metal = new Color32(0x5E, 0x62, 0x5F, 0xFF);
        [SerializeField] private Color metalDark = new Color32(0x3E, 0x42, 0x40, 0xFF);

        [Header("Trace accents (traces of human presence only)")]
        [SerializeField] private Color traceRed = new Color32(0xC8, 0x42, 0x3B, 0xFF);
        [SerializeField] private Color traceBlue = new Color32(0x3D, 0x5E, 0x9C, 0xFF);
        [SerializeField] private Color traceOffWhite = new Color32(0xF2, 0xEE, 0xE6, 0xFF);

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

        [Header("Buttons")]
        [Tooltip("A button's ring and dot. Warm and dark, never black.")]
        [SerializeField] private Color buttonMark = new Color32(0x3A, 0x2E, 0x25, 0xFF);
        [Tooltip("A soft, see-through disc behind a button, so it reads over sky and over grass.")]
        [SerializeField] private Color buttonBacking = new Color32(0xF2, 0xEE, 0xE6, 0xB0);

        public Color ButtonMark => buttonMark;
        public Color ButtonBacking => buttonBacking;
        public Color Rough => rough;
        public Color FairwayLight => fairwayLight;
        public Color FairwayDark => fairwayDark;
        public Color Green => green;
        public Color Sand => sand;
        public Color Water => water;
        public Color WaterShallow => waterShallow;
        public Color WaterDeep => waterDeep;
        public float WaterToneVariation => waterToneVariation;
        public Color Earth => earth;
        public Color TuftLight => tuftLight;
        public Color TuftMid => tuftMid;
        public Color TuftDark => tuftDark;
        public Color FoliageLight => foliageLight;
        public Color FoliageMid => foliageMid;
        public Color FoliageDark => foliageDark;
        public Color Trunk => trunk;
        public Color OliveLight => oliveLight;
        public Color StoneLight => stoneLight;
        public Color StoneMid => stoneMid;
        public Color StoneDark => stoneDark;
        public Color WoodLight => woodLight;
        public Color WoodDark => woodDark;
        public Color TileLight => tileLight;
        public Color TileDark => tileDark;
        public Color SlateLight => slateLight;
        public Color SlateMid => slateMid;
        public Color SlateDark => slateDark;
        public Color Metal => metal;
        public Color MetalDark => metalDark;
        public Color TraceRed => traceRed;
        public Color TraceBlue => traceBlue;
        public Color TraceOffWhite => traceOffWhite;
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
