using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Graphics.Animation;
using Celeste.Mods.OutbackHelper.Modules;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Lighting;


namespace Celeste.Mods.OutbackHelper.Entities.Portals
{
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.OutbackHelper.Portal")]
    public class Portal : SceneEntityState
    {
        private enum ReadyColors
        {
            Purple,
            Blue,
            Red,
            Yellow,
            Green,
            Aqua,
            Black,
            Fuchsia,
            Gray,
            Lime,
            Maroon,
            Navy,
            Olive,
            Silver,
            Teal,
            White
        }

        private enum Directions
        {
            None,
            Up,
            Down,
            Left,
            Right
        }

        private VertexLightState light;

        private Vector2 start;

        private string startLevel;

        private Vector2[] portalNodes;

        public Sprite portal;

        private Portal otherPortal;

        private readonly float maxCooldown;

        private int readyColor;

        private PackedColor[] readyColorsArray = new PackedColor[16]
        {
            new PackedColor(1f, 0.3f, 1f),
            new PackedColor(0.3f, 0.3f, 1f),
            new PackedColor(1f, 0.3f, 0.3f),
            new PackedColor(1f, 1f, 0.3f),
            new PackedColor(0.3f, 1f, 0.3f),
            new PackedColor(0f, 1f, 1f),
            new PackedColor(0f, 0f, 0f),
            new PackedColor(1f, 0f, 1f),
            new PackedColor(0.5f, 0.5f, 0.5f),
            new PackedColor(0f, 1f, 0f),
            new PackedColor(0.5f, 0f, 0f),
            new PackedColor(0f, 0f, 0.5f),
            new PackedColor(0.5f, 0.5f, 0f),
            new PackedColor(0.75f, 0.75f, 0.75f),
            new PackedColor(0f, 0.5f, 0.5f),
            new PackedColor(1f, 1f, 1f)
        };

        private Vector2[] directionsArray = new Vector2[4]
        {
            new Vector2(0f, -1f),
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f)
        };

        private double[] directionInRadiansArray = new double[4]
        {
            0.0,
            Math.PI,
            -Math.PI / 2.0,
            Math.PI / 2.0
        };

        private PackedColor cooldownColor = new PackedColor(1f, 0.5f, 0.5f);

        public float teleportInsideCooldown;

        private float cooldown;

        private List<object> portals;

        private int direction;

        private List<RoomPlayerSession> playerEntities;

        private RoomPlayerSession playerEntity;

        private const float verticalMultiplier = 1.5f;

        private bool hasStaticMover = false;

        private readonly ISourceEngineContext engine;

        private OriginalLevelSceneRoot level => (OriginalLevelSceneRoot)base.Scene;

        public Portal(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            readyColor = (int)data.Enum("readyColor", ReadyColors.Purple);
            direction = (int)data.Enum("direction", Directions.None);
            var nodes = data.NodesOffset(offset);
            portalNodes = nodes;
            base.Depth = -9999;
            portal = Get<Sprite>();
            maxCooldown = data.Float("cooldownTimer");
            Add(portal = OutbackHelperModule.Get(engine).Sprites.Create("portal"));
            Add(new PlayerColliderComponentState(OnPlayer, null, new HitboxColliderGeometry(30f, 30f, -15f, -15f)));
            portal.CenterOrigin();
            if (direction == 0)
            {
                portal.Play("idle", restart: true);
                base.Collider = new HitboxColliderGeometry(12f, 12f, -6f, -6f);
            }
            else
            {
                portal.Play("directional", restart: true);
                if (direction != 0)
                {
                    Position -= directionsArray[direction - 1];
                }

                if (direction == 1)
                {
                    base.Collider = new HitboxColliderGeometry(16f, 6f, -8f, 0.5f);
                }
                else if (direction == 2)
                {
                    base.Collider = new HitboxColliderGeometry(16f, 6f, -8f, -7.5f);
                }
                else if (direction == 3)
                {
                    base.Collider = new HitboxColliderGeometry(8f, 16f, -1f, -8f);
                }
                else if (direction == 4)
                {
                    base.Collider = new HitboxColliderGeometry(8f, 16f, -7f, -8f);
                }
            }

            Add(light = new VertexLightState(engine, PackedColor.White, 1f, 16, 32));
            if (direction != 0)
            {
                Add(new SourceStaticMoverState
                {
                    SolidChecker = IsRiding
                });
            }

            light.Visible = true;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (scene is OriginalLevelSceneRoot level)
            {
                start = Position;
                startLevel = level.Session.Level;
                portal.OnFrameChange = OnAnimate;
            }
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            playerEntities = ((Celeste.Scene.Tracking.ISourceScene)scene).SourceScene.Entities
                .OfType<RoomPlayerSession>()
                .ToList();
            if (playerEntities.Count == 0)
            {
                return;
            }

            playerEntity = playerEntities[0];
            if (direction != 0)
            {
                this.portal.Rotation = (float)directionInRadiansArray[direction - 1];
            }

            portals = level.Scheduler.Entities.GetTrackedEntities(typeof(Portal));
            this.portal.Color = readyColorsArray[readyColor];
            for (int i = 0; i < portals.Count; i++)
            {
                Portal portal = (Portal)portals[i];
                if (portal.readyColor == readyColor && portal != this)
                {
                    otherPortal = portal;
                }
            }
        }

