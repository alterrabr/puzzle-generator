using UnityEngine;
using UnityEditor;
using System.IO;

public class PuzzleGenerator : MonoBehaviour
{
    // Main puzzle image
    [SerializeField]
    private Texture2D image;

    // Sub-elements generation
    [SerializeField]
    private Texture2D subElement;

    // Custom material
    [SerializeField]
    private Material material;

    // Grid cols number
    [SerializeField]
    private int cols = 2;

    // Grid rows number
    [SerializeField]
    private int rows = 2;

    // Size of puzzle piece base, px
    [SerializeField]
    private int elementBaseSize = 256;

    // Max Atlas size, px
    private static int maxAtlasSize = 4096;

    // Sprites resolution
    private static int pixelsPerUnit = 200;

    // Atlas and shadow settings
    private static string texturePath = "Assets/_Atlas.png";
    private static bool useShadows = true;
    private static Vector3 shadowOffset = new Vector3(0.01f, -0.01f, 1);
    private static Color shadowColor = new Color(0, 0, 0, 0.5f);

    // Atlas variables
    private Rect[] atlasRects;
    private Texture2D atlas;

    // Contatins data about whole puzzle
    private Piece[] puzzleGrid;
    private PuzzleController puzzle;


    private void Awake()
    {
        if (image && subElement)
        {
            CreatePuzzle();
        }
    }

    // Aggregate function, that processes whole generation
    void CreatePuzzle()
    {
        Random.InitState(System.DateTime.Now.Millisecond);

        puzzleGrid = new Piece[cols * rows];

        try
        {
            image = PrepareAsset(image);
            GeneratePuzzlePieces(cols, rows, subElement, elementBaseSize, image);
            CreateAtlas();
            ConvertToSprites();
            puzzle = CreateGameObjects().AddComponent<PuzzleController>();
            CreateGameObjects();
            puzzle.Prepare();

            //if (generateBackground)
            //    puzzle.GenerateBackground(image);

        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Error! \n \n", ex.Message, "Ok");
        }
    }


    //Main input image check
    public static Texture2D PrepareAsset(Texture2D _source)
    {
        string texturePath = AssetDatabase.GetAssetPath(_source);

        //Modify the importer settings
        TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (!textureImporter.isReadable)
        {
            //Texture should be readable
            textureImporter.isReadable = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;

            AssetDatabase.WriteImportSettingsIfDirty(texturePath);
            AssetDatabase.ImportAsset(texturePath);
            AssetDatabase.Refresh();

            return _source;
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }


    //Generate puzzle-pieces textures and order them in puzzleGrid
    Vector2 GeneratePuzzlePieces(int _cols, int _rows, Texture2D _subElement, int _elementBaseSize, Texture2D _image)
    {
        int top, left, bottom, right;

        //Calculate piece aspect-ratio accordingly to image size    
        Vector2 elementSizeRatio = new Vector2(_image.width / (float)_cols / elementBaseSize, _image.height / (float)_rows / _elementBaseSize);

        //Prepare sub-element variants
        Color[] subElementPixels = _subElement.GetPixels();
        Color[] topPixels = subElementPixels;
        Color[] leftPixels = Rotate90(subElementPixels, _subElement.width, _subElement.height, false);

        //Generation													                                          
        for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                //Calculate shape - which type/variant of sub-elements should be  used for top/left/bottom/right parts of piece (accordingly to shapes of surrounding puzzle-pieces) 
                //(0 - flat, 1-convex, 2-concave)	
                top = y > 0 ? -puzzleGrid[((y - 1) * _cols + x)].Bottom : 0;
                left = x > 0 ? -puzzleGrid[(y * _cols + x - 1)].Right : 0;
                bottom = y < (_rows - 1) ? Random.Range(-1, 1) * 2 + 1 : 0;
                right = x < (_cols - 1) ? Random.Range(-1, 1) * 2 + 1 : 0;

                //Prepare element mask 
                puzzleGrid[y * _cols + x] = new Piece(
                                                    top, left, bottom, right,
                                                    _elementBaseSize,
                                                    _subElement,
                                                    topPixels, leftPixels
                                                    );

                //Extract and mask image-piece to be used as puzzle piece texture
                puzzleGrid[y * _cols + x].Texture = ExtractFromImage(_image, puzzleGrid[y * _cols + x], x, y, _elementBaseSize, elementSizeRatio);

                //Set pivot to Left-Top corner of puzzle piece base
                puzzleGrid[y * _cols + x].Pivot = new Vector2(
                                                            ((float)puzzleGrid[y * _cols + x].PixelOffset.x / puzzleGrid[y * _cols + x].Texture.width * elementSizeRatio.x),
                                                            (1.0f - (float)puzzleGrid[y * _cols + x].PixelOffset.y / puzzleGrid[y * _cols + x].Texture.height * elementSizeRatio.y)
                                                            );
            }

        return elementSizeRatio;
    }


