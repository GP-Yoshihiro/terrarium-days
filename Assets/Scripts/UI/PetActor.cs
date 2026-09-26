using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Renders PetBehaviour: stands the current animation frame on the terrarium floor via
    /// TerrariumProjection, mirrors it to the walking direction, grows it with the growth
    /// stage, dims it when sluggish, and floats hearts (happy) or Zzz (asleep) above its head.
    /// </summary>
    public sealed class PetActor
    {
        // Where the head sits in the 256x256 frames (facing left), for placing effects.
        private const float HeadX = 46f;
        private const float HeadTopY = 112f;

        private readonly VisualElement element;
        private readonly PetBehaviourTuning tuning;
        private readonly TerrariumArtLayout art;
        private readonly PetSpriteLibrary sprites;
        private readonly FloatingEffects effects;

        private TerrariumProjection projection;
        private ElementBox box;
        private PetMood mood = PetMood.Lively;
        private float bodyWidth;
        private int shownCheers;
        private float zzzTimer;
        private Texture2D shownFrame;
        private float walkDistance;
        private bool preShed;
        private bool hasShelter;
        private float shelterScreenX;
        private float shelterDepth;
        private Vector2 lastFeet;
        private bool hasLastFeet;

        public PetActor(VisualElement element, PetBehaviourTuning tuning, TerrariumArtLayout art,
            System.Random random, PetSpriteLibrary sprites = null, VisualElement effectsLayer = null)
        {
            this.element = element;
            this.tuning = tuning;
            this.art = art;
            this.sprites = sprites;
            effects = new FloatingEffects(effectsLayer);
            Behaviour = new PetBehaviour(tuning, random);
            bodyWidth = tuning.BabyBodyWidth;

            var pet = art.Pet;
            // Pivot mirroring on the feet so the pet turns around in place.
            element.style.transformOrigin = new TransformOrigin(
                new Length((pet.BodyLeft + pet.BodyRight) / 2f / pet.ImageWidth * 100f, LengthUnit.Percent),
                new Length(pet.BodyBottom / pet.ImageHeight * 100f, LengthUnit.Percent));
            // Hidden until the terrarium's size is known, so it never flashes at a wrong spot.
            element.style.visibility = Visibility.Hidden;
            ApplyFrame();
        }

        public PetBehaviour Behaviour { get; }

        public PetMood Mood => mood;

        public float X => Behaviour.X;

        public float Depth => Behaviour.Depth;

        public bool IsWalking => Behaviour.Clip == PetClip.Walk;

        public float BodyWidth => bodyWidth;

        public FloatingEffects Effects => effects;

        public void SetViewSize(float width, float height)
        {
            projection = new TerrariumProjection(width, height, art);
            element.style.visibility = projection.IsValid ? Visibility.Visible : Visibility.Hidden;
            UpdateShelter();
            ApplyPlacement();
        }

        public void SetPhase(DayPhase phase)
        {
            Behaviour.Phase = phase;
        }

        /// <summary>
        /// Before a shed the gecko's skin turns milky: swap to the pale frame set.
        /// </summary>
        public void SetAppetite(AppetiteState appetite)
        {
            var pale = appetite == AppetiteState.PreShed;
            if (pale != preShed)
            {
                preShed = pale;
                shownFrame = null;
                ApplyFrame();
            }
        }

        /// <summary>
        /// The decor it sleeps beside, given by where the decor's body is centred on screen and
        /// the depth it stands at. Converted into the pet's own floor X so it lies right next to it.
        /// </summary>
        public void SetShelterAt(float bodyCenterScreenX, float depth)
        {
            hasShelter = true;
            shelterScreenX = bodyCenterScreenX;
            shelterDepth = depth;
            UpdateShelter();
        }

        public void ClearShelter()
        {
            hasShelter = false;
            Behaviour.ClearShelter();
        }

        /// <summary>Whether a decor slot is currently offered to this pet as a hide (see SetShelterAt/ClearShelter).</summary>
        public bool HasShelter => hasShelter;

        private void UpdateShelter()
        {
            if (!hasShelter || projection == null || !projection.IsValid)
            {
                return;
            }

            var x = projection.FloorXForBodyCenter(art.Pet, shelterDepth, bodyWidth * projection.ViewWidth, shelterScreenX);
            Behaviour.SetShelter(x, shelterDepth);
        }

        public void SetCondition(PetMood newMood, GrowthStage stage)
        {
            mood = newMood;
            bodyWidth = BodyWidthFor(stage);
            UpdateShelter();

            var tint = newMood == PetMood.Sluggish ? tuning.SluggishTint : 1f;
            element.style.unityBackgroundImageTintColor = new Color(tint, tint, tint, 1f);
            ApplyPlacement();
        }

        public void Feed()
        {
            Behaviour.Feed();
            ApplyFrame();
        }

        public void RefuseFood()
        {
            Behaviour.RefuseFood(mood);
            ApplyFrame();
            ApplyPlacement();
        }

        public void Cheer()
        {
            Behaviour.Cheer();
            ApplyFrame();
            SpawnHeartsIfCheered();
        }

        public PetTapReaction Tap()
        {
            var reaction = Behaviour.Tap(mood);
            if (reaction == PetTapReaction.Threat || reaction == PetTapReaction.Startled)
            {
                effects.Clear();
            }

            ApplyFrame();
            SpawnHeartsIfCheered();
            return reaction;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            var wasWalking = Behaviour.Clip == PetClip.Walk;
            Behaviour.Tick(deltaSeconds, mood);
            ApplyPlacement();
            TrackWalkDistance(wasWalking);
            ApplyFrame();
            SpawnHeartsIfCheered();

            if (Behaviour.IsAsleep)
            {
                zzzTimer -= deltaSeconds;
                if (zzzTimer <= 0f)
                {
                    zzzTimer = tuning.ZzzIntervalSeconds;
                    effects.Spawn(sprites?.Zzz, HeadPoint(), tuning.ZzzSizePixels, tuning.ZzzLifeSeconds, tuning.ZzzRisePixels, 4f);
                }
            }
            else
            {
                zzzTimer = 0f;
            }

            effects.Tick(deltaSeconds);
        }

        private void SpawnHeartsIfCheered()
        {
            if (Behaviour.CheerCount == shownCheers)
            {
                return;
            }

            shownCheers = Behaviour.CheerCount;
            for (var i = 0; i < tuning.HeartsPerCheer; i++)
            {
                var origin = HeadPoint() + new Vector2((i - (tuning.HeartsPerCheer - 1) / 2f) * tuning.HeartSizePixels * 0.8f, 0f);
                effects.Spawn(sprites?.Heart, origin, tuning.HeartSizePixels, tuning.HeartLifeSeconds,
                    tuning.HeartRisePixels, 5f, i * tuning.HeartStaggerSeconds);
            }
        }

        /// <summary>Just above the head, in terrarium-view pixels, respecting facing.</summary>
        private Vector2 HeadPoint()
        {
            var pixels = box.Width / art.Pet.ImageWidth;
            var pivotX = (art.Pet.BodyLeft + art.Pet.BodyRight) / 2f;
            var headX = pivotX + (HeadX - pivotX) * Behaviour.Facing;
            return new Vector2(box.Left + headX * pixels, box.Top + HeadTopY * pixels);
        }

        /// <summary>
        /// Accumulates how far the feet moved on screen, in pet-sprite pixels, while walking.
        /// </summary>
        private void TrackWalkDistance(bool wasWalking)
        {
            var feet = FeetPoint();
            if (Behaviour.Clip != PetClip.Walk)
            {
                walkDistance = 0f;
            }
            else if (wasWalking && hasLastFeet && box.Width > 0f)
            {
                walkDistance += Vector2.Distance(feet, lastFeet) / (box.Width / art.Pet.ImageWidth);
            }

            lastFeet = feet;
            hasLastFeet = box.Width > 0f;
        }

        private Vector2 FeetPoint()
        {
            var pixels = box.Width / art.Pet.ImageWidth;
            return new Vector2(box.Left + (art.Pet.BodyLeft + art.Pet.BodyRight) / 2f * pixels, box.Top + art.Pet.BodyBottom * pixels);
        }

        /// <summary>Distance walked in the current walk, in pet-sprite pixels.</summary>
        public float WalkDistance => walkDistance;

        private void ApplyFrame()
        {
            if (sprites == null)
            {
                return;
            }

            var clip = Behaviour.Clip;
            var index = clip == PetClip.Walk
                ? PetAnimation.WalkFrameIndex(walkDistance, art.PetWalkStride, sprites.FrameCount(clip))
                : PetAnimation.FrameIndex(clip, Behaviour.ClipTime, sprites.FrameCount(clip), tuning);
            var frame = sprites.Frame(clip, index, preShed);
            if (frame != null && frame != shownFrame)
            {
                shownFrame = frame;
                element.style.backgroundImage = new StyleBackground(frame);
            }
        }

        private float BodyWidthFor(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Adult:
                    return tuning.AdultBodyWidth;
                case GrowthStage.Juvenile:
                    return tuning.JuvenileBodyWidth;
                default:
                    return tuning.BabyBodyWidth;
            }
        }

        private void ApplyPlacement()
        {
            if (projection == null || !projection.IsValid)
            {
                return;
            }

            box = projection.PlaceOnFloor(art.Pet, Behaviour.X, Behaviour.Depth, bodyWidth * projection.ViewWidth);
            element.style.left = box.Left;
            element.style.top = box.Top;
            element.style.width = box.Width;
            element.style.height = box.Height;
            element.style.scale = new Scale(new Vector3(Behaviour.Facing, 1f, 1f));
        }
    }
}
