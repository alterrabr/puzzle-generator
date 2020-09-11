using UnityEngine;

public class Piece
{
    // Puzzle piece sides shapes
    public int Top;
    public int Left;
    public int Bottom;
    public int Right;

    // Main data 
    public Texture2D Texture;
    public Vector2 Pivot;

    // Puzzle piece base offset (if texture was expanded and base  moved to accommodate convex sub-elements)
    public Rect PixelOffset;

    // Puzzle piece mask
    public float[] Mask;
    public int MaskWidth;
    public int MaskHeight;

    // Generate basic texture
    public Piece(int top, int left, int bottom, int right, int baseSize, Texture2D subElement, Color[] topPixels, Color[] leftPixels)
    {
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;

        MaskWidth = baseSize;
        MaskHeight = baseSize;

        // Expand mask canvas if there are convex sides
        if (Top == 1)
        {
            MaskHeight += subElement.height;
            PixelOffset.y = subElement.height;
        }

        if (Bottom == 1)
        {
            MaskHeight += subElement.height;
            PixelOffset.height = subElement.height;
        }

        if (Left == 1)
        {
            MaskWidth += subElement.height;
            PixelOffset.x = subElement.height;
        }

        if (Right == 1)
        {
            MaskWidth += subElement.height;
            PixelOffset.width = subElement.height;
        }

        // Create mask and fill base body of puzzle element
        Mask = new float[MaskWidth * MaskHeight];
        for (int y = MaskHeight - 1 - (int)PixelOffset.y; y > MaskHeight - 1 - (int)PixelOffset.y - baseSize; y--)
            for (int x = (int)PixelOffset.x; x < (int)PixelOffset.x + baseSize; x++)
                Mask[y * MaskWidth + x] = 1.0f;

        // Include top part (0 - flat, 1 - convex, -1 - concave)
        if (Top != 0)
            FillMask(
                (int)PixelOffset.x + (baseSize - subElement.width) / 2,
                MaskHeight - subElement.height,
                (int)PixelOffset.x + (baseSize - subElement.width) / 2 + subElement.width,
                MaskHeight,
                MaskWidth,
                ref Mask,
                topPixels,
                Top,
                Top
            );

        // Include bottom part (0 - flat, 1-convex, 2-concave)
        if (Bottom != 0)
            FillMask(
                (int)PixelOffset.x + (baseSize - subElement.width) / 2,
                0,
                (int)PixelOffset.x + (baseSize - subElement.width) / 2 + subElement.width,
                subElement.height,
                MaskWidth,
                ref Mask,
                topPixels,
                -Bottom,
                Bottom
            );

        // Include left part (0 - flat, 1-convex, 2-concave)
        if (Left != 0)
            FillMask(
                0,
                MaskHeight - (int)PixelOffset.y - baseSize + (baseSize - subElement.width) / 2,
                subElement.height,
                MaskHeight - (int)PixelOffset.y - baseSize + (baseSize - subElement.width) / 2 + subElement.width,
                MaskWidth,
                ref Mask,
                leftPixels,
                Left,
                Left
            );

        // Include right part (0 - flat, 1-convex, 2-concave)
        if (Right != 0)
            FillMask(
                MaskWidth - subElement.height,
                MaskHeight - (int)PixelOffset.y - baseSize + (baseSize - subElement.width) / 2,
                MaskWidth,
                MaskHeight - (int)PixelOffset.y - baseSize + (baseSize - subElement.width) / 2 + subElement.width,
                MaskWidth,
                ref Mask,
                leftPixels,
                -Right,
                Right
             );
    }

    // Apply simple mask
    public void ApplyMask(Color[] sourcePixels, ref Texture2D result)
    {
        int width = result.width;
        int height = result.height;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                sourcePixels[y * width + x].a *= Mask[(y * MaskHeight / height) * MaskWidth + (x * MaskWidth / width)];

        result.SetPixels(sourcePixels);
        result.Apply();
    }

    // Fill mask array in specified rectangle with alpha values from  Color[] _fill
    void FillMask(int xStart, int yStart, int width, int height, int rowWidth, ref float[] mask, Color[] fill = null, int invertion = 1, int negative = 0)
    {
        int fillPixelNum = invertion < 0 ? fill.Length - 1 : 0;

        if (negative < 0)
            for (int y = yStart; y < height; y++)
                for (int x = xStart; x < width; x++)
                {
                    mask[y * rowWidth + x] *= 1 - fill[fillPixelNum].a;
                    fillPixelNum += invertion;
                }
        else
            for (int y = yStart; y < height; y++)
                for (int x = xStart; x < width; x++)
                {
                    mask[y * rowWidth + x] = fill[fillPixelNum].a;
                    fillPixelNum += invertion;
                }
    }
}