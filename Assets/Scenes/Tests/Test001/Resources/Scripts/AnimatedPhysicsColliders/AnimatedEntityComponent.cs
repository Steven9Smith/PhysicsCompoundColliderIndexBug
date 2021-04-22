using UnityEngine;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;

public class AnimatedEntityComponent : MonoBehaviour, IConvertGameObjectToEntity
{
    public Transform RigComponentTransform;
    public Transform Hips;
    public int index;
    public bool PhysicsShapeCompoundColliderOverride;
    public AnimatedEntityComponent() {}
    public AnimatedEntityComponent(int index, Transform rigGameObject)
    {
        this.index = index;
        this.RigComponentTransform = rigGameObject;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        if (enabled)
        {
            //   Debug.Log(this.name + " = " + this.transform.position + ",," + this.transform.localPosition);
            dstManager.AddComponentData(entity, new AnimatedEntityData
            {
                RigGameObject = conversionSystem.GetPrimaryEntity(RigComponentTransform),
                index = index,
                //    offset = this.transform.localPosition
                offset = Hips.position
            });
            if (PhysicsShapeCompoundColliderOverride)
            {
                var blob = GenerateCollider(this.GetComponent<PhysicsShapeAuthoring>(), out var a, out var b);
                dstManager.AddComponentData(entity, new PhysicsCollider { Value = blob });
            }
        }
    }

    BlobAssetReference<Unity.Physics.Collider> GenerateCollider(PhysicsShapeAuthoring shape, out float3 offsetPosition, out quaternion offsetRotation)
    {
        switch (shape.ShapeType)
        {
            case ShapeType.Box:
                var boxProperties = shape.GetBoxProperties();
                offsetPosition = boxProperties.Center;
                offsetRotation = boxProperties.Orientation;
                return Unity.Physics.BoxCollider.Create(boxProperties);
            case ShapeType.Capsule:
                var capsuleProperties = shape.GetCapsuleProperties();
                var capsuleGeometry = new CapsuleGeometry
                {
                    Radius = capsuleProperties.Radius,
                    Vertex0 = capsuleProperties.Center - capsuleProperties.Height / 2 - capsuleProperties.Radius,
                    Vertex1 = capsuleProperties.Center + capsuleProperties.Height / 2 - capsuleProperties.Radius
                };
                offsetPosition = capsuleProperties.Center;
                offsetRotation = capsuleProperties.Orientation;
                return Unity.Physics.CapsuleCollider.Create(capsuleGeometry);
            case ShapeType.Cylinder:
                var cylinderProperties = shape.GetCylinderProperties();
                offsetPosition = cylinderProperties.Center;
                offsetRotation = cylinderProperties.Orientation;
                return CylinderCollider.Create(cylinderProperties);
            case ShapeType.Sphere:
                var sphereProperties = shape.GetSphereProperties(out var orientation);
                var SphereGeometry = new SphereGeometry
                {
                    Center = sphereProperties.Center,
                    Radius = sphereProperties.Radius
                };
                offsetPosition = sphereProperties.Center;
                offsetRotation = quaternion.identity;
                return Unity.Physics.SphereCollider.Create(SphereGeometry);
            case ShapeType.ConvexHull:
                NativeList<float3> points = new NativeList<float3>(Allocator.Temp);
                shape.GetConvexHullProperties(points);
                var ConvexCollider = Unity.Physics.ConvexCollider.Create(points, shape.ConvexHullGenerationParameters);
                //    points.Dispose();
                offsetPosition = float3.zero;
                offsetRotation = quaternion.identity;
                return ConvexCollider;
            case ShapeType.Mesh:
                NativeList<float3> verts = new NativeList<float3>(Allocator.Temp);
                NativeList<int3> tris = new NativeList<int3>(Allocator.Temp);
                shape.GetMeshProperties(verts, tris);
                offsetPosition = float3.zero;
                offsetRotation = quaternion.identity;
                return Unity.Physics.MeshCollider.Create(verts, tris);
            default:
                UnityEngine.Debug.LogWarning("GenerateCollider:: cannot generate collider for shapetype \"" + shape.ShapeType + "\"");
                offsetPosition = float3.zero;
                offsetRotation = quaternion.identity;
                return new BlobAssetReference<Unity.Physics.Collider>();
        }
    }
}
