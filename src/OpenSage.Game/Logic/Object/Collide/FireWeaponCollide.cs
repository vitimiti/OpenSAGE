﻿using OpenSage.Content;
using OpenSage.Data.Ini;

namespace OpenSage.Logic.Object
{
    public sealed class FireWeaponCollide : CollideModule
    {
        private readonly FireWeaponCollideModuleData _moduleData;
        private readonly Weapon _collideWeapon;

        internal FireWeaponCollide(GameObject gameObject, FireWeaponCollideModuleData moduleData)
        {
            _moduleData = moduleData;

            _collideWeapon = new Weapon(
                gameObject,
                moduleData.CollideWeapon.Value,
                WeaponSlot.Primary,
                gameObject.GameContext);
        }

        internal override void Load(SaveFileReader reader)
        {
            reader.ReadVersion(1);

            base.Load(reader);

            var unknown1 = reader.ReadBoolean();

            _collideWeapon.Load(reader);

            var unknown2 = reader.ReadBoolean();
        }
    }

    public sealed class FireWeaponCollideModuleData : CollideModuleData
    {
        internal static FireWeaponCollideModuleData Parse(IniParser parser) => parser.ParseBlock(FieldParseTable);

        private static readonly IniParseTable<FireWeaponCollideModuleData> FieldParseTable = new IniParseTable<FireWeaponCollideModuleData>
        {
            { "CollideWeapon", (parser, x) => x.CollideWeapon = parser.ParseWeaponTemplateReference() },
            { "RequiredStatus", (parser, x) => x.RequiredStatus = parser.ParseEnum<ModelConditionFlag>() }
        };

        public LazyAssetReference<WeaponTemplate> CollideWeapon { get; private set; }
        public ModelConditionFlag RequiredStatus { get; private set; }

        internal override BehaviorModule CreateModule(GameObject gameObject, GameContext context)
        {
            return new FireWeaponCollide(gameObject, this);
        }
    }
}
