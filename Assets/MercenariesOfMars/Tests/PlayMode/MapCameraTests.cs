using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class MapCameraTests
    {
        readonly List<Object> created = new();
        BattleMapCamera Create(out Camera camera, out GridBoard board)
        {
            var root=new GameObject("맵 카메라 검증"); created.Add(root);
            board=root.AddComponent<GridBoard>(); board.width=10; board.height=8;
            board.transform.position=new Vector3(3,0,2); board.transform.rotation=Quaternion.Euler(0,25,0);
            board.transform.localScale=Vector3.one*1.2f;
            var cameraObject=new GameObject("검증 카메라"); created.Add(cameraObject); camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true; camera.aspect=1.6f;
            var center=board.transform.TransformPoint(new Vector3(5,0,4));
            camera.transform.position=center+new Vector3(-6,10,-9); camera.transform.LookAt(center+Vector3.right*3);
            var navigation=root.AddComponent<BattleMapCamera>(); Assert.That(navigation.Configure(camera,board), Is.True); return navigation;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { foreach(var item in created) if(item!=null) Object.Destroy(item); created.Clear(); yield return null; }
        [UnityTest] public IEnumerator 맵중앙이_화면가운데에_정렬되고_모서리가_들어온다()
        {
            var n=Create(out var camera,out var board); yield return null;
            var center=camera.WorldToViewportPoint(board.transform.TransformPoint(new Vector3(5,0,4)));
            Assert.That(center.x, Is.EqualTo(.5f).Within(.001f)); Assert.That(center.y, Is.EqualTo(.5f).Within(.001f));
            foreach(float x in new[]{0f,10f}) foreach(float z in new[]{0f,8f})
            {
                var point=camera.WorldToViewportPoint(board.transform.TransformPoint(new Vector3(x,0,z)));
                Assert.That(point.x, Is.InRange(0f,1f)); Assert.That(point.y, Is.InRange(0f,1f));
            }
        }
        [UnityTest] public IEnumerator 휠은_확대축소하고_한계값을_넘지않는다()
        {
            var n=Create(out var camera,out var board); yield return null;
            float initial=camera.orthographicSize; n.Zoom(120); Assert.That(camera.orthographicSize, Is.LessThan(initial));
            n.Zoom(-120); Assert.That(camera.orthographicSize, Is.EqualTo(initial).Within(.001f));
            for(int i=0;i<20;i++) n.Zoom(10000); Assert.That(camera.orthographicSize, Is.EqualTo(n.minimumSize));
            for(int i=0;i<20;i++) n.Zoom(-10000); Assert.That(camera.orthographicSize, Is.EqualTo(n.maximumSize));
            camera.orthographic=false; camera.fieldOfView=60; n.Zoom(120); Assert.That(camera.fieldOfView, Is.LessThan(60));
            for(int i=0;i<20;i++) n.Zoom(-10000); Assert.That(camera.fieldOfView, Is.EqualTo(75));
        }
        [UnityTest] public IEnumerator 짧은클릭은_유지하고_드래그는_맵만이동하며_클릭으로처리하지않는다()
        {
            var n=Create(out var camera,out var board); yield return null;
            var start=camera.pixelRect.center; var original=camera.transform.position;
            n.BeginPointer(start); n.DragPointer(start+new Vector2(3,2)); Assert.That(n.EndPointer(start+new Vector2(3,2)), Is.True);
            Assert.That(camera.transform.position, Is.EqualTo(original));
            n.BeginPointer(start); n.DragPointer(start+new Vector2(100,40));
            Assert.That(n.IsDragging, Is.True); Assert.That(camera.transform.position, Is.Not.EqualTo(original));
            Assert.That(Vector3.Dot(camera.transform.position-original,board.transform.up), Is.EqualTo(0).Within(.001f));
            Assert.That(n.EndPointer(start+new Vector2(100,40)), Is.False); Assert.That(n.IsTrackingPointer, Is.False);
            n.Configure(camera,board); var center=camera.WorldToViewportPoint(board.transform.TransformPoint(new Vector3(5,0,4)));
            Assert.That(center.x, Is.EqualTo(.5f).Within(.001f)); Assert.That(center.y, Is.EqualTo(.5f).Within(.001f));
            n.BeginPointer(start); n.CancelPointer(); Assert.That(n.EndPointer(start), Is.False);
        }
    }
}
