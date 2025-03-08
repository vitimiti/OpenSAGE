﻿using System;
using ImGuiNET;
using OpenSage.Data.Ini;

namespace OpenSage.Logic.Object;

internal class DeletionUpdate : UpdateModule
{
    private readonly DeletionUpdateModuleData _moduleData;

    private LogicFrame _frameToDelete;

    public DeletionUpdate(GameObject gameObject, GameContext context, DeletionUpdateModuleData moduleData)
        : base(gameObject, context)
    {
        _moduleData = moduleData;

        _frameToDelete = context.GameLogic.CurrentFrame + context.GetRandomLogicFrameSpan(_moduleData.MinLifetime, _moduleData.MaxLifetime);
        SetNextUpdateFrame(_frameToDelete);
    }

    internal override void Update(BehaviorUpdateContext context)
    {
        if (context.LogicFrame >= _frameToDelete)
        {
            context.GameContext.GameLogic.DestroyObject(GameObject);
        }
    }

    internal override void Load(StatePersister reader)
    {
        reader.PersistVersion(1);

        base.Load(reader);

        reader.PersistLogicFrame(ref _frameToDelete);
    }

    internal override void DrawInspector()
    {
        base.DrawInspector();
        ImGui.LabelText("Frame to delete", _frameToDelete.ToString());
    }
}


public sealed class DeletionUpdateModuleData : UpdateModuleData
{
    internal static DeletionUpdateModuleData Parse(IniParser parser) => parser.ParseBlock(FieldParseTable);

    private static readonly IniParseTable<DeletionUpdateModuleData> FieldParseTable = new IniParseTable<DeletionUpdateModuleData>
    {
        { "MinLifetime", (parser, x) => x.MinLifetime = parser.ParseTimeMillisecondsToLogicFrames() },
        { "MaxLifetime", (parser, x) => x.MaxLifetime = parser.ParseTimeMillisecondsToLogicFrames() }
    };

    public LogicFrameSpan MinLifetime { get; private set; }
    public LogicFrameSpan MaxLifetime { get; private set; }

    internal override BehaviorModule CreateModule(GameObject gameObject, GameContext context)
    {
        return new DeletionUpdate(gameObject, context, this);
    }
}
