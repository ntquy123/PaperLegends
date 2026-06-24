using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class RingBallCollection : IReadOnlyList<NetworkObject>
{
    [SerializeField]
    private List<NetworkObject> entries = new List<NetworkObject>();

    // Tập trung giữ danh sách NetworkObject nằm trong playArea để tránh giữ các bi đã bị despawn hoặc đang ở ngoài vòng.
    // Khi cần đơn giản hóa, có thể thay thế bằng List<NetworkObject> thường, nhưng lớp bao này giúp tự động làm sạch phần tử null
    // và lọc theo bounds playArea mỗi lần refresh.

    public int Count => entries.Count;

    public NetworkObject this[int index] => entries[index];

    public IEnumerator<NetworkObject> GetEnumerator()
    {
        foreach (var entry in entries)
        {
            if (entry != null)
                yield return entry;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(NetworkObject obj)
    {
        return obj != null && entries.Contains(obj);
    }

    public NetworkObject Find(Predicate<NetworkObject> match)
    {
        if (match == null)
            return null;

        return entries.Find(entry => entry != null && match(entry));
    }

    public void Add(NetworkObject obj, BoxCollider playArea)
    {
        if (obj == null)
            return;

        if (playArea != null && !IsInsidePlayArea(playArea, obj.transform.position))
            return;

        CleanupNullEntries();

        if (entries.Contains(obj))
            return;

        entries.Add(obj);
    }

    public bool Remove(NetworkObject obj)
    {
        if (obj == null)
            return false;

        return entries.Remove(obj);
    }

    public void Clear()
    {
        if (entries.Count == 0)
            return;

        entries.Clear();
    }

    public List<NetworkObject> RemoveOutside(BoxCollider playArea)
    {
        var removed = new List<NetworkObject>();
        if (playArea == null)
            return removed;

        CleanupNullEntries();

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var obj = entries[i];
            if (obj == null || !IsInsidePlayArea(playArea, obj.transform.position))
            {
                if (obj != null)
                    removed.Add(obj);
                entries.RemoveAt(i);
            }
        }

        return removed;
    }

    public List<NetworkObject> ToList()
    {
        CleanupNullEntries();
        return new List<NetworkObject>(entries);
    }

    public NetworkObject FindById(NetworkId id)
    {
        return entries.FirstOrDefault(obj => obj != null && obj.Id == id);
    }

    public NetworkId[] ToIdArray(bool skipNulls = true)
    {
        if (skipNulls)
            return entries.Where(e => e != null).Select(e => e.Id).ToArray();

        return entries.Select(e => e != null ? e.Id : default).ToArray();
    }

    public void RefreshFromScene(BoxCollider playArea)
    {
        entries.Clear();

        foreach (var ring in GameObject.FindGameObjectsWithTag("RingBall"))
        {
            if (ring == null)
                continue;

            if (playArea != null && !IsInsidePlayArea(playArea, ring.transform.position))
                continue;

            var obj = ring.GetComponent<NetworkObject>();
            if (obj != null && !entries.Contains(obj))
                entries.Add(obj);
        }
    }

    private void CleanupNullEntries()
    {
        entries.RemoveAll(e => e == null);
    }

    private static bool IsInsidePlayArea(BoxCollider playArea, Vector3 position)
    {
        if (playArea == null)
            return false;

        Vector3 localPos = playArea.transform.InverseTransformPoint(position) - playArea.center;
        Vector3 halfSize = playArea.size * 0.5f;

        return localPos.x >= -halfSize.x && localPos.x <= halfSize.x &&
               localPos.y >= -halfSize.y && localPos.y <= halfSize.y &&
               localPos.z >= -halfSize.z && localPos.z <= halfSize.z;
    }

    public IEnumerable<NetworkId> EnumerateIds()
    {
        foreach (var entry in entries)
        {
            if (entry != null)
                yield return entry.Id;
        }
    }
}
