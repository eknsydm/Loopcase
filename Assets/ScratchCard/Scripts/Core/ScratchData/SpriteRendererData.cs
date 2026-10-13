using UnityEngine;

namespace ScratchCardAsset.Core.ScratchData
{
    public class SpriteRendererData : BaseData
    {
        private readonly SpriteRenderer renderer;

        public override Vector2 TextureSize => renderer.sprite.rect.size;
        protected override Vector2 Bounds => 
#if UNITY_2021_2_OR_NEWER
            renderer.localBounds.size;
#else
            renderer.bounds.size;
#endif
        
        public SpriteRendererData(Transform surface, Camera camera) : base(surface, camera)
        {
            if (surface.TryGetComponent(out renderer))
            {
                InitTriangle();
            }
        }
    }
}