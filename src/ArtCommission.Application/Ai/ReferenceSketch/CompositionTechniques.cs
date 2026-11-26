namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>
/// Niche composition techniques for the 2 AI reference sketch variants. Each generation picks
/// 2 different techniques at random and forces one per variant, so Gemini stops defaulting to
/// "centered / rule of thirds" layouts. Each entry is "name: how to apply", written in English
/// because it is passed straight into the English image prompt.
/// </summary>
public static class CompositionTechniques
{
    public static readonly string[] All =
    [
        "frame within frame: the subject is seen through a doorway, arch, window or gap that borders the scene",
        "extreme scale contrast: a tiny subject against a huge environment, subject occupies under 10% of the frame",
        "layered depth: distinct foreground, midground and background planes, with a flat-silhouette foreground element partly covering the view",
        "foreground occlusion: a large object very close to the camera hides part of the subject",
        "S-curve: a road, river, railing or fabric forms an S-shaped line leading the eye to the subject",
        "converging lines: architecture or perspective lines all converge toward the subject",
        "triangular composition: the main elements form a strong triangle, apex at the focal point",
        "diagonal tension: the main subject and lines run along a strong diagonal from corner to corner",
        "negative space: the subject is small and pushed to an edge, most of the frame is empty",
        "tension crop: the subject is cropped hard by the frame edge, only part of it visible",
        "high horizon: the horizon sits near the top edge, ground plane dominates the frame",
        "low horizon: the horizon sits near the bottom edge, sky or ceiling dominates the frame",
        "split composition: the frame is divided into two contrasting halves by a strong vertical or horizontal element",
        "broken symmetry: a near-symmetrical scene with one element deliberately off-balance",
        "reflection: the scene is mirrored in water, glass or a polished floor, reflection takes up part of the frame",
        "silhouette shape: the subject reads as a bold silhouette against a large simple light shape",
        "zigzag depth: the eye travels into the scene along a zigzag path of objects",
        "tunnel framing: surrounding elements form a tunnel or vignette that funnels attention to the subject",
        "golden spiral: elements are arranged along a spiral ending at the focal point",
        "rhythm and repetition: repeated shapes (pillars, trees, windows) create a pattern broken by the subject",
    ];

    public static string[] PickRandom(int count, Random? random = null)
    {
        random ??= Random.Shared;
        return All.OrderBy(_ => random.Next()).Take(count).ToArray();
    }
}
