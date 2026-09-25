using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>Which sprite animation the pet is playing.</summary>
    public enum PetClip
    {
        Idle,
        Walk,
        Eat,
        Sleep,
        Yawn,
        Threat,
        Happy,
        TailWag
    }

    /// <summary>How the pet responded to being tapped.</summary>
    public enum PetTapReaction
    {
        /// <summary>No special reaction; the view shows the mood hint.</summary>
        Noticed,
        Happy,
        /// <summary>Woken up and yawned.</summary>
        Woke,
        /// <summary>Woken up with a start and threatened.</summary>
        Startled,
        /// <summary>Poked too often and threatened.</summary>
        Threat
    }

    /// <summary>
    /// The pet's on-screen life as a small state machine: rest, wander, sleep (by day, next
    /// to its shelter), yawn, tail-wag and the one-shot reactions (eat, happy, threat). Owns
    /// position in floor coordinates (X 0–1 left→right, Depth 0–1 back→front) and which clip
    /// plays. No Unity types, so the whole behaviour is EditMode tested; PetActor renders it.
    /// </summary>
    public sealed class PetBehaviour
    {
        /// <summary>Facing values: the sprites face left; -1 mirrors them to face right.</summary>
        public const float FacingLeft = 1f;
        public const float FacingRight = -1f;

        private readonly PetBehaviourTuning tuning;
        private readonly PetBehaviourPlanner planner;
        private readonly Queue<float> recentTaps = new Queue<float>();

        private PetIntent walk;
        private float remaining = 1f;
        private float clock;
        private float noSleepUntil;
        private float sleepOnArrival;
        private PetClip? queuedClip;
        private float queuedSeconds;
        private bool hasShelter;
        private float shelterX;
        private float shelterDepth;

        public PetBehaviour(PetBehaviourTuning tuning, Random random)
        {
            this.tuning = tuning;
            planner = new PetBehaviourPlanner(tuning, random);
            X = 0.6f;
            Depth = 0.6f;
            Facing = FacingLeft;
            Clip = PetClip.Idle;
            Phase = DayPhase.Evening;
        }

        public float X { get; private set; }

        public float Depth { get; private set; }

        public float Facing { get; private set; }

        public PetClip Clip { get; private set; }

        /// <summary>Time of day on the player's clock; drives when the pet sleeps.</summary>
        public DayPhase Phase { get; set; }

        /// <summary>Seconds since the current clip started (drives the frame index).</summary>
        public float ClipTime { get; private set; }

        /// <summary>Increments each time a happy reaction starts (the view spawns hearts).</summary>
        public int CheerCount { get; private set; }

        public bool IsAsleep => Clip == PetClip.Sleep;

        /// <summary>True while walking over to its shelter to lie down.</summary>
        public bool IsHeadingToBed => Clip == PetClip.Walk && sleepOnArrival > 0f;

        /// <summary>Floor spot of the decor it hides next to when sleeping (a rock, driftwood…).</summary>
        public void SetShelter(float x, float depth)
        {
            hasShelter = true;
            shelterX = x;
            shelterDepth = depth;
        }

        public void ClearShelter()
        {
            hasShelter = false;
        }

        public void Tick(float deltaSeconds, PetMood mood)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            clock += deltaSeconds;
            ClipTime += deltaSeconds;

            if (Clip == PetClip.Walk)
            {
                StepTowardTarget(deltaSeconds, mood);
                return;
            }

            remaining -= deltaSeconds;
            if (remaining > 0f)
            {
                return;
            }

            if (queuedClip.HasValue)
            {
                var next = queuedClip.Value;
                queuedClip = null;
                Play(next, queuedSeconds);
                return;
            }

            switch (Clip)
            {
                case PetClip.Idle:
                    Decide(mood);
                    break;
                case PetClip.Sleep:
                    // Slept its fill: wakes with a yawn.
                    Play(PetClip.Yawn, tuning.YawnSeconds);
                    break;
                default:
                    StartRest(mood);
                    break;
            }
        }

        /// <summary>Food was offered and accepted: a quick tail twitch, then it strikes and eats.</summary>
        public void Feed()
        {
            sleepOnArrival = 0f;
            Play(PetClip.TailWag, tuning.PreStrikeTailWagSeconds);
            queuedClip = PetClip.Eat;
            queuedSeconds = tuning.EatSeconds;
        }

        /// <summary>Food was offered during a fast: it turns its head away and ignores it.</summary>
        public void RefuseFood(PetMood mood)
        {
            if (IsAsleep)
            {
                return;
            }

            Facing = -Facing;
            StartRest(mood);
        }

        /// <summary>
        /// A care action the pet enjoys. Returns false when it slept through it.
        /// </summary>
        public bool Cheer()
        {
            if (IsAsleep)
            {
                return false;
            }

            StartHappy();
            return true;
        }

        public PetTapReaction Tap(PetMood mood)
        {
            recentTaps.Enqueue(clock);
            while (recentTaps.Count > 0 && clock - recentTaps.Peek() > tuning.ThreatTapWindowSeconds)
            {
                recentTaps.Dequeue();
            }

            if (IsAsleep)
            {
                // Woken up: it stays up for a while, then may go back to bed.
                recentTaps.Clear();
                noSleepUntil = clock + tuning.AwakeAfterWakingSeconds;
                if (planner.Roll() < tuning.WakeStartleChance)
                {
                    Play(PetClip.Threat, tuning.ThreatSeconds);
                    return PetTapReaction.Startled;
                }

                Play(PetClip.Yawn, tuning.YawnSeconds);
                return PetTapReaction.Woke;
            }

            if (recentTaps.Count >= tuning.ThreatTapCount)
            {
                recentTaps.Clear();
                Play(PetClip.Threat, tuning.ThreatSeconds);
                return PetTapReaction.Threat;
            }

            if (Clip == PetClip.Threat)
            {
                return PetTapReaction.Threat;
            }

            // Already happy from the last tap: repeated pokes don't keep piling up hearts.
            if (mood == PetMood.Sluggish || Clip == PetClip.Happy)
            {
                return PetTapReaction.Noticed;
            }

            sleepOnArrival = 0f;
            StartHappy();
            return PetTapReaction.Happy;
        }

        private void Decide(PetMood mood)
        {
            var intent = planner.NextAfterRest(mood, Phase, X, Depth);
            switch (intent.Activity)
            {
                case PetActivity.Walk:
                    StartWalk(intent);
                    break;
                case PetActivity.Sleep:
                    if (clock < noSleepUntil)
                    {
                        StartRest(mood);
                    }
                    else
                    {
                        GoToBed(intent.Seconds);
                    }

                    break;
                case PetActivity.Yawn:
                    Play(PetClip.Yawn, intent.Seconds);
                    break;
                case PetActivity.TailWag:
                    Play(PetClip.TailWag, intent.Seconds);
                    break;
                default:
                    SetClip(PetClip.Idle);
                    remaining = intent.Seconds;
                    break;
            }
        }

        /// <summary>Sleeps where it is, or first walks to just behind/beside its shelter.</summary>
        private void GoToBed(float sleepSeconds)
        {
            if (!hasShelter)
            {
                Play(PetClip.Sleep, sleepSeconds);
                return;
            }

            var side = X < shelterX ? -1f : 1f;
            var bedX = Clamp01(shelterX + side * tuning.ShelterSideOffset);
            var bedDepth = Clamp01(shelterDepth - tuning.ShelterDepthOffset);

            if (Math.Abs(bedX - X) + Math.Abs(bedDepth - Depth) <= tuning.ShelterReachedDistance)
            {
                Play(PetClip.Sleep, sleepSeconds);
                return;
            }

            StartWalk(new PetIntent(PetActivity.Walk, bedX, bedDepth, 0f));
            sleepOnArrival = sleepSeconds;
        }

        private void StartWalk(PetIntent intent)
        {
            walk = intent;
            sleepOnArrival = 0f;
            Facing = intent.TargetX > X ? FacingRight : FacingLeft;
            SetClip(PetClip.Walk);
        }

        private void StepTowardTarget(float deltaSeconds, PetMood mood)
        {
            var dx = walk.TargetX - X;
            // The floor is shallow on screen, so a unit of depth is a shorter walk than a unit of X.
            var dz = (walk.TargetDepth - Depth) * tuning.DepthWalkWeight;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz);
            // Re-read speed each frame so a mood change mid-walk takes effect immediately.
            var step = planner.SpeedFor(mood) * deltaSeconds;

            if (distance <= step)
            {
                X = walk.TargetX;
                Depth = walk.TargetDepth;
                if (sleepOnArrival > 0f)
                {
                    var seconds = sleepOnArrival;
                    sleepOnArrival = 0f;
                    Play(PetClip.Sleep, seconds);
                    return;
                }

                StartRest(mood);
                return;
            }

            X += dx / distance * step;
            Depth += dz / distance * step / tuning.DepthWalkWeight;
        }

        private void StartRest(PetMood mood)
        {
            queuedClip = null;
            SetClip(PetClip.Idle);
            remaining = planner.Rest(mood).Seconds;
        }

        private void StartHappy()
        {
            Play(PetClip.Happy, tuning.HappySeconds);
            CheerCount++;
        }

        /// <summary>Starts a timed clip; anything queued behind the previous clip is dropped.</summary>
        private void Play(PetClip clip, float seconds)
        {
            queuedClip = null;
            SetClip(clip);
            remaining = seconds;
        }

        private void SetClip(PetClip clip)
        {
            Clip = clip;
            ClipTime = 0f;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
