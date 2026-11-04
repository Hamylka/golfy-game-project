using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Marks the camera that draws no world and only shows the low-res image on screen.
    /// <see cref="PixelPresentFeature"/> runs on this camera and reads the image from <see cref="Source"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelPresenter : MonoBehaviour
    {
        [SerializeField] private PixelCamera source;

        public PixelCamera Source => source;
    }
}
