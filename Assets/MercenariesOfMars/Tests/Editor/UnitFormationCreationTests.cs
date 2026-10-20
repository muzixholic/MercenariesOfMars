using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MercenariesOfMars.Editor;

namespace MercenariesOfMars.Tests
{
    public sealed class UnitFormationCreationTests
    {
        string folder;
        MechDefinition mech;
        PilotDefinition pilot;
        string PathFor(string name)=>folder+"/"+name+".asset";
        int AssetCount()=>AssetDatabase.FindAssets("",new[]{folder}).Length;
        [SetUp] public void 준비()
        {
            folder="Assets/임시편성생성검증_"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            mech=UnitFormationCreation.CreateMech(PathFor("백로"));
            pilot=UnitFormationCreation.CreatePilot(PathFor("레이나"));
        }
        [TearDown] public void 정리()=>AssetDatabase.DeleteAsset(folder);

        [Test] public void 선택한_기체와_파일럿을_공유하며_편성만_생성한다()
        {
            var mechBefore=EditorJsonUtility.ToJson(mech);var pilotBefore=EditorJsonUtility.ToJson(pilot);
            int before=AssetCount();
            var unit=UnitFormationCreation.Create(PathFor("편성"),mech,pilot,Faction.Enemy);
            Assert.That(unit.mech,Is.SameAs(mech));Assert.That(unit.pilot,Is.SameAs(pilot));
            Assert.That(unit.displayName,Is.EqualTo("백로 (레이나)"));Assert.That(unit.faction,Is.EqualTo(Faction.Enemy));
            Assert.That(AssetCount(),Is.EqualTo(before+1));
            Assert.That(EditorJsonUtility.ToJson(mech),Is.EqualTo(mechBefore));
            Assert.That(EditorJsonUtility.ToJson(pilot),Is.EqualTo(pilotBefore));
            UnitFormationCreation.Create(PathFor("다른편성"),mech,pilot,Faction.Ally);
            Assert.That(AssetCount(),Is.EqualTo(before+2));
        }
        [Test] public void 신규_기체와_파일럿은_파일이름을_사용하고_성장설정을_초기화한다()
        {
            Assert.That(mech.displayName,Is.EqualTo("백로"));Assert.That(pilot.displayName,Is.EqualTo("레이나"));
            var expected=PilotProgressionSetup.EnsureDefaults().newPilotGrowth;
            Assert.That(pilot.attackGrowth,Is.SameAs(expected));Assert.That(pilot.defenseGrowth,Is.SameAs(expected));
            Assert.That(pilot.accuracyGrowth,Is.SameAs(expected));Assert.That(pilot.evasionGrowth,Is.SameAs(expected));
            Assert.That(AssetCount(),Is.EqualTo(2));
        }
        [Test] public void 미선택과_저장되지않은_선택은_편성을_만들지않는다()
        {
            int before=AssetCount();
            Assert.Throws<ArgumentException>(()=>UnitFormationCreation.Create(PathFor("누락"),null,pilot,Faction.Ally));
            Assert.Throws<ArgumentException>(()=>UnitFormationCreation.Create(PathFor("누락"),mech,null,Faction.Ally));
            var temporary=ScriptableObject.CreateInstance<MechDefinition>();
            try {Assert.Throws<ArgumentException>(()=>UnitFormationCreation.Create(PathFor("임시"),temporary,pilot,Faction.Ally));}
            finally {UnityEngine.Object.DestroyImmediate(temporary);}
            Assert.That(AssetCount(),Is.EqualTo(before));
        }
        [Test] public void 기존파일을_덮어쓰지않는다()
        {
            string path=PathFor("백로");var before=File.ReadAllText(path);var guid=AssetDatabase.AssetPathToGUID(path);
            Assert.Throws<InvalidOperationException>(()=>UnitFormationCreation.Create(path,mech,pilot,Faction.Ally));
            Assert.Throws<InvalidOperationException>(()=>UnitFormationCreation.CreateMech(path));
            Assert.Throws<InvalidOperationException>(()=>UnitFormationCreation.CreatePilot(path));
            Assert.That(File.ReadAllText(path),Is.EqualTo(before));Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(guid));
        }
        [TestCase("../편성.asset")]
        [TestCase("Assets/../편성.asset")]
        [TestCase("Assets/없는폴더/편성.asset")]
        [TestCase("Assets/편성.txt")]
        public void 잘못된_저장경로는_거절한다(string path)
        {Assert.Throws<ArgumentException>(()=>UnitFormationCreation.Create(path,mech,pilot,Faction.Ally));Assert.That(AssetCount(),Is.EqualTo(2));}

        [Test] public void 이름의_공백과_파일명_금지문자를_처리한다()
        {
            mech.displayName="  백로/03  ";pilot.displayName="  레이나:대위  ";
            Assert.That(UnitFormationCreation.SuggestedName(mech,pilot),Is.EqualTo("백로/03 (레이나:대위)"));
            Assert.That(UnitFormationCreation.SuggestedFileName(mech,pilot),Is.EqualTo("백로_03 (레이나_대위)"));
            mech.displayName=" ";Assert.That(UnitFormationCreation.SuggestedName(mech,pilot),Does.StartWith("백로 ("));
            Assert.That(UnitFormationCreation.SuggestedName(null,null),Is.EqualTo("기체 미선택 (파일럿 미선택)"));
        }
    }
}
