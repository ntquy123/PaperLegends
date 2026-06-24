using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class BallMeshDeformer : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Mesh deformedMesh;
    private Vector3[] originalVertices;
    private Vector3[] modifiedVertices;
    private SphereCollider sphereCollider;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        sphereCollider = GetComponent<SphereCollider>();
    }

    /// <summary>
    /// Deform the mesh at world-space hit point.
    /// </summary>
    /// <param name="point">World hit position</param>
    /// <param name="radius">Effect radius</param>
    /// <param name="amount">Offset amount</param>
    public void DeformAtPoint(Vector3 point, float radius, float amount)
    {
        if (meshFilter == null)
            return;

        if (deformedMesh == null)
        {
            deformedMesh = Instantiate(meshFilter.mesh);
            meshFilter.mesh = deformedMesh;
            originalVertices = deformedMesh.vertices;
            modifiedVertices = deformedMesh.vertices;
        }

        for (int i = 0; i < modifiedVertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(originalVertices[i]);
            float dist = Vector3.Distance(worldPos, point);

            if (dist <= radius)
            {
                Vector3 dir = (worldPos - point).normalized;
                float offset = amount * (1f - dist / radius);

                // Nếu bạn muốn "mẻ sâu" thì dùng offset âm lớn
                worldPos += dir * offset * 2f;

                modifiedVertices[i] = transform.InverseTransformPoint(worldPos);
            }
        }

        deformedMesh.vertices = modifiedVertices;
        deformedMesh.RecalculateNormals();
        UpdateColliderRadius();
    }

    private void UpdateColliderRadius()
    {
        if (sphereCollider != null)
        {
            // Tạm thời tắt collider để tránh lỗi
            sphereCollider.enabled = false;

            // Cập nhật radius đơn giản như cũ
            float maxRadius = 0f;
            for (int i = 0; i < modifiedVertices.Length; i++)
            {
                float dist = modifiedVertices[i].magnitude;
                if (dist > maxRadius)
                    maxRadius = dist;
            }
            sphereCollider.radius = maxRadius;

            // Bật lại collider
            sphereCollider.enabled = true;
        }

        // Gợi ý nâng cao: bạn có thể thay SphereCollider bằng MeshCollider tại đây nếu muốn chính xác hình dạng mẻ
        var meshCol = GetComponent<MeshCollider>();
        if (meshCol != null)
        {
            meshCol.sharedMesh = deformedMesh;
        }
    }

}
