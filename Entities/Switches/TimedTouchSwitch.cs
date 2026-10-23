using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Graphics.Animation;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Mechanisms.Switches;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Game.Gameplay.Enemies.Seekers;


namespace Celeste.Mods.OutbackHelper.Entities.Switches
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.OutbackHelper.TimedTouchSwitch")]
    public class TimedTouchSwitch : OriginalTouchSwitchState
    {
        private readonly IOriginalTouchSwitchProcess process;
        private Vector2 start;

        private float startDisappearTime;
        private float currentDisappearTime;

        private PlayerColliderComponentState playerCollider;
        private SeekerColliderState seekerCollider;
        private HoldableColliderState holdableCollider;

        private new Sprite icon;
        private AtlasEntry border;

        private bool emittedFire = false;
        private List<SceneEntityState> switchGates;

        private enum DisappearTimes
        {
            Fast = 5,
            Medium = 10,
            Slow = 15
        }

        private OriginalLevelSceneRoot level
        {
            get { return (OriginalLevelSceneRoot)Scene; }
        }

        public TimedTouchSwitch(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            data.Position + offset, (IOriginalTouchSwitchProcess)engine)
        {
            process = (IOriginalTouchSwitchProcess)engine;
            startDisappearTime = process.ExtremaArithmetic.Max(
                data.Float("startDisappearTime", (float)data.Enum("startDisappearTime", DisappearTimes.Slow)), 0f);
            icon = new Sprite(engine, process.GameGraphics.GameAtlas, "collectables/outback/timedtouchswitch/idle");
            OwnedComponents.Remove(OwnedComponents.Get<Sprite>());
            Add(icon);
            border = process.GameGraphics.GameAtlas["collectables/outback/timedtouchswitch/cont"];
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin", 1f), new int[]
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7
            });
            icon.Play("spin", false, false);
            icon.CenterOrigin();

            playerCollider = OwnedComponents.Get<PlayerColliderComponentState>();
            playerCollider.OnCollide = OnPlayer;

            seekerCollider = OwnedComponents.Get<SeekerColliderState>();
            seekerCollider.OnCollide = OnSeeker;

            holdableCollider = OwnedComponents.Get<HoldableColliderState>();
            holdableCollider.OnCollide = OnHoldable;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            OriginalLevelSceneRoot level = scene as OriginalLevelSceneRoot;
            if (level != null)
            {
                start = Position;
                currentDisappearTime = process.ExtremaArithmetic.Max(0, startDisappearTime);
                icon.Color = PackedColor.White;
                icon.Rate = 0;
                Add(new SimulationCoroutine(process, FadeOut(), true));
            }
        }

        public override void Awake(object scene)
        {
            switchGates = new List<SceneEntityState>();
            var entities = ((ISourceScene)scene).SourceScene.Entities.Entities;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] is OriginalSwitchGateState gate)
                    switchGates.Add(gate);
            base.Awake(scene);
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            TryTurnOn();
        }

        private void OnSeeker(OriginalSeekerState seeker)
        {
            if (level.InsideCamera(Position, 10f))
            {
                TryTurnOn();
            }
        }

        private void OnHoldable(HoldableState holdable)
        {
            TryTurnOn();
        }

        public void TryTurnOn()
        {
            if (currentDisappearTime > 0f)
            {
                TurnOn();
            }
        }

        private IEnumerator FadeOut()
        {
            // If player not spawned or moved
            RoomPlayerSession player;
            while ((player = ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>()) ==
                   null || player.JustRespawned)
            {
                yield return null;
            }

            while (!Switch.Activated && currentDisappearTime > 0)
            {
                icon.SetAnimationFrame(8 - (int)Math.Floor(8 * currentDisappearTime / startDisappearTime));
                currentDisappearTime -= process.DeltaTime;
                if (currentDisappearTime <= 0f)
                {
                    Add(new SimulationCoroutine(process, TurnRed(icon, new PackedColor(1f, 0.2f, 0.2f) * 0.1f), true));
                    icon.Stop();
                    if (!emittedFire)
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            float angle = SourceRandomMath.NextFloat(process.Random, 2 * (float)Math.PI);
                            level.Particles.Emit(process.ParticleTypes.TouchSwitch.P_FireWhite,
                                Position + Vector2Geometry.AngleToVector(angle, 6f), angle);
                        }

                        ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)process).SpatialAudio.Play(
                            "event:/game/04_cliffside/arrowblock_break", Position);
                        emittedFire = true;
                    }

                    for (int i = 0; i < switchGates.Count; i++)
                    {
                        OriginalSwitchGateState switchGateEntity = (OriginalSwitchGateState)switchGates[i];
                        Sprite switchGateIcon = switchGateEntity.OwnedComponents.Get<Sprite>();
                        Add(new SimulationCoroutine(process, TurnRed(switchGateIcon, new PackedColor(1f, 0.2f, 0.2f)),
                            true));
                        switchGateEntity = null;
                        switchGateIcon = null;
                    }
                }

                yield return null;
            }

            if (Switch.Activated)
            {
                icon.Rate = 50;
            }
        }

        private IEnumerator TurnRed(Sprite sprite, PackedColor targetColor)
        {
            PackedColor startColor = sprite.Color;
            float rate = 1f;
            while (rate > 0)
            {
                sprite.Color =
                    SourceColorInterpolation.Lerp(startColor, targetColor, 1f - rate, process.ScalarArithmetic);
                rate -= process.DeltaTime * 4f;
                rate = process.ExtremaArithmetic.Max(0, rate);
                yield return null;
            }

            sprite.Color = targetColor;
        }
    }
}
