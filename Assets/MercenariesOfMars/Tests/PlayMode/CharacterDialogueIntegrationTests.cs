using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class CharacterDialogueIntegrationTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        Sprite MakeSprite()
        {
            var texture=new Texture2D(8,8); owned.Add(texture); var pixels=new Color[64];
            for(int i=0;i<pixels.Length;i++) pixels[i]=Color.cyan; texture.SetPixels(pixels); texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(.5f,.5f)); owned.Add(sprite); return sprite;
        }
        BattleController Create(out CharacterDefinition ally)
        {
            var root=new GameObject("캐릭터 표시 검증"); owned.Add(root); root.SetActive(false);
            var board=root.AddComponent<GridBoard>(); var camera=new GameObject("표시 검증 카메라").AddComponent<Camera>(); owned.Add(camera.gameObject);
            camera.transform.position=new Vector3(4,10,-8); camera.transform.LookAt(new Vector3(4,0,4)); camera.orthographic=true;
            ally=New<CharacterDefinition>(); ally.displayName="기사"; ally.faction=Faction.Ally; ally.level=7; ally.mapIcon=MakeSprite();
            var enemy=New<CharacterDefinition>(); enemy.faction=Faction.Enemy;
            var c=root.AddComponent<BattleController>(); c.board=board; c.battleCamera=camera; c.automaticEnemy=false;
            c.eventRunner=root.AddComponent<EventRunner>();
            c.placements.Add(new UnitPlacement { id="아군",character=ally,cell=Vector2Int.zero });
            c.placements.Add(new UnitPlacement { id="적군",character=enemy,cell=new Vector2Int(5,5) });
            root.SetActive(true); return c;
        }
        [UnityTearDown] public IEnumerator Cleanup() { foreach(var item in owned) if(item!=null) Object.Destroy(item); owned.Clear(); yield return null; }
        [UnityTest] public IEnumerator 맵아이콘은_선택과_행동완료_표시를_지원한다()
        {
            var c=Create(out var ally); yield return null; Assert.That(c.RestartBattle(),Is.True);
            var icon=c.GetComponentInChildren<SpriteRenderer>(); Assert.That(icon.sprite,Is.SameAs(ally.mapIcon));
            Assert.That(c.Session.Find("아군").Level,Is.EqualTo(7)); Assert.That(c.SelectUnit("아군"),Is.True);
            Assert.That(c.OpenUnitDetails(),Is.True); Assert.That(c.DetailsUnit.Level,Is.EqualTo(7));
            c.ClearSelection(); c.Session.TryWait(c.Session.Find("아군")); Assert.That(icon.color,Is.EqualTo(new Color(.45f,.45f,.45f)));
            c.Session.EndTurn(); Assert.That(icon.color,Is.EqualTo(Color.white));
            Assert.That(c.transform.Find("새 캐릭터 (적군)").GetComponent<Renderer>(),Is.Not.Null);
        }
        [UnityTest] public IEnumerator 캐릭터대사에서_수동대사로_넘기면_이름과_이미지를_갱신한다()
        {
            var c=Create(out var ally); var portrait=new CharacterPortrait { label="미소",sprite=MakeSprite() }; ally.portraits.Add(portrait);
            var sequence=New<EventSequence>(); sequence.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speakerCharacter=ally,portraitId=portrait.id,text="출발하자." });
            sequence.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speaker="안내",text="다음 대사" });
            yield return null; c.RestartBattle(); string notified=null; c.eventRunner.DialogueShown+=(name,text)=>notified=name;
            Assert.That(c.eventRunner.Play(sequence,c.Session),Is.True); yield return null;
            Assert.That(c.eventRunner.Speaker,Is.EqualTo("기사")); Assert.That(notified,Is.EqualTo("기사")); Assert.That(c.eventRunner.Portrait,Is.SameAs(portrait.sprite));
            for(int i=0;i<10 && c.GetComponent<DialoguePortraitUI>()==null;i++) yield return null;
            Assert.That(c.GetComponent<DialoguePortraitUI>().DisplayedSprite,Is.SameAs(portrait.sprite));
            Assert.That(c.TryAdvanceDialogue(),Is.True); yield return null; yield return null;
            Assert.That(c.eventRunner.Speaker,Is.EqualTo("안내")); Assert.That(c.eventRunner.Portrait,Is.Null);
            yield return null; Assert.That(c.GetComponent<DialoguePortraitUI>().DisplayedSprite,Is.Null);
            c.TryAdvanceDialogue(); yield return null; yield return null;
            Assert.That(c.eventRunner.IsRunning,Is.False); Assert.That(c.eventRunner.Speaker,Is.Null); Assert.That(c.eventRunner.Portrait,Is.Null);
        }
        [UnityTest] public IEnumerator 캐릭터가_전투에_배치되지_않아도_대사에_사용하고_중단시_정리한다()
        {
            var c=Create(out _); var npc=New<CharacterDefinition>(); npc.displayName="마을 주민";
            var portrait=new CharacterPortrait { sprite=MakeSprite() }; npc.portraits.Add(portrait);
            var sequence=New<EventSequence>(); sequence.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speakerCharacter=npc,portraitId=portrait.id,text="안녕하세요." });
            yield return null; c.RestartBattle(); Assert.That(c.eventRunner.Play(sequence,c.Session),Is.True); yield return null;
            Assert.That(c.eventRunner.Speaker,Is.EqualTo("마을 주민")); Assert.That(c.eventRunner.Portrait,Is.SameAs(portrait.sprite));
            c.eventRunner.StopPlayback(); yield return null;
            Assert.That(c.eventRunner.Portrait,Is.Null); Assert.That(c.eventRunner.Speaker,Is.Null); Assert.That(c.eventRunner.Dialogue,Is.Null);
        }
    }
}
