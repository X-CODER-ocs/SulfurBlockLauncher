using System.Numerics;

namespace LiteSkinViewer2D.Rendering;

public readonly record struct SkinFace(
    Vector3 V0, Vector3 V1, Vector3 V2, Vector3 V3,
    Vector2 U0, Vector2 U1, Vector2 U2, Vector2 U3,
    bool Overlay);
