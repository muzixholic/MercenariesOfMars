using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class StageDefinitionTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var x=ScriptableObject.CreateInstance<T>();owned.Add(x);return x;}
        [TearDown] public void Cleanup() {foreach(var x in owned) Object.DestroyImmediate(x);owned.Clear();}
        BattleSession Battle()
        {
            var a=New<CharacterDefinition>(); var e=New<CharacterDefinition>(); e.faction=Faction.Enemy;
            return new BattleSession(new BattleGrid(4,4),new[]{new BattleUnit("아군",a,Vector2Int.zero),new BattleUnit("적",e,Vector2Int.right)});
        }
        StageBranch Branch(StageDestination destination,params StageCondition[] conditions)
            =>new() {conditions=new List<StageCondition>(conditions),destination=destination};
        [Test] public void 분기는_위에서부터_첫일치를_선택하고_없으면_기본경로를_사용한다()
        {
            var stage=New<StageDefinition>(); var battle=Battle(); var result=new StageBattleResult(battle);
            var first=new StageDestination {kind=StageDestinationKind.Ending,endingTitle="첫 엔딩"};
            var second=new StageDestination {kind=StageDestinationKind.Ending,endingTitle="둘째 엔딩"};
            stage.branches.Add(Branch(first,new StageCondition {key="구출"}));
            stage.branches.Add(Branch(second,new StageCondition {kind=StageConditionKind.ClearTurnsAtMost,value=8}));
            Assert.That(stage.Resolve(result,_=>true),Is.SameAs(first));
            Assert.That(stage.Resolve(result,_=>false),Is.SameAs(second));
            stage.branches[1].conditions[0].value=0;
            Assert.That(stage.Resolve(result,_=>false),Is.SameAs(stage.defaultDestination));
        }
        [Test] public void 한분기의_조건은_모두_만족해야하며_미정의_조건은_일치하지않는다()
        {
            var result=new StageBattleResult(Battle());
            var branch=Branch(new StageDestination(),new StageCondition {key="플래그"},new StageCondition {kind=StageConditionKind.ClearTurnsAtMost,value=1});
            Assert.That(branch.Matches(result,_=>true),Is.True);
            Assert.That(branch.Matches(result,_=>false),Is.False);
            branch.conditions[0].kind=(StageConditionKind)999;
            Assert.That(branch.Matches(result,_=>true),Is.False);
            Assert.That(new StageBranch().Matches(result,_=>true),Is.False);
        }
        [Test] public void 전투결과는_기록후_생존상태나_턴이_바뀌어도_유지된다()
        {
            var battle=Battle(); var snapshot=new StageBattleResult(battle);
            battle.EndTurn();battle.EndTurn();battle.ChangeHp("적",-999);
            Assert.That(snapshot.ClearTurns,Is.EqualTo(1)); Assert.That(snapshot.DefeatedEnemies,Is.Zero);
            Assert.That(snapshot.SurvivingAllies,Does.Contain("아군"));
            var result=new StageBattleResult(battle);
            Assert.That(new StageCondition {kind=StageConditionKind.DefeatedEnemiesAtLeast,value=1}.Matches(result,_=>false),Is.True);
            Assert.That(new StageCondition {kind=StageConditionKind.AllySurvived,key="아군"}.Matches(result,_=>false),Is.True);
            Assert.That(new StageCondition {kind=StageConditionKind.AllySurvived,key="없는 아군"}.Matches(result,_=>false),Is.False);
        }
        [Test] public void 빈_스테이지연결은_엔딩으로_간주하지않고_오류로_검출한다()
        {
            var stage=New<StageDefinition>();
            Assert.That(stage.ValidateStage(),Has.Some.Contains("다음 스테이지"));
            stage.defaultDestination.kind=StageDestinationKind.Ending;
            Assert.That(stage.defaultDestination.Error,Is.Null);
            stage.branches.Add(new StageBranch());
            Assert.That(stage.ValidateStage(),Has.Some.Contains("조건을 추가"));
            stage.branches[0].conditions.Add(new StageCondition {kind=StageConditionKind.AllySurvived});
            Assert.That(stage.ValidateStage(),Has.Some.Contains("유닛 ID"));
        }
    }
}
