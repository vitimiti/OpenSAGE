﻿#nullable enable

using System.Numerics;
using OpenSage.Content;
using OpenSage.Data.Ini;
using OpenSage.Logic.AI;
using OpenSage.Mathematics;

namespace OpenSage.Logic.Object
{
    public class WorkerAIUpdate : SupplyAIUpdate, IBuilderAIUpdate
    {
        public GameObject? BuildTarget => _state.BuildTarget;
        public GameObject? RepairTarget => _state.RepairTarget;
        internal override WorkerAIUpdateModuleData ModuleData { get; }

        private readonly DozerAndWorkerState _state;

        private readonly WorkerAIUpdateStateMachine2 _stateMachine2;
        private uint _unknownObjectId;
        private int _unknown5;
        private int _unknown6;
        private readonly WorkerAIUpdateStateMachine3 _stateMachine3;

        internal WorkerAIUpdate(GameObject gameObject, GameContext context, WorkerAIUpdateModuleData moduleData) : base(gameObject, context, moduleData)
        {
            ModuleData = moduleData;
            _state = new DozerAndWorkerState(gameObject, context, this);
            _stateMachine2 = new WorkerAIUpdateStateMachine2(gameObject, context, this);
            _stateMachine3 = new WorkerAIUpdateStateMachine3(gameObject, context, this);
        }

        internal override void Stop()
        {
            _state.ClearDozerTasks();
            // todo: stop supply gathering?
            base.Stop();
        }

        protected override void ArrivedAtDestination()
        {
            _state.ArrivedAtDestination();
        }

        #region Dozer stuff

        public void SetBuildTarget(GameObject gameObject)
        {
            // note that the order here is important, as SetTargetPoint will clear any existing buildTarget
            // TODO: target should not be directly on the building, but rather a point along the foundation perimeter
            SetTargetPoint(gameObject.Translation);
            _state.SetBuildTarget(gameObject, Context.GameLogic.CurrentFrame.Value);
            ResetSupplyState();
        }

        public void SetRepairTarget(GameObject gameObject)
        {
            // note that the order here is important, as SetTargetPoint will clear any existing repairTarget
            SetTargetPoint(gameObject.Translation);
            _state.SetRepairTarget(gameObject, Context.GameLogic.CurrentFrame.Value);
            ResetSupplyState();
        }

        internal override void SetTargetPoint(Vector3 targetPoint)
        {
            _state.ClearDozerTasks();
            base.SetTargetPoint(targetPoint);
        }

        #endregion

        #region Supply Stuff

        private void ResetSupplyState()
        {
            CurrentSupplyTarget = null;
            SupplyGatherState = SupplyGatherStates.Default;
        }