    //Pack puzzle pieces to atlas 
    void CreateAtlas()
    {
        //Import all textures to textureArray
        Texture2D[] textureArray = new Texture2D[rows * cols];
        for (int i = 0; i < textureArray.Length; i++)
            textureArray[i] = puzzleGrid[i].Texture;

        //Make a new atlas texture
        atlas = new Texture2D(1, 1);
        atlasRects = atlas.PackTextures(textureArray, 3);

        //Scale atlas if source bigger than chosen atlas size  
        if (maxAtlasSize < atlas.width || maxAtlasSize < atlas.height)
            if (atlas.width == atlas.height)
                atlas = Scale(atlas, maxAtlasSize, maxAtlasSize);
            else
                if (atlas.width > atlas.height)
                atlas = Scale(atlas, maxAtlasSize, Mathf.RoundToInt(atlas.height / (atlas.width / (float)maxAtlasSize)));
            else
                atlas = Scale(atlas, Mathf.RoundToInt(atlas.width / (atlas.height / (float)maxAtlasSize)), maxAtlasSize);


        byte[] atlasPng = atlas.EncodeToPNG();

        if (File.Exists(texturePath))
        {
            File.Delete(texturePath);
            AssetDatabase.Refresh();
        }

        File.WriteAllBytes(texturePath, atlasPng);

        AssetDatabase.ImportAsset(texturePath);
        AssetDatabase.Refresh();
    }


    //Convert atlas texture to Multiple sprite sheet		
    void ConvertToSprites()
    {
        //Create and initialize sprites
        SpriteMetaData[] sprites = new SpriteMetaData[atlasRects.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].alignment = (int)SpriteAlignment.Custom;
            sprites[i].name = "piece_" + i.ToString();
            sprites[i].pivot = new Vector2(puzzleGrid[i].Pivot.x, puzzleGrid[i].Pivot.y);
            sprites[i].rect = new Rect(atlasRects[i].x * atlas.width, atlasRects[i].y * atlas.height, atlasRects[i].width * atlas.width, atlasRects[i].height * atlas.height);
        }

        //Modify the importer settings
        TextureImporter atlasTextureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        atlasTextureImporter.isReadable = true;
        atlasTextureImporter.mipmapEnabled = false;
        atlasTextureImporter.alphaIsTransparency = true;
        //atlasTextureImporter.maxTextureSize = maxAtlasSize;
        atlasTextureImporter.wrapMode = TextureWrapMode.Clamp;
        atlasTextureImporter.filterMode = FilterMode.Trilinear;
        atlasTextureImporter.spritePixelsPerUnit = pixelsPerUnit;
        atlasTextureImporter.textureType = TextureImporterType.Sprite;
        atlasTextureImporter.spriteImportMode = SpriteImportMode.Multiple;
        atlasTextureImporter.spritesheet = sprites;

        AssetDatabase.WriteImportSettingsIfDirty(texturePath);
        AssetDatabase.ImportAsset(texturePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }


