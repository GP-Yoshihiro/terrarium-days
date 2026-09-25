using System;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>
    /// Picks the sprite frame for a clip at a point in time. Every clip loops except the
    /// yawn, which plays once and holds its last frame.
    /// </summary>
    public static class PetAnimation
    {
        public static int FrameIndex(PetClip clip, float clipTime, int frameCount, PetBehaviourTuning tuning)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            var frame = (int)Math.Floor(Math.Max(0f, clipTime) * FpsFor(clip, tuning));
            return Loops(clip) ? frame % frameCount : Math.Min(frame, frameCount - 1);
        }

        /// <summary>
        /// Walk frame from the distance walked (in pet-sprite pixels) rather than from time,
        /// so each frame matches exactly one step of body travel and planted feet never slide,
        /// whatever the walking speed or on-screen size.
        /// </summary>
        public static int WalkFrameIndex(float distanceWalked, float stride, int frameCount)
        {
            if (frameCount <= 0 || stride <= 0f)
            {
                return 0;
            }

            var frame = (int)Math.Floor(Math.Max(0f, distanceWalked) / stride * frameCount);
            return frame % frameCount;
        }

        public static float FpsFor(PetClip clip, PetBehaviourTuning tuning)
        {
            switch (clip)
            {
                case PetClip.Walk:
                    return tuning.WalkFps;
                case PetClip.Eat:
                    return tuning.EatFps;
                case PetClip.Sleep:
                    return tuning.SleepFps;
                case PetClip.Yawn:
                    return tuning.YawnFps;
                case PetClip.Threat:
                    return tuning.ThreatFps;
                case PetClip.Happy:
                    return tuning.HappyFps;
                case PetClip.TailWag:
                    return tuning.TailWagFps;
                default:
                    return tuning.IdleFps;
            }
        }

        public static bool Loops(PetClip clip)
        {
            return clip != PetClip.Yawn;
        }

        /// <summary>Resources folder prefix of a clip's frames, e.g. "idle" → Gecko/idle_00.</summary>
        public static string ResourcePrefix(PetClip clip)
        {
            return clip.ToString().ToLowerInvariant();
        }
    }
}
