using UnityEngine;

internal static class PaperLegendUiSpriteFactory
{
    private static Sprite _solidSprite;

    public static Sprite GetSolidSprite()
    {
        if (_solidSprite != null)
            return _solidSprite;

        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        texture.SetPixels(pixels);
        texture.Apply();

        _solidSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        return _solidSprite;
    }
}
