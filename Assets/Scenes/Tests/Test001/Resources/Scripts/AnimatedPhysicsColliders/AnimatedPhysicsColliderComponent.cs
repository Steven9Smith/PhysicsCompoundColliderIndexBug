using System.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Animation.Hybrid;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Collections;

public class AnimatedPhysicsColliderComponent : MonoBehaviour, IConvertGameObjectToEntity
{
	// 
    public Vector3 InitialRootPosition;
    public Quaternion InitialRootRotation;
	/// <summary>
	/// Retreives the Transforms and PhysicsShapeAuthorings' from the children of the GameObject
	/// </summary>
	/// <param name="rigComponent"></param>
	/// <param name="children"></param>
	/// <param name="childrenShapes"></param>
    void GetData(RigComponent rigComponent, out Transform[] children, out PhysicsShapeAuthoring[] childrenShapes)
    {
        children = new Transform[rigComponent.Bones.Length];
        childrenShapes = new PhysicsShapeAuthoring[rigComponent.Bones.Length];
        for (int i = 0; i < rigComponent.Bones.Length; i++)
        {
            children[i] = rigComponent.Bones[i];
            childrenShapes[i] = children[i].GetComponent<PhysicsShapeAuthoring>();
        }
    }
	/// <summary>
	/// Generates a collider using a PhysicsShapeAuthoring and PhysicsMaterialsExtensionComponent
	/// </summary>
	/// <param name="shape"></param>
	/// <param name="shapeExt"></param>
	/// <param name="offsetPosition"></param>
	/// <param name="offsetRotation"></param>
	/// <returns></returns>
    BlobAssetReference<Unity.Physics.Collider> GenerateCollider(PhysicsShapeAuthoring shape, PhysicsMaterialsExtensionComponent shapeExt, out float3 offsetPosition, out quaternion offsetRotation)
    {
        CollisionFilter filter = new CollisionFilter
        {
            CollidesWith = shape.CollidesWith.Value,
            BelongsTo = shape.BelongsTo.Value,
            GroupIndex = 0
        };
        Unity.Physics.Material material = new Unity.Physics.Material
        {
            CollisionResponse = shape.CollisionResponse,
            CustomTags = shape.CustomTags.Value,
            Friction = shape.Friction.Value,
            FrictionCombinePolicy = shape.Friction.CombineMode,
            Restitution = shape.Restitution.Value,
            RestitutionCombinePolicy = shape.Restitution.CombineMode,
            EnableMassFactors = shapeExt != null ? shapeExt.EnableMassFactors : false,
            EnableSurfaceVelocity = shapeExt != null ? shapeExt.EnableSurfaceVelocity : false
        };
        switch (shape.ShapeType)
        {
            case ShapeType.Box:
                var boxProperties = shape.GetBoxProperties();
                offsetPosition = boxProperties.Center;
                offsetRotation = boxProperties.Orientation;
                return Unity.Physics.BoxCollider.Create(boxProperties, filter, material);
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
                return Unity.Physics.CapsuleCollider.Create(capsuleGeometry, filter, material);
            case ShapeType.Cylinder:
                var cylinderProperties = shape.GetCylinderProperties();
                offsetPosition = cylinderProperties.Center;
                offsetRotation = cylinderProperties.Orientation;
                return CylinderCollider.Create(cylinderProperties, filter, material);
            case ShapeType.Sphere:
                var sphereProperties = shape.GetSphereProperties(out var orientation);
                var SphereGeometry = new SphereGeometry
                {
                    Center = sphereProperties.Center,
                    Radius = sphereProperties.Radius
                };
                offsetPosition = sphereProperties.Center;
                offsetRotation = quaternion.identity;
                return Unity.Physics.SphereCollider.Create(SphereGeometry, filter, material);
            case ShapeType.ConvexHull:
                NativeList<float3> points = new NativeList<float3>(Allocator.Temp);
                shape.GetConvexHullProperties(points);
                var ConvexCollider = Unity.Physics.ConvexCollider.Create(points, shape.ConvexHullGenerationParameters, filter, material);
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
                return Unity.Physics.MeshCollider.Create(verts, tris, filter, material);
            default:
                UnityEngine.Debug.LogWarning("GenerateCollider:: cannot generate collider for shapetype \"" + shape.ShapeType + "\"");
                offsetPosition = float3.zero;
                offsetRotation = quaternion.identity;
                return new BlobAssetReference<Unity.Physics.Collider>();
        }
    }
	/// <summary>
	/// returns an int representing the numbers of shapes that are not null and that are enabled
	/// </summary>
	/// <param name="shapes"></param>
	/// <returns></returns>
    public int GetNumberOfValidShapes(PhysicsShapeAuthoring[] shapes)
    {
        int count = 0;
        foreach (var shape in shapes)
            if (shape != null && shape.enabled)
                count++;
        return count;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var rigComponent = GetComponent<RigComponent>();
        if (rigComponent != null)
        {
            var buffer = dstManager.AddBuffer<PhysicsColliderFollowEntityData>(entity);
            GetData(rigComponent, out Transform[] children, out var shapes);
            //    children = new Transform[0];
            if (children.Length > 1)
            {
                int numberOfValidShapes = GetNumberOfValidShapes(shapes);
                NativeList<CompoundCollider.ColliderBlobInstance> blobs = new NativeList<CompoundCollider.ColliderBlobInstance>(Allocator.TempJob);
                for (int i = 0; i < children.Length; i++)
                {
                    if (shapes[i] != null && shapes[i].enabled)
                    {
                        var shapeExt = children[i].gameObject.GetComponent<PhysicsMaterialsExtensionComponent>();

                        BlobAssetReference<Unity.Physics.Collider> collider = GenerateCollider(shapes[i], shapeExt,
                            out float3 offsetPosition, out quaternion offsetRotation);
                        blobs.Add(new CompoundCollider.ColliderBlobInstance
                        {
                            CompoundFromChild = new RigidTransform(children[i].rotation, children[i].position - this.transform.position),
                            Collider = collider
                        });
                        var ltw = new LocalToWorld { Value = new float4x4(children[i].rotation, children[i].position - this.transform.position) };
                        buffer.Add(new PhysicsColliderFollowEntityData
                        {
                            AnimationDataIndex = i,
                            CompoundColliderChildIndex = blobs.Length - 1,
                            RootTransform = new RigidTransform(InitialRootRotation, InitialRootPosition),
                            sourceEntity = conversionSystem.GetPrimaryEntity(shapes[i].gameObject),
                            CompoundColliderDataIsSet = false,
                            Old = ltw,
                            Offset = ltw
                        });
                        Debug.Log("Setting \"" + children[i].name + "\" to " + (blobs.Length - 1) + " animation index =" + i + ", valid shapes = " + numberOfValidShapes + ", belongs to " + shapes[i].BelongsTo.Value + ", collides with = " + shapes[i].CollidesWith.Value);

                        //   shapes[i].enabled = false;
                    }
                }
                if (blobs.Length > 0)
                    dstManager.AddComponentData(entity, new PhysicsCollider { Value = CompoundCollider.Create(blobs) });

                blobs.Dispose();
            }
            else if (children.Length == 1)
                dstManager.AddComponentData(entity, new PhysicsCollider { Value = GenerateCollider(shapes[0], null, out var a, out var b) });
            else Debug.LogWarning("failed to find any colliders");
        }
        else UnityEngine.Debug.LogWarning("The GameObject \"" + name + "\" does not have a RigCOmponent");
    }
}
