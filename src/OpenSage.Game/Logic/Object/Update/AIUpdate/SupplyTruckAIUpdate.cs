﻿using OpenSage.Data.Ini;

namespace OpenSage.Logic.Object
{
    public class SupplyTruckAIUpdate : SupplyAIUpdate
    {
        private readonly SupplyTruckAIUpdateModuleData _moduleData;

        private readonly WorkerAIUpdateStateMachine2 _stateMachine;
        private uint _dockId;
        private int _unknownInt;
        private bool _unknownBool;

        internal SupplyTruckAIUpdate(GameObject gameObject, GameContext context, SupplyTruckAIUpdateModuleData moduleData) : base(gameObject, context, moduleData)
        {
            _moduleData = moduleData;
            _stateMachine = new WorkerAIUpdateStateMachine2(gameObject, context, this);
        }

        internal override void Load(StatePersister reader)
        {
            reader.PersistVersion(1);

            reader.BeginObject("Base");
            base.Load(reader);
            reader.EndObject();

            reader.PersistObject(_stateMachine);
            reader.PersistObjectID(ref _dockId);
            reader.PersistInt32(ref _unknownInt);
            reader.PersistBoolean(ref _unknownBool);
        }
    }

    public class SupplyTruckAIUpdateModuleData : SupplyAIUpdateModuleData
    {
        internal new static SupplyTruckAIUpdateModuleData Parse(IniParser parser) => parser.ParseBlock(FieldParseTable);

        internal new static readonly IniParseTable<SupplyTruckAIUpdateModuleData> FieldParseTable = SupplyAIUpdateModuleData.FieldParseTable
            .Concat(new IniParseTable<SupplyTruckAIUpdateModuleData>{});

        internal override BehaviorModule CreateModule(GameObject gameObject, GameContext context)
        {
            return new SupplyTruckAIUpdate(gameObject, context, this);
        }
    }
}
