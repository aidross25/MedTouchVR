using UnityEngine;
using UnityEngine.Rendering;

namespace Obi
{
    public interface ISurfaceChunkUser
    {
        uint usedChunkCount { get; }
    }
}