        private bool IsRiding(SourceSolidEntityState solid)
        {
            bool result = direction switch
            {
                1 => CollideOutside(solid, Position + Vector2.UnitY),
                2 => SourceEntityCollision.CheckAt(this, solid, Position - Vector2.UnitY * 10f),
                3 => CollideOutside(solid, Position + Vector2.UnitX * 10f),
                4 => CollideOutside(solid, Position - Vector2.UnitX * 10f),
                _ => false,
            };
            hasStaticMover = result;
            return result;
        }

        private bool CollideOutside(ISourceEntityCollider entity, Vector2 at) =>
            !SourceEntityCollision.Check(this, entity) && SourceEntityCollision.CheckAt(this, entity, at);

        public override void Update()
        {
            cooldown -= engine.DeltaTime;
            Collidable = cooldown < 0f;
            if (teleportInsideCooldown > 0f)
            {
                if (direction == 1)
                {
                    teleportInsideCooldown -= engine.DeltaTime;
                }

                if (!SourceEntityCollision.Check(this, playerEntity))
                {
                    teleportInsideCooldown = 0f;
                    if (playerEntity.Machine.State == 11)
                    {
                        playerEntity.Machine.State = 0;
                    }
                }
            }
            else if (Collidable && portal.Color == cooldownColor)
            {
                portal.Color = readyColorsArray[readyColor];
                level.Session.SetFlag("portalOnCooldown" + readyColor, setTo: false);
            }

            base.Update();
        }

