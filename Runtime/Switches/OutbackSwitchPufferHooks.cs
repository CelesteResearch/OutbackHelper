using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Mods.OutbackHelper.Entities.Switches;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.OutbackHelper.Runtime.Switches
{
    public sealed class OutbackSwitchPufferHooks : IDisposable
    {
        private static readonly Func<OriginalPufferState, CircleColliderGeometry> ReadPushRadius = CreateRadiusReader();
        private readonly ISourceEngineContext engine;
        private readonly string movingKey;
        private readonly string timedKey;
        private readonly Hook hook;

        public OutbackSwitchPufferHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            movingKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(MovingTouchSwitch))
                .RegistryKey;
            timedKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(TimedTouchSwitch)).RegistryKey;
            hook = new Hook(
                typeof(OriginalPufferState).GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic),
                (Action<Action<OriginalPufferState>, OriginalPufferState>)Explode);
        }

        private void Explode(Action<OriginalPufferState> orig, OriginalPufferState self)
        {
            orig(self);
            if (self.Scene is not OriginalLevelSceneRoot level || !ReferenceEquals(level.EngineContext, engine)) return;
            var collider = self.Collider;
            self.Collider = ReadPushRadius(self);
            foreach (var entity in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(movingKey))
            {
                var target = (MovingTouchSwitch)entity;
                if (SourceEntityCollision.Check(self, target)) target.TriggerNextState();
            }

            foreach (var entity in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(timedKey))
            {
                var target = (TimedTouchSwitch)entity;
                if (SourceEntityCollision.Check(self, target)) target.TryTurnOn();
            }

            self.Collider = collider;
        }

        private static Func<OriginalPufferState, CircleColliderGeometry> CreateRadiusReader()
        {
            var field = typeof(OriginalPufferState).GetField("pushRadius",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var method = new DynamicMethod("ReadOutbackPufferPushRadius", typeof(CircleColliderGeometry),
                new[] { typeof(OriginalPufferState) }, typeof(OutbackSwitchPufferHooks).Module, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            return (Func<OriginalPufferState, CircleColliderGeometry>)method.CreateDelegate(
                typeof(Func<OriginalPufferState, CircleColliderGeometry>));
        }

        public void Dispose() => hook.Dispose();
    }
}
