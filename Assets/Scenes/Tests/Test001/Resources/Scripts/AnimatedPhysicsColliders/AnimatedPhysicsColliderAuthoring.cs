
using Unity.Animation;
using Unity.Animation.Hybrid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[ConverterVersion("Physics_Collider_Animation_System", 1)]
public class AnimatedPhysicsColliderSystem : SystemBase
{
	GameObjectConversionSystem conversionSystem;

	EntityQuery PhysicsColliderFollowEntityQuery;
	BuildPhysicsWorld mBuildPhysicsWorld;
	StepPhysicsWorld mStepPhysicsWorld;
	enum PhysicsFilterTag
	{
		Everything,
		None,
		ImpulseApplicator
	}

	protected override void OnCreate()
	{
		conversionSystem = World.GetExistingSystem<GameObjectConversionSystem>();
		PhysicsColliderFollowEntityQuery = GetEntityQuery(typeof(PhysicsColliderFollowEntityData), typeof(PhysicsCollider));
		mBuildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
		mStepPhysicsWorld = World.GetExistingSystem<StepPhysicsWorld>();
	}

	protected override void OnStartRunning()
	{
		base.OnStartRunning();
		// fix physics collider animation indices
		{
			var entities = PhysicsColliderFollowEntityQuery.ToEntityArray(Allocator.Temp);
			var colliders = PhysicsColliderFollowEntityQuery.ToComponentDataArray<PhysicsCollider>(Allocator.Temp);
			var GetBuffer = GetBufferFromEntity<PhysicsColliderFollowEntityData>();
			for (int a = 0; a < entities.Length; a++)
			{
				var physicsCollider = colliders[a];
				var physicsColliderFollowEntityBuffer = GetBuffer[entities[a]];
				unsafe
				{
					// We created this manually so we know that it's a compound collider
					Unity.Physics.CompoundCollider* compoundCollider = (Unity.Physics.CompoundCollider*)physicsCollider.ColliderPtr;
					int closestIndex = 0;
					float closestDistance = float.MaxValue;
					float3 closestPoint = new float3(float.MaxValue, float.MaxValue, float.MaxValue);

					for (int i = 0; i < physicsColliderFollowEntityBuffer.Length; i++)
					{
						// we don't want to run this if the colliders have been set already (may convert this into a ComponentData_Tag or something)
						if (!physicsColliderFollowEntityBuffer[i].CompoundColliderDataIsSet)
						{
							//store temp variable to be modified on the fly
							var pBuff = physicsColliderFollowEntityBuffer[i];
							// get the position to the 3 decimal place
							float3 posA = math.round(pBuff.Offset.Position * 1000) / 1000;
							// for some reason the x and z values are the oppisite value (positive = negative and vise versa)
							// we fix it by multiplying by -1
							posA.x *= -1;
							posA.z *= -1;
							// go through the children and test if the closest child's index match the one we have when the collider was created
							for (int j = 0; j < compoundCollider->NumChildren; j++)
							{
								// get child position
								float3 posB = math.round(compoundCollider->Children[j].CompoundFromChild.pos * 1000) / 1000;
								// calculate the distance
								float distance = math.distance(posA, posB);
								// test for cloest distane
								if (distance < closestDistance)
								{
									closestIndex = j;
									closestDistance = distance;
									closestPoint = posB;
								}
								//     UnityEngine.Debug.Log(pBuff.CompoundColliderChildIndex + "," + pBuff.AnimationDataIndex + ",," + posA + " =? " + posB);
							}
							// if the cloest child's index doesn't match the pre-set one then we fix it
							if (pBuff.CompoundColliderChildIndex != closestIndex)
							{
								UnityEngine.Debug.LogWarning("Detected invalid index " + pBuff.CompoundColliderChildIndex + ", supposed to be " + closestIndex);
								pBuff.CompoundColliderChildIndex = closestIndex;
							}
							// apply the new values
							pBuff.CompoundColliderDataIsSet = true;
							physicsColliderFollowEntityBuffer[i] = pBuff;
							//Verbose testing
							UnityEngine.Debug.Log(pBuff.CompoundColliderChildIndex + "," + pBuff.AnimationDataIndex + ",," + posA + " =? " + closestPoint);
						}
						// Reset
						closestIndex = 0;
						closestDistance = float.MaxValue;
						closestPoint = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
					}
					//    EntityManager.RemoveComponent<PhysicsColliderFollowEntityData_Tag>(entity);
				}
			}
		}
	}


	protected override void OnUpdate()
	{

	}
}
public struct PhysicsColliderFollowEntityData : IBufferElementData
{
    public bool CompoundColliderDataIsSet;
//    public unsafe Unity.Physics.Collider* collider;
    public int AnimationDataIndex;
    public int CompoundColliderChildIndex;
    public LocalToWorld Old, New, Offset;
    public RigidTransform RootTransform;
    public Entity sourceEntity;
//    public ImpulseApplicationSettings ImpulseApplicationSettings;

    public static ColliderType GetColliderType(ShapeType type)
    {
        switch (type)
        {
            case ShapeType.Box: return ColliderType.Box;
            case ShapeType.Capsule: return ColliderType.Capsule;
            case ShapeType.Mesh: return ColliderType.Mesh;
            case ShapeType.ConvexHull: return ColliderType.Convex;
            case ShapeType.Cylinder: return ColliderType.Cylinder;
            case ShapeType.Plane: return ColliderType.Quad;
            case ShapeType.Sphere: return ColliderType.Sphere;
            default: return ColliderType.Compound;
        }
    }

    public static ColliderType GetColliderType(Transform transform, ShapeType shapeType, bool hasPhysicsBody = false, bool forceTriangle = false, bool forceTerrain = false, bool forceCompound = false)
    {
        if (forceCompound) return ColliderType.Compound;
        else if (forceTerrain) return ColliderType.Terrain;
        else if (forceTriangle) return ColliderType.Triangle;
        else
        {
            if (!hasPhysicsBody && transform.parent != null) return ColliderType.Compound;
            else if (hasPhysicsBody && transform.childCount > 0)
            {
                //TODO: go through children and find any physics shapes
                return ColliderType.Compound;
            }
            else return GetColliderType(shapeType);
        }
    }
}
public struct AnimatedEntityData : IComponentData
{
    public Entity RigGameObject;
    public int index;
    public float3 offset;
}
