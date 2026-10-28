using System;
using UnityEngine;

[Serializable]
public struct DamageTextGlyph
{
    public bool Available;
    public Rect Uv;
    public Vector2 Size, Bearing;
    public float Advance;
}

/// <summary>Fixed digit atlas. Runtime consumers freeze the metrics during Prepare.</summary>
[CreateAssetMenu(menuName = "UIData/Damage text glyph atlas")]
public sealed class DamageTextGlyphAtlas : ScriptableObject
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private float _referencePixelSize = 24;
    [SerializeField] private string _contentVersion = "digits-v1";
    [SerializeField] private string _textureContentHash = "unknown";
    [SerializeField] private DamageTextGlyph[] _digits = new DamageTextGlyph[10];
    [SerializeField, HideInInspector] private int _revision;
    public Texture2D Texture => _texture;
    public int Revision => _revision;

    public void Initialize(Texture2D texture, float referencePixelSize, DamageTextGlyph[] digits,
        string contentVersion, string textureContentHash)
    {
        _texture = texture; _referencePixelSize = referencePixelSize;
        _digits = digits == null ? null : (DamageTextGlyph[])digits.Clone();
        _contentVersion = contentVersion; _textureContentHash = textureContentHash; _revision++;
    }

    private void OnValidate() { _revision++; }

    public Snapshot Freeze()
    {
        if (_texture == null || !DamageTextRequest.Finite(_referencePixelSize) || _referencePixelSize <= 0 ||
            _digits == null || _digits.Length != 10 || string.IsNullOrEmpty(_contentVersion) ||
            string.IsNullOrEmpty(_textureContentHash) || _textureContentHash == "unknown")
            throw new InvalidOperationException("A fixed, versioned digit atlas and texture content hash are required.");
        var digits = (DamageTextGlyph[])_digits.Clone();
        foreach (var glyph in digits)
        {
            if (!glyph.Available) continue;
            if (!DamageTextRequest.Finite(glyph.Advance) || glyph.Advance <= 0 ||
                !DamageTextRequest.Finite(glyph.Size.x) || !DamageTextRequest.Finite(glyph.Size.y) ||
                glyph.Size.x <= 0 || glyph.Size.y <= 0 ||
                !DamageTextRequest.Finite(glyph.Bearing.x) || !DamageTextRequest.Finite(glyph.Bearing.y) ||
                !DamageTextRequest.Finite(glyph.Uv.x) || !DamageTextRequest.Finite(glyph.Uv.y) ||
                !DamageTextRequest.Finite(glyph.Uv.width) || !DamageTextRequest.Finite(glyph.Uv.height) ||
                glyph.Uv.xMin < 0 || glyph.Uv.yMin < 0 || glyph.Uv.xMax > 1 || glyph.Uv.yMax > 1 ||
                glyph.Uv.width <= 0 || glyph.Uv.height <= 0)
                throw new InvalidOperationException("Invalid digit metrics or atlas UV.");
        }
        string hash = DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-atlas-v1"); writer.Write(_contentVersion); writer.Write(_textureContentHash);
            writer.Write(_texture.width); writer.Write(_texture.height); writer.Write((int)_texture.filterMode);
            writer.Write((int)_texture.wrapMode); writer.Write(_referencePixelSize);
            foreach (var glyph in digits)
            {
                writer.Write(glyph.Available); writer.Write(glyph.Uv.x); writer.Write(glyph.Uv.y);
                writer.Write(glyph.Uv.width); writer.Write(glyph.Uv.height);
                writer.Write(glyph.Size.x); writer.Write(glyph.Size.y);
                writer.Write(glyph.Bearing.x); writer.Write(glyph.Bearing.y); writer.Write(glyph.Advance);
            }
        });
        return new Snapshot(this, digits, _referencePixelSize, hash);
    }

    public sealed class Snapshot
    {
        private readonly DamageTextGlyphAtlas _source;
        private readonly int _revision;
        private readonly DamageTextGlyph[] _digits;
        public Texture2D Texture { get; }
        public float ReferencePixelSize { get; }
        public string Hash { get; }
        public bool IsValid => _source != null && _source.Revision == _revision && Texture != null && _source.Texture == Texture;
        internal Snapshot(DamageTextGlyphAtlas source, DamageTextGlyph[] digits, float referencePixelSize, string hash)
        {
            _source = source; _revision = source.Revision; Texture = source.Texture;
            _digits = digits; ReferencePixelSize = referencePixelSize; Hash = hash;
        }
        public DamageTextGlyph GetGlyph(int digit) => _digits[digit];
        public bool Contains(uint amount)
        {
            do { if (!_digits[amount % 10].Available) return false; amount /= 10; } while (amount > 0);
            return true;
        }
    }
}