    //Generate puzzle pieces gameObjects and compose them in the scene
    GameObject CreateGameObjects()
    {
        //Load sprites assets
        Object[] spritePieces = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        ArrayUtility.RemoveAt(ref spritePieces, 0);

        //Calculate final sprite size, taking into account pixelsPerUnit and possible atlas scale
        Vector2 spriteBaseSize = new Vector2(
                                                image.width / (float)cols / pixelsPerUnit / (puzzleGrid[0].Texture.width / (spritePieces[0] as Sprite).rect.width),
                                                image.height / (float)rows / pixelsPerUnit / (puzzleGrid[0].Texture.height / (spritePieces[0] as Sprite).rect.height)
                                            );

        GameObject puzzle = new GameObject();
        GameObject piece;
        GameObject shadow;
        SpriteRenderer spriteRenderer;
        SpriteRenderer shadowRenderer;


        puzzle.name = "Puzzle_" + image.name + "_" + cols.ToString() + "x" + rows.ToString();

        //Go through array and create gameObjects
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                //Generate sprite
                piece = new GameObject();
                piece.name = "piece_" + x.ToString() + "x" + y.ToString();
                piece.transform.SetParent(puzzle.transform);

                piece.transform.position = new Vector3(x * spriteBaseSize.x, -y * spriteBaseSize.y, 0);

                spriteRenderer = piece.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = spritePieces[y * cols + x] as Sprite;


                //Generate shadow as darkened copy of originalsprite
                if (useShadows)
                {
                    shadow = Instantiate(piece);
                    shadow.transform.parent = piece.transform;
                    shadow.transform.localPosition = shadowOffset;
                    shadow.name = piece.name + "_Shadow";

                    shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                    shadowRenderer.color = shadowColor;
                    shadowRenderer.sortingOrder = -1;
                }

                //Assign custom material to puzzle piece
                if (material)
                    spriteRenderer.material = material;

            }

        return puzzle;
    }


    //Extract and mask image-piece to be used as puzzle piece texture
    Texture2D ExtractFromImage(Texture2D _image, Piece _puzzlElement, int _x, int _y, int _elementBaseSize, Vector2 _elementSizeRatio)
    {
        //Get proper piece of image 
        Color[] pixels = _image.GetPixels
                            (
                                (int)((_x * _elementBaseSize - _puzzlElement.PixelOffset.x) * _elementSizeRatio.x),
                                (int)(_image.height - (_y + 1) * _elementBaseSize * _elementSizeRatio.y - _puzzlElement.PixelOffset.height * _elementSizeRatio.y),
                                (int)(_puzzlElement.MaskWidth * _elementSizeRatio.x),
                                (int)(_puzzlElement.MaskHeight * _elementSizeRatio.y)
                            );


        Texture2D result = new Texture2D(
                                            (int)(_puzzlElement.MaskWidth * _elementSizeRatio.x),
                                            (int)(_puzzlElement.MaskHeight * _elementSizeRatio.y)
                                        );

        //Apply mask
        result.wrapMode = TextureWrapMode.Clamp;
        _puzzlElement.ApplyMask(pixels, ref result);

        return result;
    }


    //Scale Texture2D with new width/height
    public static Texture2D Scale(Texture2D _source, int _targetWidth, int _targetHeight)
    {
        if (_targetWidth <= 0 || _targetHeight <= 0)
        {
            Debug.LogWarning("Scale is impossible! Target size should be at least 1x1");
            return null;
        }

        Texture2D result = new Texture2D(_targetWidth, _targetHeight);
        Color[] pixels = new Color[_targetWidth * _targetHeight];

        Vector2 fraction;

        for (int y = 0; y < result.height; y++)
            for (int x = 0; x < result.width; x++)
            {
                fraction.x = Mathf.Clamp01(x / (result.width + 0.0f));
                fraction.y = Mathf.Clamp01(y / (result.height + 0.0f));

                //Get the relative pixel positions
                pixels[y * result.width + x] = _source.GetPixelBilinear(fraction.x, fraction.y);
            }

        result.SetPixels(pixels);
        result.Apply();

        return result;
    }

    public static Color[] Rotate90(Color[] _source, int _width, int _height, bool _clockwise = true)
    {
        Color[] result = new Color[_source.Length];
        int rotatedPixelId, originalPixelId;

        if (_clockwise)
            for (int y = 0; y < _height; ++y)
                for (int x = 0; x < _width; ++x)
                {
                    rotatedPixelId = (x + 1) * _height - y - 1;
                    originalPixelId = _source.Length - 1 - (y * _width + x);
                    result[rotatedPixelId] = _source[originalPixelId];
                }
        else
            for (int y = 0; y < _height; ++y)
                for (int x = 0; x < _width; ++x)
                {
                    rotatedPixelId = (x + 1) * _height - y - 1;
                    originalPixelId = y * _width + x;
                    result[rotatedPixelId] = _source[originalPixelId];
                }

        return result;
    }

}