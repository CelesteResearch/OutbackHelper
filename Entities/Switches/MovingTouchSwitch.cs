using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Mechanisms.Switches;
using Celeste.Game.Gameplay.Enemies.Seekers;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.OutbackHelper.Entities.Switches
{
    [RuntimeTypeAlias("Celeste.Mod.OutbackHelper.MovingTouchSwitch")]
    [SceneTracked(false)]
    public partial class MovingTouchSwitch : OriginalTouchSwitchState
    {
        private readonly IOriginalTouchSwitchProcess process;
        private Vector2 start;
        private int nodeIndex;
        private readonly Vector2[] touchSwitchNodes;
        private bool isMoving;
        public PackedColor MovingColor { get; set; } = new(1f, .5f, .5f);
        public float MovingDelay { get; set; } = .8f;
        public Sprite TouchIcon => icon;
        public ParticleTypePresentation FinishedFire { get; set; }

        protected override ParticleTypePresentation FireParticle =>
            FinishedFire ?? process.ParticleTypes.TouchSwitch.P_Fire;

        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public MovingTouchSwitch(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, null, data, offset)
        {
        }

        public MovingTouchSwitch(ISourceEngineContext engine, Vector2[] nodes, EntityData data, Vector2 offset)
            : base(data.Position + offset, (IOriginalTouchSwitchProcess)engine)
        {
            process = (IOriginalTouchSwitchProcess)engine;
            nodes = data.NodesOffset(offset);
            touchSwitchNodes = nodes;
            Get<PlayerColliderComponentState>().OnCollide = OnPlayer;
            Get<SeekerColliderState>().OnCollide = OnSeeker;
            Get<HoldableColliderState>().OnCollide = OnHoldable;
        }

        public static MovingTouchSwitch Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public void ConfigureAppearance(PackedColor inactive, PackedColor active, PackedColor finish,
            Sprite replacementIcon, ParticleTypePresentation fire)
        {
            inactiveColor = inactive;
            activeColor = active;
            finishColor = finish;
            icon.RemoveSelf();
            icon = replacementIcon;
            Add(icon);
            FinishedFire = fire;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (scene is not OriginalLevelSceneRoot) start = Position;
        }

        private void OnPlayer(RoomPlayerSession player) => TriggerNextState();

        private void OnSeeker(OriginalSeekerState seeker)
        {
            if (Level.InsideCamera(Position, 10f)) TriggerNextState();
        }

        private void OnHoldable(HoldableState holdable) => TriggerNextState();

        public void TriggerNextState()
        {
            if (!Switch.Activated) Add(new SimulationCoroutine(process, TriggeredSwitch()));
        }
    }
}