        public void OnAnimate(string id)
        {
            int currentAnimationFrame = portal.CurrentAnimationFrame;
            if (id == "teleport" && currentAnimationFrame == 5)
            {
                if (direction == 0)
                {
                    portal.Play("idle", restart: true);
                }
                else
                {
                    portal.Play("directional", restart: true);
                }
            }
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (!(teleportInsideCooldown <= 0f) || otherPortal == null)
            {
                return;
            }

            Portal portal = (Portal)otherPortal;
            if (direction == 0)
            {
                this.portal.Play("teleport", restart: true);
                portal.portal.Play("teleport", restart: true);
                Vector2 vec = otherPortal.Center + new Vector2(0f, 6f);
                player.Position = Vector2Geometry.Round(vec);
            }

            if (portal.direction != 0)
            {
                portals = level.Scheduler.Entities.GetTrackedEntities(typeof(Portal));
                if (!portal.Active)
                {
                    for (int i = 0; i < portals.Count; i++)
                    {
                        Portal portal2 = (Portal)portals[i];
                        if (portal2.readyColor == readyColor && portal2 != this && portal2.Active)
                        {
                            otherPortal = portal2;
                        }
                    }
                }

                Vector2 vec2 = otherPortal.Center + new Vector2(0f, 6f) + directionsArray[portal.direction - 1] * 5f;
                player.Position = Vector2Geometry.Round(vec2);
                float rotationDifference = BitConverter.Int32BitsToSingle(
                    BitConverter.SingleToInt32Bits(portal.portal.Rotation - this.portal.Rotation));
                float radians = (float)((double)rotationDifference + Math.PI);
                Vector2 vector = new Vector2((float)Math.Cos(this.portal.Rotation),
                    0f - (float)Math.Sin(this.portal.Rotation));
                var list = SourceSceneCollision.AllRect<SourceSolidEntityState>(level.Scheduler.Entities,
                    "Celeste.Solid",
                    new IntRect((int)portal.X, (int)portal.Y, (int)directionsArray[portal.direction - 1].X * 8,
                        (int)directionsArray[portal.direction - 1].Y * 8));
                var list2 = SourceSceneCollision.AllRect<ISourceSolidTilesEntity>(level.Scheduler.Entities,
                    "Celeste.SolidTiles",
                    new IntRect((int)portal.X, (int)portal.Y, (int)directionsArray[portal.direction - 1].X * 8,
                        (int)directionsArray[portal.direction - 1].Y * 8));
                foreach (var entity in level.Scheduler.Entities.GetTrackedEntities("Celeste.Platform"))
                {
                    if (entity is ISourceSolidTilesEntity && SourceSceneCollision.CheckRect(
                            new IntRect((int)portal.X, (int)portal.Y, (int)directionsArray[portal.direction - 1].X * 8,
                                (int)directionsArray[portal.direction - 1].Y * 8), (ISourceEntityCollider)entity))
                    {
                        list2.Add((ISourceSolidTilesEntity)entity);
                    }
                }

                if (list.Count > 0 || list2.Count > 0)
                {
                    player.Die(Vector2.Zero);
                }

                if (direction == 1)
                {
                    player.Data.Motion.Normal.Speed.Y = Math.Max(player.Data.Motion.Normal.Speed.Y, 150f);
                }

                int facing = ((directionsArray[direction - 1].X == directionsArray[portal.direction - 1].X)
                    ? (0 - player.Data.Facing)
                    : ((int)player.Data.Facing));
                player.Data.Facing = facing;
                player.Data.Motion.Normal.Speed = SourceVector2Math.Transform(player.Data.Motion.Normal.Speed,
                    SourceMatrixMath.CreateRotationZ(radians));
                if (player.Machine.State == 2)
                {
                    player.Machine.State = 11;
                }

                if (direction == 1)
                {
                    if (player.Machine.State != 5)
                    {
                        player.Data.Motion.Normal.Speed *= 1.5f;
                    }

                    if ((portal.direction == 3 || portal.direction == 4) && player.Machine.State != 5)
                    {
                        player.Data.Motion.Normal.Speed.Y = player.Data.Motion.Normal.Speed.Y - 150f;
                    }
                }
                else if (direction == 2 && player.Machine.State != 5)
                {
                    player.Data.Motion.Normal.Speed *= 1.5f;
                }

                if (portal.direction == 1)
                {
                    player.Data.Motion.Normal.Speed.Y = Math.Min(player.Data.Motion.Normal.Speed.Y, -150f);
                }

                if (hasStaticMover && player.Machine.State != 5 &&
                    Get<SourceStaticMoverState>().Platform.GetType() != typeof(CassetteBlockState))
                {
                    player.Data.Motion.Normal.Speed += Get<SourceStaticMoverState>().Platform.LiftSpeed;
                }
            }

            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/char/badeline/disappear", player.Position,
                ((IOriginalGameplayProcess)engine).SaveContext);
            level.Displacement.AddBurst(otherPortal.Position, 0.35f, 8f, 48f, 0.25f);
            level.Displacement.AddBurst(Position, 0.35f, 8f, 48f, 0.25f);
            level.Particles.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.Player.P_Split, 16,
                otherPortal.Center,
                Vector2.One * 6f);
            portal.teleportInsideCooldown = 0.5f;
            teleportInsideCooldown = 0.5f;
            portal.cooldown = portal.maxCooldown;
            cooldown = maxCooldown;
            portal.portal.Color = cooldownColor;
            this.portal.Color = cooldownColor;
            level.Session.SetFlag("portalOnCooldown" + readyColor);
        }
    }
}
