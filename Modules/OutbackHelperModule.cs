using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Graphics.Content;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.OutbackHelper.Entities.Portals;

namespace Celeste.Mods.OutbackHelper.Modules
{
    public sealed class OutbackHelperModule(ISourceEngineContext engine) : SourceEverestModule(engine)
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, OutbackHelperModule> instances = new();
        private global::Celeste.Mods.OutbackHelper.Runtime.Switches.OutbackSwitchPufferHooks movingSwitchPufferHooks;
        public SpriteBank Sprites { get; private set; }

        public static OutbackHelperModule Get(ISourceEngineContext engine) =>
            instances.TryGetValue(engine, out var module)
                ? module
                : throw new InvalidOperationException("OutbackHelper is not loaded for this engine.");

        public override void Load()
        {
            instances.Add(SourceEngine, this);

            ((IEverestLevelEntityLoadEnvironment)SourceEngine).LevelUpdateEvents.OnLoadEntity += OnLoadEntity;
            movingSwitchPufferHooks =
                new global::Celeste.Mods.OutbackHelper.Runtime.Switches.OutbackSwitchPufferHooks(SourceEngine);
        }

        public override void Unload()
        {
            ((IEverestLevelEntityLoadEnvironment)SourceEngine).LevelUpdateEvents.OnLoadEntity -= OnLoadEntity;
            movingSwitchPufferHooks.Dispose();
            movingSwitchPufferHooks = null;
            instances.Remove(SourceEngine);
        }

        public override void LoadContent(bool firstLoad)
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)SourceEngine).SourceContentRegistry
                   .Get("Graphics/OutbackSprites").Stream)
                xml.Load(stream);
            Sprites = new SpriteBank(SourceEngine,
                ((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas, xml);
        }

        private bool OnLoadEntity(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            if (data.Name == "outback/movingtouchswitch")
            {
                level.Scheduler.QueueSourceEntity(
                    new global::Celeste.Mods.OutbackHelper.Entities.Switches.MovingTouchSwitch(SourceEngine, data,
                        offset));
                return true;
            }

            if (data.Name == "outback/timedtouchswitch")
            {
                level.Scheduler.QueueSourceEntity(
                    new global::Celeste.Mods.OutbackHelper.Entities.Switches.TimedTouchSwitch(SourceEngine, data,
                        offset));
                return true;
            }

            if (data.Name != "outback/portal") return false;
            level.Scheduler.QueueSourceEntity(new Portal(SourceEngine, data, offset));
            return true;
        }
    }
}