        internal override void ClearConditionFlags()
        {
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestPreparation, false);
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestAction, false);
            base.ClearConditionFlags();
        }

        protected override int GetAdditionalValuePerSupplyBox(ScopedAssetCollection<UpgradeTemplate> upgrades)
        {
            // this is also hardcoded in original SAGE, replaced by BonusScience and BonusScienceMultiplier (SupplyCenterDockUpdate) in later games
            var upgradeDefinition = upgrades.GetByName("Upgrade_GLAWorkerShoes");
            return GameObject.HasUpgrade(upgradeDefinition) ? ModuleData.UpgradedSupplyBoost : 0;
        }

        internal override GameObject? FindClosestSupplyWarehouse(BehaviorUpdateContext context)
        {
            if (ModuleData.HarvestTrees)
            {
                var nearbyTrees = context.GameContext.Game.PartitionCellManager.QueryObjects(
                    context.GameObject,
                    context.GameObject.Translation,
                    ModuleData.SupplyWarehouseScanDistance,
                    new PartitionQueries.KindOfQuery(ObjectKinds.Tree));

                GameObject? closestTree = null;
                var closestDistance = float.MaxValue;

                foreach (var tree in nearbyTrees)
                {
                    if (!tree.Definition.IsHarvestable)
                    {
                        continue;
                    }

                    if (tree.Supply <= 0)
                    {
                        continue;
                    }

                    var distance = context.GameContext.Game.PartitionCellManager.GetDistanceBetweenObjectsSquared(context.GameObject, tree);

                    if (distance < closestDistance)
                    {
                        closestTree = tree;
                        closestDistance = distance;
                    }
                }

                return closestTree;
            }
            else
            {
                return base.FindClosestSupplyWarehouse(context);
            }
        }

        internal override float GetHarvestActivationRange() => ModuleData.HarvestActivationRange;
        internal override LogicFrameSpan GetPreparationTime() => ModuleData.HarvestPreparationTime;

        internal override bool SupplySourceHasBoxes(BehaviorUpdateContext context, SupplyWarehouseDockUpdate dockUpdate, GameObject supplySource)
        {
            if (ModuleData.HarvestTrees && supplySource.Definition.KindOf.Get(ObjectKinds.Tree))
            {
                return supplySource.Supply > 0;
            }
            return base.SupplySourceHasBoxes(context, dockUpdate, supplySource);
        }

        internal override void GetBox(BehaviorUpdateContext context)
        {
            if (ModuleData.HarvestTrees && CurrentSupplySource.Definition.KindOf.Get(ObjectKinds.Tree))
            {
                CurrentSupplySource.Supply -= context.GameContext.AssetLoadContext.AssetStore.GameData.Current.ValuePerSupplyBox;
                if (CurrentSupplySource.Supply <= 0)
                {
                    CurrentSupplySource.Update();
                    CurrentSupplySource = null;
                }
                return;
            }
            base.GetBox(context);
        }

        internal override void SetGatheringConditionFlags()
        {
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestPreparation, true);
        }

        internal override LogicFrameSpan GetPickingUpTime() => ModuleData.HarvestActionTime;

        internal override void SetActionConditionFlags()
        {
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestPreparation, false);
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestAction, true);
        }

        internal override void ClearActionConditionFlags()
        {
            GameObject.ModelConditionFlags.Set(ModelConditionFlag.HarvestAction, false);
        }

        #endregion

        internal override void Update(BehaviorUpdateContext context)
        {
            base.Update(context);
            _state.Update(context);

            var isMoving = GameObject.ModelConditionFlags.Get(ModelConditionFlag.Moving);

            switch (SupplyGatherState)
            {
                case SupplyGatherStates.Default:
                    if (!isMoving)
                    {
                        SupplyGatherState = SupplyGatherStateToResume;
                        break;
                    }
                    _waitUntil = context.LogicFrame + ModuleData.BoredTime;
                    break;
            }
        }

        internal override void Load(StatePersister reader)
        {
            reader.PersistVersion(1);

            reader.BeginObject("Base");
            base.Load(reader);
            reader.EndObject();

            _state.Persist(reader);

            reader.PersistObject(_stateMachine2);
            reader.PersistObjectID(ref _unknownObjectId);
            reader.PersistInt32(ref _unknown5);

            reader.PersistInt32(ref _unknown6); // was 1 when worker was carrying supplies, wandering around cc with no supply stash available

            reader.PersistObject(_stateMachine3);
        }

        private sealed class WorkerAIUpdateStateMachine3 : StateMachineBase
        {
            public override WorkerAIUpdate AIUpdate { get; }

            public WorkerAIUpdateStateMachine3(GameObject gameObject, GameContext context, WorkerAIUpdate aiUpdate) : base(gameObject, context, aiUpdate)
            {
                AIUpdate = aiUpdate;

                AddState(0, new WorkerUnknown0State(this));
                AddState(1, new WorkerUnknown1State(this));
            }

            public override void Persist(StatePersister reader)
            {
                reader.PersistVersion(1);

                reader.BeginObject("Base");
                base.Persist(reader);
                reader.EndObject();
            }

            private sealed class WorkerUnknown0State : State
            {
                internal WorkerUnknown0State(WorkerAIUpdateStateMachine3 stateMachine) : base(stateMachine)
                {
                }

                public override void Persist(StatePersister reader)
                {
                    // No version?
                }
            }

            private sealed class WorkerUnknown1State : State
            {
                internal WorkerUnknown1State(WorkerAIUpdateStateMachine3 stateMachine) : base(stateMachine)
                {
                }

                public override void Persist(StatePersister reader)
                {
                    // No version?
                }
            }
        }
    }

    internal sealed class WorkerAIUpdateStateMachine2 : StateMachineBase
    {
        public override SupplyAIUpdate AIUpdate { get; }

        public WorkerAIUpdateStateMachine2(GameObject gameObject, GameContext context, SupplyAIUpdate aiUpdate) : base(gameObject, context, aiUpdate)
        {
            AIUpdate = aiUpdate;

            AddState(0, new WorkerUnknown0State(this));
            AddState(1, new WorkerUnknown1State(this));
            AddState(2, new WorkerUnknown2State(this)); // occurred when loaded chinook was flying over war factory (only remaining building) attempting to drop off supplies
            AddState(3, new WorkerUnknown3State(this)); // occurred when loaded chinook was flying over war factory (only remaining building) attempting to drop off supplies
            AddState(4, new WorkerUnknown4State(this));
        }

        public override void Persist(StatePersister reader)
        {
            reader.PersistVersion(1);

            reader.BeginObject("Base");
            base.Persist(reader);
            reader.EndObject();
        }

        private sealed class WorkerUnknown0State : State
        {
            internal WorkerUnknown0State(WorkerAIUpdateStateMachine2 stateMachine) : base(stateMachine)
            {
            }

            public override void Persist(StatePersister reader)
            {

            }
        }

        private sealed class WorkerUnknown1State : State
        {
            internal WorkerUnknown1State(WorkerAIUpdateStateMachine2 stateMachine) : base(stateMachine)
            {
            }

            public override void Persist(StatePersister reader)
            {

            }
        }

        private sealed class WorkerUnknown2State : State
        {
            internal WorkerUnknown2State(WorkerAIUpdateStateMachine2 stateMachine) : base(stateMachine)
            {
            }

            public override void Persist(StatePersister reader)
            {
                reader.PersistVersion(1);
            }
        }

        private sealed class WorkerUnknown3State : State
        {
            internal WorkerUnknown3State(WorkerAIUpdateStateMachine2 stateMachine) : base(stateMachine)
            {
            }

            public override void Persist(StatePersister reader)
            {
                reader.PersistVersion(1);
            }
        }

        private sealed class WorkerUnknown4State : State
        {
            internal WorkerUnknown4State(WorkerAIUpdateStateMachine2 stateMachine) : base(stateMachine)
            {
            }

            public override void Persist(StatePersister reader)
            {
                reader.PersistVersion(1);
            }
        }
    }

    /// <summary>
    /// Allows the use of VoiceRepair, VoiceBuildResponse, VoiceSupply, VoiceNoBuild, and
    /// VoiceTaskComplete within UnitSpecificSounds section of the object.
    /// Requires Kindof = DOZER.
    /// </summary>
    public sealed class WorkerAIUpdateModuleData : SupplyAIUpdateModuleData, IBuilderAIUpdateData
    {
        internal new static WorkerAIUpdateModuleData Parse(IniParser parser) => parser.ParseBlock(FieldParseTable);

        private new static readonly IniParseTable<WorkerAIUpdateModuleData> FieldParseTable = SupplyAIUpdateModuleData.FieldParseTable
            .Concat(new IniParseTable<WorkerAIUpdateModuleData>
            {
                { "RepairHealthPercentPerSecond", (parser, x) => x.RepairHealthPercentPerSecond = parser.ParsePercentage() },
                { "BoredTime", (parser, x) => x.BoredTime = parser.ParseTimeMillisecondsToLogicFrames() },
                { "BoredRange", (parser, x) => x.BoredRange = parser.ParseInteger() },
                { "UpgradedSupplyBoost", (parser, x) => x.UpgradedSupplyBoost = parser.ParseInteger() },
                { "HarvestTrees", (parser, x) => x.HarvestTrees = parser.ParseBoolean() },
                { "HarvestActivationRange", (parser, x) => x.HarvestActivationRange = parser.ParseInteger() },
                { "HarvestPreparationTime", (parser, x) => x.HarvestPreparationTime = parser.ParseTimeMillisecondsToLogicFrames() },
                { "HarvestActionTime", (parser, x) => x.HarvestActionTime = parser.ParseTimeMillisecondsToLogicFrames() },
            });

        public Percentage RepairHealthPercentPerSecond { get; private set; }
        public LogicFrameSpan BoredTime { get; private set; }
        public int BoredRange { get; private set; }

        [AddedIn(SageGame.CncGeneralsZeroHour)]
        public int UpgradedSupplyBoost { get; private set; }

        [AddedIn(SageGame.Bfme)]
        public bool HarvestTrees { get; private set; }

        [AddedIn(SageGame.Bfme)]
        public int HarvestActivationRange { get; private set; }

        [AddedIn(SageGame.Bfme)]
        public LogicFrameSpan HarvestPreparationTime { get; private set; }

        [AddedIn(SageGame.Bfme)]
        public LogicFrameSpan HarvestActionTime { get; private set; }

        internal override BehaviorModule CreateModule(GameObject gameObject, GameContext context)
        {
            return new WorkerAIUpdate(gameObject, context, this);
        }
    }
}
