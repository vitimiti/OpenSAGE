﻿using OpenSage.Content;
using OpenSage.Data.Ini;
using OpenSage.Logic.AI;
using OpenSage.Logic.AI.AIStates;
using OpenSage.Mathematics;

namespace OpenSage.Logic.Object
{
    public class ChinookAIUpdate : SupplyTruckAIUpdate
    {
        internal ChinookAIUpdateModuleData ModuleData { get; }

        private UnknownStateData _queuedCommand;
        private ChinookState _state;
        private uint _airfieldToRepairAt;

        internal ChinookAIUpdate(GameObject gameObject, GameContext context, ChinookAIUpdateModuleData moduleData) : base(gameObject, context, moduleData)
        {
            ModuleData = moduleData;
        }

        private protected override AIUpdateStateMachine CreateStateMachine(GameObject gameObject, GameContext context) => new ChinookAIUpdateStateMachine(gameObject, context, this);

        protected override int GetAdditionalValuePerSupplyBox(ScopedAssetCollection<UpgradeTemplate> upgrades)
        {
            // this is also hardcoded in original SAGE, replaced by BonusScience and BonusScienceMultiplier (SupplyCenterDockUpdate) in later games
            var upgradeDefinition = upgrades.GetByName("Upgrade_AmericaSupplyLines");
            return GameObject.HasUpgrade(upgradeDefinition) ? ModuleData.UpgradedSupplyBoost : 0;
        }

        internal override void Load(StatePersister reader)
        {
            var version = reader.PersistVersion(2);

            reader.BeginObject("Base");
            base.Load(reader);
            reader.EndObject();

            var hasQueuedCommand = _queuedCommand != null;
            reader.PersistBoolean(ref hasQueuedCommand);
            if (hasQueuedCommand)
            {
                _queuedCommand ??= new UnknownStateData();
                reader.PersistObject(_queuedCommand);
            }

            reader.PersistEnum(ref _state);
            reader.PersistObjectID(ref _airfieldToRepairAt);

            if (version >= 2)
            {
                reader.SkipUnknownBytes(12);
            }
        }
    }

    internal enum ChinookState
    {
        Takeoff = 0,
        InAir = 1,
        CombatDropMaybe = 2,
        Landing = 3,
        OnGround = 4,
    }

    internal sealed class ChinookAIUpdateStateMachine : AIUpdateStateMachine
    {
        public ChinookAIUpdateStateMachine(GameObject gameObject, GameContext context, ChinookAIUpdate aiUpdate)
            : base(gameObject, context, aiUpdate)
        {
            AddState(1001, new ChinookTakeoffAndLandingState(gameObject, context, aiUpdate, false)); // Takeoff
            AddState(1002, new ChinookTakeoffAndLandingState(gameObject, context, aiUpdate, true));  // Landing
            AddState(1003, new MoveTowardsState(gameObject, context, aiUpdate));                     // Moving towards airfield to repair at
            AddState(1004, new MoveTowardsState(gameObject, context, aiUpdate));                     // Moving towards evacuation point
            AddState(1005, new ChinookTakeoffAndLandingState(gameObject, context, aiUpdate, true));  // Landing for evacuation
            // 1006?
            AddState(1007, new MoveTowardsState(gameObject, context, aiUpdate));                     // Moving towards reinforcement point
            AddState(1008, new ChinookTakeoffAndLandingState(gameObject, context, aiUpdate, true));  // Landing for reinforcement
            // 1009?
            AddState(1010, new ChinookTakeoffAndLandingState(gameObject, context, aiUpdate, false)); // Takeoff after reinforcement
            AddState(1011, new ChinookExitMapState(gameObject, context, aiUpdate));                  // Exit map after reinforcement
            AddState(1012, new ChinookMoveToCombatDropState(gameObject, context, aiUpdate));         // Moving towards combat drop location
            AddState(1013, new ChinookCombatDropState(gameObject, context, aiUpdate));               // Combat drop
        }
    }

    /// <summary>
    /// Logic requires bones for either end of the rope to be defined as RopeEnd and RopeStart.
    /// Infantry (or tanks) can be made to rappel down a rope by adding CAN_RAPPEL to the object's
    /// KindOf field. Having done that, the "RAPPELLING" ModelConditionState becomes available for
    /// rappelling out of the object that has the rappel code of this module.
    /// </summary>
    public sealed class ChinookAIUpdateModuleData : SupplyTruckAIUpdateModuleData
    {
        internal new static ChinookAIUpdateModuleData Parse(IniParser parser) => parser.ParseBlock(FieldParseTable);

        private new static readonly IniParseTable<ChinookAIUpdateModuleData> FieldParseTable = SupplyTruckAIUpdateModuleData.FieldParseTable
            .Concat(new IniParseTable<ChinookAIUpdateModuleData>
            {
                { "NumRopes", (parser, x) => x.NumRopes = parser.ParseInteger() },
                { "PerRopeDelayMin", (parser, x) => x.PerRopeDelayMin = parser.ParseInteger() },
                { "PerRopeDelayMax", (parser, x) => x.PerRopeDelayMax = parser.ParseInteger() },
                { "RopeWidth", (parser, x) => x.RopeWidth = parser.ParseFloat() },
                { "RopeColor", (parser, x) => x.RopeColor = parser.ParseColorRgb() },
                { "RopeWobbleLen", (parser, x) => x.RopeWobbleLen = parser.ParseInteger() },
                { "RopeWobbleAmplitude", (parser, x) => x.RopeWobbleAmplitude = parser.ParseFloat() },
                { "RopeWobbleRate", (parser, x) => x.RopeWobbleRate = parser.ParseInteger() },
                { "RopeFinalHeight", (parser, x) => x.RopeFinalHeight = parser.ParseInteger() },
                { "RappelSpeed", (parser, x) => x.RappelSpeed = parser.ParseInteger() },
                { "MinDropHeight", (parser, x) => x.MinDropHeight = parser.ParseInteger() },
                { "UpgradedSupplyBoost", (parser, x) => x.UpgradedSupplyBoost = parser.ParseInteger() },
                { "RotorWashParticleSystem", (parser, x) => x.RotorWashParticleSystem = parser.ParseAssetReference() },
            });

        public int NumRopes { get; private set; }
        public int PerRopeDelayMin { get; private set; }
        public int PerRopeDelayMax { get; private set; }
        public float RopeWidth { get; private set; }
        public ColorRgb RopeColor { get; private set; }
        public int RopeWobbleLen { get; private set; }
        public float RopeWobbleAmplitude { get; private set; }
        public int RopeWobbleRate { get; private set; }
        public int RopeFinalHeight { get; private set; }
        public int RappelSpeed { get; private set; }
        public int MinDropHeight { get; private set; }

        [AddedIn(SageGame.CncGeneralsZeroHour)]
        public int UpgradedSupplyBoost { get; private set; }

        [AddedIn(SageGame.CncGeneralsZeroHour)]
        public string RotorWashParticleSystem { get; private set; }

        internal override BehaviorModule CreateModule(GameObject gameObject, GameContext context)
        {
            return new ChinookAIUpdate(gameObject, context, this);
        }
    }
}
